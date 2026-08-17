using NUnit.Framework;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace VolumeBox.Toolbox.Tests
{
    [PrebuildSetup(typeof(TestPrebuild))]
    internal class MessengerTest : ToolboxTestBase
    {
        private string message;

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator MessengerReactTest()
        {
            message = "null";

            Toolbox.Messenger.ClearSubscribers();
            
            Toolbox.Messenger.Subscribe<MockMessage>(x => React(x.message));
            Toolbox.Messenger.Send<MockMessage>();
            Assert.AreEqual("Reacted", message);
            message = "null";
            Toolbox.Messenger.ClearSubscribers();
            Toolbox.Messenger.Subscribe(typeof(MockMessage), x => React((x as MockMessage).message));
            Toolbox.Messenger.Send<MockMessage>();
            Assert.AreEqual("Reacted", message);

            yield return null;
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator MessengerSceneHandleTest()
        {
            Toolbox.Messenger.ClearSubscribers();

            var obj = new GameObject("A");

            Subscriber subToRemove = Toolbox.Messenger.Subscribe<MockMessage>(m => React(m.message), obj);

            message = "null";

            Toolbox.Messenger.Send(new SceneUnloadedMessage(obj.scene.name));
            Toolbox.Messenger.Send<MockMessage>();

            Assert.AreEqual("null", message);

            Toolbox.Messenger.ClearSubscribers();

            message = "null";

            Subscriber subToStay = Toolbox.Messenger.Subscribe<MockMessage>(m => React(m.message));

            Toolbox.Messenger.Send(new SceneUnloadedMessage(obj.scene.name));
            Toolbox.Messenger.Send<MockMessage>();

            Assert.AreEqual("Reacted", message);

            yield return null;
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator MessengerObjectBindTest()
        {
            Toolbox.Messenger.ClearSubscribers();

            var obj = new GameObject("A");

            message = "null";

            Toolbox.Messenger.Subscribe<MockMessage>(m => React(m.message), obj);

            Toolbox.Pooler.DespawnOrDestroy(obj);

            Toolbox.Messenger.Send<MockMessage>();
            Assert.AreEqual("null", message);
            yield return null;

            yield return null;
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator DestroyedSubscriberDoesNotBlockRemainingSubscribersTest()
        {
            Toolbox.Messenger.ClearSubscribers();

            var destroyedBinding = new GameObject("Destroyed subscriber binding");
            Toolbox.Messenger.Subscribe<MockMessage>(_ => { }, destroyedBinding);

            var received = false;
            Toolbox.Messenger.Subscribe<MockMessage>(_ => received = true);

            UnityEngine.Object.DestroyImmediate(destroyedBinding);
            Toolbox.Messenger.Send(new MockMessage());

            Assert.IsTrue(received);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MessengerCanBeReusedAfterClearTest()
        {
            var gameObject = new GameObject("Messenger clear test");
            var messenger = gameObject.AddComponent<Messenger>();
            var received = false;

            messenger.Clear();
            messenger.Subscribe<MockMessage>(_ => received = true);
            messenger.Send(new MockMessage());

            Assert.IsTrue(received);
            UnityEngine.Object.DestroyImmediate(gameObject);
            yield return null;
        }

        [Test]
        public void DifferentMessageTypesDispatchOnlyToTheirOwnSubscribers()
        {
            var gameObject = new GameObject("Messenger message type test");
            var messenger = gameObject.AddComponent<Messenger>();
            var firstCount = 0;
            var secondCount = 0;

            messenger.Subscribe<MockMessage>(_ => firstCount++);
            messenger.Subscribe<SecondMockMessage>(_ => secondCount++);

            messenger.Send(new MockMessage());

            Assert.AreEqual(1, firstCount);
            Assert.AreEqual(0, secondCount);

            messenger.Send(new SecondMockMessage());

            Assert.AreEqual(1, firstCount);
            Assert.AreEqual(1, secondCount);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SubscriberCanRemoveItselfDuringDispatch()
        {
            var gameObject = new GameObject("Messenger self removal test");
            var messenger = gameObject.AddComponent<Messenger>();
            var selfCount = 0;
            var remainingCount = 0;
            Subscriber self = null;

            self = messenger.Subscribe<MockMessage>(_ =>
            {
                selfCount++;
                messenger.RemoveSubscriber(self);
            });
            messenger.Subscribe<MockMessage>(_ => remainingCount++);

            messenger.Send(new MockMessage());
            messenger.Send(new MockMessage());

            Assert.AreEqual(1, selfCount);
            Assert.AreEqual(2, remainingCount);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void RemovingAnotherSubscriberPreservesCurrentDispatchSnapshot()
        {
            var gameObject = new GameObject("Messenger receiver removal test");
            var messenger = gameObject.AddComponent<Messenger>();
            var firstCount = 0;
            var removedCount = 0;
            Subscriber subscriberToRemove = null;

            messenger.Subscribe<MockMessage>(_ =>
            {
                firstCount++;
                messenger.RemoveSubscriber(subscriberToRemove);
            });
            subscriberToRemove = messenger.Subscribe<MockMessage>(_ => removedCount++);

            messenger.Send(new MockMessage());
            messenger.Send(new MockMessage());

            Assert.AreEqual(2, firstCount);
            Assert.AreEqual(1, removedCount);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SubscriberAddedDuringDispatchStartsWithNextSend()
        {
            var gameObject = new GameObject("Messenger deferred subscription test");
            var messenger = gameObject.AddComponent<Messenger>();
            var addedCount = 0;
            Subscriber addedSubscriber = null;

            messenger.Subscribe<MockMessage>(_ =>
            {
                if (addedSubscriber == null)
                {
                    addedSubscriber = messenger.Subscribe<MockMessage>(_ => addedCount++);
                }
            });

            messenger.Send(new MockMessage());
            Assert.AreEqual(0, addedCount);

            messenger.Send(new MockMessage());
            Assert.AreEqual(1, addedCount);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void MutationsRemainDeferredAcrossMultipleNestedSends()
        {
            var gameObject = new GameObject("Messenger nested dispatch test");
            var messenger = gameObject.AddComponent<Messenger>();
            var existingCount = 0;
            var addedCount = 0;
            Subscriber existingSubscriber = null;
            Subscriber addedSubscriber = null;

            messenger.Subscribe<MockMessage>(_ => messenger.Send(new SecondMockMessage()));
            existingSubscriber = messenger.Subscribe<MockMessage>(_ => existingCount++);
            messenger.Subscribe<SecondMockMessage>(_ => messenger.Send(new ThirdMockMessage()));
            messenger.Subscribe<ThirdMockMessage>(_ =>
            {
                messenger.RemoveSubscriber(existingSubscriber);

                if (addedSubscriber == null)
                {
                    addedSubscriber = messenger.Subscribe<MockMessage>(_ => addedCount++);
                }
            });

            messenger.Send(new MockMessage());

            Assert.AreEqual(1, existingCount);
            Assert.AreEqual(0, addedCount);

            messenger.Send(new MockMessage());

            Assert.AreEqual(1, existingCount);
            Assert.AreEqual(1, addedCount);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ClearSubscribersDuringDispatchPreservesSnapshotAndKeepSubscribers()
        {
            var gameObject = new GameObject("Messenger clear subscribers test");
            var messenger = gameObject.AddComponent<Messenger>();
            var clearingCount = 0;
            var regularCount = 0;
            var keepCount = 0;

            messenger.Subscribe<MockMessage>(_ =>
            {
                clearingCount++;
                messenger.ClearSubscribers();
            });
            messenger.Subscribe<MockMessage>(_ => regularCount++);
            messenger.Subscribe<MockMessage>(_ => keepCount++, keep: true);

            messenger.Send(new MockMessage());
            messenger.Send(new MockMessage());

            Assert.AreEqual(1, clearingCount);
            Assert.AreEqual(1, regularCount);
            Assert.AreEqual(2, keepCount);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator MessageCachingTest()
        {
            StaticData.Settings.UseMessageCaching = true;

            Assert.AreEqual(0, Toolbox.Messenger.ClearMessageCache());
            Toolbox.Messenger.Send<MockMessage>();
            Assert.AreEqual(true, Toolbox.Messenger.Send<MockMessage>());
            Assert.AreEqual(1, Toolbox.Messenger.ClearMessageCache());

            yield return null;
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator SendWithoutCacheCreatesExactlyOneMessage()
        {
            StaticData.Settings.UseMessageCaching = false;
            ConstructorCounterMessage.Instances = 0;

            Toolbox.Messenger.Send<ConstructorCounterMessage>();

            Assert.AreEqual(1, ConstructorCounterMessage.Instances);
            yield return null;
        }

        [UnityTest, PrebuildSetup(typeof(TestPrebuild))]
        public IEnumerator CachedMessageIsTheOneActuallyDispatched()
        {
            StaticData.Settings.UseMessageCaching = true;
            ConstructorCounterMessage.Instances = 0;
            ConstructorCounterMessage received = null;

            Toolbox.Messenger.ClearSubscribers();
            Toolbox.Messenger.ClearMessageCache();
            Toolbox.Messenger.Subscribe<ConstructorCounterMessage>(x => received = x);

            Toolbox.Messenger.Send<ConstructorCounterMessage>();

            Assert.AreEqual(1, ConstructorCounterMessage.Instances);
            Assert.AreSame(
                Toolbox.Messenger.MessagesCache[typeof(ConstructorCounterMessage)],
                received);

            Toolbox.Messenger.ClearSubscribers();
            Toolbox.Messenger.ClearMessageCache();
            yield return null;
        }

        private void React(string test)
        {
            message = test;
        }

        [Serializable]
        public class MockMessage: Message
        {
            public string message = "Reacted";
        }

        public class ConstructorCounterMessage : Message
        {
            public static int Instances;

            public ConstructorCounterMessage()
            {
                Instances++;
            }
        }

        public class SecondMockMessage : Message
        {
        }

        public class ThirdMockMessage : Message
        {
        }
    }
}
