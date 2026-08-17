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

        [Test]
        public void InitializeObjectsInitializesAllRootsWithGlobalLifecycleStaging()
        {
            var lifecycleEvents = new System.Collections.Generic.List<string>();
            var firstRoot = new GameObject("Updater first root");
            var inactiveChild = new GameObject("Updater inactive child");
            var secondRoot = new GameObject("Updater second root");

            inactiveChild.transform.SetParent(firstRoot.transform);

            var first = AddLifecycleFoo(firstRoot, "First", lifecycleEvents);
            var inactive = AddLifecycleFoo(inactiveChild, "Inactive", lifecycleEvents);
            var second = AddLifecycleFoo(secondRoot, "Second", lifecycleEvents);
            inactiveChild.SetActive(false);

            Toolbox.Updater.InitializeObjects(new[] { firstRoot, secondRoot });

            CollectionAssert.AreEqual(
                new[]
                {
                    "First.Rise",
                    "Inactive.Rise",
                    "Second.Rise",
                    "First.Ready",
                    "Inactive.Ready",
                    "Second.Ready"
                },
                lifecycleEvents);
            AssertLifecycleInvokedOnce(first, inactive, second);

            UnityEngine.Object.DestroyImmediate(firstRoot);
            UnityEngine.Object.DestroyImmediate(secondRoot);
        }

        [Test]
        public void InitializeObjectsHandlesDuplicateAndOverlappingRootsOnce()
        {
            var lifecycleEvents = new System.Collections.Generic.List<string>();
            var parent = new GameObject("Updater overlapping parent");
            var child = new GameObject("Updater overlapping child");
            child.transform.SetParent(parent.transform);

            var parentFoo = AddLifecycleFoo(parent, "Parent", lifecycleEvents);
            var childFoo = AddLifecycleFoo(child, "Child", lifecycleEvents);

            Toolbox.Updater.InitializeObjects(new[] { parent, parent, child });

            AssertLifecycleInvokedOnce(parentFoo, childFoo);
            CollectionAssert.AreEqual(
                new[] { "Parent.Rise", "Child.Rise", "Parent.Ready", "Child.Ready" },
                lifecycleEvents);

            UnityEngine.Object.DestroyImmediate(parent);
        }

        [Test]
        public void InitializeObjectsSkipsAlreadyRunningAndHandlesEmptyOrNullInput()
        {
            var lifecycleEvents = new System.Collections.Generic.List<string>();
            var root = new GameObject("Updater already running root");
            var foo = AddLifecycleFoo(root, "Running", lifecycleEvents);

            Toolbox.Updater.InitializeObject(root);
            lifecycleEvents.Clear();

            Assert.DoesNotThrow(() => Toolbox.Updater.InitializeObjects(System.Array.Empty<GameObject>()));
            Assert.DoesNotThrow(() => Toolbox.Updater.InitializeObjects(null));
            Assert.DoesNotThrow(() => Toolbox.Updater.InitializeObjects(new[] { null, root }));

            AssertLifecycleInvokedOnce(foo);
            Assert.IsEmpty(lifecycleEvents);

            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Foo AddLifecycleFoo(
            GameObject gameObject,
            string lifecycleId,
            System.Collections.Generic.List<string> lifecycleEvents)
        {
            var foo = gameObject.AddComponent<Foo>();
            foo.LifecycleId = lifecycleId;
            foo.LifecycleEvents = lifecycleEvents;
            return foo;
        }

        private static void AssertLifecycleInvokedOnce(params Foo[] monos)
        {
            for (int i = 0; i < monos.Length; i++)
            {
                Assert.AreEqual(1, monos[i].RiseCount);
                Assert.AreEqual(1, monos[i].ReadyCount);
            }
        }
    }
}
