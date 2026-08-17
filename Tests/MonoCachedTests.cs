using NUnit.Framework;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace VolumeBox.Toolbox.Tests
{
    [PrebuildSetup(typeof(TestPrebuild))]
    internal class MonoCachedTests
    {
        private static readonly MethodInfo ProcessControlMethod = typeof(MonoCached).GetMethod(
            "ProcessControl",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo FixedProcessControlMethod = typeof(MonoCached).GetMethod(
            "FixedProcessControl",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo LateProcessControlMethod = typeof(MonoCached).GetMethod(
            "LateProcessControl",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [UnityTest]
        public IEnumerator MonoCachedActiveStateProcessTest()
        {
            var parent = new GameObject("Parent", typeof(MonoCachedComponent)).GetComponent<MonoCachedComponent>();
            var child = new GameObject("Child", typeof(MonoCachedComponent)).GetComponent<MonoCachedComponent>();

            child.transform.SetParent(parent.transform);

            //check default, not process if inactive, not process if inactive in hierarchy
            parent.DisableGameObject();
            Assert.AreEqual(true, parent.Paused);
            Assert.AreEqual(true, child.Paused);
            parent.EnableGameObject();

            //check if process if inactive
            parent.ProcessIfInactiveSelf = true;
            child.ProcessIfInactiveSelf = true;
            parent.DisableGameObject();
            Assert.AreEqual(true, child.Paused);
            Assert.AreEqual(false, parent.Paused);
            parent.EnableGameObject();
            child.ProcessIfInactiveInHierarchy = true;
            parent.DisableGameObject();
            Assert.AreEqual(false, child.Paused);
            parent.EnableGameObject();
            parent.ProcessIfInactiveSelf = false;
            child.ProcessIfInactiveSelf = false;
            child.ProcessIfInactiveInHierarchy = false;

            //check if process if inactive in hierarchy
            child.ProcessIfInactiveInHierarchy = true;
            parent.DisableGameObject();
            Assert.AreEqual(true, parent.Paused);
            Assert.AreEqual(false, child.Paused);
            parent.ProcessIfInactiveInHierarchy = false;
            child.ProcessIfInactiveInHierarchy = false;

            yield return null;
        }

        [Test]
        public void RenderIntervalPairsTickAndLateTickInTheSameCycle()
        {
            var gameObject = new GameObject("Render interval scheduler test");
            var component = gameObject.AddComponent<MonoCachedComponent>();
            component.Interval = 0.1f;

            Process(component, 0.04f);
            LateProcess(component, 0.04f);

            Assert.AreEqual(0, component.TickCount);
            Assert.AreEqual(0, component.LateTickCount);

            Process(component, 0.07f);

            Assert.AreEqual(1, component.TickCount);
            Assert.AreEqual(0, component.LateTickCount);
            Assert.That(component.LastTickDelta, Is.EqualTo(0.11f).Within(0.0001f));

            LateProcess(component, 0.07f);

            Assert.AreEqual(1, component.LateTickCount);
            Assert.Greater(component.LateTickOrder, component.TickOrder);

            LateProcess(component, 0.07f);
            Process(component, 0.08f);
            LateProcess(component, 0.08f);

            Assert.AreEqual(1, component.TickCount);
            Assert.AreEqual(1, component.LateTickCount);

            Process(component, 0.02f);
            LateProcess(component, 0.02f);

            Assert.AreEqual(2, component.TickCount);
            Assert.AreEqual(2, component.LateTickCount);
            Assert.That(component.LastTickDelta, Is.EqualTo(0.1f).Within(0.0001f));

            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void FixedIntervalIsIndependentFromRenderInterval()
        {
            var gameObject = new GameObject("Independent fixed interval scheduler test");
            var component = gameObject.AddComponent<MonoCachedComponent>();
            component.Interval = 0.5f;
            component.FixedInterval = 0.1f;

            FixedProcess(component, 0.04f);
            FixedProcess(component, 0.07f);

            Assert.AreEqual(1, component.FixedTickCount);
            Assert.That(component.LastFixedTickDelta, Is.EqualTo(0.11f).Within(0.0001f));
            Assert.AreEqual(0, component.TickCount);
            Assert.AreEqual(0, component.LateTickCount);

            Process(component, 0.2f);
            Process(component, 0.2f);
            FixedProcess(component, 0.05f);
            Process(component, 0.1f);

            Assert.AreEqual(1, component.TickCount);
            Assert.AreEqual(0, component.LateTickCount);

            LateProcess(component, 0.1f);

            Assert.AreEqual(1, component.LateTickCount);
            Assert.AreEqual(1, component.FixedTickCount);
            Assert.That(component.LastTickDelta, Is.EqualTo(0.5f).Within(0.0001f));

            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void MultipleFixedUpdatesDoNotReuseDueRenderInterval()
        {
            var gameObject = new GameObject("Multiple fixed updates scheduler test");
            var component = gameObject.AddComponent<MonoCachedComponent>();
            component.Interval = 0.1f;
            component.FixedInterval = 0.2f;

            Process(component, 0.11f);

            FixedProcess(component, 0.051f);
            FixedProcess(component, 0.051f);
            FixedProcess(component, 0.051f);

            Assert.AreEqual(0, component.FixedTickCount);

            FixedProcess(component, 0.051f);
            FixedProcess(component, 0.05f);

            Assert.AreEqual(1, component.FixedTickCount);
            Assert.That(component.LastFixedTickDelta, Is.EqualTo(0.204f).Within(0.0001f));
            Assert.AreEqual(1, component.TickCount);

            LateProcess(component, 0.11f);

            Assert.AreEqual(1, component.LateTickCount);
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ZeroIntervalsProcessEveryControlCall()
        {
            var gameObject = new GameObject("Zero interval scheduler test");
            var component = gameObject.AddComponent<MonoCachedComponent>();
            component.Interval = -1f;
            component.FixedInterval = -1f;

            Assert.AreEqual(0, component.Interval);
            Assert.AreEqual(0, component.FixedInterval);

            Process(component, 0.1f);
            LateProcess(component, 0.1f);
            Process(component, 0.2f);
            LateProcess(component, 0.2f);
            FixedProcess(component, 0.15f);
            FixedProcess(component, 0.3f);

            Assert.AreEqual(2, component.TickCount);
            Assert.AreEqual(2, component.LateTickCount);
            Assert.AreEqual(2, component.FixedTickCount);
            Assert.That(component.LastTickDelta, Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(component.LastFixedTickDelta, Is.EqualTo(0.3f).Within(0.0001f));

            Object.DestroyImmediate(gameObject);
        }

        private static void Process(MonoCached component, float delta)
        {
            ProcessControlMethod.Invoke(component, new object[] { delta });
        }

        private static void FixedProcess(MonoCached component, float delta)
        {
            FixedProcessControlMethod.Invoke(component, new object[] { delta });
        }

        private static void LateProcess(MonoCached component, float delta)
        {
            LateProcessControlMethod.Invoke(component, new object[] { delta });
        }
    }
}
