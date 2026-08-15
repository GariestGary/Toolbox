using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VolumeBox.Toolbox.Editor
{
    [CustomPropertyDrawer(typeof(FromPoolAttribute))]
    public class PoolerTagPropertyDrawer : PropertyDrawer
    {
        private PoolAdvancedDropdown m_Dropdown;
        private PoolerDataHolder m_DataHolder;
        private string[] m_PoolerEntries;
        private List<PoolDropdownGroup> m_ScenePoolGroups;
        private bool m_ManualEnabled = false;

        private static List<PoolDropdownGroup> s_ScenePoolGroups;

        public static bool IsPoolsChanged { get; set; }

        static PoolerTagPropertyDrawer()
        {
            EditorBuildSettings.sceneListChanged += InvalidateScenePoolCache;
            EditorApplication.hierarchyChanged += InvalidateScenePoolCache;
            EditorApplication.projectChanged += InvalidateScenePoolCache;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginProperty(position, GUIContent.none, property);
            ValidateEntries(property);

            var labelRect = position;
            labelRect.width = EditorGUIUtility.labelWidth + 2;

            EditorGUI.LabelField(labelRect, label);

            var poolRect = position;
            poolRect.x += labelRect.width;
            poolRect.width -= labelRect.width + 20;
            bool hasPools = m_PoolerEntries.Length > 0 || m_ScenePoolGroups.Count > 0;
            
            if(!hasPools && !m_ManualEnabled && !property.stringValue.IsValuable())
            {
                EditorGUI.LabelField(poolRect, "There is no pools available", EditorStyles.popup);
            }
            else
            {
                UpdateDropdown(property);

                if(m_ManualEnabled)
                {
                    property.stringValue = EditorGUI.TextField(poolRect, property.stringValue);
                }
                else
                {
                    if(GUI.Button(poolRect, property.stringValue, EditorStyles.popup))
                    {
                        m_Dropdown.Show(poolRect);
                    }
                }
            }

            poolRect.x += poolRect.width;
            poolRect.width = 20;

            m_ManualEnabled = GUI.Toggle(
                poolRect,
                m_ManualEnabled,
                ToolboxEditorGUI.Icon("editicon.sml", "✎", "Toggle manual pool ID editing"),
                "Button"
            );

            EditorGUI.EndProperty();
            EditorGUI.EndChangeCheck();
        }

        private void ValidateEntries(SerializedProperty property)
        {
            if (m_DataHolder == null)
            {
                m_DataHolder = ResourcesUtils.ResolveScriptable<PoolerDataHolder>(SettingsData.poolerResourcesDataPath);
            }

            m_PoolerEntries = GetPoolerEntries(m_DataHolder);

            if (s_ScenePoolGroups == null || IsPoolsChanged)
            {
                s_ScenePoolGroups = GetScenePoolGroups();
                IsPoolsChanged = false;
            }

            m_ScenePoolGroups = s_ScenePoolGroups;
        }

        private void UpdateDropdown(SerializedProperty property)
        {
            m_Dropdown = new PoolAdvancedDropdown(
                    new UnityEditor.IMGUI.Controls.AdvancedDropdownState(),
                    m_PoolerEntries,
                    m_ScenePoolGroups,
                    name => OnPoolSelectedCallback(name, property));
        }

        private void OnPoolSelectedCallback(string poolName, SerializedProperty property)
        {
            property.serializedObject.Update();
            property.stringValue = poolName;
            property.serializedObject.ApplyModifiedProperties();
        }

        private string[] GetPoolerEntries(PoolerDataHolder dataHolder)
        {
            var entries = new string[0];

            if (dataHolder != null && dataHolder.PoolsList.Count > 0)
            {
                entries = new string[dataHolder.PoolsList.Count];

                for (int i = 0; i < entries.Length; i++)
                {
                    entries[i] = dataHolder.PoolsList[i].tag;
                }
            }

            return entries;
        }

        private static void InvalidateScenePoolCache()
        {
            s_ScenePoolGroups = null;
        }

        private List<PoolDropdownGroup> GetScenePoolGroups()
        {
            var groups = new List<PoolDropdownGroup>();

            foreach (var buildScene in BuildSettingsSceneUtils.GetScenes())
            {
                var scene = SceneManager.GetSceneByPath(buildScene.Path);
                var isPreviewScene = !scene.IsValid() || !scene.isLoaded;

                if (isPreviewScene)
                {
                    scene = EditorSceneManager.OpenPreviewScene(buildScene.Path);
                }

                try
                {
                    var scenePoolGroups = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<ScenePool>(true))
                        .Where(scenePool => scenePool.Pools != null)
                        .Select(scenePool => new PoolDropdownGroup(
                            scenePool.Name.IsValuable() ? scenePool.Name : scenePool.gameObject.name,
                            scenePool.Pools
                                .Where(pool => pool != null && pool.tag.IsValuable())
                                .Select(pool => pool.tag)
                                .Distinct()
                                .ToArray()
                        ))
                        .Where(group => group.Entries.Length > 0)
                        .ToList();

                    if (scenePoolGroups.Count > 0)
                    {
                        groups.Add(new PoolDropdownGroup(
                            buildScene.DisplayName,
                            children: scenePoolGroups
                        ));
                    }
                }
                finally
                {
                    if (isPreviewScene && scene.IsValid())
                    {
                        EditorSceneManager.ClosePreviewScene(scene);
                    }
                }
            }

            return groups;
        }
    }
}
