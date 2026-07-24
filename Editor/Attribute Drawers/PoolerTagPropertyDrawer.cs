using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

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

        public static bool IsPoolsChanged { get; set; }

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

            m_ManualEnabled = GUI.Toggle(poolRect, m_ManualEnabled, EditorGUIUtility.IconContent("d_editicon.sml"), "Button");

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

            if(property.GetValue() is Component)
            {
                var gameObject = (property.GetValue() as Component).gameObject;
                m_ScenePoolGroups = GetScenePoolGroups(gameObject);
            }
            else
            {
                m_ScenePoolGroups = new List<PoolDropdownGroup>();
            }
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

        private List<PoolDropdownGroup> GetScenePoolGroups(GameObject sceneObject)
        {
            var scenePools = Resources.FindObjectsOfTypeAll<ScenePool>()
                .Where(x => x.gameObject.scene == sceneObject.scene)
                .OrderBy(x => x.Name)
                .ToArray();

            var groups = new List<PoolDropdownGroup>();

            for (int i = 0; i < scenePools.Length; i++)
            {
                var entries = scenePools[i].Pools
                    .Select(pool => pool.tag)
                    .Where(tag => tag.IsValuable())
                    .ToArray();

                if (entries.Length > 0)
                {
                    var groupName = scenePools[i].Name.IsValuable()
                        ? scenePools[i].Name
                        : scenePools[i].gameObject.name;
                    groups.Add(new PoolDropdownGroup(groupName, entries));
                }
            }

            return groups;
        }
    }
}
