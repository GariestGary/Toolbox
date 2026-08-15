using UnityEditor;
using UnityEngine;

namespace VolumeBox.Toolbox.Editor
{
    public static class ToolboxEditorGUI
    {
        private const string ArrayDragDataKey = "VolumeBox.Toolbox.SerializedArrayDrag";
        private static SerializedArrayDragData s_PendingDrag;

        public static GUIContent Icon(string iconName, string fallback, string tooltip)
        {
            var icon = EditorGUIUtility.IconContent(iconName);

            return icon?.image != null
                ? new GUIContent(icon.image, tooltip)
                : new GUIContent(fallback, tooltip);
        }

        public static void ArrayDragHandle(SerializedProperty array, int index)
        {
            var rect = GUILayoutUtility.GetRect(18f, 25f, GUILayout.Width(18f), GUILayout.Height(25f));
            GUI.Label(rect, new GUIContent("≡", "Drag to reorder"), EditorStyles.centeredGreyMiniLabel);
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.Pan);

            var currentEvent = Event.current;

            if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && rect.Contains(currentEvent.mousePosition))
            {
                s_PendingDrag = new SerializedArrayDragData(array, index);
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseDrag && s_PendingDrag != null)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(ArrayDragDataKey, s_PendingDrag);
                DragAndDrop.StartDrag("Reorder element");
                s_PendingDrag = null;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.MouseUp)
            {
                s_PendingDrag = null;
            }

            var dragData = DragAndDrop.GetGenericData(ArrayDragDataKey) as SerializedArrayDragData;

            if (dragData == null || !dragData.Matches(array) || !rect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            if (currentEvent.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                currentEvent.Use();
            }
            else if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                array.MoveArrayElement(dragData.Index, index);
                DragAndDrop.SetGenericData(ArrayDragDataKey, null);
                currentEvent.Use();
            }
        }

        private sealed class SerializedArrayDragData
        {
            private readonly Object m_Target;
            private readonly string m_PropertyPath;

            public int Index { get; }

            public SerializedArrayDragData(SerializedProperty array, int index)
            {
                m_Target = array.serializedObject.targetObject;
                m_PropertyPath = array.propertyPath;
                Index = index;
            }

            public bool Matches(SerializedProperty array)
            {
                return array.serializedObject.targetObject == m_Target && array.propertyPath == m_PropertyPath;
            }
        }
    }
}
