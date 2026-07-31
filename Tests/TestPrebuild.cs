using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace VolumeBox.Toolbox.Tests
{
    internal class TestPrebuild: IPrebuildSetup, IPostBuildCleanup
    {
#if UNITY_EDITOR
        private const string PreviousStartSceneKey = "VolumeBox.Toolbox.Tests.PreviousStartScene";
#endif

        public void Cleanup()
        {
#if UNITY_EDITOR
            var previousScenePath = SessionState.GetString(PreviousStartSceneKey, string.Empty);
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previousScenePath)
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScenePath);
            SessionState.EraseString(PreviousStartSceneKey);
#endif
        }

        public void Setup()
        {
#if UNITY_EDITOR
            var previousScenePath = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene);
            SessionState.SetString(PreviousStartSceneKey, previousScenePath);
            EditorSceneManager.playModeStartScene = null;
#endif
        }
    }

    internal abstract class ToolboxTestBase
    {
        private GameObject _Container;
        private GameObject _PoolRoot;
        private bool _PreviousAutoplayResolve;

        [UnitySetUp]
        public IEnumerator SetUpToolbox()
        {
            _PreviousAutoplayResolve = StaticData.Settings.AutoResolveScenesAtPlay;
            StaticData.Settings.AutoResolveScenesAtPlay = false;

            var prefab = Resources.Load<GameObject>("Toolbox Container");
            Assert.IsNotNull(prefab, "Toolbox Container prefab was not found in Resources");

            _Container = Object.Instantiate(prefab);
            var entry = _Container.GetComponent<ToolboxEntry>();
            Assert.IsNotNull(entry, "Toolbox Container does not contain ToolboxEntry");

            // Prevent Start from initializing the same services a second time.
            entry.enabled = false;
            entry.InitializeComponents();

            var poolRootField = typeof(Pooler).GetField("objectPoolParent", BindingFlags.Instance | BindingFlags.NonPublic);
            var poolRoot = poolRootField?.GetValue(Toolbox.Pooler) as Transform;
            _PoolRoot = poolRoot != null ? poolRoot.gameObject : null;

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDownToolbox()
        {
            if (Toolbox.HasInstance)
            {
                var pooler = Toolbox.Pooler;
                if (pooler != null)
                    pooler.DisableGC();
            }

            if (_Container != null)
                Object.Destroy(_Container);

            if (_PoolRoot != null)
                Object.Destroy(_PoolRoot);

            StaticData.Settings.AutoResolveScenesAtPlay = _PreviousAutoplayResolve;
            yield return null;
        }
    }
}
