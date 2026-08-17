using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VolumeBox.Toolbox.Tests
{
    [PrebuildSetup(typeof(TestPrebuild))]
    internal class PoolerTests : ToolboxTestBase
    {
        private static readonly FieldInfo PoolsByTagField = typeof(Pooler).GetField(
            "_poolsByTag",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo PoolsWithNullTagField = typeof(Pooler).GetField(
            "_poolsWithNullTag",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private int spawnCount;

        [Test]
        public void TryDespawn_AfterClear_ReturnsFalse()
        {
            var poolerObject = new GameObject("Cleared Pooler Test");
            var objectToDespawn = new GameObject("Object To Despawn");
            var pooler = poolerObject.AddComponent<Pooler>();

            try
            {
                pooler.Clear();

                Assert.IsFalse(pooler.TryDespawn(objectToDespawn));
            }
            finally
            {
                Object.DestroyImmediate(objectToDespawn);
                Object.DestroyImmediate(poolerObject);
            }
        }

        [Test]
        public void SpawnUsesSinglePoolRegisteredForTag()
        {
            const string poolTag = "Lookup Single Enemy";
            var prefab = new GameObject("Single Enemy Prefab");
            var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);

            var poolsByTag = GetPoolsByTag(Toolbox.Pooler);

            Assert.IsTrue(poolsByTag.TryGetValue(poolTag, out var matchingPools));
            Assert.AreEqual(1, matchingPools.Count);
            Assert.AreSame(pool, matchingPools[0]);

            var spawned = Toolbox.Pooler.Spawn(poolTag);

            Assert.AreSame(pool.objects[0].GameObject, spawned);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void SpawnWithMissingTagReturnsNull()
        {
            const string missingTag = "Lookup Missing Pool";

            LogAssert.Expect(LogType.Warning, $"Object pool with tag '{missingTag}' doesn't exists");

            Assert.IsNull(Toolbox.Pooler.Spawn(missingTag));
        }

        [Test]
        public void MultiplePoolsWithSameTagAreRegisteredExactlyOnce()
        {
            const string poolTag = "Lookup Shared Enemy";
            var firstPrefab = new GameObject("First Shared Enemy Prefab");
            var secondPrefab = new GameObject("Second Shared Enemy Prefab");
            var firstPool = Toolbox.Pooler.TryAddPool(poolTag, firstPrefab, 1);
            var secondPool = Toolbox.Pooler.TryAddPool(poolTag, secondPrefab, 1);

            var matchingPools = GetPoolsByTag(Toolbox.Pooler)[poolTag];

            Assert.AreEqual(2, matchingPools.Count);
            CollectionAssert.AreEquivalent(new[] { firstPool, secondPool }, matchingPools);
            Assert.AreEqual(2, Toolbox.Pooler.GetPoolObjectsCount(poolTag));
            Assert.IsNotNull(Toolbox.Pooler.Spawn(poolTag));

            Object.DestroyImmediate(firstPrefab);
            Object.DestroyImmediate(secondPrefab);
        }

        [Test]
        public void RemovingTagDoesNotAffectPoolsWithDifferentTags()
        {
            const string enemyTag = "Lookup Isolated Enemy";
            const string projectileTag = "Lookup Isolated Projectile";
            var enemyPrefab = new GameObject("Isolated Enemy Prefab");
            var projectilePrefab = new GameObject("Isolated Projectile Prefab");
            Toolbox.Pooler.TryAddPool(enemyTag, enemyPrefab, 1);
            var projectilePool = Toolbox.Pooler.TryAddPool(projectileTag, projectilePrefab, 1);

            Assert.IsTrue(Toolbox.Pooler.TryRemovePool(enemyTag));

            var poolsByTag = GetPoolsByTag(Toolbox.Pooler);

            Assert.IsFalse(poolsByTag.ContainsKey(enemyTag));
            Assert.AreSame(projectilePool, poolsByTag[projectileTag][0]);
            Assert.AreSame(projectilePool.objects[0].GameObject, Toolbox.Pooler.Spawn(projectileTag));

            Object.DestroyImmediate(enemyPrefab);
            Object.DestroyImmediate(projectilePrefab);
        }

        [Test]
        public void RemovingSameTagPoolsUpdatesAndThenRemovesLookupEntry()
        {
            const string poolTag = "Lookup Removable Enemy";
            var firstPrefab = new GameObject("First Removable Enemy Prefab");
            var secondPrefab = new GameObject("Second Removable Enemy Prefab");
            var firstPool = Toolbox.Pooler.TryAddPool(poolTag, firstPrefab, 1);
            var secondPool = Toolbox.Pooler.TryAddPool(poolTag, secondPrefab, 1);

            Assert.IsTrue(Toolbox.Pooler.TryRemovePool(firstPool));

            var poolsByTag = GetPoolsByTag(Toolbox.Pooler);

            Assert.AreEqual(1, poolsByTag[poolTag].Count);
            Assert.AreSame(secondPool, poolsByTag[poolTag][0]);
            Assert.AreSame(secondPool.objects[0].GameObject, Toolbox.Pooler.Spawn(poolTag));

            Assert.IsTrue(Toolbox.Pooler.TryRemovePool(secondPool));
            Assert.IsFalse(poolsByTag.ContainsKey(poolTag));

            LogAssert.Expect(LogType.Warning, $"Object pool with tag '{poolTag}' doesn't exists");
            Assert.IsNull(Toolbox.Pooler.Spawn(poolTag));

            Object.DestroyImmediate(firstPrefab);
            Object.DestroyImmediate(secondPrefab);
        }

        [Test]
        public void ClearAndReinitializeDoNotRetainStaleLookupEntries()
        {
            const string staleTag = "Lookup Stale Pool";
            const string freshTag = "Lookup Fresh Pool";
            var stalePrefab = new GameObject("Stale Pool Prefab");
            var freshPrefab = new GameObject("Fresh Pool Prefab");
            Toolbox.Pooler.TryAddPool(staleTag, stalePrefab, 1);

            Toolbox.Pooler.Clear();

            Assert.AreEqual(0, GetPoolsByTag(Toolbox.Pooler).Count);
            Assert.AreEqual(0, GetPoolsWithNullTag(Toolbox.Pooler).Count);
            LogAssert.Expect(LogType.Warning, $"Object pool with tag '{staleTag}' doesn't exists");
            Assert.IsNull(Toolbox.Pooler.Spawn(staleTag));

            Toolbox.Pooler.Initialize(Toolbox.Messenger, Toolbox.Updater);
            Toolbox.Pooler.DisableGC();

            Assert.IsFalse(GetPoolsByTag(Toolbox.Pooler).ContainsKey(staleTag));

            var freshPool = Toolbox.Pooler.TryAddPool(freshTag, freshPrefab, 1);

            Assert.AreSame(freshPool.objects[0].GameObject, Toolbox.Pooler.Spawn(freshTag));
            Object.DestroyImmediate(stalePrefab);
            Object.DestroyImmediate(freshPrefab);
        }

        [Test]
        public void NullTagPreservesExistingLookupBehavior()
        {
            var prefab = new GameObject("Null Tag Pool Prefab");
            var pool = Toolbox.Pooler.TryAddPool(null, prefab, 1);

            Assert.AreEqual(1, GetPoolsWithNullTag(Toolbox.Pooler).Count);
            Assert.AreSame(pool.objects[0].GameObject, Toolbox.Pooler.Spawn(null));
            Object.DestroyImmediate(prefab);
        }
        
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

        private static Dictionary<string, List<Pool>> GetPoolsByTag(Pooler pooler)
        {
            return (Dictionary<string, List<Pool>>)PoolsByTagField.GetValue(pooler);
        }

        private static List<Pool> GetPoolsWithNullTag(Pooler pooler)
        {
            return (List<Pool>)PoolsWithNullTagField.GetValue(pooler);
        }

        [Test]
        public void SpawnWithoutPooledHandlersStillSucceeds()
        {
            const string poolTag = "Cached Handlers None";
            var prefab = new GameObject("No Handler Prefab");
            var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);

            var spawned = Toolbox.Pooler.Spawn(poolTag);

            Assert.AreSame(pool.objects[0].GameObject, spawned);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void TraversalDisabledInvokesOnlyFirstCachedHandler()
        {
            const string poolTag = "Cached Handlers First Only";
            var prefab = CreateMultipleHandlerPrefab("First Only Handler Prefab");
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            PooledDefaultObj.InvocationOrder.Clear();

            var spawned = Toolbox.Pooler.Spawn(poolTag);
            var handlers = spawned.GetComponentsInChildren<PooledDefaultObj>(true);

            CollectionAssert.AreEqual(new[] { "root-first" }, PooledDefaultObj.InvocationOrder);
            Assert.AreEqual(1, handlers[0].SpawnCount);
            Assert.AreEqual(0, handlers[1].SpawnCount);
            Assert.AreEqual(0, handlers[2].SpawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void TraversalEnabledInvokesAllCachedHandlersInHierarchyOrder()
        {
            const string poolTag = "Cached Handlers All";
            var prefab = CreateMultipleHandlerPrefab("All Handler Prefab");
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            PooledDefaultObj.InvocationOrder.Clear();

            var spawned = Toolbox.Pooler.Spawn(poolTag, traverseHierarchy: true);
            var handlers = spawned.GetComponentsInChildren<PooledDefaultObj>(true);

            CollectionAssert.AreEqual(
                new[] { "root-first", "root-second", "inactive-child" },
                PooledDefaultObj.InvocationOrder);
            Assert.AreEqual(1, handlers[0].SpawnCount);
            Assert.AreEqual(1, handlers[1].SpawnCount);
            Assert.AreEqual(1, handlers[2].SpawnCount);
            Assert.IsFalse(handlers[2].gameObject.activeSelf);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedHandlerRemainsValidAcrossDespawnAndRespawn()
        {
            const string poolTag = "Cached Handler Repeated Spawn";
            var prefab = new GameObject("Repeated Handler Prefab");
            prefab.AddComponent<PooledDefaultObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);

            var firstSpawn = Toolbox.Pooler.Spawn(poolTag);
            var handler = firstSpawn.GetComponent<PooledDefaultObj>();

            Assert.AreEqual(1, handler.SpawnCount);
            Assert.IsTrue(Toolbox.Pooler.DespawnOrDestroy(firstSpawn));

            var secondSpawn = Toolbox.Pooler.Spawn(poolTag);

            Assert.AreSame(firstSpawn, secondSpawn);
            Assert.AreEqual(2, handler.SpawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void ExpandedPoolCachesGenericHandlerForNewInstance()
        {
            const string poolTag = "Cached Handler Expanded Pool";
            var prefab = new GameObject("Expanded Generic Handler Prefab");
            prefab.AddComponent<PooledGenericObj>();
            var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var firstData = new TestData { TestString = "first" };
            var secondData = new TestData { TestString = "second" };

            var firstSpawn = Toolbox.Pooler.Spawn<PooledGenericObj>(poolTag, firstData);
            var expandedSpawn = Toolbox.Pooler.Spawn<PooledGenericObj>(poolTag, secondData);

            Assert.AreEqual(2, pool.objects.Count);
            Assert.AreNotSame(firstSpawn, expandedSpawn);
            Assert.AreEqual("first", firstSpawn.compare);
            Assert.AreEqual("second", expandedSpawn.compare);
            Assert.AreEqual(1, firstSpawn.SpawnCount);
            Assert.AreEqual(1, expandedSpawn.SpawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedNonGenericInvokerIgnoresSuppliedData()
        {
            const string poolTag = "Cached Invoker Non Generic";
            var prefab = new GameObject("Non Generic Invoker Prefab");
            prefab.AddComponent<PooledDefaultObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);

            var spawned = Toolbox.Pooler.Spawn(poolTag, data: new WrongTestData());
            var handler = spawned.GetComponent<PooledDefaultObj>();

            Assert.AreEqual(1, handler.SpawnCount);
            Assert.AreEqual("data", handler.compare);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedGenericInvokerReceivesSameReferenceInstance()
        {
            const string poolTag = "Cached Invoker Reference Data";
            var prefab = new GameObject("Reference Data Invoker Prefab");
            prefab.AddComponent<PooledGenericObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var data = new TestData { TestString = "reference" };

            var handler = Toolbox.Pooler.Spawn<PooledGenericObj>(poolTag, data);

            Assert.AreSame(data, handler.ReceivedData);
            Assert.AreEqual("reference", handler.compare);
            Assert.AreEqual(1, handler.SpawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedGenericInvokerReceivesStructValue()
        {
            const string poolTag = "Cached Invoker Struct Data";
            var prefab = new GameObject("Struct Data Invoker Prefab");
            prefab.AddComponent<PooledStructObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var data = new TestStruct { Value = 42 };

            var handler = Toolbox.Pooler.Spawn<PooledStructObj>(poolTag, data);

            Assert.AreEqual(42, handler.ReceivedData.Value);
            Assert.AreEqual(1, handler.SpawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedGenericInvokerAcceptsNullForReferenceData()
        {
            const string poolTag = "Cached Invoker Null Reference";
            var prefab = new GameObject("Null Reference Invoker Prefab");
            prefab.AddComponent<PooledGenericObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);

            var handler = Toolbox.Pooler.Spawn<PooledGenericObj>(poolTag, data: null);

            Assert.IsNull(handler.ReceivedData);
            Assert.IsNull(handler.compare);
            Assert.AreEqual(1, handler.SpawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedGenericInvokerAcceptsNullForNullableStruct()
        {
            const string poolTag = "Cached Invoker Nullable Struct";
            var prefab = new GameObject("Nullable Struct Invoker Prefab");
            prefab.AddComponent<PooledNullableStructObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);

            var handler = Toolbox.Pooler.Spawn<PooledNullableStructObj>(poolTag, data: null);

            Assert.IsFalse(handler.ReceivedData.HasValue);
            Assert.AreEqual(1, handler.SpawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedGenericInvokerRejectsIncorrectDataType()
        {
            const string poolTag = "Cached Invoker Incorrect Data";
            var prefab = new GameObject("Incorrect Data Invoker Prefab");
            prefab.AddComponent<PooledGenericObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);

            Assert.Throws<System.InvalidCastException>(() =>
                Toolbox.Pooler.Spawn(poolTag, data: new WrongTestData()));

            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedGenericInvokerRejectsNullForNonNullableStruct()
        {
            const string poolTag = "Cached Invoker Null Struct";
            var prefab = new GameObject("Null Struct Invoker Prefab");
            prefab.AddComponent<PooledStructObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);

            Assert.Throws<System.NullReferenceException>(() =>
                Toolbox.Pooler.Spawn(poolTag, data: null));

            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedHandlerPreservesMultiplePooledInterfaces()
        {
            const string poolTag = "Cached Invoker Multiple Interfaces";
            var prefab = new GameObject("Multiple Interface Invoker Prefab");
            prefab.AddComponent<MultiplePooledInterfacesObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var data = new OtherTestData { TestString = "multiple" };

            var handler = Toolbox.Pooler.Spawn<MultiplePooledInterfacesObj>(poolTag, data);

            Assert.AreEqual(1, handler.TestDataSpawnCount);
            Assert.AreEqual(1, handler.OtherDataSpawnCount);
            Assert.AreSame(data, handler.ReceivedTestData);
            Assert.AreSame(data, handler.ReceivedOtherData);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void DespawnWithoutHandlersStillSucceeds()
        {
            const string poolTag = "Cached Despawn None";
            var prefab = new GameObject("No Despawn Handler Prefab");
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var spawned = Toolbox.Pooler.Spawn(poolTag);

            Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawned));
            Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawned));
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedDespawnHandlerRunsExactlyOnce()
        {
            const string poolTag = "Cached Despawn Single";
            var prefab = new GameObject("Single Despawn Handler Prefab");
            prefab.AddComponent<DespawnTestObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var spawned = Toolbox.Pooler.Spawn(poolTag);
            var handler = spawned.GetComponent<DespawnTestObj>();

            Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawned));
            Assert.AreEqual(1, handler.DespawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedDespawnHandlersPreserveHierarchyAndComponentOrder()
        {
            const string poolTag = "Cached Despawn Ordered Hierarchy";
            var prefab = CreateMultipleDespawnHandlerPrefab("Ordered Despawn Handler Prefab");
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            DespawnTestObj.InvocationOrder.Clear();
            var spawned = Toolbox.Pooler.Spawn(poolTag);

            Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawned));

            CollectionAssert.AreEqual(
                new[] { "root-first", "root-second", "inactive-child", "nested-grandchild" },
                DespawnTestObj.InvocationOrder);

            var handlers = spawned.GetComponentsInChildren<DespawnTestObj>(true);

            Assert.AreEqual(4, handlers.Length);

            for (var i = 0; i < handlers.Length; i++)
            {
                Assert.AreEqual(1, handlers[i].DespawnCount);
            }

            Assert.IsFalse(handlers[2].gameObject.activeSelf);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CachedDespawnHandlerRunsOncePerActualReuseCycle()
        {
            const string poolTag = "Cached Despawn Repeated Reuse";
            var prefab = new GameObject("Repeated Despawn Handler Prefab");
            prefab.AddComponent<DespawnTestObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var spawned = Toolbox.Pooler.Spawn(poolTag);
            var handler = spawned.GetComponent<DespawnTestObj>();

            Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawned));
            Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawned));
            Assert.AreEqual(1, handler.DespawnCount);

            Assert.AreSame(spawned, Toolbox.Pooler.Spawn(poolTag));
            Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawned));
            Assert.AreEqual(2, handler.DespawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void ExpandedPoolCachesDespawnHandlersForNewInstance()
        {
            const string poolTag = "Cached Despawn Expanded Pool";
            var prefab = new GameObject("Expanded Despawn Handler Prefab");
            prefab.AddComponent<DespawnTestObj>();
            var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var firstSpawn = Toolbox.Pooler.Spawn(poolTag);
            var expandedSpawn = Toolbox.Pooler.Spawn(poolTag);
            var expandedHandler = expandedSpawn.GetComponent<DespawnTestObj>();

            Assert.AreEqual(2, pool.objects.Count);
            Assert.AreNotSame(firstSpawn, expandedSpawn);
            Assert.IsTrue(Toolbox.Pooler.TryDespawn(expandedSpawn));
            Assert.AreEqual(1, expandedHandler.DespawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void RemovingUsedPoolInvokesDespawnHandlerOnlyOnce()
        {
            const string poolTag = "Cached Despawn Removed Pool";
            var prefab = new GameObject("Removed Pool Despawn Handler Prefab");
            prefab.AddComponent<DespawnTestObj>();
            var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var spawned = Toolbox.Pooler.Spawn(poolTag);
            var handler = spawned.GetComponent<DespawnTestObj>();

            Assert.IsTrue(Toolbox.Pooler.TryRemovePool(pool));
            Assert.AreEqual(1, handler.DespawnCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void DestroyedCachedDespawnHandlerIsSkippedSafely()
        {
            const string poolTag = "Cached Despawn Destroyed Handler";
            var prefab = new GameObject("Destroyed Despawn Handler Prefab");
            prefab.AddComponent<DespawnTestObj>();
            Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var spawned = Toolbox.Pooler.Spawn(poolTag);
            var handler = spawned.GetComponent<DespawnTestObj>();

            Object.DestroyImmediate(handler);

            Assert.DoesNotThrow(() => Toolbox.Pooler.TryDespawn(spawned));
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void InitialPooledObjectLifecycleInitializesExactlyOnce()
        {
            const string poolTag = "Single Initialization Initial Pool";
            var prefab = new GameObject("Initial Lifecycle Prefab");
            prefab.AddComponent<PooledLifecycleTestObj>();
            var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
            var pooledLifecycle = pool.objects[0].GameObject.GetComponent<PooledLifecycleTestObj>();

            Assert.AreEqual(1, pooledLifecycle.RiseCount);
            Assert.AreEqual(1, pooledLifecycle.ReadyCount);

            var spawned = Toolbox.Pooler.Spawn(poolTag);
            Toolbox.Pooler.TryDespawn(spawned);
            Toolbox.Pooler.Spawn(poolTag);

            Assert.AreEqual(1, pooledLifecycle.RiseCount);
            Assert.AreEqual(1, pooledLifecycle.ReadyCount);
            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void ExpandedPooledObjectLifecycleInitializesExactlyOnce()
        {
            const string poolTag = "Single Initialization Expanded Pool";
            var prefab = new GameObject("Expanded Lifecycle Prefab");
            prefab.AddComponent<PooledLifecycleTestObj>();
            var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);

            Toolbox.Pooler.Spawn(poolTag);
            Toolbox.Pooler.Spawn(poolTag);

            Assert.AreEqual(2, pool.objects.Count);

            for (var i = 0; i < pool.objects.Count; i++)
            {
                var lifecycle = pool.objects[i].GameObject.GetComponent<PooledLifecycleTestObj>();
                Assert.AreEqual(1, lifecycle.RiseCount);
                Assert.AreEqual(1, lifecycle.ReadyCount);
            }

            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void CustomInstantiationInitializesPooledLifecycleExactlyOnce()
        {
            const string poolTag = "Single Initialization Custom Instantiation";
            var prefab = new GameObject("Custom Lifecycle Prefab");
            prefab.AddComponent<PooledLifecycleTestObj>();
            var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1, InstantiateFuncRenameObj);
            var pooledObject = pool.objects[0].GameObject;
            var lifecycle = pooledObject.GetComponent<PooledLifecycleTestObj>();

            Assert.AreEqual("instantiated", pooledObject.name);
            Assert.AreEqual(1, lifecycle.RiseCount);
            Assert.AreEqual(1, lifecycle.ReadyCount);
            Object.DestroyImmediate(prefab);
        }

        private static GameObject CreateMultipleHandlerPrefab(string name)
        {
            var prefab = new GameObject(name);
            prefab.AddComponent<PooledDefaultObj>().HandlerId = "root-first";
            prefab.AddComponent<PooledDefaultObj>().HandlerId = "root-second";

            var inactiveChild = new GameObject("Inactive Handler Child");
            inactiveChild.transform.SetParent(prefab.transform);
            inactiveChild.AddComponent<PooledDefaultObj>().HandlerId = "inactive-child";
            inactiveChild.SetActive(false);

            return prefab;
        }

        private static GameObject CreateMultipleDespawnHandlerPrefab(string name)
        {
            var prefab = new GameObject(name);
            prefab.AddComponent<DespawnTestObj>().HandlerId = "root-first";
            prefab.AddComponent<DespawnTestObj>().HandlerId = "root-second";

            var inactiveChild = new GameObject("Inactive Despawn Handler Child");
            inactiveChild.transform.SetParent(prefab.transform);
            inactiveChild.AddComponent<DespawnTestObj>().HandlerId = "inactive-child";

            var nestedGrandchild = new GameObject("Nested Despawn Handler Grandchild");
            nestedGrandchild.transform.SetParent(inactiveChild.transform);
            nestedGrandchild.AddComponent<DespawnTestObj>().HandlerId = "nested-grandchild";
            inactiveChild.SetActive(false);

            return prefab;
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
