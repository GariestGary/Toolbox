using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VolumeBox.Toolbox;

namespace VolumeBox.Toolbox.Tests
{
    internal class Foo : MonoCached
    {
        public float Delta => delta;
        public int TickCount { get; private set; }
        public int RiseCount { get; private set; }
        public int ReadyCount { get; private set; }
        public string LifecycleId { get; set; }
        public List<string> LifecycleEvents { get; set; }
        public System.Action RiseAction { get; set; }
        public float counter = 0;

        protected override void Rise()
        {
            RiseCount++;
            LifecycleEvents?.Add($"{LifecycleId}.Rise");
            RiseAction?.Invoke();
        }

        protected override void Ready()
        {
            ReadyCount++;
            LifecycleEvents?.Add($"{LifecycleId}.Ready");
        }

        protected override void Tick()
        {
            TickCount++;
            counter += delta;
        }
    }
}
