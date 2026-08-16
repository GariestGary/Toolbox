#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VolumeBox.Toolbox.Editor
{
    [CustomEditor(typeof(ScenePool))]
    public class ScenePoolEditor : UnityEditor.Editor
    {
        private SerializedProperty m_ScenePoolName;
        private SerializedProperty m_poolsList;
        private SerializedProperty m_RunType;
        private string searchValue;
        private Vector2 currentScrollPos;

        private Dictionary<int, string> m_RunTypes = new Dictionary<int, string>()
        {
            {0, "On Rise"},
            {1, "On Ready"},
            {2, "Manual"}
        };

        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            m_ScenePoolName = serializedObject.FindProperty("m_ScenePoolName");
            m_poolsList = serializedObject.FindProperty("m_Pools");
            m_RunType = serializedObject.FindProperty("m_RunType");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(m_ScenePoolName, new GUIContent("Pool Name"));
            EditorGUILayout.Space(3);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Initialization type");
            if(EditorGUILayout.DropdownButton(new GUIContent(m_RunTypes[m_RunType.intValue]), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("On Rise"), m_RunType.intValue == 0, OnRunTypeSelected, 0);
                menu.AddItem(new GUIContent("On Ready"), m_RunType.intValue == 1, OnRunTypeSelected, 1);
                menu.AddItem(new GUIContent("Manual"), m_RunType.intValue == 2, OnRunTypeSelected, 2);
                menu.ShowAsContext();
            }
            EditorGUILayout.EndHorizontal();
            ToolboxEditorGUI.DividerLine();

            PoolerEditor.DrawPoolerHeader(ref searchValue, m_poolsList, ref currentScrollPos);

            EditorGUILayout.Space(5);

            PoolerEditor.DrawPools(m_poolsList, searchValue, ref currentScrollPos);
            // EditorGUI.indentLevel++;
            // EditorGUILayout.BeginVertical();
            // currentScrollPos = EditorGUILayout.BeginScrollView(currentScrollPos);
            //
            //
            // for (int i = 0; i < m_poolsList.arraySize; i++)
            // {
            //     var pool = m_poolsList.GetArrayElementAtIndex(i);
            //
            //     if (searchValue.IsValuable())
            //     {
            //         if (pool.FindPropertyRelative("tag").stringValue.ToLower().Contains(searchValue.ToLower()))
            //         {
            //             PoolerEditor.DrawElement(pool, m_poolsList, i);
            //         }
            //     }
            //     else
            //     {
            //         PoolerEditor.DrawElement(pool, m_poolsList, i);
            //     }
            //
            //     GUILayout.Space(3);
            // }
            //
            // EditorGUILayout.EndScrollView();
            // EditorGUILayout.EndVertical();
            // EditorGUI.indentLevel--;

            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
                PoolerTagPropertyDrawer.IsPoolsChanged = true;
            }
        }

        private void OnRunTypeSelected(object userdata)
        {
            EditorGUI.BeginChangeCheck();
            m_RunType.intValue = (int)userdata;
            serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}
#endif
