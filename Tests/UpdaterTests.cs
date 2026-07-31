using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VolumeBox.Toolbox.Tests
{
    [PrebuildSetup(typeof(TestPrebuild))]
    internal class UpdaterTests : ToolboxTestBase
    {
        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator TimeScaleTest()
        {
            var testGO = new GameObject("Timescale Test");
            var foo = testGO.AddComponent<Foo>();
            Toolbox.Updater.InitializeObject(testGO);

            Toolbox.Updater.TimeScale = 0.5f;

            yield return null;

            Assert.AreEqual(Toolbox.Updater.Delta, foo.Delta);
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator TimeIntervalTest()
        {
            var testGO = new GameObject("Time Interval Test");
            var foo = testGO.AddComponent<Foo>();
            Toolbox.Updater.InitializeObject(testGO);
            Toolbox.Updater.TimeScale = 1;

            const float interval = 0.1f;
            const float timeout = 1f;
            foo.Interval = interval;

            var deadline = Time.realtimeSinceStartup + timeout;

            while (foo.counter <= 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.Greater(foo.counter, 0, $"Interval callback did not run within {timeout} seconds");
            Assert.GreaterOrEqual(foo.counter, interval, "Interval callback ran before enough time was accumulated");

            UnityEngine.Object.DestroyImmediate(testGO);
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator IgnoreTimeScaleTest()
        {
            var testGO = new GameObject("Ignore Timescale Test");
            var foo = testGO.AddComponent<Foo>();
            Toolbox.Updater.InitializeObject(testGO);

            foo.IgnoreTimeScale = true;
            yield return null;
            Toolbox.Updater.TimeScale = 0;
            yield return null;
            Assert.AreEqual(true, foo.Delta > 0);
            foo.IgnoreTimeScale = false;
            yield return null;
            Assert.AreEqual(true, foo.Delta == 0);
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator InitializeMonoIgnoresDuplicatesAndNullsTest()
        {
            var testGO = new GameObject("Updater duplicate test");
            var foo = testGO.AddComponent<Foo>();

            Assert.DoesNotThrow(() => Toolbox.Updater.InitializeMonos(new MonoCached[] { null }));

            Toolbox.Updater.InitializeMono(foo);
            Toolbox.Updater.InitializeMono(foo);

            yield return null;

            Assert.AreEqual(1, foo.TickCount);
            UnityEngine.Object.DestroyImmediate(testGO);
        }
    }
}
