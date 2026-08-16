#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace VolumeBox.Toolbox.Editor
{
    [CustomEditor(typeof(SettingsData))]
    public class SettingsDataEditor: UnityEditor.Editor
    {
        public const float LABEL_WIDTH = 150;
        private const float SECTION_MARGIN = 3f;
        private const int SECTION_PADDING = 10;

        private SerializedProperty m_resolveAtPlay;
        private SerializedProperty m_timeScale;
        private SerializedProperty m_targetFrameRate;
        private SerializedProperty m_initialSceneName;
        private SerializedProperty m_initialSceneArgs;

        private int selectedScene;
        private string[] scenesList;

        private void OnEnable()
        {
            m_resolveAtPlay = serializedObject.FindProperty("AutoResolveScenesAtPlay");
            m_timeScale = serializedObject.FindProperty("TimeScale");
            m_targetFrameRate = serializedObject.FindProperty("TargetFrameRate");
            m_initialSceneName = serializedObject.FindProperty("InitialSceneName");
            m_initialSceneArgs = serializedObject.FindProperty("InitialSceneArgs");
            RebuildScenesList();
        }

        private void RebuildScenesList()
        {
            scenesList = EditorBuildSettings.scenes.ToList().ConvertAll(x =>
            {
                int pos = x.path.LastIndexOf("/") + 1;
                return x.path.Substring(pos, x.path.Length - pos).Replace(".unity", "");
            }).ToArray();

            selectedScene = scenesList.ToList().IndexOf(m_initialSceneName.stringValue);

            if (selectedScene < 0)
            {
                selectedScene = 0;
            }
        }

        private void OnSceneSelectedCallback(string sceneName)
        {
            serializedObject.Update();
            m_initialSceneName.stringValue = sceneName;
            serializedObject.ApplyModifiedProperties();
        }

        public void DrawIMGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            //SCENE MANAGEMENT
            GUILayout.Space(SECTION_MARGIN);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(SECTION_MARGIN);
            //HEADER
            ToolboxEditorGUI.BeginRoundedVertical(SECTION_PADDING);
            EditorGUILayout.LabelField("Scene Management", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Resolve Scenes On Play", GUILayout.Width(LABEL_WIDTH));
            var prevResolveScenesValue = m_resolveAtPlay.boolValue;
            m_resolveAtPlay.boolValue = EditorGUILayout.Toggle(m_resolveAtPlay.boolValue, GUILayout.Width(EditorGUIUtility.singleLineHeight));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(3);
            ToolboxEditorGUI.EndRoundedVertical();
            GUILayout.Space(SECTION_MARGIN);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(SECTION_MARGIN);


            //TIMINGS
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(SECTION_MARGIN);
            //HEADER
            ToolboxEditorGUI.BeginRoundedVertical(SECTION_PADDING);
            EditorGUILayout.LabelField("Timings", EditorStyles.largeLabel);
            EditorGUILayout.Space(5);

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Time Scale", GUILayout.Width(LABEL_WIDTH));
            m_timeScale.floatValue = EditorGUILayout.Slider(m_timeScale.floatValue, 0, 5);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Frame Rate", GUILayout.Width(LABEL_WIDTH));
            var oldFramerate = m_targetFrameRate.intValue;
            var newFramerate = Mathf.Max(1, EditorGUILayout.IntField(GUIContent.none, m_targetFrameRate.intValue));
            m_targetFrameRate.intValue = newFramerate;

            if(oldFramerate != newFramerate)
            {
                ToolboxEntry.UpdateTargetFramerate(newFramerate);
            }

            GUILayout.EndHorizontal();

            var optionDataList = new List<string>();

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; ++i)
            {
                string name = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                optionDataList.Add(name);
            }

            GUILayout.Space(3);
            ToolboxEditorGUI.EndRoundedVertical();
            GUILayout.Space(SECTION_MARGIN);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(SECTION_MARGIN);

            //INITIAL SCENE
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(SECTION_MARGIN);
            //HEADER
            ToolboxEditorGUI.BeginRoundedVertical(SECTION_PADDING);
            EditorGUILayout.LabelField("Initial Scene", EditorStyles.largeLabel);
            EditorGUILayout.Space(5);

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Initial Scene Name", GUILayout.Width(EditorGUIUtility.labelWidth));

            var rect = GUILayoutUtility.GetRect(new GUIContent(m_initialSceneName.stringValue), EditorStyles.iconButton, GUILayout.ExpandWidth(true), GUILayout.Height(EditorGUIUtility.singleLineHeight + 5));
            var sceneClicked = GUI.Button(rect, m_initialSceneName.stringValue, EditorStyles.popup);
            
            if(sceneClicked)
            {
                var dropdown = new SceneAdvancedDropdown(new AdvancedDropdownState(), OnSceneSelectedCallback);
                dropdown.Show(rect);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Initial Scene Args", GUILayout.Width(LABEL_WIDTH));
            EditorGUILayout.PropertyField(m_initialSceneArgs, GUIContent.none);
            GUILayout.EndHorizontal();

            GUILayout.Space(3);
            ToolboxEditorGUI.EndRoundedVertical();
            GUILayout.Space(SECTION_MARGIN);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(SECTION_MARGIN);


            //MESSENGER
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(SECTION_MARGIN);
            //HEADER
            ToolboxEditorGUI.BeginRoundedVertical(SECTION_PADDING);
            EditorGUILayout.LabelField("Messenger", EditorStyles.largeLabel);
            EditorGUILayout.Space(5);

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Message Caching", GUILayout.Width(EditorGUIUtility.labelWidth));
            StaticData.Settings.UseMessageCaching = EditorGUILayout.Toggle(StaticData.Settings.UseMessageCaching, GUILayout.Width(EditorGUIUtility.labelWidth));
            GUILayout.EndHorizontal();


            //VALIDATING DATA
            serializedObject.ApplyModifiedProperties();
            
            if(EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }
            
            if (m_resolveAtPlay.boolValue != prevResolveScenesValue)
            {
                EditorLoadUtils.ValidateStartScene();
            }

            GUILayout.Space(3);
            ToolboxEditorGUI.EndRoundedVertical();
            GUILayout.Space(SECTION_MARGIN);
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
