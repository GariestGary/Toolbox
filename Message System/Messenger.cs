using System;
using System.Collections.Generic;
using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class Messenger : MonoBehaviour, IClear
    {
        private readonly Dictionary<Type, List<Subscriber>> _subscribersByType = new();
        private readonly List<Subscriber> _pendingAdditions = new();
        private readonly List<Subscriber> _pendingRemovals = new();
        private readonly Dictionary<Type, Message> _MessagesCache = new();
        private Pooler _Pool;
        private int _dispatchDepth;

#if TOOLBOX_DEBUG

        public Dictionary<Type, Message> MessagesCache => _MessagesCache;

#endif

        public void Initialize(Pooler pool)
        {
            _Pool = pool;
            Subscribe<SceneUnloadedMessage>(message => CheckSceneSubscribers(message.SceneName), null, true);
            Subscribe<GameObjectRemovedMessage>(CheckRemovedObject, null, true);
        }

        private void CheckSceneSubscribers(string scene)
        {
            foreach (var pair in _subscribersByType)
            {
                var subscribers = pair.Value;

                for (var i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];

                    if (!subscriber.HasBind)
                    {
                        continue;
                    }

                    var bindedObject = subscriber.BindedObject;

                    if (bindedObject == null || bindedObject.scene.name == scene)
                    {
                        RemoveSubscriber(subscriber);
                    }
                }
            }
        }

        private void CheckRemovedObject(GameObjectRemovedMessage message)
        {
            if (message.RemoveType != GameObjectRemoveType.Destroyed)
            {
                return;
            }

            foreach (var pair in _subscribersByType)
            {
                var subscribers = pair.Value;

                for (var i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];

                    if (subscriber.HasBind && subscriber.BindedObject == message.Obj)
                    {
                        RemoveSubscriber(subscriber);
                        return;
                    }
                }
            }
        }

        public void ClearSubscribers()
        {
            foreach (var pair in _subscribersByType)
            {
                var subscribers = pair.Value;

                for (var i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];

                    if (!subscriber.Keep && !_pendingRemovals.Contains(subscriber))
                    {
                        _pendingRemovals.Add(subscriber);
                    }
                }
            }

            for (var i = _pendingAdditions.Count - 1; i >= 0; i--)
            {
                if (!_pendingAdditions[i].Keep)
                {
                    _pendingAdditions.RemoveAt(i);
                }
            }

            ApplyPendingMutationsIfPossible();
        }

        public void RemoveSubscriber(Subscriber subscriber)
        {
            if (subscriber == null || subscriber.Type == null)
            {
                return;
            }

            if (_dispatchDepth == 0)
            {
                RemoveSubscriberImmediate(subscriber);
                return;
            }

            var pendingAdditionIndex = _pendingAdditions.IndexOf(subscriber);

            if (pendingAdditionIndex >= 0)
            {
                _pendingAdditions.RemoveAt(pendingAdditionIndex);
                return;
            }

            if (!_subscribersByType.TryGetValue(subscriber.Type, out var subscribers) ||
                !subscribers.Contains(subscriber) ||
                _pendingRemovals.Contains(subscriber))
            {
                return;
            }

            _pendingRemovals.Add(subscriber);
        }

        public void RemoveSubscribers(IEnumerable<Subscriber> subscribers)
        {
            foreach (var subscriber in subscribers)
            {
                RemoveSubscriber(subscriber);
            }
        }

        public Subscriber Subscribe<T>(Action<T> next, GameObject bind = null, bool keep = false) where T : Message
        {
            var subscriber = new Subscriber(typeof(T), Callback, bind, keep);
            AddSubscriber(subscriber);
            return subscriber;

            void Callback(object args) => next((T)args);
        }

        public Subscriber Subscribe<T>(Action next, GameObject bind = null, bool keep = false) where T : Message
        {
            var subscriber = new Subscriber(typeof(T), Callback, bind, keep);
            AddSubscriber(subscriber);
            return subscriber;

            void Callback(object args) => next();
        }

        public Subscriber Subscribe(Type messageType, Action<Message> next, GameObject bind = null, bool keep = false)
        {
            var subscriber = new Subscriber(messageType, Callback, bind, keep);
            AddSubscriber(subscriber);
            return subscriber;

            void Callback(Message args) => next(args);
        }

        public Subscriber Subscribe(Type messageType, Action next, GameObject bind = null, bool keep = false)
        {
            var subscriber = new Subscriber(messageType, Callback, bind, keep);
            AddSubscriber(subscriber);
            return subscriber;

            void Callback(Message args) => next();
        }

#if TOOLBOX_DEBUG
        public bool Send<T>() where T : Message
#else
        public void Send<T>() where T : Message
#endif
        {
            T message;

#if TOOLBOX_DEBUG
            var usedCache = false;
#endif

            if (StaticData.Settings.UseMessageCaching &&
                _MessagesCache.TryGetValue(typeof(T), out var cachedMessage))
            {
                message = cachedMessage as T;
#if TOOLBOX_DEBUG
                usedCache = true;
#endif
            }
            else
            {
                message = (T)Activator.CreateInstance(typeof(T));

                if (StaticData.Settings.UseMessageCaching)
                {
                    _MessagesCache.Add(typeof(T), message);
                }
            }

            Send(message);

#if TOOLBOX_DEBUG
            return usedCache;
#endif
        }

        public void Send<T>(T message) where T : Message
        {
            message ??= (T)Activator.CreateInstance(typeof(T));

            if (!_subscribersByType.TryGetValue(message.GetType(), out var receivers) || receivers.Count == 0)
            {
                return;
            }

            _dispatchDepth++;

            try
            {
                var receiverCount = receivers.Count;

                for (var i = 0; i < receiverCount; i++)
                {
                    var receiver = receivers[i];

                    try
                    {
                        if (receiver.HasBind && receiver.BindedObject == null)
                        {
                            RemoveSubscriber(receiver);
                            continue;
                        }
                    }
                    catch
                    {
                        RemoveSubscriber(receiver);
                        continue;
                    }

                    if (receiver.HasBind)
                    {
                        var receiverState = _Pool.IsObjectPooledAndUsed(receiver.BindedObject);

                        if (!receiverState.IsPooled || receiverState.IsUsed)
                        {
                            receiver.Callback.Invoke(message);
                        }
                    }
                    else
                    {
                        receiver.Callback.Invoke(message);
                    }
                }
            }
            finally
            {
                _dispatchDepth--;
                ApplyPendingMutationsIfPossible();
            }
        }

        public int ClearMessageCache()
        {
            var clearedCount = _MessagesCache.Count;
            _MessagesCache.Clear();
            return clearedCount;
        }

        public void Clear()
        {
            _MessagesCache.Clear();

            if (_dispatchDepth == 0)
            {
                _subscribersByType.Clear();
                _pendingAdditions.Clear();
                _pendingRemovals.Clear();
                return;
            }

            foreach (var pair in _subscribersByType)
            {
                var subscribers = pair.Value;

                for (var i = 0; i < subscribers.Count; i++)
                {
                    var subscriber = subscribers[i];

                    if (!_pendingRemovals.Contains(subscriber))
                    {
                        _pendingRemovals.Add(subscriber);
                    }
                }
            }

            _pendingAdditions.Clear();
        }

        private void AddSubscriber(Subscriber subscriber)
        {
            if (_dispatchDepth > 0)
            {
                _pendingAdditions.Add(subscriber);
            }
            else
            {
                AddSubscriberImmediate(subscriber);
            }
        }

        private void AddSubscriberImmediate(Subscriber subscriber)
        {
            if (!_subscribersByType.TryGetValue(subscriber.Type, out var subscribers))
            {
                subscribers = new List<Subscriber>();
                _subscribersByType.Add(subscriber.Type, subscribers);
            }

            subscribers.Add(subscriber);
        }

        private void RemoveSubscriberImmediate(Subscriber subscriber)
        {
            if (!_subscribersByType.TryGetValue(subscriber.Type, out var subscribers) ||
                !subscribers.Remove(subscriber))
            {
                return;
            }

            if (subscribers.Count == 0)
            {
                _subscribersByType.Remove(subscriber.Type);
            }
        }

        private void ApplyPendingMutationsIfPossible()
        {
            if (_dispatchDepth > 0)
            {
                return;
            }

            for (var i = 0; i < _pendingRemovals.Count; i++)
            {
                RemoveSubscriberImmediate(_pendingRemovals[i]);
            }

            _pendingRemovals.Clear();

            for (var i = 0; i < _pendingAdditions.Count; i++)
            {
                AddSubscriberImmediate(_pendingAdditions[i]);
            }

            _pendingAdditions.Clear();
        }
    }
}
