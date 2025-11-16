#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VolumeBox.Toolbox
{
    [InitializeOnLoad]
    public static class EditorLoadUtils
    {
        // private const string DevelopmentSceneAssetPath = "Assets/Scripts/Unity Toolbox/Scenes/MAIN.unity";
        // private const string DevelopmentScenePath = "Assets/Scripts/Unity Toolbox/Scenes/MAIN.unity";
        // private const string ProductionSceneAssetPath = "Assets/Scenes/MAIN.unity";
        // private const string ProductionScenePath = "Scenes/MAIN.unity";
        private const string MainScenePath =
        #if TOOLBOX_DEBUG
            "Assets/Scripts/Toolbox/Scenes/MAIN.unity";
        #else
            "Assets/Scenes/MAIN.unity";
        #endif
        
        private const string PackageScenePath = "Packages/com.volumebox.toolbox/Scenes/MAIN.unity";
        private const string PackageName = "com.volumebox.toolbox";
        private const string MainSceneName = "MAIN";
        
        static EditorLoadUtils()
        {
            ValidateStartScene();
        }

        public static void ValidateStartScene()
        {
            if (StaticData.Settings.AutoResolveScenesAtPlay)
            {
                SceneAsset mainScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
                if (mainScene != null)
                    EditorSceneManager.playModeStartScene = mainScene;
            }
            else
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }

        public static bool IsMainSceneCorrectInBuild()
        {
            var length = EditorBuildSettings.scenes.Length;

            if (length <= 0)
            {
                return false;
            }

            var path = EditorBuildSettings.scenes[0].path;

            if(path != MainScenePath)
            {
                return false;
            }

            return true;
        }

        public static async void InitializeMain()
        {
#if TOOLBOX_DEBUG
#else
            var scenes = EditorBuildSettings.scenes.ToList();
            
            for (int i = 0; i < scenes.Count; i++)
            {
                string sceneName = Path.GetFileNameWithoutExtension(EditorBuildSettings.scenes[i].path);
                
                if (sceneName == MainSceneName)
                {
                    scenes.RemoveAt(i);
                }
            }

            var parentAssetPath = Directory.GetParent(Application.dataPath);
            var fullProductionPath = parentAssetPath + "/" + MainScenePath;
            
            if (!File.Exists(fullProductionPath))
            {
                var fullPackagePath = parentAssetPath + "/" + PackageScenePath;
            
                if (!File.Exists(fullProductionPath))
                {
                    var dir = Directory.GetParent(MainScenePath).FullName;

                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    
                    await ToolboxExtensions.CopyFileAsync(fullPackagePath, fullProductionPath);
                }
            }
                
            scenes.Insert(0, new EditorBuildSettingsScene(MainScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.Refresh();
#endif

            Debug.Log("MAIN scene initialized");
        }

        public static async void OpenMainScene()
        {
            if(!IsMainSceneCorrectInBuild()) return;
            
            int scenesCount = EditorSceneManager.sceneCount;

            for (int i = 0; i < scenesCount; i++)
            {
                string sceneName = EditorSceneManager.GetSceneAt(i).name;

                if (sceneName == MainSceneName)
                {
                    Debug.Log("MAIN scene already opened");
                    return;
                }
            }

            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
        }
    }
}
#endif