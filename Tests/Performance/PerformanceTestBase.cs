using NUnit.Framework;
using UnityEngine;

namespace VolumeBox.Toolbox.Tests.Performance
{
    internal abstract class PerformanceTestBase : ToolboxTestBase
    {
        private GameObject _benchmarkRoot;
        private int _previousVSyncCount;
        private int _previousTargetFrameRate;
        private bool _previousMessageCaching;

        [SetUp]
        public void SetUpPerformanceEnvironment()
        {
            _previousVSyncCount = QualitySettings.vSyncCount;
            _previousTargetFrameRate = Application.targetFrameRate;
            _previousMessageCaching = StaticData.Settings.UseMessageCaching;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            _benchmarkRoot = new GameObject("Toolbox performance objects")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        [TearDown]
        public void TearDownPerformanceEnvironment()
        {
            if (Toolbox.HasInstance)
            {
                Toolbox.Pooler?.DisableGC();
                Toolbox.Messenger?.Clear();
            }

            if (_benchmarkRoot != null)
            {
                Object.DestroyImmediate(_benchmarkRoot);
            }

            _benchmarkRoot = null;
            StaticData.Settings.UseMessageCaching = _previousMessageCaching;
            Application.targetFrameRate = _previousTargetFrameRate;
            QualitySettings.vSyncCount = _previousVSyncCount;
        }

        protected GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            gameObject.transform.SetParent(_benchmarkRoot.transform, false);
            return gameObject;
        }
    }
}
