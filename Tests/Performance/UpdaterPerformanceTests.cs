using System.Collections;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.TestTools;

namespace VolumeBox.Toolbox.Tests.Performance
{
    [Category("UpdaterPerformance")]
    public sealed class UpdaterPerformanceTests
    {
        private const int WarmupFrameCount = 10;
        private const int MeasurementFrameCount = 30;
        private const int MethodWarmupCount = 1;
        private const int MethodMeasurementCount = 5;

        private static readonly int[] DispatchObjectCounts =
        {
            100,
            1_000,
            5_000,
            10_000
        };

        private static readonly int[] InitializationObjectCounts =
        {
            100,
            1_000,
            5_000,
            10_000
        };

        private static readonly int[] MembershipObjectCounts =
        {
            100,
            1_000,
            10_000
        };

        private GameObject _benchmarkRoot;
        private int _previousVSyncCount;
        private int _previousTargetFrameRate;
        private float _previousTimeScale;
        private float _previousFixedDeltaTime;

        [SetUp]
        public void SetUp()
        {
            _previousVSyncCount = QualitySettings.vSyncCount;
            _previousTargetFrameRate = Application.targetFrameRate;
            _previousTimeScale = Time.timeScale;
            _previousFixedDeltaTime = Time.fixedDeltaTime;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 1_000f;
            _benchmarkRoot = new GameObject("Updater performance objects")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            ResetCounters();
        }

        [TearDown]
        public void TearDown()
        {
            if (_benchmarkRoot != null)
            {
                Object.DestroyImmediate(_benchmarkRoot);
            }

            _benchmarkRoot = null;
            ResetCounters();
            Time.fixedDeltaTime = _previousFixedDeltaTime;
            Time.timeScale = _previousTimeScale;
            Application.targetFrameRate = _previousTargetFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
        }

        [UnityTest, Performance]
        public IEnumerator RegularUpdateEmpty(
            [ValueSource(nameof(DispatchObjectCounts))] int objectCount)
        {
            CreateComponents<RegularEmptyUpdateBenchmarkBehaviour>(objectCount);
            yield return WarmUpFrames();
            RegularEmptyUpdateBenchmarkBehaviour.InvocationCount = 0;

            var sampleName = $"RegularUpdate/{objectCount}/Empty";
            yield return MeasureFrameScope(sampleName);

            AssertAndRecordCallbackCount(
                sampleName,
                objectCount,
                RegularEmptyUpdateBenchmarkBehaviour.InvocationCount);
        }

        [UnityTest, Performance]
        public IEnumerator MonoCachedTickEmpty(
            [ValueSource(nameof(DispatchObjectCounts))] int objectCount)
        {
            var updater = CreateUpdater();
            var components = CreateComponents<MonoCachedEmptyUpdateBenchmarkBehaviour>(objectCount);
            updater.InitializeMonos(components);
            yield return WarmUpFrames();
            MonoCachedEmptyUpdateBenchmarkBehaviour.InvocationCount = 0;

            var sampleName = $"MonoCachedTick/{objectCount}/Empty";
            yield return MeasureFrameScope(sampleName);

            AssertAndRecordCallbackCount(
                sampleName,
                objectCount,
                MonoCachedEmptyUpdateBenchmarkBehaviour.InvocationCount);
        }

        [UnityTest, Performance]
        public IEnumerator RegularUpdateTiny(
            [ValueSource(nameof(DispatchObjectCounts))] int objectCount)
        {
            CreateComponents<RegularTinyUpdateBenchmarkBehaviour>(objectCount);
            yield return WarmUpFrames();
            RegularTinyUpdateBenchmarkBehaviour.InvocationCount = 0;
            RegularTinyUpdateBenchmarkBehaviour.Sink = 0f;

            var sampleName = $"RegularUpdate/{objectCount}/Tiny";
            yield return MeasureFrameScope(sampleName);

            AssertAndRecordCallbackCount(
                sampleName,
                objectCount,
                RegularTinyUpdateBenchmarkBehaviour.InvocationCount);
            Assert.Greater(RegularTinyUpdateBenchmarkBehaviour.Sink, 0f);
        }

        [UnityTest, Performance]
        public IEnumerator MonoCachedTickTiny(
            [ValueSource(nameof(DispatchObjectCounts))] int objectCount)
        {
            var updater = CreateUpdater();
            var components = CreateComponents<MonoCachedTinyUpdateBenchmarkBehaviour>(objectCount);
            updater.InitializeMonos(components);
            yield return WarmUpFrames();
            MonoCachedTinyUpdateBenchmarkBehaviour.InvocationCount = 0;
            MonoCachedTinyUpdateBenchmarkBehaviour.Sink = 0f;

            var sampleName = $"MonoCachedTick/{objectCount}/Tiny";
            yield return MeasureFrameScope(sampleName);

            AssertAndRecordCallbackCount(
                sampleName,
                objectCount,
                MonoCachedTinyUpdateBenchmarkBehaviour.InvocationCount);
            Assert.Greater(MonoCachedTinyUpdateBenchmarkBehaviour.Sink, 0f);
        }

        [Test, Performance]
        public void RegularInitialization(
            [ValueSource(nameof(InitializationObjectCounts))] int objectCount)
        {
            GameObject[] objects = null;
            RegularTinyUpdateBenchmarkBehaviour[] components = null;

            Measure.Method(() =>
                {
                    for (int i = 0; i < objectCount; i++)
                    {
                        components[i] = objects[i].AddComponent<RegularTinyUpdateBenchmarkBehaviour>();
                    }
                })
                .SetUp(() =>
                {
                    objects = CreateBareObjects(objectCount, "Regular initialization");
                    components = new RegularTinyUpdateBenchmarkBehaviour[objectCount];
                })
                .CleanUp(() => DestroyObjects(objects))
                .WarmupCount(MethodWarmupCount)
                .MeasurementCount(MethodMeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"RegularInitialization/{objectCount}")
                .GC()
                .Run();
        }

        [Test, Performance]
        public void MonoCachedInitialization(
            [ValueSource(nameof(InitializationObjectCounts))] int objectCount)
        {
            GameObject[] objects = null;
            GameObject updaterObject = null;
            Updater updater = null;
            MonoCachedTinyUpdateBenchmarkBehaviour[] components = null;

            Measure.Method(() =>
                {
                    for (int i = 0; i < objectCount; i++)
                    {
                        components[i] = objects[i].AddComponent<MonoCachedTinyUpdateBenchmarkBehaviour>();
                    }

                    updater.InitializeMonos(components);
                })
                .SetUp(() =>
                {
                    objects = CreateBareObjects(objectCount, "MonoCached initialization");
                    components = new MonoCachedTinyUpdateBenchmarkBehaviour[objectCount];
                    updaterObject = CreateBenchmarkObject("Initialization Updater");
                    updater = updaterObject.AddComponent<Updater>();
                })
                .CleanUp(() =>
                {
                    Object.DestroyImmediate(updaterObject);
                    DestroyObjects(objects);
                })
                .WarmupCount(MethodWarmupCount)
                .MeasurementCount(MethodMeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"MonoCachedInitialization/{objectCount}")
                .GC()
                .Run();
        }

        [Test, Performance]
        public void InitializeMonosMembership(
            [ValueSource(nameof(MembershipObjectCounts))] int objectCount)
        {
            var updater = CreateUpdater();
            var components = CreateComponents<MonoCachedEmptyUpdateBenchmarkBehaviour>(objectCount);

            updater.InitializeMonos(components);
            RemoveAllAndFlush(updater, components);

            Measure.Method(() => updater.InitializeMonos(components))
                .CleanUp(() => RemoveAllAndFlush(updater, components))
                .WarmupCount(MethodWarmupCount)
                .MeasurementCount(MethodMeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"UpdaterInitializeMonos/{objectCount}")
                .GC()
                .Run();
        }

        [Test, Performance]
        public void RemoveMonoMembership(
            [ValueSource(nameof(MembershipObjectCounts))] int objectCount)
        {
            var updater = CreateUpdater();
            var components = CreateComponents<MonoCachedEmptyUpdateBenchmarkBehaviour>(objectCount);

            Measure.Method(() => RemoveAllAndFlush(updater, components))
                .SetUp(() => updater.InitializeMonos(components))
                .WarmupCount(MethodWarmupCount)
                .MeasurementCount(MethodMeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"UpdaterRemoveMonosEndToEnd/{objectCount}")
                .GC()
                .Run();
        }

        private Updater CreateUpdater()
        {
            var updaterObject = CreateBenchmarkObject("Benchmark Updater");
            return updaterObject.AddComponent<Updater>();
        }

        private T[] CreateComponents<T>(int objectCount) where T : Component
        {
            var components = new T[objectCount];
            var objectName = typeof(T).Name;

            for (int i = 0; i < objectCount; i++)
            {
                var gameObject = CreateBenchmarkObject(objectName);
                components[i] = gameObject.AddComponent<T>();
            }

            return components;
        }

        private GameObject[] CreateBareObjects(int objectCount, string objectName)
        {
            var objects = new GameObject[objectCount];

            for (int i = 0; i < objectCount; i++)
            {
                objects[i] = CreateBenchmarkObject(objectName);
            }

            return objects;
        }

        private GameObject CreateBenchmarkObject(string objectName)
        {
            var gameObject = new GameObject(objectName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            gameObject.transform.SetParent(_benchmarkRoot.transform, false);
            return gameObject;
        }

        private static void DestroyObjects(GameObject[] objects)
        {
            if (objects == null)
            {
                return;
            }

            for (int i = objects.Length - 1; i >= 0; i--)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }
        }

        private static void RemoveAll(Updater updater, MonoCached[] components)
        {
            for (int i = 0; i < components.Length; i++)
            {
                updater.RemoveMonoFromUpdate(components[i]);
            }
        }

        private static void RemoveAllAndFlush(Updater updater, MonoCached[] components)
        {
            RemoveAll(updater, components);
            updater.InitializeObjects(System.Array.Empty<GameObject>());
        }

        private static IEnumerator WarmUpFrames()
        {
            // Manual warmup allows each benchmark to reset its callback counter before Measure.Frames starts.
            for (int i = 0; i < WarmupFrameCount; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator MeasureFrameScope(string sampleName)
        {
            // FramesMeasurement.Run ends with WaitForEndOfFrame, which can remain suspended in the Editor when
            // the Game view is not being repainted. Its supported scoped API records the same real PlayerLoop
            // frames without that trailing yield instruction.
            using (Measure.Frames().Scope(new SampleGroup(sampleName, SampleUnit.Millisecond)))
            {
                for (int i = 0; i < MeasurementFrameCount; i++)
                {
                    yield return null;
                }
            }
        }

        private static void AssertAndRecordCallbackCount(
            string sampleName,
            int objectCount,
            long invocationCount)
        {
            var expectedCount = (long)objectCount * MeasurementFrameCount;
            Assert.AreEqual(expectedCount, invocationCount);
            Measure.Custom(
                new SampleGroup($"{sampleName}/Callbacks", SampleUnit.Undefined, true),
                invocationCount);
        }

        private static void ResetCounters()
        {
            RegularEmptyUpdateBenchmarkBehaviour.InvocationCount = 0;
            RegularTinyUpdateBenchmarkBehaviour.InvocationCount = 0;
            RegularTinyUpdateBenchmarkBehaviour.Sink = 0f;
            MonoCachedEmptyUpdateBenchmarkBehaviour.InvocationCount = 0;
            MonoCachedTinyUpdateBenchmarkBehaviour.InvocationCount = 0;
            MonoCachedTinyUpdateBenchmarkBehaviour.Sink = 0f;
        }
    }
}
