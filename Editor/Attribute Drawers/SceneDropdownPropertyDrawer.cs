using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VolumeBox.Toolbox.Editor
{
    [CustomPropertyDrawer(typeof(SceneDropdownAttribute))]
    public class SceneDropdownPropertyDrawer : PropertyDrawer
    {
        private SerializedProperty m_Property;
        private SceneAdvancedDropdown m_Dropdown;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var labelRect = position;
            labelRect.width = EditorGUIUtility.labelWidth;
            EditorGUI.LabelField(labelRect, label);
            var fieldRect = position;
            fieldRect.x += labelRect.width + 2;
            fieldRect.width -= labelRect.width + 2;

            if(property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(fieldRect, "Field is not a string type");
                return;
            }

            m_Property = property;

            EditorGUI.BeginProperty(position, GUIContent.none, property);

            m_Property.serializedObject.Update();

            var scenes = BuildSettingsSceneUtils.GetScenes();

            if (scenes.Count == 0)
            {
                if (GUI.Button(fieldRect, new GUIContent("No scenes in Build Settings", "Open Build Settings"), EditorStyles.miniButton))
                {
                    if (!EditorApplication.ExecuteMenuItem("File/Build Settings..."))
                    {
                        EditorApplication.ExecuteMenuItem("File/Build Profiles");
                    }
                }

                EditorGUI.EndProperty();
                return;
            }

            var isValid = !property.stringValue.IsValuable() || scenes.Any(scene =>
                scene.Value == property.stringValue || scene.Name == property.stringValue || scene.Path == property.stringValue);
            var oldColor = GUI.backgroundColor;

            if (!isValid)
            {
                GUI.backgroundColor = new Color(1f, 0.65f, 0.35f);
                fieldRect.width -= 24f;
            }

            var caption = property.stringValue.IsValuable() ? property.stringValue : "Select Scene";

            if(GUI.Button(fieldRect, new GUIContent(caption, isValid ? "Select a scene" : "Scene is missing from Build Settings"), EditorStyles.popup))
            {
                m_Dropdown = new SceneAdvancedDropdown(new AdvancedDropdownState(), OnSceneSelectedCallback);
                m_Dropdown.Show(fieldRect);
            }

            GUI.backgroundColor = oldColor;

            if (!isValid)
            {
                var buildSettingsRect = fieldRect;
                buildSettingsRect.x += fieldRect.width + 2f;
                buildSettingsRect.width = 22f;

                if (GUI.Button(
                        buildSettingsRect,
                        ToolboxEditorGUI.Icon("SceneAsset Icon", "!", "Scene is missing. Open Build Settings"),
                        EditorStyles.miniButton))
                {
                    if (!EditorApplication.ExecuteMenuItem("File/Build Settings..."))
                    {
                        EditorApplication.ExecuteMenuItem("File/Build Profiles");
                    }
                }
            }

            EditorGUI.EndProperty();
        }

        private void OnSceneSelectedCallback(string sceneName)
        {
            m_Property.serializedObject.Update();
            m_Property.stringValue = sceneName;
            m_Property.serializedObject.ApplyModifiedProperties();
        }
    }
}
