using System.Collections.Generic;
using UnityEngine;
using VolumeBox.Toolbox;

namespace VolumeBox.Toolbox.Tests
{
    internal class PooledDefaultObj: MonoCached, IPooled
    {
        public static readonly List<string> InvocationOrder = new();

        public string compare;
        public string HandlerId;
        public int SpawnCount { get; private set; }
        
        public void OnSpawn()
        {
            SpawnCount++;
            compare = "data";

            if (!string.IsNullOrEmpty(HandlerId))
            {
                InvocationOrder.Add(HandlerId);
            }
        }
    }

    internal class DespawnTestObj : MonoBehaviour, IDespawn
    {
        public static readonly List<string> InvocationOrder = new();

        public string HandlerId;
        public int DespawnCount { get; private set; }

        public void OnDespawn()
        {
            DespawnCount++;

            if (!string.IsNullOrEmpty(HandlerId))
            {
                InvocationOrder.Add(HandlerId);
            }
        }
    }

    internal class PooledLifecycleTestObj : MonoCached
    {
        public int RiseCount { get; private set; }
        public int ReadyCount { get; private set; }

        protected override void Rise()
        {
            RiseCount++;
        }

        protected override void Ready()
        {
            ReadyCount++;
        }
    }

    internal class ReentrantPooledObj : MonoCached, IPooled
    {
        public static System.Action SpawnAction;

        public void OnSpawn()
        {
            SpawnAction?.Invoke();
        }
    }
}
