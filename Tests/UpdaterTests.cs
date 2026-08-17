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

        [Test]
        public void InitializeMonosFiltersBatchAndPreservesGlobalLifecycleStaging()
        {
            var lifecycleEvents = new System.Collections.Generic.List<string>();
            var firstRoot = new GameObject("Updater batch first");
            var secondRoot = new GameObject("Updater batch second");
            var thirdRoot = new GameObject("Updater batch third");
            var first = AddLifecycleFoo(firstRoot, "First", lifecycleEvents);
            var second = AddLifecycleFoo(secondRoot, "Second", lifecycleEvents);
            var third = AddLifecycleFoo(thirdRoot, "Third", lifecycleEvents);

            Toolbox.Updater.InitializeMonos(new MonoCached[] { first, null, second, first, third });

            CollectionAssert.AreEqual(
                new[]
                {
                    "First.Rise",
                    "Second.Rise",
                    "Third.Rise",
                    "First.Ready",
                    "Second.Ready",
                    "Third.Ready"
                },
                lifecycleEvents);
            AssertLifecycleInvokedOnce(first, second, third);
            AssertRunningMembership(first, true);
            AssertRunningMembership(second, true);
            AssertRunningMembership(third, true);

            UnityEngine.Object.DestroyImmediate(firstRoot);
            UnityEngine.Object.DestroyImmediate(secondRoot);
            UnityEngine.Object.DestroyImmediate(thirdRoot);
        }

        [Test]
        public void InitializationEntryPointsKeepRunningMembershipSynchronized()
        {
            var lifecycleEvents = new System.Collections.Generic.List<string>();
            var monoRoot = new GameObject("Updater mono entry point");
            var objectRoot = new GameObject("Updater object entry point");
            var objectsRoot = new GameObject("Updater objects entry point");
            var newRoot = new GameObject("Updater new batch entry");
            var monoFoo = AddLifecycleFoo(monoRoot, "Mono", lifecycleEvents);
            var objectFoo = AddLifecycleFoo(objectRoot, "Object", lifecycleEvents);
            var objectsFoo = AddLifecycleFoo(objectsRoot, "Objects", lifecycleEvents);
            var newFoo = AddLifecycleFoo(newRoot, "New", lifecycleEvents);

            Toolbox.Updater.InitializeMono(monoFoo);
            Toolbox.Updater.InitializeObject(objectRoot);
            Toolbox.Updater.InitializeObjects(new[] { objectsRoot });
            lifecycleEvents.Clear();

            Toolbox.Updater.InitializeMonos(
                new MonoCached[] { monoFoo, objectFoo, objectsFoo, newFoo, newFoo, null });

            CollectionAssert.AreEqual(new[] { "New.Rise", "New.Ready" }, lifecycleEvents);
            AssertLifecycleInvokedOnce(monoFoo, objectFoo, objectsFoo, newFoo);
            AssertRunningMembership(monoFoo, true);
            AssertRunningMembership(objectFoo, true);
            AssertRunningMembership(objectsFoo, true);
            AssertRunningMembership(newFoo, true);

            UnityEngine.Object.DestroyImmediate(monoRoot);
            UnityEngine.Object.DestroyImmediate(objectRoot);
            UnityEngine.Object.DestroyImmediate(objectsRoot);
            UnityEngine.Object.DestroyImmediate(newRoot);
        }

        [Test]
        public void RemoveMonoFromUpdateKeepsMembershipSynchronized()
        {
            var lifecycleEvents = new System.Collections.Generic.List<string>();
            var runningRoot = new GameObject("Updater removable mono");
            var unknownRoot = new GameObject("Updater unknown mono");
            var running = AddLifecycleFoo(runningRoot, "Running", lifecycleEvents);
            var unknown = AddLifecycleFoo(unknownRoot, "Unknown", lifecycleEvents);

            Toolbox.Updater.InitializeMono(running);
            Toolbox.Updater.RemoveMonoFromUpdate(running);

            AssertRunningMembership(running, false);
            Assert.IsTrue(running.Paused);
            Assert.DoesNotThrow(() => Toolbox.Updater.RemoveMonoFromUpdate(running));

            Toolbox.Updater.InitializeMonos(new[] { running });

            AssertRunningMembership(running, true);
            AssertLifecycleInvokedOnce(running);
            Assert.IsTrue(running.Paused, "Bulk initialization must preserve existing resume behavior");

            Toolbox.Updater.RemoveMonoFromUpdate(unknown);
            AssertRunningMembership(unknown, false);
            Assert.IsTrue(unknown.Paused, "Removing an unknown mono must preserve existing Pause behavior");

            UnityEngine.Object.DestroyImmediate(runningRoot);
            UnityEngine.Object.DestroyImmediate(unknownRoot);
        }

        [Test]
        public void InitializeMonosUsesReentrancySafeBatchBuffers()
        {
            var lifecycleEvents = new System.Collections.Generic.List<string>();
            var firstRoot = new GameObject("Updater reentrant first");
            var secondRoot = new GameObject("Updater reentrant second");
            var nestedRoot = new GameObject("Updater reentrant nested");
            var first = AddLifecycleFoo(firstRoot, "First", lifecycleEvents);
            var second = AddLifecycleFoo(secondRoot, "Second", lifecycleEvents);
            var nested = AddLifecycleFoo(nestedRoot, "Nested", lifecycleEvents);

            first.RiseAction = () => Toolbox.Updater.InitializeMonos(new[] { nested });

            Toolbox.Updater.InitializeMonos(new[] { first, second });

            CollectionAssert.AreEqual(
                new[]
                {
                    "First.Rise",
                    "Nested.Rise",
                    "Nested.Ready",
                    "Second.Rise",
                    "First.Ready",
                    "Second.Ready"
                },
                lifecycleEvents);
            AssertLifecycleInvokedOnce(first, second, nested);
            AssertRunningMembership(first, true);
            AssertRunningMembership(second, true);
            AssertRunningMembership(nested, true);

            UnityEngine.Object.DestroyImmediate(firstRoot);
            UnityEngine.Object.DestroyImmediate(secondRoot);
            UnityEngine.Object.DestroyImmediate(nestedRoot);
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

        private static void AssertRunningMembership(MonoCached mono, bool expected)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var runningListField = typeof(Updater).GetField("_RunningMonos", flags);
            var runningSetField = typeof(Updater).GetField("_RunningMonosSet", flags);

            Assert.IsNotNull(runningListField);
            Assert.IsNotNull(runningSetField);

            var runningList =
                (System.Collections.Generic.List<MonoCached>)runningListField.GetValue(Toolbox.Updater);
            var runningSet =
                (System.Collections.Generic.HashSet<MonoCached>)runningSetField.GetValue(Toolbox.Updater);
            var occurrences = 0;

            for (int i = 0; i < runningList.Count; i++)
            {
                if (runningList[i] == mono)
                {
                    occurrences++;
                }
            }

            Assert.AreEqual(expected, runningList.Contains(mono));
            Assert.AreEqual(expected, runningSet.Contains(mono));
            Assert.AreEqual(expected ? 1 : 0, occurrences);
            Assert.AreEqual(runningList.Count, runningSet.Count);
        }
    }
}
