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

            AssertLogicalMembership(running, false);
            AssertPhysicalMembership(running, true);
            Assert.IsTrue(GetPendingRemovals(Toolbox.Updater).Contains(running));
            Assert.IsTrue(running.Paused);
            Assert.DoesNotThrow(() => Toolbox.Updater.RemoveMonoFromUpdate(running));
            Assert.AreEqual(1, GetPendingRemovals(Toolbox.Updater).Count);

            Toolbox.Updater.InitializeMonos(new[] { running });

            AssertRunningMembership(running, true);
            AssertLifecycleInvokedOnce(running);
            Assert.IsTrue(running.Paused, "Bulk initialization must preserve existing resume behavior");

            Toolbox.Updater.RemoveMonoFromUpdate(unknown);
            AssertLogicalMembership(unknown, false);
            AssertPhysicalMembership(unknown, false);
            Assert.IsTrue(unknown.Paused, "Removing an unknown mono must preserve existing Pause behavior");

            UnityEngine.Object.DestroyImmediate(runningRoot);
            UnityEngine.Object.DestroyImmediate(unknownRoot);
        }

        [Test]
        public void PendingRemovalsCompactInPlaceAndPreserveSurvivorOrder()
        {
            var roots = new GameObject[5];
            var monos = new Foo[5];

            for (int i = 0; i < roots.Length; i++)
            {
                roots[i] = new GameObject($"Updater compaction {i}");
                monos[i] = roots[i].AddComponent<Foo>();
            }

            Toolbox.Updater.InitializeMonos(monos);
            Toolbox.Updater.RemoveMonoFromUpdate(monos[1]);
            Toolbox.Updater.RemoveMonoFromUpdate(monos[3]);

            Assert.AreEqual(3, GetRunningSet(Toolbox.Updater).Count);
            Assert.AreEqual(5, GetRunningList(Toolbox.Updater).Count);
            Assert.AreEqual(2, GetPendingRemovals(Toolbox.Updater).Count);

            Toolbox.Updater.InitializeMonos(System.Array.Empty<MonoCached>());

            CollectionAssert.AreEqual(
                new MonoCached[] { monos[0], monos[2], monos[4] },
                GetRunningList(Toolbox.Updater));
            Assert.AreEqual(3, GetRunningSet(Toolbox.Updater).Count);
            Assert.IsEmpty(GetPendingRemovals(Toolbox.Updater));

            Toolbox.Updater.RemoveMonoFromUpdate(monos[0]);
            Toolbox.Updater.RemoveMonoFromUpdate(monos[2]);
            Toolbox.Updater.RemoveMonoFromUpdate(monos[4]);
            Toolbox.Updater.InitializeMonos(System.Array.Empty<MonoCached>());

            Assert.IsEmpty(GetRunningList(Toolbox.Updater));
            Assert.IsEmpty(GetRunningSet(Toolbox.Updater));
            Assert.IsEmpty(GetPendingRemovals(Toolbox.Updater));

            for (int i = 0; i < roots.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(roots[i]);
            }
        }

        [Test]
        public void SingleRemovalStopsTicksWhileTheSurvivorContinues()
        {
            var tickEvents = new System.Collections.Generic.List<string>();
            var removedRoot = new GameObject("Updater single removed");
            var survivorRoot = new GameObject("Updater single survivor");
            var removed = removedRoot.AddComponent<Foo>();
            var survivor = survivorRoot.AddComponent<Foo>();
            removed.LifecycleId = "Removed";
            survivor.LifecycleId = "Survivor";
            removed.TickEvents = tickEvents;
            survivor.TickEvents = tickEvents;

            Toolbox.Updater.InitializeMonos(new[] { removed, survivor });
            Toolbox.Updater.RemoveMonoFromUpdate(removed);
            InvokeUpdate(Toolbox.Updater);

            CollectionAssert.AreEqual(new[] { "Survivor" }, tickEvents);
            CollectionAssert.AreEqual(
                new MonoCached[] { survivor },
                GetRunningList(Toolbox.Updater));
            AssertLogicalMembership(removed, false);

            UnityEngine.Object.DestroyImmediate(removedRoot);
            UnityEngine.Object.DestroyImmediate(survivorRoot);
            Toolbox.Updater.InitializeMonos(System.Array.Empty<MonoCached>());
        }

        [Test]
        public void RemovalDuringTickIsDeferredAndPausedMonosDoNotTick()
        {
            var tickEvents = new System.Collections.Generic.List<string>();
            var firstRoot = new GameObject("Updater tick removal first");
            var middleRoot = new GameObject("Updater tick removal middle");
            var lastRoot = new GameObject("Updater tick removal last");
            var first = firstRoot.AddComponent<Foo>();
            var middle = middleRoot.AddComponent<Foo>();
            var last = lastRoot.AddComponent<Foo>();

            first.LifecycleId = "First";
            middle.LifecycleId = "Middle";
            last.LifecycleId = "Last";
            first.TickEvents = tickEvents;
            middle.TickEvents = tickEvents;
            last.TickEvents = tickEvents;
            first.TickAction = () =>
            {
                Toolbox.Updater.RemoveMonoFromUpdate(first);
                Toolbox.Updater.RemoveMonoFromUpdate(middle);
            };

            Toolbox.Updater.InitializeMonos(new[] { first, middle, last });
            InvokeUpdate(Toolbox.Updater);

            CollectionAssert.AreEqual(new[] { "First", "Last" }, tickEvents);
            Assert.AreEqual(3, GetRunningList(Toolbox.Updater).Count);
            Assert.AreEqual(1, GetRunningSet(Toolbox.Updater).Count);
            Assert.AreEqual(2, GetPendingRemovals(Toolbox.Updater).Count);
            Assert.IsTrue(first.Paused);
            Assert.IsTrue(middle.Paused);

            InvokeUpdate(Toolbox.Updater);

            CollectionAssert.AreEqual(new[] { "First", "Last", "Last" }, tickEvents);
            CollectionAssert.AreEqual(
                new MonoCached[] { last },
                GetRunningList(Toolbox.Updater));
            Assert.IsEmpty(GetPendingRemovals(Toolbox.Updater));

            UnityEngine.Object.DestroyImmediate(firstRoot);
            UnityEngine.Object.DestroyImmediate(middleRoot);
            UnityEngine.Object.DestroyImmediate(lastRoot);
            Toolbox.Updater.InitializeMonos(System.Array.Empty<MonoCached>());
        }

        [Test]
        public void RemoveThenReinitializeDuringTickRestoresTheExistingListEntry()
        {
            var tickEvents = new System.Collections.Generic.List<string>();
            var firstRoot = new GameObject("Updater tick reinitialize first");
            var middleRoot = new GameObject("Updater tick reinitialize middle");
            var lastRoot = new GameObject("Updater tick reinitialize last");
            var first = firstRoot.AddComponent<Foo>();
            var middle = middleRoot.AddComponent<Foo>();
            var last = lastRoot.AddComponent<Foo>();

            first.LifecycleId = "First";
            middle.LifecycleId = "Middle";
            last.LifecycleId = "Last";
            first.TickEvents = tickEvents;
            middle.TickEvents = tickEvents;
            last.TickEvents = tickEvents;
            first.TickAction = () =>
            {
                Toolbox.Updater.RemoveMonoFromUpdate(middle);
                Toolbox.Updater.InitializeMono(middle);
            };

            Toolbox.Updater.InitializeMonos(new[] { first, middle, last });
            InvokeUpdate(Toolbox.Updater);

            CollectionAssert.AreEqual(new[] { "First", "Middle", "Last" }, tickEvents);
            CollectionAssert.AreEqual(
                new MonoCached[] { first, middle, last },
                GetRunningList(Toolbox.Updater));
            Assert.AreEqual(3, GetRunningSet(Toolbox.Updater).Count);
            Assert.IsEmpty(GetPendingRemovals(Toolbox.Updater));

            UnityEngine.Object.DestroyImmediate(firstRoot);
            UnityEngine.Object.DestroyImmediate(middleRoot);
            UnityEngine.Object.DestroyImmediate(lastRoot);
            Toolbox.Updater.InitializeMonos(System.Array.Empty<MonoCached>());
        }

        [Test]
        public void RemoveThenReinitializeWorksThroughEveryInitializationEntryPoint()
        {
            var lifecycleEvents = new System.Collections.Generic.List<string>();
            var monoRoot = new GameObject("Updater reinitialize mono");
            var monosRoot = new GameObject("Updater reinitialize monos");
            var objectRoot = new GameObject("Updater reinitialize object");
            var objectsRoot = new GameObject("Updater reinitialize objects");
            var mono = AddLifecycleFoo(monoRoot, "Mono", lifecycleEvents);
            var monos = AddLifecycleFoo(monosRoot, "Monos", lifecycleEvents);
            var obj = AddLifecycleFoo(objectRoot, "Object", lifecycleEvents);
            var objects = AddLifecycleFoo(objectsRoot, "Objects", lifecycleEvents);

            Toolbox.Updater.InitializeMonos(new[] { mono, monos, obj, objects });

            Toolbox.Updater.RemoveMonoFromUpdate(mono);
            Toolbox.Updater.InitializeMono(mono);
            Toolbox.Updater.RemoveMonoFromUpdate(monos);
            Toolbox.Updater.InitializeMonos(new[] { monos });
            Toolbox.Updater.RemoveMonoFromUpdate(obj);
            Toolbox.Updater.InitializeObject(objectRoot);
            Toolbox.Updater.RemoveMonoFromUpdate(objects);
            Toolbox.Updater.InitializeObjects(new[] { objectsRoot });

            AssertLifecycleInvokedOnce(mono, monos, obj, objects);
            AssertRunningMembership(mono, true);
            AssertRunningMembership(monos, true);
            AssertRunningMembership(obj, true);
            AssertRunningMembership(objects, true);
            Assert.AreEqual(4, GetRunningList(Toolbox.Updater).Count);
            Assert.IsEmpty(GetPendingRemovals(Toolbox.Updater));

            UnityEngine.Object.DestroyImmediate(monoRoot);
            UnityEngine.Object.DestroyImmediate(monosRoot);
            UnityEngine.Object.DestroyImmediate(objectRoot);
            UnityEngine.Object.DestroyImmediate(objectsRoot);
            Toolbox.Updater.InitializeMonos(System.Array.Empty<MonoCached>());
        }

        [Test]
        public void RemoveObjectsFromUpdateFlushesTheCollectedBatchOnce()
        {
            var root = new GameObject("Updater bulk removal root");
            var child = new GameObject("Updater bulk removal child");
            child.transform.SetParent(root.transform);
            var rootMono = root.AddComponent<Foo>();
            var childMono = child.AddComponent<Foo>();

            Toolbox.Updater.InitializeObject(root);
            Toolbox.Updater.RemoveObjectsFromUpdate(new[] { root });

            Assert.IsTrue(rootMono.Paused);
            Assert.IsTrue(childMono.Paused);
            Assert.IsEmpty(GetRunningList(Toolbox.Updater));
            Assert.IsEmpty(GetRunningSet(Toolbox.Updater));
            Assert.IsEmpty(GetPendingRemovals(Toolbox.Updater));

            UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public void DestroyedMonosRemainDeferredUntilTheNextSafeBoundary()
        {
            var root = new GameObject("Updater destroy removal root");
            var firstChild = new GameObject("Updater destroy removal first child");
            var secondChild = new GameObject("Updater destroy removal second child");
            firstChild.transform.SetParent(root.transform);
            secondChild.transform.SetParent(root.transform);
            root.AddComponent<Foo>();
            firstChild.AddComponent<Foo>();
            secondChild.AddComponent<Foo>();

            Toolbox.Updater.InitializeObject(root);
            UnityEngine.Object.DestroyImmediate(root);

            Assert.IsEmpty(GetRunningSet(Toolbox.Updater));
            Assert.AreEqual(3, GetRunningList(Toolbox.Updater).Count);
            Assert.AreEqual(3, GetPendingRemovals(Toolbox.Updater).Count);

            InvokeUpdate(Toolbox.Updater);

            Assert.IsEmpty(GetRunningList(Toolbox.Updater));
            Assert.IsEmpty(GetRunningSet(Toolbox.Updater));
            Assert.IsEmpty(GetPendingRemovals(Toolbox.Updater));
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
            var runningList = GetRunningList(Toolbox.Updater);
            var runningSet = GetRunningSet(Toolbox.Updater);
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

        private static void AssertLogicalMembership(MonoCached mono, bool expected)
        {
            Assert.AreEqual(expected, GetRunningSet(Toolbox.Updater).Contains(mono));
        }

        private static void AssertPhysicalMembership(MonoCached mono, bool expected)
        {
            Assert.AreEqual(expected, GetRunningList(Toolbox.Updater).Contains(mono));
        }

        private static System.Collections.Generic.List<MonoCached> GetRunningList(Updater updater)
        {
            return GetPrivateField<System.Collections.Generic.List<MonoCached>>(updater, "_RunningMonos");
        }

        private static System.Collections.Generic.HashSet<MonoCached> GetRunningSet(Updater updater)
        {
            return GetPrivateField<System.Collections.Generic.HashSet<MonoCached>>(updater, "_RunningMonosSet");
        }

        private static System.Collections.Generic.HashSet<MonoCached> GetPendingRemovals(Updater updater)
        {
            return GetPrivateField<System.Collections.Generic.HashSet<MonoCached>>(updater, "_PendingRemovals");
        }

        private static T GetPrivateField<T>(Updater updater, string fieldName)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var field = typeof(Updater).GetField(fieldName, flags);

            Assert.IsNotNull(field);
            return (T)field.GetValue(updater);
        }

        private static void InvokeUpdate(Updater updater)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var update = typeof(Updater).GetMethod("Update", flags);

            Assert.IsNotNull(update);
            update.Invoke(updater, null);
        }
    }
}
