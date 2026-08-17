using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

namespace VolumeBox.Toolbox.Tests.Performance
{
    [Category("PoolerPerformance")]
    [PrebuildSetup(typeof(TestPrebuild))]
    internal sealed class PoolerPerformanceTests : PerformanceTestBase
    {
        private const int WarmupCount = 1;
        private const int DefaultMeasurementCount = 5;
        private const int ExpensiveMeasurementCount = 3;
        private const int CycleOperationsPerSample = 1_000;
        private const int LookupOperationsPerSample = 10_000;

        private static readonly FieldInfo PoolsField = typeof(Pooler).GetField(
            "pools",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly int[] ThroughputCounts =
        {
            100,
            1_000,
            5_000,
            10_000
        };

        private static readonly int[] HandlerComparisonCounts =
        {
            100,
            1_000
        };

        private static readonly int[] OtherPoolCounts =
        {
            1,
            10,
            100,
            1_000
        };

        private static readonly int[] SameTagPoolCounts =
        {
            1,
            10,
            100
        };

        private int _poolSequence;

        [TearDown]
        public void TearDownPoolerPerformanceObjects()
        {
            if (!Toolbox.HasInstance)
            {
                return;
            }

            var pooler = Toolbox.Pooler;
            pooler.DisableGC();
            var pools = PoolsField?.GetValue(pooler) as List<Pool>;

            if (pools != null)
            {
                for (int poolIndex = 0; poolIndex < pools.Count; poolIndex++)
                {
                    var objects = pools[poolIndex].objects;

                    for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
                    {
                        var pooledObject = objects[objectIndex];

                        if (pooledObject?.GameObject != null)
                        {
                            Object.DestroyImmediate(pooledObject.GameObject);
                        }
                    }
                }
            }

            pooler.Clear();
        }

        [Test, Performance]
        public void PrewarmedSpawnDespawn(
            [ValueSource(nameof(ThroughputCounts))] int objectCount)
        {
            var pooler = PreparePooler();
            var pool = CreatePool(pooler, "Prewarmed SpawnDespawn", objectCount);
            var spawned = new GameObject[objectCount];

            Measure.Method(() =>
                {
                    SpawnAll(pooler, pool.tag, spawned);
                    DespawnAll(pooler, spawned);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(GetMeasurementCount(objectCount))
                .IterationsPerMeasurement(1)
                .SampleGroup($"Pooler/Prewarmed/SpawnDespawn/{objectCount}")
                .GC()
                .Run();

            AssertSpawnedAndUnused(pool, spawned, objectCount);
        }

        [Test, Performance]
        public void PrewarmedSpawnOnly(
            [ValueSource(nameof(ThroughputCounts))] int objectCount)
        {
            var pooler = PreparePooler();
            var pool = CreatePool(pooler, "Prewarmed Spawn", objectCount);
            var spawned = new GameObject[objectCount];

            Measure.Method(() => SpawnAll(pooler, pool.tag, spawned))
                .CleanUp(() => DespawnAll(pooler, spawned))
                .WarmupCount(WarmupCount)
                .MeasurementCount(GetMeasurementCount(objectCount))
                .IterationsPerMeasurement(1)
                .SampleGroup($"Pooler/Prewarmed/SpawnOnly/{objectCount}")
                .GC()
                .Run();

            SpawnAll(pooler, pool.tag, spawned);
            AssertSpawnedAndUsed(pool, spawned, objectCount);
            DespawnAll(pooler, spawned);
        }

        [Test, Performance]
        public void PrewarmedDespawnOnly(
            [ValueSource(nameof(ThroughputCounts))] int objectCount)
        {
            var pooler = PreparePooler();
            var pool = CreatePool(
                pooler,
                "Prewarmed Despawn",
                objectCount,
                lifecycleHandlerCount: 1);
            var spawned = new GameObject[objectCount];

            PoolerBenchmarkLifecycle.ResetCounters();
            Measure.Method(() => DespawnAll(pooler, spawned))
                .SetUp(() => SpawnAll(pooler, pool.tag, spawned))
                .WarmupCount(WarmupCount)
                .MeasurementCount(GetMeasurementCount(objectCount))
                .IterationsPerMeasurement(1)
                .SampleGroup($"Pooler/Prewarmed/DespawnOnly/{objectCount}")
                .GC()
                .Run();

            PoolerBenchmarkLifecycle.ResetCounters();
            SpawnAll(pooler, pool.tag, spawned);
            PoolerBenchmarkLifecycle.DespawnCount = 0;
            DespawnAll(pooler, spawned);

            Assert.AreEqual(objectCount, PoolerBenchmarkLifecycle.DespawnCount);
            AssertSpawnedAndUnused(pool, spawned, objectCount);
        }

        [Test, Performance]
        public void IsObjectPooledAndUsedLookup(
            [ValueSource(nameof(ThroughputCounts))] int objectCount)
        {
            var pooler = PreparePooler();
            var pool = CreatePool(pooler, "Pooled Object Lookup", objectCount);
            var target = pool.objects[pool.objects.Count - 1].GameObject;
            var state = default(ObjectPooledState);

            Measure.Method(() =>
                {
                    for (int i = 0; i < LookupOperationsPerSample; i++)
                    {
                        state = pooler.IsObjectPooledAndUsed(target);
                    }
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(GetMeasurementCount(objectCount))
                .IterationsPerMeasurement(1)
                .SampleGroup($"Pooler/ObjectLookup/{objectCount}PooledObjects/{LookupOperationsPerSample}Queries")
                .GC()
                .Run();

            Assert.IsTrue(state.IsPooled);
            Assert.IsFalse(state.IsUsed);
        }

        [Test, Performance]
        public void LifecycleHandlerCount(
            [Values(0, 1, 3)] int handlerCount,
            [ValueSource(nameof(HandlerComparisonCounts))] int objectCount)
        {
            var pooler = PreparePooler();
            var pool = CreatePool(
                pooler,
                $"Lifecycle {handlerCount}",
                objectCount,
                lifecycleHandlerCount: handlerCount);
            var spawned = new GameObject[objectCount];

            PoolerBenchmarkLifecycle.ResetCounters();
            Measure.Method(() =>
                {
                    SpawnAll(pooler, pool.tag, spawned, traverseHierarchy: true);
                    DespawnAll(pooler, spawned);
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(DefaultMeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Pooler/Handlers/{handlerCount}Handlers/{objectCount}")
                .GC()
                .Run();

            PoolerBenchmarkLifecycle.ResetCounters();
            SpawnAll(pooler, pool.tag, spawned, traverseHierarchy: true);
            DespawnAll(pooler, spawned);

            Assert.AreEqual(objectCount * handlerCount, PoolerBenchmarkLifecycle.SpawnCount);
            Assert.AreEqual(objectCount * handlerCount, PoolerBenchmarkLifecycle.DespawnCount);
        }

        [Test, Performance]
        public void HierarchyTraversal([Values(false, true)] bool traverseHierarchy)
        {
            var pooler = PreparePooler();
            var prefab = CreateHierarchyPrefab();
            var pool = pooler.TryAddPool("Hierarchy Traversal", prefab, 1);
            GameObject spawned = null;

            PoolerBenchmarkLifecycle.ResetCounters();
            Measure.Method(() =>
                {
                    for (int i = 0; i < CycleOperationsPerSample; i++)
                    {
                        spawned = pooler.Spawn(
                            pool.tag,
                            data: null,
                            traverseHierarchy: traverseHierarchy);
                        pooler.TryDespawn(spawned);
                    }
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(DefaultMeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Pooler/Hierarchy/Traverse{traverseHierarchy}/{CycleOperationsPerSample}Cycles")
                .GC()
                .Run();

            PoolerBenchmarkLifecycle.ResetCounters();
            spawned = pooler.Spawn(
                pool.tag,
                data: null,
                traverseHierarchy: traverseHierarchy);
            Assert.IsNotNull(spawned);
            Assert.IsTrue(pooler.TryDespawn(spawned));
            Assert.AreEqual(traverseHierarchy ? 3 : 1, PoolerBenchmarkLifecycle.SpawnCount);
            Assert.AreEqual(3, PoolerBenchmarkLifecycle.DespawnCount);
        }

        [Test, Performance]
        public void GenericSpawnDispatch([Values("NonGeneric", "Int", "Reference")] string dispatchKind)
        {
            var pooler = PreparePooler();
            var prefab = CreateGameObject($"Pooler {dispatchKind} dispatch prefab");
            object data = null;

            switch (dispatchKind)
            {
                case "NonGeneric":
                    prefab.AddComponent<PoolerBenchmarkLifecycle>();
                    break;
                case "Int":
                    prefab.AddComponent<PoolerBenchmarkIntLifecycle>();
                    data = 7;
                    break;
                default:
                    prefab.AddComponent<PoolerBenchmarkReferenceLifecycle>();
                    data = new PoolerBenchmarkReferenceData { Value = 7 };
                    break;
            }

            var pool = pooler.TryAddPool($"Generic Dispatch {dispatchKind}", prefab, 1);
            GameObject spawned = null;
            ResetLifecycleCounters();

            Measure.Method(() =>
                {
                    for (int i = 0; i < CycleOperationsPerSample; i++)
                    {
                        spawned = pooler.Spawn(pool.tag, data: data);
                        pooler.TryDespawn(spawned);
                    }
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(DefaultMeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Pooler/GenericDispatch/{dispatchKind}/{CycleOperationsPerSample}Cycles")
                .GC()
                .Run();

            ResetLifecycleCounters();
            spawned = pooler.Spawn(pool.tag, data: data);
            Assert.IsNotNull(spawned);
            Assert.IsTrue(pooler.TryDespawn(spawned));
            AssertDispatchCounts(dispatchKind);
        }

        [Test, Performance]
        public void TargetTagLookupWithUnrelatedPools(
            [ValueSource(nameof(OtherPoolCounts))] int otherPoolCount)
        {
            const int operationsPerSample = 10_000;
            var pooler = PreparePooler();

            for (int i = 0; i < otherPoolCount; i++)
            {
                CreatePool(pooler, $"Unrelated Pool {i}", 1);
            }

            var targetPool = CreatePool(pooler, "Target Lookup Pool", 1);
            var targetObject = targetPool.objects[0];
            GameObject spawned = null;

            Measure.Method(() =>
                {
                    for (int i = 0; i < operationsPerSample; i++)
                    {
                        spawned = pooler.Spawn(targetPool.tag);
                        pooler.TryDespawn(spawned);
                    }
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(DefaultMeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Pooler/TagLookup/{otherPoolCount}OtherPools/{operationsPerSample}Spawns")
                .GC()
                .Run();

            Assert.AreSame(targetObject.GameObject, spawned);
            Assert.IsFalse(targetObject.Used);
        }

        [Test, Performance]
        public void MultiplePoolsUnderSameTag(
            [ValueSource(nameof(SameTagPoolCounts))] int poolCount)
        {
            const int operationsPerSample = 10_000;
            const string tag = "Shared Benchmark Tag";
            var pooler = PreparePooler();
            var objectsByGameObject = new Dictionary<GameObject, PooledGameObject>(poolCount);

            for (int i = 0; i < poolCount; i++)
            {
                var pool = CreatePool(pooler, tag, 1, uniqueTag: false);
                objectsByGameObject.Add(pool.objects[0].GameObject, pool.objects[0]);
            }

            var previousRandomState = Random.state;
            GameObject spawned = null;

            try
            {
                Random.InitState(179);
                Measure.Method(() =>
                    {
                        for (int i = 0; i < operationsPerSample; i++)
                        {
                            spawned = pooler.Spawn(tag);
                            pooler.TryDespawn(spawned);
                        }
                    })
                    .WarmupCount(WarmupCount)
                    .MeasurementCount(DefaultMeasurementCount)
                    .IterationsPerMeasurement(1)
                    .SampleGroup($"Pooler/DuplicateTag/{poolCount}Pools/{operationsPerSample}Spawns")
                    .GC()
                    .Run();
            }
            finally
            {
                Random.state = previousRandomState;
            }

            Assert.IsNotNull(spawned);
            Assert.IsTrue(objectsByGameObject.ContainsKey(spawned));
            Assert.IsFalse(objectsByGameObject[spawned].Used);
        }

        [Test, Performance]
        public void RuntimeExpansion([Values(1, 100)] int expansionCount)
        {
            var pooler = PreparePooler();
            var prefab = CreateGameObject("Pooler expansion prefab");
            var spawned = new GameObject[expansionCount];
            Pool pool = null;
            var setupIndex = 0;

            Measure.Method(() => SpawnAll(pooler, pool.tag, spawned))
                .SetUp(() =>
                {
                    pool = pooler.TryAddPool($"Expansion {expansionCount} {setupIndex++}", prefab, 1);
                    Assert.IsNotNull(pooler.Spawn(pool.tag));
                })
                .CleanUp(() => pooler.TryRemovePool(pool))
                .WarmupCount(WarmupCount)
                .MeasurementCount(ExpensiveMeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Pooler/Expansion/{expansionCount}")
                .GC()
                .Run();

            pool = pooler.TryAddPool($"Expansion validation {expansionCount}", prefab, 1);
            Assert.IsNotNull(pooler.Spawn(pool.tag));
            SpawnAll(pooler, pool.tag, spawned);
            Assert.AreEqual(expansionCount + 1, pool.CurrentObjectsCount);
            AssertAllNonNull(spawned, expansionCount);
        }

        private Pool CreatePool(
            Pooler pooler,
            string tag,
            int size,
            int lifecycleHandlerCount = 0,
            bool uniqueTag = true)
        {
            var prefab = CreateGameObject($"{tag} prefab");

            for (int i = 0; i < lifecycleHandlerCount; i++)
            {
                prefab.AddComponent<PoolerBenchmarkLifecycle>();
            }

            var resolvedTag = uniqueTag ? $"{tag} {_poolSequence++}" : tag;
            var pool = pooler.TryAddPool(resolvedTag, prefab, size);
            Assert.IsNotNull(pool);
            Assert.AreEqual(size, pool.CurrentObjectsCount);
            return pool;
        }

        private GameObject CreateHierarchyPrefab()
        {
            var root = CreateGameObject("Pooler hierarchy root");
            var child = CreateGameObject("Pooler hierarchy child");
            var grandchild = CreateGameObject("Pooler hierarchy grandchild");
            child.transform.SetParent(root.transform);
            grandchild.transform.SetParent(child.transform);
            root.AddComponent<PoolerBenchmarkLifecycle>();
            child.AddComponent<PoolerBenchmarkLifecycle>();
            grandchild.AddComponent<PoolerBenchmarkLifecycle>();
            return root;
        }

        private static Pooler PreparePooler()
        {
            var pooler = Toolbox.Pooler;
            Assert.IsNotNull(pooler);
            pooler.DisableGC();
            return pooler;
        }

        private static int GetMeasurementCount(int objectCount)
        {
            return objectCount >= 5_000
                ? ExpensiveMeasurementCount
                : DefaultMeasurementCount;
        }

        private static void SpawnAll(
            Pooler pooler,
            string tag,
            GameObject[] destination,
            bool traverseHierarchy = false)
        {
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = pooler.Spawn(
                    tag,
                    data: null,
                    traverseHierarchy: traverseHierarchy);
            }
        }

        private static void DespawnAll(Pooler pooler, GameObject[] objects)
        {
            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i] != null)
                {
                    pooler.TryDespawn(objects[i]);
                }
            }
        }

        private static void AssertSpawnedAndUsed(Pool pool, GameObject[] spawned, int expectedCount)
        {
            AssertAllNonNull(spawned, expectedCount);
            Assert.AreEqual(expectedCount, pool.objects.Count);

            for (int i = 0; i < pool.objects.Count; i++)
            {
                Assert.IsTrue(pool.objects[i].Used);
            }
        }

        private static void AssertSpawnedAndUnused(Pool pool, GameObject[] spawned, int expectedCount)
        {
            AssertAllNonNull(spawned, expectedCount);
            Assert.AreEqual(expectedCount, pool.objects.Count);

            for (int i = 0; i < pool.objects.Count; i++)
            {
                Assert.IsFalse(pool.objects[i].Used);
            }
        }

        private static void AssertAllNonNull(GameObject[] objects, int expectedCount)
        {
            Assert.AreEqual(expectedCount, objects.Length);

            for (int i = 0; i < objects.Length; i++)
            {
                Assert.IsNotNull(objects[i]);
            }
        }

        private static void ResetLifecycleCounters()
        {
            PoolerBenchmarkLifecycle.ResetCounters();
            PoolerBenchmarkIntLifecycle.ResetCounters();
            PoolerBenchmarkReferenceLifecycle.ResetCounters();
        }

        private static void AssertDispatchCounts(string dispatchKind)
        {
            switch (dispatchKind)
            {
                case "NonGeneric":
                    Assert.AreEqual(1, PoolerBenchmarkLifecycle.SpawnCount);
                    Assert.AreEqual(1, PoolerBenchmarkLifecycle.DespawnCount);
                    break;
                case "Int":
                    Assert.AreEqual(1, PoolerBenchmarkIntLifecycle.SpawnCount);
                    Assert.AreEqual(1, PoolerBenchmarkIntLifecycle.DespawnCount);
                    Assert.AreEqual(7, PoolerBenchmarkIntLifecycle.Sink);
                    break;
                default:
                    Assert.AreEqual(1, PoolerBenchmarkReferenceLifecycle.SpawnCount);
                    Assert.AreEqual(1, PoolerBenchmarkReferenceLifecycle.DespawnCount);
                    Assert.AreEqual(7, PoolerBenchmarkReferenceLifecycle.Sink);
                    break;
            }
        }
    }
}
