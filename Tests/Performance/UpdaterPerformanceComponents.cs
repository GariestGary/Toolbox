using UnityEngine;

namespace VolumeBox.Toolbox.Tests.Performance
{
    public sealed class RegularEmptyUpdateBenchmarkBehaviour : MonoBehaviour
    {
        public static long InvocationCount;

        private void Update()
        {
            InvocationCount++;
        }
    }

    public sealed class RegularTinyUpdateBenchmarkBehaviour : MonoBehaviour
    {
        public static long InvocationCount;
        public static float Sink;

        private void Update()
        {
            InvocationCount++;
            Sink += Time.deltaTime;
        }
    }

    public sealed class MonoCachedEmptyUpdateBenchmarkBehaviour : MonoCached
    {
        public static long InvocationCount;

        protected override void Tick()
        {
            InvocationCount++;
        }
    }

    public sealed class MonoCachedTinyUpdateBenchmarkBehaviour : MonoCached
    {
        public static long InvocationCount;
        public static float Sink;

        protected override void Tick()
        {
            InvocationCount++;
            Sink += delta;
        }
    }
}
