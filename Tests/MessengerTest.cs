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

        private void React(string test)
        {
            message = test;
        }

        [Serializable]
        public class MockMessage: Message
        {
            public string message = "Reacted";
        }
    }
}
