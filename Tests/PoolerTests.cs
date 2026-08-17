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

        private static readonly FieldInfo PoolsField = typeof(Pooler).GetField(
            "pools",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo AvailableObjectsField = typeof(Pool).GetField(
            "_availableObjects",
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
        public void InitialAvailabilitySpawnsDistinctObjectsInPoolOrder()
        {
            const string poolTag = "Availability Initial Order";
            var prefab = new GameObject("Availability Initial Order Prefab");

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 3);
                var expected = new[]
                {
                    pool.objects[0].GameObject,
                    pool.objects[1].GameObject,
                    pool.objects[2].GameObject
                };

                AssertAvailableCount(pool, 3);

                var spawned = new[]
                {
                    Toolbox.Pooler.Spawn(poolTag),
                    Toolbox.Pooler.Spawn(poolTag),
                    Toolbox.Pooler.Spawn(poolTag)
                };

                CollectionAssert.AreEqual(expected, spawned);
                CollectionAssert.AllItemsAreUnique(spawned);
                AssertAvailableCount(pool, 0);

                for (int i = 0; i < pool.objects.Count; i++)
                {
                    Assert.IsTrue(pool.objects[i].Used);
                }
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void ExhaustedPoolExpandsOnceAndReusesExpandedObject()
        {
            const string poolTag = "Availability Runtime Expansion";
            var prefab = new GameObject("Availability Runtime Expansion Prefab");

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 2);
                Toolbox.Pooler.Spawn(poolTag);
                Toolbox.Pooler.Spawn(poolTag);
                AssertAvailableCount(pool, 0);

                var expandedObject = Toolbox.Pooler.Spawn(poolTag);

                Assert.AreEqual(3, pool.objects.Count);
                Assert.AreSame(pool.objects[2].GameObject, expandedObject);
                AssertAvailableCount(pool, 0);

                Assert.IsTrue(Toolbox.Pooler.TryDespawn(expandedObject));
                AssertAvailableCount(pool, 1);
                Assert.AreSame(expandedObject, Toolbox.Pooler.Spawn(poolTag));
                Assert.AreEqual(3, pool.objects.Count);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void DespawnPreservesEarliestObjectReuseOrder()
        {
            const string poolTag = "Availability Earliest Reuse";
            var prefab = new GameObject("Availability Earliest Reuse Prefab");

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 3);
                var firstObject = Toolbox.Pooler.Spawn(poolTag);
                Toolbox.Pooler.Spawn(poolTag);

                Assert.IsTrue(Toolbox.Pooler.TryDespawn(firstObject));
                AssertAvailableCount(pool, 2);
                Assert.AreSame(firstObject, Toolbox.Pooler.Spawn(poolTag));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void ArbitraryDespawnOrderStillReusesEarliestPoolEntriesFirst()
        {
            const string poolTag = "Availability Arbitrary Reuse";
            var prefab = new GameObject("Availability Arbitrary Reuse Prefab");

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 4);
                var spawned = new GameObject[4];

                for (int i = 0; i < spawned.Length; i++)
                {
                    spawned[i] = Toolbox.Pooler.Spawn(poolTag);
                }

                Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawned[2]));
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawned[0]));
                AssertAvailableCount(pool, 2);
                Assert.AreSame(spawned[0], Toolbox.Pooler.Spawn(poolTag));
                Assert.AreSame(spawned[2], Toolbox.Pooler.Spawn(poolTag));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void DuplicateDespawnDoesNotDuplicateAvailabilityEntry()
        {
            const string poolTag = "Availability Duplicate Despawn";
            var prefab = new GameObject("Availability Duplicate Despawn Prefab");

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
                var firstObject = Toolbox.Pooler.Spawn(poolTag);

                AssertAvailableCount(pool, 0);
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(firstObject));
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(firstObject));
                AssertAvailableCount(pool, 1);

                Assert.AreSame(firstObject, Toolbox.Pooler.Spawn(poolTag));
                AssertAvailableCount(pool, 0);

                var expandedObject = Toolbox.Pooler.Spawn(poolTag);

                Assert.AreEqual(2, pool.objects.Count);
                Assert.AreNotSame(firstObject, expandedObject);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void AvailabilityIsIsolatedBetweenDifferentPoolTags()
        {
            const string firstTag = "Availability Isolated First";
            const string secondTag = "Availability Isolated Second";
            var firstPrefab = new GameObject("Availability Isolated First Prefab");
            var secondPrefab = new GameObject("Availability Isolated Second Prefab");

            try
            {
                var firstPool = Toolbox.Pooler.TryAddPool(firstTag, firstPrefab, 1);
                var secondPool = Toolbox.Pooler.TryAddPool(secondTag, secondPrefab, 1);
                var firstObject = Toolbox.Pooler.Spawn(firstTag);

                AssertAvailableCount(firstPool, 0);
                AssertAvailableCount(secondPool, 1);
                Assert.AreSame(secondPool.objects[0].GameObject, Toolbox.Pooler.Spawn(secondTag));
                AssertAvailableCount(secondPool, 0);

                Assert.IsTrue(Toolbox.Pooler.TryDespawn(firstObject));
                AssertAvailableCount(firstPool, 1);
                AssertAvailableCount(secondPool, 0);
            }
            finally
            {
                Object.DestroyImmediate(firstPrefab);
                Object.DestroyImmediate(secondPrefab);
            }
        }

        [Test]
        public void PoolsSharingTagKeepIndependentAvailability()
        {
            const string sharedTag = "Availability Shared Tag";
            var firstPrefab = new GameObject("Availability Shared First Prefab");
            var secondPrefab = new GameObject("Availability Shared Second Prefab");
            var previousRandomState = Random.state;

            try
            {
                var firstPool = Toolbox.Pooler.TryAddPool(sharedTag, firstPrefab, 1);
                var secondPool = Toolbox.Pooler.TryAddPool(sharedTag, secondPrefab, 1);

                Random.InitState(FindRandomSeedForIndex(2, 0));
                var firstObject = Toolbox.Pooler.Spawn(sharedTag);

                Assert.AreSame(firstPool.objects[0].GameObject, firstObject);
                AssertAvailableCount(firstPool, 0);
                AssertAvailableCount(secondPool, 1);

                Random.InitState(FindRandomSeedForIndex(2, 1));
                var secondObject = Toolbox.Pooler.Spawn(sharedTag);

                Assert.AreSame(secondPool.objects[0].GameObject, secondObject);
                AssertAvailableCount(firstPool, 0);
                AssertAvailableCount(secondPool, 0);
            }
            finally
            {
                Random.state = previousRandomState;
                Object.DestroyImmediate(firstPrefab);
                Object.DestroyImmediate(secondPrefab);
            }
        }

        [Test]
        public void ReentrantSpawnActionCannotAcquireReservedObject()
        {
            const string poolTag = "Availability Reentrant Spawn Action";
            var prefab = new GameObject("Availability Reentrant Spawn Action Prefab");
            GameObject nestedObject = null;
            var spawnNested = true;

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(
                    poolTag,
                    prefab,
                    2,
                    spawnAction: _ =>
                    {
                        if (!spawnNested)
                        {
                            return;
                        }

                        spawnNested = false;
                        nestedObject = Toolbox.Pooler.Spawn(poolTag);
                    });

                var outerObject = Toolbox.Pooler.Spawn(poolTag);

                Assert.IsNotNull(nestedObject);
                Assert.AreNotSame(outerObject, nestedObject);
                Assert.AreEqual(2, pool.objects.Count);
                AssertAvailableCount(pool, 0);
                AssertPooledState(outerObject, true, true);
                AssertPooledState(nestedObject, true, true);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void ReentrantOnSpawnCannotAcquireReservedObject()
        {
            const string poolTag = "Availability Reentrant OnSpawn";
            var prefab = new GameObject("Availability Reentrant OnSpawn Prefab");
            prefab.AddComponent<ReentrantPooledObj>();
            GameObject nestedObject = null;
            var spawnNested = true;

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 2);
                ReentrantPooledObj.SpawnAction = () =>
                {
                    if (!spawnNested)
                    {
                        return;
                    }

                    spawnNested = false;
                    nestedObject = Toolbox.Pooler.Spawn(poolTag);
                };

                var outerObject = Toolbox.Pooler.Spawn(poolTag);

                Assert.IsNotNull(nestedObject);
                Assert.AreNotSame(outerObject, nestedObject);
                Assert.AreEqual(2, pool.objects.Count);
                AssertAvailableCount(pool, 0);
                AssertPooledState(outerObject, true, true);
                AssertPooledState(nestedObject, true, true);
            }
            finally
            {
                ReentrantPooledObj.SpawnAction = null;
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void GarbageCollectorDoesNotRemoveObjectReservedBySpawnCallback()
        {
            const string poolTag = "Availability Reentrant Garbage Collection";
            var prefab = new GameObject("Availability Reentrant Garbage Collection Prefab");
            var runGarbageCollector = false;

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(
                    poolTag,
                    prefab,
                    1,
                    spawnAction: _ =>
                    {
                        if (runGarbageCollector)
                        {
                            Toolbox.Pooler.ForceGarbageCollector();
                        }
                    });
                var firstObject = Toolbox.Pooler.Spawn(poolTag);
                var secondObject = Toolbox.Pooler.Spawn(poolTag);

                Assert.IsTrue(Toolbox.Pooler.TryDespawn(firstObject));
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(secondObject));
                AssertAvailableCount(pool, 2);

                runGarbageCollector = true;
                var spawnedObject = Toolbox.Pooler.Spawn(poolTag);

                Assert.AreSame(firstObject, spawnedObject);
                Assert.AreEqual(2, pool.objects.Count);
                AssertAvailableCount(pool, 1);
                AssertPooledState(spawnedObject, true, true);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SpawnCallbackExceptionRestoresAvailability(bool throwFromOnSpawn)
        {
            const string poolTag = "Availability Callback Exception";
            var prefab = new GameObject("Availability Callback Exception Prefab");
            var shouldThrow = true;

            if (throwFromOnSpawn)
            {
                prefab.AddComponent<ReentrantPooledObj>();
            }

            try
            {
                System.Action throwIfRequested = () =>
                {
                    if (shouldThrow)
                    {
                        throw new System.InvalidOperationException("Expected spawn callback failure");
                    }
                };

                var pool = Toolbox.Pooler.TryAddPool(
                    poolTag,
                    prefab,
                    1,
                    spawnAction: throwFromOnSpawn ? null : _ => throwIfRequested());

                if (throwFromOnSpawn)
                {
                    ReentrantPooledObj.SpawnAction = throwIfRequested;
                }

                Assert.Throws<System.InvalidOperationException>(() => Toolbox.Pooler.Spawn(poolTag));
                AssertAvailableCount(pool, 1);
                Assert.IsFalse(pool.objects[0].Used);

                shouldThrow = false;
                Assert.AreSame(pool.objects[0].GameObject, Toolbox.Pooler.Spawn(poolTag));
                AssertAvailableCount(pool, 0);
                Assert.IsTrue(pool.objects[0].Used);
            }
            finally
            {
                ReentrantPooledObj.SpawnAction = null;
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void PooledObjectLookupTracksSpawnAndDespawnState()
        {
            const string poolTag = "Object Lookup Lifecycle";
            var prefab = new GameObject("Object Lookup Lifecycle Prefab");
            var nonPooledObject = new GameObject("Non-Pooled Object");

            try
            {
                AssertPooledState(nonPooledObject, false, false);
                Assert.IsFalse(Toolbox.Pooler.TryDespawn(nonPooledObject));

                var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
                var pooledObject = pool.objects[0].GameObject;

                AssertPooledState(pooledObject, true, false);

                var spawnedObject = Toolbox.Pooler.Spawn(poolTag);

                Assert.AreSame(pooledObject, spawnedObject);
                AssertPooledState(spawnedObject, true, true);
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawnedObject));
                AssertPooledState(spawnedObject, true, false);

                Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawnedObject));
                AssertPooledState(spawnedObject, true, false);
            }
            finally
            {
                Object.DestroyImmediate(nonPooledObject);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void TryDespawnResolvesExactObjectAcrossDifferentPools()
        {
            const string firstTag = "Object Lookup First Pool";
            const string secondTag = "Object Lookup Second Pool";
            var firstPrefab = new GameObject("First Object Lookup Prefab");
            var secondPrefab = new GameObject("Second Object Lookup Prefab");

            try
            {
                Toolbox.Pooler.TryAddPool(firstTag, firstPrefab, 1);
                Toolbox.Pooler.TryAddPool(secondTag, secondPrefab, 1);
                var firstObject = Toolbox.Pooler.Spawn(firstTag);
                var secondObject = Toolbox.Pooler.Spawn(secondTag);

                AssertPooledState(firstObject, true, true);
                AssertPooledState(secondObject, true, true);

                Assert.IsTrue(Toolbox.Pooler.TryDespawn(secondObject));

                AssertPooledState(firstObject, true, true);
                AssertPooledState(secondObject, true, false);
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(firstObject));
            }
            finally
            {
                Object.DestroyImmediate(firstPrefab);
                Object.DestroyImmediate(secondPrefab);
            }
        }

        [Test]
        public void TryDespawnResolvesExactObjectAcrossPoolsSharingTag()
        {
            const string sharedTag = "Object Lookup Shared Tag";
            var firstPrefab = new GameObject("First Shared Lookup Prefab");
            var secondPrefab = new GameObject("Second Shared Lookup Prefab");
            var previousRandomState = Random.state;

            try
            {
                var firstPool = Toolbox.Pooler.TryAddPool(sharedTag, firstPrefab, 1);
                var secondPool = Toolbox.Pooler.TryAddPool(sharedTag, secondPrefab, 1);
                var firstPooledObject = firstPool.objects[0];
                var secondPooledObject = secondPool.objects[0];

                Random.InitState(FindRandomSeedForIndex(2, 0));
                Assert.AreSame(firstPooledObject.GameObject, Toolbox.Pooler.Spawn(sharedTag));
                Random.InitState(FindRandomSeedForIndex(2, 1));
                Assert.AreSame(secondPooledObject.GameObject, Toolbox.Pooler.Spawn(sharedTag));

                Assert.IsTrue(Toolbox.Pooler.TryDespawn(secondPooledObject.GameObject));

                AssertPooledState(firstPooledObject.GameObject, true, true);
                AssertPooledState(secondPooledObject.GameObject, true, false);
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(firstPooledObject.GameObject));
            }
            finally
            {
                Random.state = previousRandomState;
                Object.DestroyImmediate(firstPrefab);
                Object.DestroyImmediate(secondPrefab);
            }
        }

        [Test]
        public void DuplicateGameObjectRegistrationThrowsInsteadOfOverwritingLookup()
        {
            const string poolTag = "Object Lookup Duplicate Registration";
            var prefab = new GameObject("Duplicate Registration Prefab");
            var sharedInstance = new GameObject("Duplicate Registration Instance");

            try
            {
                Assert.Throws<System.ArgumentException>(() => Toolbox.Pooler.TryAddPool(
                    poolTag,
                    prefab,
                    2,
                    (obj, position, rotation, parent) => sharedInstance));

                AssertPooledState(sharedInstance, false, false);
                Assert.IsFalse(Toolbox.Pooler.TryDespawn(sharedInstance));
            }
            finally
            {
                if (sharedInstance != null)
                {
                    Object.DestroyImmediate(sharedInstance);
                }

                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void DuplicateRegistrationDoesNotReplaceExistingOwner()
        {
            const string ownerTag = "Object Lookup Existing Owner";
            const string duplicateTag = "Object Lookup Duplicate Owner";
            var ownerPrefab = new GameObject("Existing Owner Prefab");
            var duplicatePrefab = new GameObject("Duplicate Owner Prefab");
            var sharedInstance = new GameObject("Existing Owned Instance");

            try
            {
                var ownerPool = Toolbox.Pooler.TryAddPool(
                    ownerTag,
                    ownerPrefab,
                    1,
                    (obj, position, rotation, parent) => sharedInstance);

                Assert.AreSame(sharedInstance, ownerPool.objects[0].GameObject);
                AssertPooledState(sharedInstance, true, false);

                Assert.Throws<System.ArgumentException>(() => Toolbox.Pooler.TryAddPool(
                    duplicateTag,
                    duplicatePrefab,
                    1,
                    (obj, position, rotation, parent) => sharedInstance));

                Assert.AreSame(sharedInstance, ownerPool.objects[0].GameObject);
                AssertPooledState(sharedInstance, true, false);

                var spawnedObject = Toolbox.Pooler.Spawn(ownerTag);

                Assert.AreSame(sharedInstance, spawnedObject);
                AssertPooledState(sharedInstance, true, true);
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(sharedInstance));
                AssertPooledState(sharedInstance, true, false);
            }
            finally
            {
                if (sharedInstance != null)
                {
                    Object.DestroyImmediate(sharedInstance);
                }

                Object.DestroyImmediate(ownerPrefab);
                Object.DestroyImmediate(duplicatePrefab);
            }
        }

        [Test]
        public void RemovedPoolObjectsAreRemovedFromObjectLookup()
        {
            const string poolTag = "Object Lookup Removed Pool";
            var prefab = new GameObject("Removed Object Lookup Prefab");

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
                var spawnedObject = Toolbox.Pooler.Spawn(poolTag);

                AssertAvailableCount(pool, 0);
                AssertPooledState(spawnedObject, true, true);
                Assert.IsTrue(Toolbox.Pooler.TryRemovePool(pool));
                AssertAvailableCount(pool, 0);
                AssertPooledState(spawnedObject, false, false);
                Assert.IsFalse(Toolbox.Pooler.TryDespawn(spawnedObject));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void PoolRemovalKeepsObjectIndexedDuringDespawnedMessage()
        {
            const string poolTag = "Object Lookup Removal Message";
            var prefab = new GameObject("Removal Message Lookup Prefab");
            Subscriber subscriber = null;

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
                var spawnedObject = Toolbox.Pooler.Spawn(poolTag);
                var messageObserved = false;
                var stateDuringMessage = default(ObjectPooledState);

                subscriber = Toolbox.Messenger.Subscribe<GameObjectRemovedMessage>(message =>
                {
                    if (message.RemoveType != GameObjectRemoveType.Despawned ||
                        !ReferenceEquals(message.Obj, spawnedObject))
                    {
                        return;
                    }

                    messageObserved = true;
                    stateDuringMessage = Toolbox.Pooler.IsObjectPooledAndUsed(spawnedObject);
                }, keep: true);

                Assert.IsTrue(Toolbox.Pooler.TryRemovePool(pool));
                Assert.IsTrue(messageObserved);
                Assert.IsTrue(stateDuringMessage.IsPooled);
                Assert.IsFalse(stateDuringMessage.IsUsed);
                AssertPooledState(spawnedObject, false, false);
            }
            finally
            {
                Toolbox.Messenger.RemoveSubscriber(subscriber);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void GarbageCollectedObjectsAreRemovedFromObjectLookup()
        {
            const string poolTag = "Object Lookup Garbage Collection";
            var prefab = new GameObject("Garbage Collected Lookup Prefab");
            Subscriber subscriber = null;

            try
            {
                var pool = Toolbox.Pooler.TryAddPool(poolTag, prefab, 1);
                var firstObject = Toolbox.Pooler.Spawn(poolTag);
                var secondObject = Toolbox.Pooler.Spawn(poolTag);
                var retainedObject = Toolbox.Pooler.Spawn(poolTag);
                var firstDestroyedMessageObserved = false;
                var secondDestroyedMessageObserved = false;
                var firstStateDuringMessage = default(ObjectPooledState);
                var secondStateDuringMessage = default(ObjectPooledState);

                Assert.IsTrue(Toolbox.Pooler.TryDespawn(firstObject));
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(secondObject));
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(retainedObject));
                AssertAvailableCount(pool, 3);

                subscriber = Toolbox.Messenger.Subscribe<GameObjectRemovedMessage>(message =>
                {
                    if (message.RemoveType != GameObjectRemoveType.Destroyed)
                    {
                        return;
                    }

                    if (ReferenceEquals(message.Obj, firstObject))
                    {
                        firstDestroyedMessageObserved = true;
                        firstStateDuringMessage = Toolbox.Pooler.IsObjectPooledAndUsed(firstObject);
                    }
                    else if (ReferenceEquals(message.Obj, secondObject))
                    {
                        secondDestroyedMessageObserved = true;
                        secondStateDuringMessage = Toolbox.Pooler.IsObjectPooledAndUsed(secondObject);
                    }
                }, keep: true);

                Toolbox.Pooler.ForceGarbageCollector();

                Assert.AreEqual(1, pool.objects.Count);
                Assert.AreSame(retainedObject, pool.objects[0].GameObject);
                AssertAvailableCount(pool, 1);
                Assert.IsTrue(firstDestroyedMessageObserved);
                Assert.IsTrue(secondDestroyedMessageObserved);
                Assert.IsFalse(firstStateDuringMessage.IsPooled);
                Assert.IsFalse(firstStateDuringMessage.IsUsed);
                Assert.IsFalse(secondStateDuringMessage.IsPooled);
                Assert.IsFalse(secondStateDuringMessage.IsUsed);
                AssertPooledState(firstObject, false, false);
                AssertPooledState(secondObject, false, false);
                AssertPooledState(retainedObject, true, false);
                Assert.IsFalse(Toolbox.Pooler.TryDespawn(firstObject));
                Assert.IsFalse(Toolbox.Pooler.TryDespawn(secondObject));

                var spawnedAfterGarbageCollection = Toolbox.Pooler.Spawn(poolTag);

                Assert.AreSame(retainedObject, spawnedAfterGarbageCollection);
                AssertAvailableCount(pool, 0);
                AssertPooledState(retainedObject, true, true);
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(retainedObject));
                AssertAvailableCount(pool, 1);
            }
            finally
            {
                Toolbox.Messenger.RemoveSubscriber(subscriber);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void TryDespawnPreservesNestedIndependentlyPooledChildBehavior()
        {
            const string parentTag = "Object Lookup Nested Parent";
            const string childTag = "Object Lookup Nested Child";
            var parentPrefab = new GameObject("Nested Parent Lookup Prefab");
            var childPrefab = new GameObject("Nested Child Lookup Prefab");

            try
            {
                Toolbox.Pooler.TryAddPool(parentTag, parentPrefab, 1);
                Toolbox.Pooler.TryAddPool(childTag, childPrefab, 1);
                var parentObject = Toolbox.Pooler.Spawn(parentTag);
                var childObject = Toolbox.Pooler.Spawn(childTag, parent: parentObject.transform);

                Assert.AreSame(parentObject.transform, childObject.transform.parent);
                Assert.IsTrue(Toolbox.Pooler.TryDespawn(parentObject));
                AssertPooledState(parentObject, true, false);
                AssertPooledState(childObject, true, false);
                Assert.IsFalse(parentObject.activeSelf);
                Assert.IsFalse(childObject.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(parentPrefab);
                Object.DestroyImmediate(childPrefab);
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
            var stalePool = Toolbox.Pooler.TryAddPool(staleTag, stalePrefab, 1);
            var staleObject = stalePool.objects[0].GameObject;
            AssertAvailableCount(stalePool, 1);

            Toolbox.Pooler.Clear();

            Assert.AreEqual(0, GetPoolsByTag(Toolbox.Pooler).Count);
            Assert.AreEqual(0, GetPoolsWithNullTag(Toolbox.Pooler).Count);
            AssertAvailableCount(stalePool, 0);
            AssertPooledState(staleObject, false, false);
            Assert.IsFalse(Toolbox.Pooler.TryDespawn(staleObject));
            LogAssert.Expect(LogType.Warning, $"Object pool with tag '{staleTag}' doesn't exists");
            Assert.IsNull(Toolbox.Pooler.Spawn(staleTag));

            Toolbox.Pooler.Initialize(Toolbox.Messenger, Toolbox.Updater);
            Toolbox.Pooler.DisableGC();

            Assert.IsFalse(GetPoolsByTag(Toolbox.Pooler).ContainsKey(staleTag));
            AssertPooledState(staleObject, false, false);

            var freshPool = Toolbox.Pooler.TryAddPool(freshTag, freshPrefab, 1);

            var freshObject = Toolbox.Pooler.Spawn(freshTag);

            Assert.AreSame(freshPool.objects[0].GameObject, freshObject);
            AssertAvailableCount(freshPool, 0);
            AssertPooledState(freshObject, true, true);
            Assert.IsTrue(Toolbox.Pooler.TryDespawn(freshObject));
            AssertAvailableCount(freshPool, 1);
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

        private static List<Pool> GetPools(Pooler pooler)
        {
            return (List<Pool>)PoolsField.GetValue(pooler);
        }

        private static void AssertPooledState(GameObject obj, bool isPooled, bool isUsed)
        {
            var state = Toolbox.Pooler.IsObjectPooledAndUsed(obj);

            Assert.AreEqual(isPooled, state.IsPooled);
            Assert.AreEqual(isUsed, state.IsUsed);
        }

        private static void AssertAvailableCount(Pool pool, int expectedCount)
        {
            Assert.IsNotNull(AvailableObjectsField);
            var availableObjects = AvailableObjectsField.GetValue(pool) as List<PooledGameObject>;
            Assert.IsNotNull(availableObjects);
            Assert.AreEqual(expectedCount, availableObjects.Count);
        }

        private static int FindRandomSeedForIndex(int count, int expectedIndex)
        {
            var previousState = Random.state;

            try
            {
                for (int seed = 0; seed < 10_000; seed++)
                {
                    Random.InitState(seed);

                    if (Random.Range(0, count) == expectedIndex)
                    {
                        return seed;
                    }
                }
            }
            finally
            {
                Random.state = previousState;
            }

            throw new System.InvalidOperationException(
                $"Could not find a deterministic Random seed for index {expectedIndex}");
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
            AssertPooledState(expandedSpawn, true, true);
            Assert.IsTrue(Toolbox.Pooler.TryDespawn(expandedSpawn));
            AssertPooledState(expandedSpawn, true, false);
            Assert.AreEqual(1, expandedHandler.DespawnCount);
            Assert.IsTrue(Toolbox.Pooler.TryDespawn(firstSpawn));
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

            var spawnedObject = Toolbox.Pooler.Spawn(poolTag);

            Assert.AreSame(pooledObject, spawnedObject);
            AssertPooledState(spawnedObject, true, true);
            Assert.IsTrue(Toolbox.Pooler.TryDespawn(spawnedObject));
            AssertPooledState(spawnedObject, true, false);
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

        [Test]
        public void GarbageCollectorKeepsUnusedObjectsWithinReserve()
        {
            var noUnusedPool = CreateGarbagePool(
                "GC no unused",
                3,
                out var noUnusedObjects,
                true,
                true);
            var belowReservePool = CreateGarbagePool(
                "GC below reserve",
                5,
                out var belowReserveObjects,
                false,
                false,
                false);
            var equalReservePool = CreateGarbagePool(
                "GC equal reserve",
                3,
                out var equalReserveObjects,
                false,
                false,
                false);

            try
            {
                Toolbox.Pooler.ForceGarbageCollector();

                Assert.AreEqual(2, noUnusedPool.objects.Count);
                Assert.AreEqual(3, belowReservePool.objects.Count);
                Assert.AreEqual(3, equalReservePool.objects.Count);
                CollectionAssert.AreEqual(noUnusedObjects, noUnusedPool.objects);
                CollectionAssert.AreEqual(belowReserveObjects, belowReservePool.objects);
                CollectionAssert.AreEqual(equalReserveObjects, equalReservePool.objects);
            }
            finally
            {
                DestroyGarbagePool(noUnusedPool, noUnusedObjects);
                DestroyGarbagePool(belowReservePool, belowReserveObjects);
                DestroyGarbagePool(equalReservePool, equalReserveObjects);
            }
        }

        [Test]
        public void GarbageCollectorRemovesFirstExcessUnusedObjectsAndSendsMessages()
        {
            var pool = CreateGarbagePool(
                "GC mixed order",
                3,
                out var objects,
                false,
                true,
                false,
                true,
                false,
                true,
                false,
                true,
                false);
            var removedObjects = new List<GameObject>();
            var removeTypes = new List<GameObjectRemoveType>();
            var subscriber = Toolbox.Messenger.Subscribe<GameObjectRemovedMessage>(message =>
            {
                for (int i = 0; i < objects.Length; i++)
                {
                    if (!ReferenceEquals(objects[i].GameObject, message.Obj))
                    {
                        continue;
                    }

                    removedObjects.Add(message.Obj);
                    removeTypes.Add(message.RemoveType);
                    break;
                }
            });

            try
            {
                Toolbox.Pooler.ForceGarbageCollector();

                Assert.AreEqual(7, pool.objects.Count);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        objects[1],
                        objects[3],
                        objects[4],
                        objects[5],
                        objects[6],
                        objects[7],
                        objects[8]
                    },
                    pool.objects);
                Assert.IsTrue(pool.objects[0].Used);
                Assert.IsTrue(pool.objects[1].Used);
                Assert.IsFalse(pool.objects[2].Used);
                Assert.IsTrue(pool.objects[3].Used);
                Assert.IsFalse(pool.objects[4].Used);
                Assert.IsTrue(pool.objects[5].Used);
                Assert.IsFalse(pool.objects[6].Used);
                CollectionAssert.AreEqual(
                    new[] { objects[0].GameObject, objects[2].GameObject },
                    removedObjects);
                CollectionAssert.AreEqual(
                    new[] { GameObjectRemoveType.Destroyed, GameObjectRemoveType.Destroyed },
                    removeTypes);

                Toolbox.Pooler.ForceGarbageCollector();

                Assert.AreEqual(7, pool.objects.Count);
                Assert.AreEqual(2, removedObjects.Count);
            }
            finally
            {
                Toolbox.Messenger.RemoveSubscriber(subscriber);
                DestroyGarbagePool(pool, objects);
            }
        }

        [Test]
        public void ForceGarbageCollectorProcessesPoolsIndependently()
        {
            var firstPool = CreateGarbagePool(
                "GC independent first",
                1,
                out var firstObjects,
                false,
                false,
                false);
            var secondPool = CreateGarbagePool(
                "GC independent second",
                2,
                out var secondObjects,
                true,
                false,
                false,
                false);

            try
            {
                Toolbox.Pooler.ForceGarbageCollector();

                CollectionAssert.AreEqual(new[] { firstObjects[2] }, firstPool.objects);
                CollectionAssert.AreEqual(
                    new[] { secondObjects[0], secondObjects[2], secondObjects[3] },
                    secondPool.objects);
            }
            finally
            {
                DestroyGarbagePool(firstPool, firstObjects);
                DestroyGarbagePool(secondPool, secondObjects);
            }
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

        private static Pool CreateGarbagePool(
            string tag,
            int reserveSize,
            out PooledGameObject[] objects,
            params bool[] usedStates)
        {
            objects = new PooledGameObject[usedStates.Length];

            for (int i = 0; i < usedStates.Length; i++)
            {
                objects[i] = new PooledGameObject
                {
                    GameObject = new GameObject($"{tag} {i}"),
                    Used = usedStates[i]
                };
            }

            var pool = new Pool(tag, null, reserveSize, new List<PooledGameObject>(objects));
            GetPools(Toolbox.Pooler).Add(pool);
            return pool;
        }

        private static void DestroyGarbagePool(Pool pool, PooledGameObject[] objects)
        {
            GetPools(Toolbox.Pooler).Remove(pool);

            for (int i = 0; i < objects.Length; i++)
            {
                if (objects[i].GameObject != null)
                {
                    Object.DestroyImmediate(objects[i].GameObject);
                }
            }
        }
    }
}
