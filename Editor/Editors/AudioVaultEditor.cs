#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VolumeBox.Toolbox.Editor
{
    [CustomEditor(typeof(AudioVault))]
    public class AudioVaultEditor : UnityEditor.Editor
    {
        private readonly Dictionary<int, string> m_RunTypes = new()
        {
            { 0, "On Rise" },
            { 1, "On Ready" },
            { 2, "Manual" }
        };

        private GUISkin m_Skin;
        private SerializedProperty m_AudioVaultName;
        private SerializedProperty m_Albums;
        private SerializedProperty m_RunType;
        private string m_SearchValue;
        private Vector2 m_ScrollPosition;

        private void OnEnable()
        {
            m_AudioVaultName = serializedObject.FindProperty("m_AudioVaultName");
            m_Albums = serializedObject.FindProperty("m_Albums");
            m_RunType = serializedObject.FindProperty("m_RunType");
            m_Skin = ResourcesUtils.GetOrLoadAsset(m_Skin, "toolbox_styles.guiskin");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(m_AudioVaultName, new GUIContent("Audio Vault Name"));
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Initialization Type");

            if (EditorGUILayout.DropdownButton(new GUIContent(m_RunTypes[m_RunType.intValue]), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("On Rise"), m_RunType.intValue == 0, () => SetRunType(0));
                menu.AddItem(new GUIContent("On Ready"), m_RunType.intValue == 1, () => SetRunType(1));
                menu.AddItem(new GUIContent("Manual"), m_RunType.intValue == 2, () => SetRunType(2));
                menu.ShowAsContext();
            }

            EditorGUILayout.EndHorizontal();
            AudioPlayerEditor.DrawAlbums(
                m_Albums,
                ref m_SearchValue,
                ref m_ScrollPosition,
                m_Skin,
                true
            );

            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
                AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
            }
        }

        private void SetRunType(int value)
        {
            serializedObject.Update();
            m_RunType.intValue = value;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private void OnDisable()
        {
            AudioUtils.StopAllPreviewClips();
        }
    }
}
#endif
