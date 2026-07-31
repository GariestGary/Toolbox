using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VolumeBox.Toolbox.Tests
{
    [PrebuildSetup(typeof(TestPrebuild))]
    internal class PoolerTests : ToolboxTestBase
    {
        private int spawnCount;
        
        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator PoolerSpawnObjectTest()
        {
            GameObject pooledGO = new GameObject("Pooler Test");
            var poolName = "Pool Spawn Test";
            Toolbox.Pooler.TryAddPool(poolName, pooledGO, 3);

            var test = Toolbox.Pooler.Spawn(poolName, Vector3.zero, Quaternion.identity);
            Toolbox.Pooler.Spawn(poolName, Vector3.zero, Quaternion.identity);
            Toolbox.Pooler.Spawn(poolName, Vector3.zero, Quaternion.identity);
            Toolbox.Pooler.DespawnOrDestroy(test);

            var obj = Toolbox.Pooler.Spawn(poolName, Vector3.zero, Quaternion.identity);
            
            Assert.AreEqual
            (
                true,
                test == obj
            );

            yield return null;
        }
        
        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator PoolerInstantiateFuncObjectTest()
        {
            var initialName = "Pooler Test";
            GameObject pooledGO = new GameObject(initialName);
            var poolName = "Pool Instantiate Test";
            Toolbox.Pooler.TryAddPool(poolName, pooledGO, 3, InstantiateFuncRenameObj);

            var test = Toolbox.Pooler.Spawn(poolName, Vector3.zero, Quaternion.identity);
            
            Assert.AreEqual
            (
                "instantiated",
                test.name
            );

            yield return null;
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator PoolerSpawnActionObjectTest()
        {
            spawnCount = 0;
            var initialName = "Pooler Test";
            GameObject pooledGO = new GameObject(initialName);
            var poolName = "Pool Spawn Action Test";
            Toolbox.Pooler.TryAddPool(poolName, pooledGO, 3, null, SpawnActionRenameObj);

            spawnCount++;
            var test = Toolbox.Pooler.Spawn(poolName, Vector3.zero, Quaternion.identity);
            
            Assert.AreEqual
            (
                $"spawned {spawnCount}",
                test.name
            );

            spawnCount++;
            test = Toolbox.Pooler.Spawn(poolName, Vector3.zero, Quaternion.identity);

            Assert.AreEqual
            (
                $"spawned {spawnCount}",
                test.name
            );

            yield return null;
        }

        private GameObject InstantiateFuncRenameObj(GameObject obj, Vector3 position, Quaternion rotation, Transform parent)
        {
            var inst = Object.Instantiate(obj, position, rotation, parent);
            inst.name = "instantiated";
            return inst;
        }

        private void SpawnActionRenameObj(GameObject obj)
        {
            obj.name = $"spawned {spawnCount}";
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator SpawnMethodCallTest()
        {
            GameObject defaultObj = new GameObject("Default Object");
            var defaultComp = defaultObj.AddComponent<PooledDefaultObj>();
            defaultComp.compare = "null";
            Toolbox.Pooler.TryAddPool("Default", defaultObj, 3);
            
            GameObject genericObj = new GameObject("Generic Object");
            var genericComp = genericObj.AddComponent<PooledGenericObj>();
            genericComp.compare = "null";
            Toolbox.Pooler.TryAddPool("Generic", genericObj, 3);

            var testData = new TestData();
            testData.TestString = "data";
            var genericSpawned = Toolbox.Pooler.Spawn<PooledGenericObj>("Generic", testData);
            var defaultSpawned = Toolbox.Pooler.Spawn<PooledDefaultObj>("Default");
            
            Assert.AreEqual("data", genericSpawned.compare);
            Assert.AreEqual("data", defaultSpawned.compare);

            yield return null;
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator NestedSpawnObjectsTest()
        {
            GameObject pooledGO = new GameObject("Pooler Test Nest");
            Toolbox.Pooler.TryAddPool("Test pool Nest", pooledGO, 3);

            var nest1 = Toolbox.Pooler.Spawn("Test pool Nest");
            var nest2 = Toolbox.Pooler.Spawn("Test pool Nest", null, nest1.transform);
            var nest3 = Toolbox.Pooler.Spawn("Test pool Nest", null, nest2.transform);


            Assert.AreEqual(nest2.transform, nest3.transform.parent);
            Assert.AreEqual(nest1.transform, nest2.transform.parent);
            Toolbox.Pooler.DespawnOrDestroy(nest1);
            Assert.AreEqual(3, Toolbox.Pooler.GetPoolObjectsCount("Test pool Nest"));

            yield return null;
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator PoolerGCTest()
        {
            GameObject test = new GameObject();
            Toolbox.Pooler.TryAddPool("Test pool GC", test, 5);

            Assert.AreEqual(5, Toolbox.Pooler.GetPoolObjectsCount("Test pool GC"));

            var objList = new List<GameObject>();

            for (int i = 0; i < 10; i++)
            {
                objList.Add(Toolbox.Pooler.Spawn("Test pool GC", Vector3.zero, Quaternion.identity));
            }

            foreach (var obj in objList)
            {
                Toolbox.Pooler.DespawnOrDestroy(obj);
            }    

            Assert.AreEqual(10, Toolbox.Pooler.GetPoolObjectsCount("Test pool GC"));

            Toolbox.Pooler.ForceGarbageCollector();

            Assert.AreEqual(5, Toolbox.Pooler.GetPoolObjectsCount("Test pool GC"));

            yield return null;
        }
    }
}
