using UnityEngine;
using VolumeBox.Toolbox;

namespace VolumeBox.Toolbox.Tests
{
    internal class PooledGenericObj : MonoCached, IPooled<TestData>
    {
        public string compare = "null";
        public int SpawnCount { get; private set; }
        public TestData ReceivedData { get; private set; }

        public void OnSpawn(TestData data)
        {
            SpawnCount++;
            ReceivedData = data;
            compare = data?.TestString;
        }
    }

    internal class TestData
    {
        public string TestString;
    }

    internal class OtherTestData : TestData
    {
    }

    internal class WrongTestData
    {
    }

    internal struct TestStruct
    {
        public int Value;
    }

    internal class PooledStructObj : MonoCached, IPooled<TestStruct>
    {
        public int SpawnCount { get; private set; }
        public TestStruct ReceivedData { get; private set; }

        public void OnSpawn(TestStruct data)
        {
            SpawnCount++;
            ReceivedData = data;
        }
    }

    internal class PooledNullableStructObj : MonoCached, IPooled<TestStruct?>
    {
        public int SpawnCount { get; private set; }
        public TestStruct? ReceivedData { get; private set; }

        public void OnSpawn(TestStruct? data)
        {
            SpawnCount++;
            ReceivedData = data;
        }
    }

    internal class MultiplePooledInterfacesObj : MonoCached, IPooled<TestData>, IPooled<OtherTestData>
    {
        public int TestDataSpawnCount { get; private set; }
        public int OtherDataSpawnCount { get; private set; }
        public TestData ReceivedTestData { get; private set; }
        public OtherTestData ReceivedOtherData { get; private set; }

        void IPooled<TestData>.OnSpawn(TestData data)
        {
            TestDataSpawnCount++;
            ReceivedTestData = data;
        }

        void IPooled<OtherTestData>.OnSpawn(OtherTestData data)
        {
            OtherDataSpawnCount++;
            ReceivedOtherData = data;
        }
    }
}
