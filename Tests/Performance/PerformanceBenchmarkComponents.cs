using UnityEngine;

namespace VolumeBox.Toolbox.Tests.Performance
{
    internal sealed class MessengerBenchmarkMessage : Message
    {
        public int Value = 1;
    }

    internal sealed class MessengerCachedBenchmarkMessage : Message
    {
        public int Value = 1;
    }

    internal sealed class MessengerIrrelevantMessageA : Message
    {
    }

    internal sealed class MessengerIrrelevantMessageB : Message
    {
    }

    internal sealed class MessengerIrrelevantMessageC : Message
    {
    }

    internal sealed class PoolerBenchmarkReferenceData
    {
        public int Value;
    }

    internal sealed class PoolerBenchmarkLifecycle : MonoCached, IPooled, IDespawn
    {
        public static int SpawnCount;
        public static int DespawnCount;

        public void OnSpawn()
        {
            SpawnCount++;
        }

        public void OnDespawn()
        {
            DespawnCount++;
        }

        public static void ResetCounters()
        {
            SpawnCount = 0;
            DespawnCount = 0;
        }
    }

    internal sealed class PoolerBenchmarkIntLifecycle : MonoCached, IPooled<int>, IDespawn
    {
        public static int SpawnCount;
        public static int DespawnCount;
        public static int Sink;

        public void OnSpawn(int data)
        {
            SpawnCount++;
            Sink += data;
        }

        public void OnDespawn()
        {
            DespawnCount++;
        }

        public static void ResetCounters()
        {
            SpawnCount = 0;
            DespawnCount = 0;
            Sink = 0;
        }
    }

    internal sealed class PoolerBenchmarkReferenceLifecycle :
        MonoCached,
        IPooled<PoolerBenchmarkReferenceData>,
        IDespawn
    {
        public static int SpawnCount;
        public static int DespawnCount;
        public static int Sink;

        public void OnSpawn(PoolerBenchmarkReferenceData data)
        {
            SpawnCount++;
            Sink += data.Value;
        }

        public void OnDespawn()
        {
            DespawnCount++;
        }

        public static void ResetCounters()
        {
            SpawnCount = 0;
            DespawnCount = 0;
            Sink = 0;
        }
    }
}
