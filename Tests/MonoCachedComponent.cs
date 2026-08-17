namespace VolumeBox.Toolbox.Tests
{
    internal class MonoCachedComponent : MonoCached
    {
        public int TickCount { get; private set; }
        public int FixedTickCount { get; private set; }
        public int LateTickCount { get; private set; }
        public int TickOrder { get; private set; }
        public int LateTickOrder { get; private set; }
        public float LastTickDelta { get; private set; }
        public float LastFixedTickDelta { get; private set; }

        private int _processOrder;

        protected override void Tick()
        {
            TickCount++;
            TickOrder = ++_processOrder;
            LastTickDelta = delta;
        }

        protected override void FixedTick()
        {
            FixedTickCount++;
            LastFixedTickDelta = fixedDelta;
        }

        protected override void LateTick()
        {
            LateTickCount++;
            LateTickOrder = ++_processOrder;
        }
    }
}
