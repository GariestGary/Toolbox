using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class Pooler: MonoBehaviour, IClear
    {
        [SerializeField] private Transform _PredefinedRoot;
        [SerializeField] private bool _UseDefaultInstantiating = true;

        private PoolerDataHolder _Data;
        private Transform objectPoolParent;
        private List<Pool> pools = new List<Pool>();
        private GameObjectRemovedMessage _removeMessage;
        private CancellationTokenSource m_GCTokenSource = new CancellationTokenSource();
        private Messenger _Msg;
        private Updater _Upd;
        
        public void Initialize(Messenger msg, Updater upd)
        {
            _Upd = upd;
            _Msg = msg;
            _Data = ResourcesUtils.ResolveScriptable<PoolerDataHolder>(SettingsData.poolerResourcesDataPath);
            
            SetCustomRoot(_PredefinedRoot);

            pools = new List<Pool>();

            for (int i = 0; i < _Data.PoolsList.Count; i++)
            {
                TryAddPool(_Data.PoolsList[i]);
            }

            _Msg.Subscribe<SceneUnloadingMessage>(m => HandleSceneUnload(m.SceneName), null, true);

            _removeMessage = new GameObjectRemovedMessage();

            EnableGC();
        }
        
        public void SetCustomRoot(Transform root)
        {
            if(root == null)
            {
                objectPoolParent = new GameObject("Pool Parent").transform;
            }
            else
            {
                objectPoolParent = root;
            }
        }
        
        #region Garbage Collector

        private async UniTask GCWorkerStart(CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_Data.GarbageCollectorWorkInterval), cancellationToken: token);

                if(token.IsCancellationRequested)
                {
                    return;
                }

                ForceGarbageCollector();
            }
        }
        private void GCWorkerStop()
        {
            m_GCTokenSource = m_GCTokenSource.CancelAndCreate();
        }

        public void EnableGC()
        {
            GCWorkerStart(m_GCTokenSource.Token).Forget();
        }

        public void DisableGC()
        {
            GCWorkerStop();
        }

        public void ForceGarbageCollector()
        {
            if(pools != null)
            {
                for (int i = 0; i < pools.Count; i++)
                {
                    ClearPoolGarbage(pools[i]);
                }
            }
        }
        private void ClearPoolGarbage(Pool pool)
        {
            var unusedObjects = pool.objects.Where(o => !o.Used).ToList();

            if(unusedObjects.Count <= 0)
            {
                return;
            }

            var allObjectsCount = pool.objects.Count;
            var excessObjectsCount = allObjectsCount - pool.size;
            var canBeCleared = excessObjectsCount - (unusedObjects.Count - pool.size) >= 0;
            var usedObjects = allObjectsCount - unusedObjects.Count;
            excessObjectsCount -= usedObjects;

            if (canBeCleared)
            {
                for(int i = 0; i < excessObjectsCount; i++) 
                {
                    var unused = unusedObjects[i];
                    pool.objects.Remove(unused);
                    Destroy(unused.GameObject);
                    _removeMessage.Obj = unused.GameObject;
                    _removeMessage.RemoveType = GameObjectRemoveType.Destroyed;
                    _Msg.Send(_removeMessage);
                }
            }
        }

        #endregion

        public void Clear()
        {
            objectPoolParent = null;
            pools?.Clear();
            pools = null;
        }

        public bool TryRemovePool(Pool pool)
        {
            if (pools == null)
            {
                return false;
            }

            if(!pools.Contains(pool))
            {
                return false;
            }

            for (int i = 0; i < pool.objects.Count; i++)
            {
                var obj = pool.objects[i];

                TryDespawn(obj);
                Destroy(obj.GameObject);
            }

            pools.Remove(pool);
            return true;
        }

        public bool TryRemovePool(string tag)
        {
            Pool poolToRemove = null;

            for (int i = 0; i < pools.Count; i++)
            {
                var pool = pools[i];

                if (pool.tag == tag)
                {
                    poolToRemove = pool;
                    break;
                }
            }

            if (poolToRemove == null)
            {
                Debug.LogWarning($"There is no pool named {tag}");
                return false;
            }

            return TryRemovePool(poolToRemove);
        }
        
        public Pool TryAddPool(PoolData poolToAdd, Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc = null, Action<GameObject> spawnAction = null)
        {
            if(poolToAdd.pooledObject == null)
            {
                Debug.LogWarning($"Pool with tag {poolToAdd.tag} has no prefab setted");
                return null;
            }

            if(pools == null)
            {
                pools = new List<Pool>();
            }

            List<PooledGameObject> objectPoolList = new List<PooledGameObject>();

            if(poolToAdd.size <= 0)
            {
                poolToAdd.size = 1;
            }

            for (int j = 0; j < poolToAdd.size; j++)
            {
                CreateNewPoolObject(poolToAdd.pooledObject, objectPoolList, true, instantiateFunc);
            }

            var pool = new Pool(poolToAdd.tag, poolToAdd.pooledObject, poolToAdd.size, objectPoolList, instantiateFunc, spawnAction);
            pools.Add(pool);
            return pool;
        }

        public Pool TryAddPool(string tag, GameObject obj, int size, Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc = null, Action<GameObject> spawnAction = null)
        {
            PoolData pool = new PoolData() { tag = tag, pooledObject = obj, size = size };
            return TryAddPool(pool, instantiateFunc, spawnAction);
        }
        
        public int GetPoolObjectsCount(string poolTag)
        {
            var allPools = pools.Where(p => p.tag == poolTag);

            if(allPools.Count() <= 0)
            {
                Debug.LogWarning($"There is no pool named {poolTag}");
                return -1;
            }

            return allPools.Sum(p => p.CurrentObjectsCount);
        }

        #region Instantiating

        /// <summary>
        /// Spawns GameObject from pool with specified tag
        /// </summary>
        /// <param name="poolTag">pool tag with necessary object</param>
        /// <param name="position">initial position</param>
        /// <param name="rotation">initial rotation</param>
        /// <param name="parent">parent transform for GameObject</param>
        /// <param name="data">data to provide in GameObject</param>
        /// <param name="traverseHierarchy">should pooler traverse all gameobject hierarchy to call OnSpawn method</param>
        /// <returns>GameObject from pool</returns>
        public GameObject Spawn(string poolTag, Vector3 position, Quaternion rotation, Transform parent = null, object data = null, bool traverseHierarchy = false)
        {
            //Returns null if object pool with specified tag doesn't exists
            var poolsToUse = pools.Where(p => p.tag == poolTag).ToArray();

            if(poolsToUse.Length <= 0)
            {
                Debug.LogWarning($"Object pool with tag '{poolTag}' doesn't exists");
                return null;
            }

            //get first unused obj
            var poolToUse = poolsToUse[UnityEngine.Random.Range(0, poolsToUse.Length)];
            var objToSpawn = poolToUse.objects.FirstOrDefault(o => !o.Used);

            //Create new object if last in list is active
            if (objToSpawn is null)
            {
                CreateNewPoolObject(poolToUse.referenceObject, poolToUse.objects, true, poolToUse.instantiateFunc);
                objToSpawn = poolToUse.objects.FirstOrDefault(o => !o.Used);
            }

            //Return null if last object is null;
            if (objToSpawn == null)
            {
                Debug.Log($"object from pool '{poolTag}' you trying to spawn is null");
                return null;
            }

            //Calling spawn action
            poolToUse.spawnFunc?.Invoke(objToSpawn.GameObject);  
            
            //Setting transform
            var t = objToSpawn.GameObject.transform;
            t.SetParent(parent);
            t.localPosition = Vector3.zero;
            t.localScale = Vector3.one;
            t.position = position;
            t.rotation = rotation;
            objToSpawn.GameObject.Enable();

            //Call all spawn methods in gameobject
            if (traverseHierarchy)
            {
                CallSpawns(objToSpawn.GameObject, data);
            }
            else
            {
                CallSpawn(objToSpawn.GameObject, data);    
            }

            objToSpawn.Used = true;
            
            return objToSpawn.GameObject;
        }

        /// <summary>
        /// Spawns GameObject from pool with specified tag
        /// </summary>
        /// <param name="poolTag">pool tag with necessary object</param>
        /// <param name="parent">parent transform for GameObject</param>
        /// <param name="data">data to provide in GameObject</param>
        /// <param name="traverseHierarchy">should pooler traverse all gameobject hierarchy to call OnSpawn method</param>
        /// <returns>GameObject from pool</returns>
        public GameObject Spawn(string poolTag, object data = null, Transform parent = null, bool traverseHierarchy = false)
        {
            return Spawn(poolTag, Vector3.zero, Quaternion.identity, parent, data, traverseHierarchy);
        }

        /// <summary>
        /// Spawns GameObject from pool with specified tag and returns specified component from it
        /// </summary>
        /// <typeparam name="T">Component type</typeparam>
        /// <param name="poolTag">pool tag with necessary object</param>
        /// <param name="position">initial position</param>
        /// <param name="rotation">initial rotation</param>
        /// <param name="parent">parent transform for GameObject</param>
        /// <param name="data">data to provide in GameObject</param>
        /// <param name="traverseHierarchy">should pooler traverse all gameobject hierarchy to call OnSpawn method</param>
        /// <returns>Component from spawned GameObject</returns>
        public T Spawn<T>(string poolTag, Vector3 position, Quaternion rotation, Transform parent = null, object data = null, bool traverseHierarchy = false) where T: Component
        {
            return Spawn(poolTag, position, rotation, parent, data, traverseHierarchy).GetComponent<T>();
        }

        /// <summary>
        /// Spawns GameObject from pool with specified tag and returns specified component from it
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="poolTag"></param>
        /// <param name="data"></param>
        /// <param name="parent"></param>
        /// <param name="traverseHierarchy">should pooler traverse all gameobject hierarchy to call OnSpawn method</param>
        /// <returns></returns>
        public T Spawn<T>(string poolTag, object data = null, Transform parent = null, bool traverseHierarchy = false)
        {
            return Spawn(poolTag, data, parent).GetComponent<T>();
        }

        /// <summary>
        /// Alternative to Unity's Instantiate method, that automatically injects object
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="parent"></param>
        /// <returns>Instantiated GameObject</returns>
        public GameObject Create(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc = null
            )
        {
            GameObject inst = null;
            
            if (instantiateFunc == null)
            {
                inst = InstantiateDefault(prefab, position, rotation, parent);    
            }
            else
            {
                inst = instantiateFunc(prefab, position, rotation, parent);
            }
            
            _Upd.InitializeObject(inst);

            return inst;
        }

        private GameObject InstantiateDefault(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return Instantiate(prefab, position, rotation, parent);
        }
        
        /// <summary>
        /// Alternative to Unity's Instantiate method, which uses Vector3.zero, Quaternion.identity and automatically injects object
        /// </summary>
        /// <param name="prefab">Prefab to Instantiate</param>
        /// <param name="parent">Parent object to instantiated GameObject</param>
        /// <returns>Instantiated GameObject</returns>
        public GameObject Create(
            GameObject prefab,
            Transform parent = null,
            Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc = null
            )
        {
            return Create(prefab, Vector3.zero, Quaternion.identity, parent, instantiateFunc);
        }

        public T Create<T>(
            GameObject prefab,
            Transform parent = null,
            Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc = null
            ) where T: Component
        {
            return Create(prefab, parent, instantiateFunc).GetComponent<T>();
        }

        public T Create<T>(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null,
            Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc = null
            ) where T: Component
        {
            return Create(prefab, position, rotation, parent, instantiateFunc).GetComponent<T>();
        }
        
        #endregion

        #region Destroying

        /// <summary>
        /// Removes GameObject from scene and returns it to pool
        /// </summary>
        /// <param name="objectToDespawn">object to despawn</param>
        /// <param name="delay">delay before despawning</param>
        public bool TryDespawn(GameObject objectToDespawn)
        {
            if (objectToDespawn == null)
            {
                return false;
            }

            Pool p = null;
            PooledGameObject pgo = null;

            for (int i = 0; i < pools.Count; i++)
            {
                bool found = false;

                for (int j = 0; j < pools[i].objects.Count; j++)
                {
                    var objToCheck = pools[i].objects[j];

                    if (objToCheck.GameObject == objectToDespawn)
                    {
                        pgo = objToCheck;
                        found = true;
                        TraversePooledObjectAndDespawnOtherPooledObjects(objectToDespawn.transform);
                        break;
                    }
                }

                if(found)
                {
                    p = pools[i];
                    break;
                }
            }

            
            if (p == null)
            {
                return false;
            }

            return TryDespawn(pgo);
        }

        private bool TryDespawn(PooledGameObject pgo)
        {
            if (pgo == null || !pgo.Used)
            {
                return true;
            }

            CallDespawns(pgo.GameObject);

            pgo.Used = false;
            ReturnToPool(pgo.GameObject);

            _removeMessage.Obj = pgo.GameObject;
            _removeMessage.RemoveType = GameObjectRemoveType.Despawned;
            _Msg.Send(_removeMessage);

            return true;
        }

        /// <summary>
        /// Decides to despawn GameObject if it in pools list or destroy if not
        /// </summary>
        /// <param name="obj">GameObject to despawn or destroy</param>
        /// <param name="delay">Delay before despawning or destroying</param>
        public bool DespawnOrDestroy(GameObject obj)
        {
            var despawned = TryDespawn(obj);

            if (!despawned)
            {
                Destroy(obj);

                _removeMessage.Obj = obj;
                _removeMessage.RemoveType = GameObjectRemoveType.Destroyed;
                _Msg.Send(_removeMessage);

                return true;
            }

            return despawned;
        }
        
        private void ReturnToPool(GameObject obj)
        {
            obj.Disable();
            obj.transform.SetParent(objectPoolParent);
        }
        
        private void TraversePooledObjectAndDespawnOtherPooledObjects(Transform transform)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                TraversePooledObjectAndDespawnOtherPooledObjects(transform.GetChild(i));
                TryDespawn(transform.GetChild(i).gameObject);
            }
        }
        
        #endregion
        
        private void HandleSceneUnload(string unloadedSceneName)
        {
            for (int i = 0; i < pools.Count; i++)
            {
                for (int j = 0; j < pools[i].objects.Count; j++)
                {
                    var obj = pools[i].objects[j];

                    if (obj.GameObject.scene.name == unloadedSceneName && obj.Used)
                    {
                        TryDespawn(obj);
                    }
                }
            }
        }
        
        public ObjectPooledState IsObjectPooledAndUsed(GameObject obj)
        {
            for (int i = 0; i < pools.Count; i++)
            {
                for (int j = 0; j < pools[i].objects.Count; j++)
                {
                    var objToCheck = pools[i].objects[j];

                    if (objToCheck.GameObject == obj)
                    {
                        return new(true, objToCheck.Used);
                    }
                }
            }

            return new ObjectPooledState(false, false);
        }

        private void CallSpawn(GameObject obj, object data)
        {
            MonoCached mono = obj.GetComponentsInChildren<MonoCached>(true).FirstOrDefault(o => o is IPooledBase);
            CallPooledInterface(data, mono);
        }
        
        private void CallSpawns(GameObject obj, object data) 
        {
            MonoCached[] pooledMono = obj.GetComponentsInChildren<MonoCached>(true).Where(o => o is IPooledBase).ToArray();

            for (int i = 0; i < pooledMono.Length; i++)
            {
                var mono = pooledMono[i];
                CallPooledInterface(data, mono);
            }
        }

        private void CallPooledInterface(object data, MonoCached mono)
        {
            if (mono == null)
            {
                return;
            }

            var type = mono.GetType();
            var interfaces = type.GetInterfaces();

            for (int j = 0; j < interfaces.Length; j++)
            {
                var inter = interfaces[j];

                if (inter.GetInterface(nameof(IPooledBase)) != null)
                {
                    var generics = inter.GetGenericArguments();
                    var onSpawnMethod = inter.GetMethod("OnSpawn");

                    if (generics.Length > 0)
                    {
                        onSpawnMethod?.Invoke(mono, new[] { data });
                    }
                    else
                    {
                        onSpawnMethod?.Invoke(mono, new object[]{});
                    }
                }
            }
        }

        private void CallDespawns(GameObject obj)
        {
            IDespawn[] despawns = obj.GetComponentsInChildren<IDespawn>(true);

            for(int i = 0; i < despawns.Length; i++)
            {
                var despawnable = despawns[i];

                if(despawnable != null)
                {
                    despawnable.OnDespawn();
                }
            }
        }
        
        private GameObject CreateNewPoolObject(GameObject obj, List<PooledGameObject> poolQueue, bool addToPoolParent = true, Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc = null)
        {
            Transform poolParent = null;

            if(addToPoolParent)
            {
                poolParent = objectPoolParent;
            }

            GameObject poolObj = Create(obj, poolParent, instantiateFunc);

            _Upd.InitializeObject(poolObj);

            poolObj.Disable();

            PooledGameObject pgo = new PooledGameObject()
            {
                GameObject = poolObj,
                Used = false
            };
            
            poolQueue.Add(pgo);

            return poolObj;
        }
    }
    
    public sealed class Pool
    {
        public string tag;
        public int size;
        public GameObject referenceObject;
        public List<PooledGameObject> objects;
        public Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc;
        public Action<GameObject> spawnFunc;

        public int CurrentObjectsCount => objects.Count;

        public Pool(
            string tag,
            GameObject referenceObject,
            int size,
            List<PooledGameObject> objects = null,
            Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc = null,
            Action<GameObject> spawnFunc = null
            )
        {
            this.tag = tag;
            this.size = size;
            this.referenceObject = referenceObject;
            this.instantiateFunc = instantiateFunc;
            this.spawnFunc = spawnFunc;

            if(objects == null)
            {
                this.objects = new List<PooledGameObject>();
            }
            else
            {
                this.objects = objects;
            }
        }
    }
    
    public sealed class PooledGameObject
    {
        public GameObject GameObject;
        public bool Used;
    }
    
    public class GameObjectRemovedMessage: Message
    {
        public GameObject Obj;
        public GameObjectRemoveType RemoveType;
    }

    public struct ObjectPooledState
    {
        public bool IsPooled { get; private set; }
        public bool IsUsed { get; private set; }

        public ObjectPooledState(bool isPooled, bool isUsed)
        {
            IsPooled = isPooled;
            IsUsed = isUsed;
        }
    }

    public enum GameObjectRemoveType
    {
        Destroyed,
        Despawned
    }
}