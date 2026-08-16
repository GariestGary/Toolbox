#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VolumeBox.Toolbox.Editor
{
    [CustomEditor(typeof(PoolerDataHolder))]
    public class PoolerEditor : UnityEditor.Editor
    {
        private SerializedProperty m_poolsList;
        private SerializedProperty m_poolGCInterval;
        private string searchValue;
        private Vector2 currentScrollPos;

        private static Color buttonColor = new Color(0.8705882352941176f, 0.3450980392156863f, 0.3450980392156863f);

        private void OnEnable()
        {
            if(target == null)
            {
                return;
            }

            m_poolsList = serializedObject.FindProperty("poolsList");
            m_poolGCInterval = serializedObject.FindProperty("m_GarbageCollectorWorkInterval");
        }

        public override VisualElement CreateInspectorGUI()
        {
            var element = new IMGUIContainer(DrawIMGUI);
            return element;
        }

        public void DrawIMGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            DrawPoolerHeader(ref searchValue, m_poolsList, ref currentScrollPos);
            
            EditorGUILayout.Space(3);

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GC Collect Interval", GUILayout.Width(SettingsDataEditor.LABEL_WIDTH));
            var interval = EditorGUILayout.FloatField(m_poolGCInterval.floatValue);
            interval = Mathf.Clamp(interval, 0.5f, float.MaxValue);
            m_poolGCInterval.floatValue = interval;
            GUILayout.EndHorizontal();

            ToolboxEditorGUI.DividerLine();
            
            EditorGUILayout.BeginHorizontal();
            
            ToolboxEditorGUI.DrawSectionLabel("Pools:");
            GUILayout.FlexibleSpace();
            ToolboxEditorGUI.DrawExpandCollapseButtons(m_poolsList);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);
            
            GUILayout.BeginHorizontal();
            GUILayout.Space(3f);
            DrawPools(m_poolsList, searchValue, ref currentScrollPos);
            GUILayout.Space(3f);
            GUILayout.EndHorizontal();
            
            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }
        }

        public static void DrawPools(SerializedProperty list, string searchValue, ref Vector2 currentScrollPos)
        {
            ToolboxEditorGUI.DrawSearchableFoldoutsList(
                list,
                element => string.IsNullOrEmpty(searchValue) ||
                           element.FindPropertyRelative("tag").stringValue.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) >= 0,
                (element, index) => DrawElement(element, list, index),
                ref currentScrollPos);
        }

        private void SetExpandedStateForAll(bool value)
        {
            for (int i = 0; i < m_poolsList.arraySize; i++)
            {
                m_poolsList.GetArrayElementAtIndex(i).isExpanded = value;
            }
        }

        public static void DrawPoolerHeader(ref string searchValue, SerializedProperty poolsList, ref Vector2 currentYScrollPos)
        {
            GUILayout.BeginHorizontal();
            ToolboxEditorGUI.DrawSearchHeader(ref searchValue);
            
            if (GUILayout.Button("Add Pool", GUILayout.MinWidth(0f), GUILayout.MaxWidth(80f), GUILayout.Height(ToolboxEditorGUI.SearchBarHeight)))
            {
                AddPool(poolsList);
                currentYScrollPos.y = float.MaxValue;
                PoolerTagPropertyDrawer.IsPoolsChanged = true;
            }
            
            GUILayout.EndHorizontal();
        }

        public static void DrawElement(SerializedProperty property, SerializedProperty list, int index)
        {
            var tag = property.FindPropertyRelative("tag");
            var expansion = ToolboxEditorGUI.ListItemFoldout(
                property.isExpanded,
                tag.stringValue,
                list,
                index,
                () => DrawItemInline(tag.stringValue, list, index),
                () => DrawItemContent(tag, property));

            if (expansion.HasValue)
            {
                property.isExpanded = expansion.Value;
            }
        }

        private static bool DrawItemInline(string label, SerializedProperty list, int index)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = buttonColor;

            if (GUILayout.Button(ToolboxEditorGUI.Icon("TreeEditor.Trash", "×", "Delete pool"), GUILayout.Width(25), GUILayout.ExpandHeight(true)))
            {
                if (EditorUtility.DisplayDialog("Confirm delete", $"Are you sure want to delete {label} pool?", "Yes", "Cancel"))
                {
                    GUI.backgroundColor = oldColor;
                    list.DeleteArrayElementAtIndex(index);
                    PoolerTagPropertyDrawer.IsPoolsChanged = true;
                    return true;
                }
            }
            
            GUILayout.Space(4);
            GUI.backgroundColor = oldColor;
            return false;
        }

        private static void DrawItemContent(SerializedProperty tag, SerializedProperty property)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.Space(20);
            EditorGUILayout.BeginVertical();
            
            //Tag draw
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Tag");
            var prevTag = tag.stringValue;
            tag.stringValue = EditorGUILayout.TextField(tag.stringValue);

            if(prevTag != tag.stringValue)
            {
                PoolerTagPropertyDrawer.IsPoolsChanged = true;
            }

            EditorGUILayout.EndHorizontal();

            //Prefab draw
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Prefab");
            var pooledObj = property.FindPropertyRelative("pooledObject");
            EditorGUILayout.PropertyField(pooledObj, GUIContent.none);

            EditorGUILayout.EndHorizontal();

            //Pool size draw
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField("Size");
            var initialSize = property.FindPropertyRelative("size");
            var settedValue = EditorGUILayout.IntField(initialSize.intValue);
            settedValue = Mathf.Clamp(settedValue, 1, int.MaxValue);

            initialSize.intValue = settedValue;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            var preview = AssetPreview.GetAssetPreview(pooledObj.objectReferenceValue);
            var size = EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;
            GUILayout.Label(preview, GUILayout.Width(size), GUILayout.Height(size));

            EditorGUILayout.EndHorizontal();
        }

        public static void AddPool(SerializedProperty poolsList)
        {
            var index = poolsList.arraySize;
            poolsList.arraySize++;
            var pool = poolsList.GetArrayElementAtIndex(index);
            pool.isExpanded = false;
            pool.FindPropertyRelative("tag").stringValue = string.Empty;
            pool.FindPropertyRelative("pooledObject").objectReferenceValue = null;
            pool.FindPropertyRelative("size").intValue = 1;
        }
    }
}
#endif
