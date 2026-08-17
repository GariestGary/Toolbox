using System;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;

namespace VolumeBox.Toolbox.Tests.Performance
{
    [UnityEngine.TestTools.PrebuildSetup(typeof(TestPrebuild))]
    [Category("MessengerPerformance")]
    internal sealed class MessengerPerformanceTests : PerformanceTestBase
    {
        private const int WarmupCount = 1;
        private const int MeasurementCount = 5;

        private static readonly int[] SubscriberCounts =
        {
            0,
            1,
            10,
            100,
            1_000,
            5_000
        };

        private static readonly int[] IrrelevantSubscriberCounts =
        {
            0,
            100,
            1_000,
            5_000,
            10_000
        };

        private static readonly int[] DelegateSubscriberCounts =
        {
            1,
            10,
            100,
            1_000
        };

        private static readonly int[] MutationCounts =
        {
            100,
            1_000,
            10_000
        };

        private static readonly int[] DeferredRemovalCounts =
        {
            100,
            1_000,
            5_000,
            10_000
        };

        [Test, Performance]
        public void SendWithUnboundSubscribers(
            [ValueSource(nameof(SubscriberCounts))] int subscriberCount)
        {
            var messenger = PrepareMessenger();
            var message = new MessengerBenchmarkMessage();
            var invocationCount = 0;
            var sink = 0;
            var operationsPerSample = GetSendOperationsPerSample(subscriberCount);

            for (int i = 0; i < subscriberCount; i++)
            {
                messenger.Subscribe<MessengerBenchmarkMessage>(payload =>
                {
                    invocationCount++;
                    sink += payload.Value;
                });
            }

            Measure.Method(() => SendBatch(messenger, message, operationsPerSample))
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Messenger/Send/{subscriberCount}UnboundSubscribers/{operationsPerSample}Sends")
                .GC()
                .Run();

            invocationCount = 0;
            sink = 0;
            messenger.Send(message);

            Assert.AreEqual(subscriberCount, invocationCount);
            Assert.AreEqual(subscriberCount, sink);
        }

        [Test, Performance]
        public void SendWithLiveGameObjectBindings(
            [Values(1, 100, 1_000)] int subscriberCount)
        {
            var messenger = PrepareMessenger();
            var binding = CreateGameObject("Messenger benchmark live binding");
            var message = new MessengerBenchmarkMessage();
            var invocationCount = 0;
            var operationsPerSample = GetSendOperationsPerSample(subscriberCount);

            for (int i = 0; i < subscriberCount; i++)
            {
                messenger.Subscribe<MessengerBenchmarkMessage>(
                    _ => invocationCount++,
                    binding);
            }

            Measure.Method(() => SendBatch(messenger, message, operationsPerSample))
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Messenger/Send/{subscriberCount}LiveBindings/{operationsPerSample}Sends")
                .GC()
                .Run();

            invocationCount = 0;
            messenger.Send(message);
            Assert.AreEqual(subscriberCount, invocationCount);
        }

        [Test, Performance]
        public void TargetSendIgnoresUnrelatedSubscriberCount(
            [ValueSource(nameof(IrrelevantSubscriberCounts))] int irrelevantSubscriberCount)
        {
            const int operationsPerSample = 10_000;
            var messenger = PrepareMessenger();
            var message = new MessengerBenchmarkMessage();
            var invocationCount = 0;

            messenger.Subscribe<MessengerBenchmarkMessage>(_ => invocationCount++);

            for (int i = 0; i < irrelevantSubscriberCount; i++)
            {
                switch (i % 3)
                {
                    case 0:
                        messenger.Subscribe<MessengerIrrelevantMessageA>(() => { });
                        break;
                    case 1:
                        messenger.Subscribe<MessengerIrrelevantMessageB>(() => { });
                        break;
                    default:
                        messenger.Subscribe<MessengerIrrelevantMessageC>(() => { });
                        break;
                }
            }

            Measure.Method(() => SendBatch(messenger, message, operationsPerSample))
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Messenger/IrrelevantScaling/{irrelevantSubscriberCount}OtherSubscribers/{operationsPerSample}Sends")
                .GC()
                .Run();

            invocationCount = 0;
            messenger.Send(message);
            Assert.AreEqual(1, invocationCount);
        }

        [Test, Performance]
        public void DirectDelegateBaseline(
            [ValueSource(nameof(DelegateSubscriberCounts))] int listenerCount)
        {
            var message = new MessengerBenchmarkMessage();
            var invocationCount = 0;
            var sink = 0;
            var operationsPerSample = GetSendOperationsPerSample(listenerCount);
            Action<MessengerBenchmarkMessage> callbacks = null;

            for (int i = 0; i < listenerCount; i++)
            {
                callbacks += payload =>
                {
                    invocationCount++;
                    sink += payload.Value;
                };
            }

            Measure.Method(() => InvokeDelegateBatch(callbacks, message, operationsPerSample))
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"DirectDelegate/{listenerCount}Listeners/{operationsPerSample}Invokes")
                .GC()
                .Run();

            invocationCount = 0;
            sink = 0;
            callbacks.Invoke(message);
            Assert.AreEqual(listenerCount, invocationCount);
            Assert.AreEqual(listenerCount, sink);
        }

        [Test, Performance]
        public void ParameterlessSendCaching([Values(true, false)] bool cachingEnabled)
        {
            const int operationsPerSample = 10_000;
            var messenger = PrepareMessenger();
            var invocationCount = 0;
            StaticData.Settings.UseMessageCaching = cachingEnabled;
            messenger.Subscribe<MessengerCachedBenchmarkMessage>(_ => invocationCount++);

            if (cachingEnabled)
            {
                messenger.Send<MessengerCachedBenchmarkMessage>();
                invocationCount = 0;
            }

            Measure.Method(() => SendCachedBatch(messenger, operationsPerSample))
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Messenger/ParameterlessSend/Cache{(cachingEnabled ? "On" : "Off")}/{operationsPerSample}Sends")
                .GC()
                .Run();

            invocationCount = 0;
            messenger.Send<MessengerCachedBenchmarkMessage>();
            Assert.AreEqual(1, invocationCount);
        }

        [Test, Performance]
        public void ExistingInstanceVersusCachedParameterlessSend(
            [Values(true, false)] bool useExistingInstance)
        {
            const int operationsPerSample = 10_000;
            var messenger = PrepareMessenger();
            var message = new MessengerCachedBenchmarkMessage();
            var invocationCount = 0;
            StaticData.Settings.UseMessageCaching = true;
            messenger.Subscribe<MessengerCachedBenchmarkMessage>(_ => invocationCount++);
            messenger.Send<MessengerCachedBenchmarkMessage>();
            invocationCount = 0;

            Measure.Method(() =>
                {
                    if (useExistingInstance)
                    {
                        SendBatch(messenger, message, operationsPerSample);
                    }
                    else
                    {
                        SendCachedBatch(messenger, operationsPerSample);
                    }
                })
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Messenger/Send/{(useExistingInstance ? "ExistingInstance" : "CachedParameterless")}/{operationsPerSample}Sends")
                .GC()
                .Run();

            invocationCount = 0;
            if (useExistingInstance)
            {
                messenger.Send(message);
            }
            else
            {
                messenger.Send<MessengerCachedBenchmarkMessage>();
            }

            Assert.AreEqual(1, invocationCount);
        }

        [Test, Performance]
        public void SubscribeBatch([ValueSource(nameof(MutationCounts))] int subscriberCount)
        {
            var messenger = PrepareMessenger();
            var subscribers = new Subscriber[subscriberCount];

            Measure.Method(() => SubscribeBatch(messenger, subscribers))
                .SetUp(messenger.Clear)
                .CleanUp(messenger.Clear)
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Messenger/Subscribe/{subscriberCount}")
                .GC()
                .Run();

            messenger.Clear();
            SubscribeBatch(messenger, subscribers);

            for (int i = 0; i < subscribers.Length; i++)
            {
                Assert.IsNotNull(subscribers[i]);
                Assert.AreEqual(typeof(MessengerBenchmarkMessage), subscribers[i].Type);
            }

            messenger.Clear();
        }

        [Test, Performance]
        public void UnsubscribeSingleRepeated([ValueSource(nameof(MutationCounts))] int subscriberCount)
        {
            var messenger = PrepareMessenger();
            var subscribers = new Subscriber[subscriberCount];
            var invocationCount = 0;

            Measure.Method(() => RemoveSingleRepeated(messenger, subscribers))
                .SetUp(() =>
                {
                    messenger.Clear();
                    for (int i = 0; i < subscribers.Length; i++)
                    {
                        subscribers[i] = messenger.Subscribe<MessengerBenchmarkMessage>(
                            _ => invocationCount++);
                    }
                })
                .CleanUp(messenger.Clear)
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Messenger/UnsubscribeSingleRepeated/{subscriberCount}")
                .GC()
                .Run();

            messenger.Clear();
            for (int i = 0; i < subscribers.Length; i++)
            {
                subscribers[i] = messenger.Subscribe<MessengerBenchmarkMessage>(_ => invocationCount++);
            }

            RemoveSingleRepeated(messenger, subscribers);
            invocationCount = 0;
            messenger.Send(new MessengerBenchmarkMessage());
            Assert.AreEqual(0, invocationCount);
        }

        [Test, Performance]
        public void UnsubscribeBatch([ValueSource(nameof(MutationCounts))] int subscriberCount)
        {
            var messenger = PrepareMessenger();
            var subscribers = new Subscriber[subscriberCount];
            var invocationCount = 0;

            Measure.Method(() => messenger.RemoveSubscribers(subscribers))
                .SetUp(() =>
                {
                    messenger.Clear();
                    for (int i = 0; i < subscribers.Length; i++)
                    {
                        subscribers[i] = messenger.Subscribe<MessengerBenchmarkMessage>(
                            _ => invocationCount++);
                    }
                })
                .CleanUp(messenger.Clear)
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Messenger/UnsubscribeBatch/{subscriberCount}")
                .GC()
                .Run();

            messenger.Clear();
            for (int i = 0; i < subscribers.Length; i++)
            {
                subscribers[i] = messenger.Subscribe<MessengerBenchmarkMessage>(_ => invocationCount++);
            }

            messenger.RemoveSubscribers(subscribers);
            invocationCount = 0;
            messenger.Send(new MessengerBenchmarkMessage());
            Assert.AreEqual(0, invocationCount);
        }

        [Test, Performance]
        public void DeferredSelfRemoval([ValueSource(nameof(DeferredRemovalCounts))] int subscriberCount)
        {
            var messenger = PrepareMessenger();
            var subscribers = new Subscriber[subscriberCount];
            var message = new MessengerBenchmarkMessage();
            var invocationCount = 0;
            Action onInvoke = () => invocationCount++;

            Measure.Method(() => messenger.Send(message))
                .SetUp(() =>
                {
                    messenger.Clear();
                    invocationCount = 0;
                    SubscribeSelfRemovingBatch(messenger, subscribers, onInvoke);
                })
                .CleanUp(messenger.Clear)
                .WarmupCount(WarmupCount)
                .MeasurementCount(MeasurementCount)
                .IterationsPerMeasurement(1)
                .SampleGroup($"Messenger/DeferredSelfRemoval/{subscriberCount}")
                .GC()
                .Run();

            messenger.Clear();
            invocationCount = 0;
            SubscribeSelfRemovingBatch(messenger, subscribers, onInvoke);
            messenger.Send(message);
            Assert.AreEqual(subscriberCount, invocationCount);
            messenger.Send(message);
            Assert.AreEqual(subscriberCount, invocationCount);
        }

        private static Messenger PrepareMessenger()
        {
            var messenger = Toolbox.Messenger;
            Assert.IsNotNull(messenger);
            Toolbox.Pooler.DisableGC();
            messenger.Clear();
            return messenger;
        }

        private static int GetSendOperationsPerSample(int subscriberCount)
        {
            if (subscriberCount <= 10)
            {
                return 10_000;
            }

            if (subscriberCount <= 100)
            {
                return 1_000;
            }

            if (subscriberCount <= 1_000)
            {
                return 100;
            }

            return 20;
        }

        private static void SendBatch<T>(
            Messenger messenger,
            T message,
            int operationCount) where T : Message
        {
            for (int i = 0; i < operationCount; i++)
            {
                messenger.Send(message);
            }
        }

        private static void SendCachedBatch(Messenger messenger, int operationCount)
        {
            for (int i = 0; i < operationCount; i++)
            {
                messenger.Send<MessengerCachedBenchmarkMessage>();
            }
        }

        private static void InvokeDelegateBatch(
            Action<MessengerBenchmarkMessage> callbacks,
            MessengerBenchmarkMessage message,
            int operationCount)
        {
            for (int i = 0; i < operationCount; i++)
            {
                callbacks.Invoke(message);
            }
        }

        private static void SubscribeBatch(Messenger messenger, Subscriber[] subscribers)
        {
            for (int i = 0; i < subscribers.Length; i++)
            {
                subscribers[i] = messenger.Subscribe<MessengerBenchmarkMessage>(_ => { });
            }
        }

        private static void RemoveSingleRepeated(Messenger messenger, Subscriber[] subscribers)
        {
            for (int i = 0; i < subscribers.Length; i++)
            {
                messenger.RemoveSubscriber(subscribers[i]);
            }
        }

        private static void SubscribeSelfRemovingBatch(
            Messenger messenger,
            Subscriber[] subscribers,
            Action onInvoke)
        {
            for (int i = 0; i < subscribers.Length; i++)
            {
                Subscriber subscriber = null;
                subscriber = messenger.Subscribe<MessengerBenchmarkMessage>(_ =>
                {
                    onInvoke.Invoke();
                    messenger.RemoveSubscriber(subscriber);
                });
                subscribers[i] = subscriber;
            }
        }
    }
}
