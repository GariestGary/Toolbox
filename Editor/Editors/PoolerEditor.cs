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
        private GUISkin m_Skin;

        private SerializedProperty m_poolsList;
        private SerializedProperty m_poolGCInterval;
        private string searchValue;
        private Vector2 currentScrollPos;
        private float labelsWidth = 110;

        private static Color buttonColor = new Color(0.8705882352941176f, 0.3450980392156863f, 0.3450980392156863f);

        private void OnEnable()
        {
            if(target == null)
            {
                return;
            }

            m_poolsList = serializedObject.FindProperty("poolsList");
            m_poolGCInterval = serializedObject.FindProperty("m_GarbageCollectorWorkInterval");
            m_Skin = ResourcesUtils.GetOrLoadAsset(m_Skin, "toolbox_styles.guiskin");
        }

        public override VisualElement CreateInspectorGUI()
        {
            var element = new IMGUIContainer(() => 
            {
                DrawIMGUI();
            });

            return element;
        }

        public void DrawIMGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            DrawSearchHeader(ref searchValue, m_poolsList, ref currentScrollPos.y);
            EditorGUILayout.Space(3);

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("GC Collect Interval", GUILayout.Width(SettingsDataEditor.LABEL_WIDTH));
            var interval = EditorGUILayout.FloatField(m_poolGCInterval.floatValue);
            interval = Mathf.Clamp(interval, 0.5f, float.MaxValue);
            m_poolGCInterval.floatValue = interval;
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(1);
            EditorGUILayout.BeginHorizontal();

            
            if(m_poolsList.arraySize > 0)
            {
                EditorGUILayout.LabelField("Pools:", m_Skin.GetStyle("Label"));
                
                if(GUILayout.Button("Expand All"))
                {
                    SetExpandedStateForAll(true);
                }

                if(GUILayout.Button("Collapse All"))
                {
                    SetExpandedStateForAll(false);
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginVertical();
            currentScrollPos = EditorGUILayout.BeginScrollView(currentScrollPos);

            for (int i = 0; i < m_poolsList.arraySize; i++)
            {
                var pool = m_poolsList.GetArrayElementAtIndex(i);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(4);
                if (searchValue.IsValuable())
                {
                    if (pool.FindPropertyRelative("tag").stringValue.ToLower().Contains(searchValue.ToLower()))
                    {
                        DrawElement(pool, m_poolsList, i, labelsWidth, m_Skin);
                    }
                }
                else
                {
                    DrawElement(pool, m_poolsList, i, labelsWidth, m_Skin);
                }
                GUILayout.Space(4);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(3);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }
        }

        private void SetExpandedStateForAll(bool value)
        {
            for (int i = 0; i < m_poolsList.arraySize; i++)
            {
                m_poolsList.GetArrayElementAtIndex(i).isExpanded = value;
            }
        }

        public static void DrawSearchHeader(ref string searchValue, SerializedProperty poolsList, ref float currentScrollPosY)
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
            searchValue = EditorGUILayout.TextField(searchValue, GUI.skin.FindStyle("ToolbarSearchTextField"));
            if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton")))
            {
                searchValue = "";
                GUI.FocusControl(null);
            }

            if (GUILayout.Button("Add Pool", GUILayout.Width(80), GUILayout.ExpandHeight(true)))
            {
                AddPool(poolsList);
                currentScrollPosY = float.MaxValue;
                PoolerTagPropertyDrawer.IsPoolsChanged = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        public static void DrawElement(SerializedProperty property, SerializedProperty list, int index, float labelsWidth, GUISkin skin = null)
        {
            var oldSkin = GUI.skin;
            if(skin != null)
            {
                GUI.skin = skin;
            }

            EditorGUILayout.BeginVertical(GUI.skin.FindStyle("Box"));
            GUILayout.Space(2);
            GUI.skin = oldSkin;
            EditorGUILayout.BeginHorizontal(GUILayout.Height(25));
            GUILayout.Space(4);
            ToolboxEditorGUI.ArrayDragHandle(list, index);
            GUILayout.Space(4);
            var tag = property.FindPropertyRelative("tag");

            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, tag.stringValue, true, skin.GetStyle("Foldout"));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = buttonColor;

            if (GUILayout.Button(ToolboxEditorGUI.Icon("TreeEditor.Trash", "×", "Delete pool"), GUILayout.Width(25), GUILayout.ExpandHeight(true)))
            {
                if (EditorUtility.DisplayDialog("Confirm delete", $"Are you sure want to delete {tag.stringValue} pool?", "Yes", "Cancel"))
                {
                    GUI.backgroundColor = oldColor;
                    list.DeleteArrayElementAtIndex(index);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    PoolerTagPropertyDrawer.IsPoolsChanged = true;
                    return;
                }
            }

            
            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();


            GUI.backgroundColor = oldColor;
            
            if(property.isExpanded)
            {
                EditorGUILayout.Space(8);

                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.Space(20);

                EditorGUILayout.BeginVertical();


                //Tag draw
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("Tag", GUILayout.Width(labelsWidth));
                var prevTag = tag.stringValue;
                tag.stringValue = EditorGUILayout.TextField(tag.stringValue);

                if(prevTag != tag.stringValue)
                {
                    PoolerTagPropertyDrawer.IsPoolsChanged = true;
                }

                EditorGUILayout.EndHorizontal();

                //Prefab draw
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("Prefab", GUILayout.Width(labelsWidth));
                var pooledObj = property.FindPropertyRelative("pooledObject");
                EditorGUILayout.PropertyField(pooledObj, GUIContent.none);

                EditorGUILayout.EndHorizontal();

                //Pool size draw
                EditorGUILayout.BeginHorizontal();

                EditorGUILayout.LabelField("Size", GUILayout.Width(labelsWidth));
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

                GUILayout.Space(7);
            }
            else
            {
                GUILayout.Space(2);
            }

            EditorGUILayout.EndVertical();
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
