using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class Messenger: MonoBehaviour, IClear
	{
		private List<Subscriber> subscribers = new();
		private Dictionary<Type, Message> _MessagesCache = new();
		private Pooler _Pool;
		
#if TOOLBOX_DEBUG

		public Dictionary<Type, Message> MessagesCache => _MessagesCache;		

#endif

		public void Initialize(Pooler pool)
		{
			_Pool = pool;
            Subscribe<SceneUnloadedMessage>(m => CheckSceneSubscribers(m.SceneName), null, true);
			Subscribe<GameObjectRemovedMessage>(m => CheckRemovedObject(m), null, true);
        }

		private void CheckSceneSubscribers(string scene)
		{
			subscribers.RemoveAll(x => x.HasBind && x.BindedObject == null);

			var sceneSubs = subscribers.Where(x => x.BindedObject != null && x.BindedObject.scene.name == scene).ToList();

			foreach (var sub in sceneSubs)
			{
				RemoveSubscriber(sub);
			}
		}

		private void CheckRemovedObject(GameObjectRemovedMessage msg)
		{
			if(msg.RemoveType != GameObjectRemoveType.Destroyed)
			{
				return;
			}

			var bindedSub = subscribers.FirstOrDefault(x => x.HasBind && x.BindedObject == msg.Obj);

			if(bindedSub is not null)
			{
				RemoveSubscriber(bindedSub);
			}
		}

        public void ClearSubscribers()
        {
			subscribers.RemoveAll(s => !s.Keep);
        }

        public void RemoveSubscriber(Subscriber subscriber)
        {
	        if(subscriber == null) return;

	        if (subscribers == null || subscribers.Count <= 0)
	        {
		        return;
	        }

	        if(subscribers.Contains(subscriber))
			{
				subscribers.Remove(subscriber);
			}
        }

        public void RemoveSubscribers(IEnumerable<Subscriber> subscribers)
        {
	        foreach (var subscriber in subscribers)
	        {
		        RemoveSubscriber(subscriber);
	        }
        }

		public Subscriber Subscribe<T>(Action<T> next, GameObject bind = null, bool keep = false) where T: Message
		{
			var sub = new Subscriber(typeof(T), Callback, bind, keep);
            subscribers.Add(sub);
            return sub;
            void Callback(object args) => next((T)args);
		}

		public Subscriber Subscribe<T>(Action next, GameObject bind = null, bool keep = false) where T : Message
		{
			var sub = new Subscriber(typeof(T), Callback, bind, keep);
			subscribers.Add(sub);
			return sub;
			void Callback(object args) => next();
		}

		public Subscriber Subscribe(Type messageType, Action<Message> next, GameObject bind = null, bool keep = false)
		{
			var sub = new Subscriber(messageType, Callback, bind, keep);
			subscribers.Add(sub);
			return sub;
			void Callback(Message args) => next(args);
		}
		
		public Subscriber Subscribe(Type messageType, Action next, GameObject bind = null, bool keep = false)
		{
			var sub = new Subscriber(messageType, Callback, bind, keep);
			subscribers.Add(sub);
			return sub;
			void Callback(Message args) => next();
		}

#if TOOLBOX_DEBUG
		public bool Send<T>() where T : Message
#else
		public void Send<T>() where T: Message
#endif
		{
			T msg = null;

#if TOOLBOX_DEBUG
			var usedCache = false;
#endif

			if(StaticData.Settings.UseMessageCaching && _MessagesCache.TryGetValue(typeof(T), out var cachedMessage))
			{
				msg = cachedMessage as T;
#if TOOLBOX_DEBUG
				usedCache = true;
#endif
			}
			else
			{
				var message = (T)Activator.CreateInstance(typeof(T));

				if(StaticData.Settings.UseMessageCaching)
				{
					_MessagesCache.Add(typeof(T), message);
				}
			}

			Send(msg);

#if TOOLBOX_DEBUG
			return usedCache;
#endif
		}

		public void Send<T>(T message) where T: Message
		{
			message ??= (T)Activator.CreateInstance(typeof(T));

			var receivers = subscribers.Where(x => x.Type == message.GetType()).ToList();

			for (int i = 0; i < receivers.Count(); i++)
			{
				var receiver = receivers[i];
				
				try
				{
					if(receiver.HasBind && (receiver.BindedObject == null))
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

				if(receiver.HasBind)
				{
					var receiverState = _Pool.IsObjectPooledAndUsed(receiver.BindedObject);
					
					if(!receiverState.IsPooled || (receiverState.IsPooled && receiverState.IsUsed))
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

		public int ClearMessageCache()
		{
			var clearedCount = _MessagesCache.Count;
			_MessagesCache.Clear();
			return clearedCount;
		}

		public void Clear()
		{
			subscribers.Clear();
			_MessagesCache.Clear();
		}
	}
}
