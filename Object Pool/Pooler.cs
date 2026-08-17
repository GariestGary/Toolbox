using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, List<Pool>> _poolsByTag = new(StringComparer.Ordinal);
        private readonly List<Pool> _poolsWithNullTag = new();
        private readonly Dictionary<GameObject, PooledGameObject> _pooledObjectsByGameObject = new();
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

            ClearPoolAvailabilityState();
            pools = new List<Pool>();
            _poolsByTag.Clear();
            _poolsWithNullTag.Clear();
            _pooledObjectsByGameObject.Clear();

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
            var unusedCount = 0;

            for (int i = 0; i < pool.objects.Count; i++)
            {
                if (!pool.objects[i].Used && !pool.objects[i].AvailabilityReserved)
                {
                    unusedCount++;
                }
            }

            var objectsToRemove = unusedCount - pool.size;

            if (objectsToRemove <= 0)
            {
                return;
            }

            for (int i = 0; i < pool.objects.Count && objectsToRemove > 0;)
            {
                var pooledObject = pool.objects[i];

                if (pooledObject.Used || pooledObject.AvailabilityReserved)
                {
                    i++;
                    continue;
                }

                pool.RemovePooledObjectAt(i);
                UnregisterPooledObject(pooledObject);
                Destroy(pooledObject.GameObject);
                _removeMessage.Obj = pooledObject.GameObject;
                _removeMessage.RemoveType = GameObjectRemoveType.Destroyed;
                _Msg.Send(_removeMessage);
                objectsToRemove--;
            }
        }

        #endregion

        private void ClearPoolAvailabilityState()
        {
            if (pools == null)
            {
                return;
            }

            for (int i = 0; i < pools.Count; i++)
            {
                pools[i]?.DetachAllPooledObjects();
            }
        }

        public void Clear()
        {
            objectPoolParent = null;
            ClearPoolAvailabilityState();
            pools?.Clear();
            pools = null;
            _poolsByTag.Clear();
            _poolsWithNullTag.Clear();
            _pooledObjectsByGameObject.Clear();
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

            for (int i = 0; i < pool.objects.Count; i++)
            {
                UnregisterPooledObject(pool.objects[i]);
            }

            pool.DetachAllPooledObjects();
            pools.Remove(pool);
            UnregisterPool(pool);
            return true;
        }

        public bool TryRemovePool(string tag)
        {
            if (!TryGetPoolsByTag(tag, out var matchingPools) || matchingPools.Count == 0)
            {
                Debug.LogWarning($"There is no pool named {tag}");
                return false;
            }

            return TryRemovePool(matchingPools[0]);
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

            if(poolToAdd.size <= 0)
            {
                poolToAdd.size = 1;
            }

            var pool = new Pool(
                poolToAdd.tag,
                poolToAdd.pooledObject,
                poolToAdd.size,
                instantiateFunc: instantiateFunc,
                spawnFunc: spawnAction);

            try
            {
                for (int j = 0; j < poolToAdd.size; j++)
                {
                    CreateNewPoolObject(poolToAdd.pooledObject, pool, true, instantiateFunc);
                }
            }
            catch
            {
                for (int i = 0; i < pool.objects.Count; i++)
                {
                    var pooledObject = pool.objects[i];
                    UnregisterPooledObject(pooledObject);
                    Destroy(pooledObject.GameObject);
                }

                pool.DetachAllPooledObjects();
                throw;
            }

            pools.Add(pool);
            RegisterPool(pool);
            return pool;
        }

        public Pool TryAddPool(string tag, GameObject obj, int size, Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc = null, Action<GameObject> spawnAction = null)
        {
            PoolData pool = new PoolData() { tag = tag, pooledObject = obj, size = size };
            return TryAddPool(pool, instantiateFunc, spawnAction);
        }

        private bool TryGetPoolsByTag(string tag, out List<Pool> matchingPools)
        {
            if (tag == null)
            {
                matchingPools = _poolsWithNullTag;
                return matchingPools.Count > 0;
            }

            return _poolsByTag.TryGetValue(tag, out matchingPools);
        }

        private void RegisterPool(Pool pool)
        {
            List<Pool> matchingPools;

            if (pool.tag == null)
            {
                matchingPools = _poolsWithNullTag;
            }
            else if (!_poolsByTag.TryGetValue(pool.tag, out matchingPools))
            {
                matchingPools = new List<Pool>();
                _poolsByTag.Add(pool.tag, matchingPools);
            }

            if (!matchingPools.Contains(pool))
            {
                matchingPools.Add(pool);
            }
        }

        private void UnregisterPool(Pool pool)
        {
            if (_poolsWithNullTag.Remove(pool))
            {
                return;
            }

            string emptyTag = null;

            foreach (var pair in _poolsByTag)
            {
                if (!pair.Value.Remove(pool))
                {
                    continue;
                }

                if (pair.Value.Count == 0)
                {
                    emptyTag = pair.Key;
                }

                break;
            }

            if (emptyTag != null)
            {
                _poolsByTag.Remove(emptyTag);
            }
        }
        
        public int GetPoolObjectsCount(string poolTag)
        {
            if (!TryGetPoolsByTag(poolTag, out var matchingPools) || matchingPools.Count == 0)
            {
                Debug.LogWarning($"There is no pool named {poolTag}");
                return -1;
            }

            var objectsCount = 0;

            for (var i = 0; i < matchingPools.Count; i++)
            {
                objectsCount += matchingPools[i].CurrentObjectsCount;
            }

            return objectsCount;
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
            if (!TryGetPoolsByTag(poolTag, out var poolsToUse) || poolsToUse.Count == 0)
            {
                Debug.LogWarning($"Object pool with tag '{poolTag}' doesn't exists");
                return null;
            }

            // Preserve random selection between pools sharing the same tag.
            var poolToUse = poolsToUse[UnityEngine.Random.Range(0, poolsToUse.Count)];
            var objToSpawn = poolToUse.TakeFirstAvailable();

            // Expand only the selected pool and acquire the newly available object without scanning.
            if (objToSpawn is null)
            {
                CreateNewPoolObject(poolToUse.referenceObject, poolToUse, true, poolToUse.instantiateFunc);
                objToSpawn = poolToUse.TakeFirstAvailable();
            }

            //Return null if last object is null;
            if (objToSpawn == null)
            {
                Debug.Log($"object from pool '{poolTag}' you trying to spawn is null");
                return null;
            }

            try
            {
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
                    CallSpawns(objToSpawn, data);
                }
                else
                {
                    CallSpawn(objToSpawn, data);
                }

                objToSpawn.Used = true;
                objToSpawn.AvailabilityReserved = false;
                return objToSpawn.GameObject;
            }
            catch
            {
                if (!objToSpawn.Used)
                {
                    poolToUse.ReturnToAvailable(objToSpawn);
                }

                throw;
            }
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
            if (objectToDespawn == null || pools == null)
            {
                return false;
            }

            if (!_pooledObjectsByGameObject.TryGetValue(objectToDespawn, out var pooledObject))
            {
                return false;
            }

            TraversePooledObjectAndDespawnOtherPooledObjects(objectToDespawn.transform);
            return TryDespawn(pooledObject);
        }

        private bool TryDespawn(PooledGameObject pgo)
        {
            if (pgo == null || !pgo.Used)
            {
                return true;
            }

            CallDespawns(pgo);

            pgo.Used = false;

            try
            {
                ReturnToPool(pgo.GameObject);
            }
            finally
            {
                pgo.OwnerPool?.ReturnToAvailable(pgo);
            }

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
            if (obj != null && _pooledObjectsByGameObject.TryGetValue(obj, out var pooledObject))
            {
                return new ObjectPooledState(true, pooledObject.Used);
            }

            return new ObjectPooledState(false, false);
        }

        private void CallSpawn(PooledGameObject pooledObject, object data)
        {
            if (pooledObject.SpawnHandlers.Length == 0)
            {
                return;
            }

            pooledObject.SpawnHandlers[0].Invoke(data);
        }

        private void CallSpawns(PooledGameObject pooledObject, object data)
        {
            for (int i = 0; i < pooledObject.SpawnHandlers.Length; i++)
            {
                pooledObject.SpawnHandlers[i].Invoke(data);
            }
        }

        private void CallDespawns(PooledGameObject pooledObject)
        {
            for (var i = 0; i < pooledObject.DespawnHandlers.Length; i++)
            {
                var despawnable = pooledObject.DespawnHandlers[i];
                var unityObject = despawnable as UnityEngine.Object;

                if (unityObject != null)
                {
                    despawnable.OnDespawn();
                }
            }
        }
        
        private GameObject CreateNewPoolObject(GameObject obj, Pool pool, bool addToPoolParent = true, Func<GameObject, Vector3, Quaternion, Transform, GameObject> instantiateFunc = null)
        {
            Transform poolParent = null;

            if(addToPoolParent)
            {
                poolParent = objectPoolParent;
            }

            GameObject poolObj = Create(obj, poolParent, instantiateFunc);

            poolObj.Disable();

            PooledGameObject pgo = new PooledGameObject()
            {
                GameObject = poolObj,
                Used = false,
                SpawnHandlers = FindSpawnHandlers(poolObj),
                DespawnHandlers = poolObj.GetComponentsInChildren<IDespawn>(true)
            };

            pool.AddPooledObject(pgo);

            try
            {
                RegisterPooledObject(pgo);
            }
            catch
            {
                pool.RemovePooledObject(pgo);
                throw;
            }

            return poolObj;
        }

        private void RegisterPooledObject(PooledGameObject pooledObject)
        {
            var gameObject = pooledObject?.GameObject;

            if (gameObject == null)
            {
                throw new InvalidOperationException("Cannot register a pooled object without a live GameObject");
            }

            _pooledObjectsByGameObject.Add(gameObject, pooledObject);
        }

        private void UnregisterPooledObject(PooledGameObject pooledObject)
        {
            var gameObject = pooledObject?.GameObject;

            if (!ReferenceEquals(gameObject, null))
            {
                _pooledObjectsByGameObject.Remove(gameObject);
            }
        }

        private PooledSpawnHandler[] FindSpawnHandlers(GameObject poolObject)
        {
            var components = poolObject.GetComponentsInChildren<MonoCached>(true);
            var handlerCount = 0;

            for (var i = 0; i < components.Length; i++)
            {
                if (components[i] is IPooledBase)
                {
                    handlerCount++;
                }
            }

            if (handlerCount == 0)
            {
                return Array.Empty<PooledSpawnHandler>();
            }

            var handlers = new PooledSpawnHandler[handlerCount];
            var handlerIndex = 0;

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];

                if (component is not IPooledBase)
                {
                    continue;
                }

                handlers[handlerIndex] = CreateSpawnHandler(component);
                handlerIndex++;
            }

            return handlers;
        }

        private PooledSpawnHandler CreateSpawnHandler(MonoCached component)
        {
            var interfaces = component.GetType().GetInterfaces();
            var invokerCount = 0;

            for (var i = 0; i < interfaces.Length; i++)
            {
                if (interfaces[i].GetMethod(
                        "InvokePooledSpawn",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic) != null)
                {
                    invokerCount++;
                }
            }

            var invokers = new Action<object>[invokerCount];
            var invokerIndex = 0;

            for (var i = 0; i < interfaces.Length; i++)
            {
                var invokeMethod = interfaces[i].GetMethod(
                    "InvokePooledSpawn",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

                if (invokeMethod == null)
                {
                    continue;
                }

                invokers[invokerIndex] = (Action<object>)invokeMethod.CreateDelegate(
                    typeof(Action<object>),
                    component);
                invokerIndex++;
            }

            return new PooledSpawnHandler(invokers);
        }
    }
    
    public sealed class Pool
    {
        private readonly List<PooledGameObject> _availableObjects = new();
        private int _nextObjectOrder;

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

            try
            {
                for (int i = 0; i < this.objects.Count; i++)
                {
                    AttachPooledObject(this.objects[i]);
                }
            }
            catch
            {
                DetachAllPooledObjects();
                throw;
            }
        }

        internal void AddPooledObject(PooledGameObject pooledObject)
        {
            objects.Add(pooledObject);

            try
            {
                AttachPooledObject(pooledObject);
            }
            catch
            {
                if (ReferenceEquals(pooledObject?.OwnerPool, this))
                {
                    RemoveAvailable(pooledObject);
                    DetachPooledObject(pooledObject);
                }

                objects.RemoveAt(objects.Count - 1);
                throw;
            }
        }

        internal bool RemovePooledObject(PooledGameObject pooledObject)
        {
            var index = objects.IndexOf(pooledObject);

            if (index < 0)
            {
                return false;
            }

            RemovePooledObjectAt(index);
            return true;
        }

        internal PooledGameObject RemovePooledObjectAt(int index)
        {
            var pooledObject = objects[index];
            RemoveAvailable(pooledObject);
            objects.RemoveAt(index);
            DetachPooledObject(pooledObject);
            return pooledObject;
        }

        internal PooledGameObject TakeFirstAvailable()
        {
            while (_availableObjects.Count > 0)
            {
                var pooledObject = _availableObjects[0];
                RemoveAvailableAt(0);

                if (!pooledObject.Used)
                {
                    pooledObject.AvailabilityReserved = true;
                    return pooledObject;
                }
            }

            return null;
        }

        internal bool ReturnToAvailable(PooledGameObject pooledObject)
        {
            if (pooledObject == null ||
                !ReferenceEquals(pooledObject.OwnerPool, this) ||
                pooledObject.Used ||
                pooledObject.AvailableHeapIndex >= 0)
            {
                return false;
            }

            pooledObject.AvailabilityReserved = false;
            AddAvailable(pooledObject);
            return true;
        }

        internal void DetachAllPooledObjects()
        {
            for (int i = 0; i < _availableObjects.Count; i++)
            {
                _availableObjects[i].AvailableHeapIndex = -1;
            }

            _availableObjects.Clear();

            for (int i = 0; i < objects.Count; i++)
            {
                DetachPooledObject(objects[i]);
            }
        }

        private void AttachPooledObject(PooledGameObject pooledObject)
        {
            if (pooledObject == null)
            {
                throw new ArgumentNullException(nameof(pooledObject));
            }

            if (pooledObject.OwnerPool != null)
            {
                throw new InvalidOperationException("PooledGameObject already belongs to a Pool");
            }

            pooledObject.OwnerPool = this;
            pooledObject.PoolOrder = _nextObjectOrder++;
            pooledObject.AvailableHeapIndex = -1;
            pooledObject.AvailabilityReserved = false;

            if (!pooledObject.Used)
            {
                AddAvailable(pooledObject);
            }
        }

        private void DetachPooledObject(PooledGameObject pooledObject)
        {
            if (pooledObject == null || !ReferenceEquals(pooledObject.OwnerPool, this))
            {
                return;
            }

            pooledObject.OwnerPool = null;
            pooledObject.AvailableHeapIndex = -1;
            pooledObject.AvailabilityReserved = false;
        }

        private void AddAvailable(PooledGameObject pooledObject)
        {
            var index = _availableObjects.Count;
            _availableObjects.Add(pooledObject);
            pooledObject.AvailableHeapIndex = index;
            SiftAvailableUp(index);
        }

        private bool RemoveAvailable(PooledGameObject pooledObject)
        {
            var index = pooledObject?.AvailableHeapIndex ?? -1;

            if (index < 0 ||
                index >= _availableObjects.Count ||
                !ReferenceEquals(_availableObjects[index], pooledObject))
            {
                return false;
            }

            RemoveAvailableAt(index);
            return true;
        }

        private void RemoveAvailableAt(int index)
        {
            var lastIndex = _availableObjects.Count - 1;
            var removedObject = _availableObjects[index];
            var lastObject = _availableObjects[lastIndex];
            _availableObjects.RemoveAt(lastIndex);
            removedObject.AvailableHeapIndex = -1;

            if (index == lastIndex)
            {
                return;
            }

            _availableObjects[index] = lastObject;
            lastObject.AvailableHeapIndex = index;

            var parentIndex = (index - 1) / 2;

            if (index > 0 &&
                _availableObjects[index].PoolOrder < _availableObjects[parentIndex].PoolOrder)
            {
                SiftAvailableUp(index);
            }
            else
            {
                SiftAvailableDown(index);
            }
        }

        private void SiftAvailableUp(int index)
        {
            while (index > 0)
            {
                var parentIndex = (index - 1) / 2;

                if (_availableObjects[parentIndex].PoolOrder <= _availableObjects[index].PoolOrder)
                {
                    return;
                }

                SwapAvailable(index, parentIndex);
                index = parentIndex;
            }
        }

        private void SiftAvailableDown(int index)
        {
            while (true)
            {
                var leftIndex = (index * 2) + 1;

                if (leftIndex >= _availableObjects.Count)
                {
                    return;
                }

                var rightIndex = leftIndex + 1;
                var smallestIndex = rightIndex < _availableObjects.Count &&
                    _availableObjects[rightIndex].PoolOrder < _availableObjects[leftIndex].PoolOrder
                        ? rightIndex
                        : leftIndex;

                if (_availableObjects[index].PoolOrder <= _availableObjects[smallestIndex].PoolOrder)
                {
                    return;
                }

                SwapAvailable(index, smallestIndex);
                index = smallestIndex;
            }
        }

        private void SwapAvailable(int firstIndex, int secondIndex)
        {
            var first = _availableObjects[firstIndex];
            var second = _availableObjects[secondIndex];
            _availableObjects[firstIndex] = second;
            _availableObjects[secondIndex] = first;
            first.AvailableHeapIndex = secondIndex;
            second.AvailableHeapIndex = firstIndex;
        }
    }
    
    public sealed class PooledGameObject
    {
        public GameObject GameObject;
        public bool Used;
        internal Pool OwnerPool;
        internal int PoolOrder;
        internal int AvailableHeapIndex = -1;
        internal bool AvailabilityReserved;
        // Lifecycle metadata captures creation-time hierarchy order; runtime changes are intentionally not rescanned.
        internal PooledSpawnHandler[] SpawnHandlers = Array.Empty<PooledSpawnHandler>();
        internal IDespawn[] DespawnHandlers = Array.Empty<IDespawn>();
    }

    internal sealed class PooledSpawnHandler
    {
        private readonly Action<object>[] _invokers;

        public PooledSpawnHandler(Action<object>[] invokers)
        {
            _invokers = invokers;
        }

        public void Invoke(object data)
        {
            for (var i = 0; i < _invokers.Length; i++)
            {
                _invokers[i].Invoke(data);
            }
        }
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
