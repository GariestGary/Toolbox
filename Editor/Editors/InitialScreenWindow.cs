#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace VolumeBox.Toolbox.Editor
{
    public class InitialScreenWindow : EditorWindow
    {
        private Texture2D m_Logo;

        private const int WindowWidth = 480;
        private const int WindowHeight = 440;

        private static Texture2D m_HeaderTex;

        [InitializeOnLoadMethod]
        private static void InitLoad()
        {
            EditorApplication.update += InitialShow;
        }

        private static void InitialShow()
        {
            InitialShowAsync();
        }

        private static void InitialShowAsync()
        {
            if (!Application.isPlaying && !StaticData.HasSettings)
            {
                OpenWindow();
                EditorApplication.update -= InitialShow;
            }
        }

        [MenuItem("Toolbox/Setup Screen", priority = 1)]
        public static void OpenWindow()
        {
            GetWindow<InitialScreenWindow>(true, "Toolbox Setup");
        }

        private void OnEnable()
        {
            this.position = new Rect((Screen.width / 2.0f) - WindowWidth / 2, (Screen.height / 2.0f) - WindowHeight / 2, WindowWidth, WindowHeight);
            this.minSize = new Vector2(WindowWidth, WindowHeight);
            this.maxSize = new Vector2(WindowWidth, WindowHeight);
        }

        private void OnGUI()
        {
            if(m_HeaderTex == null)
            {
                m_HeaderTex = ResourcesUtils.GetOrLoadAsset(m_Logo, "toolbox_banner.png");;
            }

            GUILayout.Space(10);
            Rect headerRect = GUILayoutUtility.GetRect(480, 140);
            GUI.DrawTexture(headerRect, m_HeaderTex, ScaleMode.ScaleToFit);
            GUILayout.Space(10);

            var buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 20;
            
            if (GUILayout.Button(new GUIContent("Open Documentation", EditorGUIUtility.IconContent("d_TextAsset Icon").image), buttonStyle, GUILayout.Height(45)))
            {
                ToolboxSettingsEditorWindow.OpenDocumentation();
            }

            if (!EditorLoadUtils.IsMainSceneCorrectInBuild())
            {
                EditorGUILayout.HelpBox("MAIN scene is not in build setting or it's index not 0. You can fix this by pressing button below. It may take a while", MessageType.Error);

                if (GUILayout.Button(new GUIContent("Initialize MAIN Scene", EditorGUIUtility.IconContent("d_SceneAsset Icon").image), buttonStyle, GUILayout.Height(45)))
                {
                    EditorLoadUtils.InitializeMain();
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent("Open MAIN Scene", EditorGUIUtility.IconContent("d_SceneAsset Icon").image), buttonStyle, GUILayout.Height(45)))
                {
                    EditorLoadUtils.OpenMainScene();
                }
            }

            if(GUILayout.Button(new GUIContent("Open Toolbox Settings", EditorGUIUtility.IconContent("CustomTool@2x").image), buttonStyle, GUILayout.Height(45)))
            {
                GetWindow<ToolboxSettingsEditorWindow>(false, "Toolbox Settings");
            }

            string buttonCaption = "Recreate Settings Data";
            string iconPath = "Refresh@2x";

            if(!StaticData.HasSettings)
            {
                buttonCaption = "Create Settings Data";
                iconPath = "Collab.FileAdded";
                EditorGUILayout.HelpBox("Toolbox requires settings data files to be created in Resources folder. Click button below to create them", MessageType.Info);
            }

            if(GUILayout.Button(new GUIContent(buttonCaption, EditorGUIUtility.IconContent(iconPath).image), buttonStyle, GUILayout.Height(45)))
            {
                ToolboxSettingsEditorWindow.CreateAssets();
            }
        }
    }
}
#endif
