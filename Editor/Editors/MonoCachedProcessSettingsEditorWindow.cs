using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace VolumeBox.Toolbox.Editor
{
    public class MonoCachedProcessSettingsEditorWindow : EditorWindow
    {
        private static MonoCachedProcessSettingsEditorWindow instance;
        private MonoCached m_Target;
        
        public void DrawTarget(MonoCached mono)
        {
            m_Target = mono;
            rootVisualElement.Clear();

            var inHierarchy = new Toggle("Process If Inactive In Hierarchy");
            var self = new Toggle("Process If Inactive Self");
            var ignoreTimeScale = new Toggle("Ignore Time Scale");
            var serializedObject = new SerializedObject(m_Target);
            var selfProperty = serializedObject.FindProperty("processIfInactiveSelf");
            var inHierarchyProperty = serializedObject.FindProperty("processIfInactiveInHierarchy");
            var ignoreTimeScaleProperty = serializedObject.FindProperty("ignoreTimeScale");
            ignoreTimeScale.BindProperty(ignoreTimeScaleProperty);
            inHierarchy.BindProperty(inHierarchyProperty);
            self.BindProperty(selfProperty);
            self.style.flexShrink = new StyleFloat(1);
            rootVisualElement.Add(self);
            rootVisualElement.Add(inHierarchy);
            rootVisualElement.Add(ignoreTimeScale);
        }

        public static void Open(MonoCached mono)
        {
            if(instance != null)
            {
                instance.Close();
            }

            var window = GetWindow<MonoCachedProcessSettingsEditorWindow>(mono.GetType().ToString() + " Settings");
            window.maxSize = new UnityEngine.Vector2(300, 100);
            instance = window;
            window.minSize = new UnityEngine.Vector2(300, 100);
            window.DrawTarget(mono);
            window.Show();
        }

        private void OnDisable()
        {
            if (instance == this)
            {
                instance = null;
            }
        }
    }

    public static class MonoCachedHeaderGUI
    {
        [InitializeOnLoadMethod]
        private static void Register()
        {
            UnityEditor.Editor.finishedDefaultHeaderGUI -= DrawHeaderButton;
            UnityEditor.Editor.finishedDefaultHeaderGUI += DrawHeaderButton;
        }

        private static void DrawHeaderButton(UnityEditor.Editor editor)
        {
            if (editor.target is not MonoCached mono)
            {
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Space(16f);

            var icon = ToolboxEditorGUI.Icon("Settings", "⚙", "Open MonoCached process settings");
            var content = icon.image != null
                ? new GUIContent(" MonoCached Settings", icon.image, icon.tooltip)
                : new GUIContent("⚙ MonoCached Settings", icon.tooltip);

            if (GUILayout.Button(
                    content,
                    EditorStyles.miniButton,
                    GUILayout.Width(150),
                    GUILayout.Height(20)))
            {
                MonoCachedProcessSettingsEditorWindow.Open(mono);
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}
