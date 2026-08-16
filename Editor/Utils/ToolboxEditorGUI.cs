using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VolumeBox.Toolbox.Editor
{
    public static class ToolboxEditorGUI
    {
        private const string ArrayDragDataKey = "VolumeBox.Toolbox.SerializedArrayDrag";
        public const float RoundedBackgroundRadius = 5f;
        public const float SearchBarHeight = 22f;
        public const float SearchControlHeight = 18f;
        public const float SearchIconSize = 16f;
        public const float SearchHorizontalPadding = 4f;
        public const float SearchControlSpacing = 2f;
        public const float TabHeight = 40f;
        private const float FoldoutIconWidth = 14f;
        private const float FoldoutLabelSpacing = 2f;
        private static readonly Color RoundedBackgroundColor = new Color(0f, 0f, 0f, 52f / 255f);
        private static SerializedArrayDragData s_PendingDrag;
        private static Object s_StylesSkin;
        private static GUIStyle s_SearchFieldStyle;
        private static GUIStyle s_SearchIconStyle;
        private static GUIStyle s_SearchClearStyle;
        private static GUIStyle s_SectionLabelStyle;
        private static GUIStyle s_FoldoutIconStyle;
        private static GUIStyle s_FoldoutLabelStyle;
        private static GUIStyle s_RoundedVerticalLayoutStyle;
        private static int s_RoundedVerticalPadding = -1;

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

            switch (currentEvent.type)
            {
                case EventType.MouseDown when currentEvent.button == 0 && rect.Contains(currentEvent.mousePosition):
                    s_PendingDrag = new SerializedArrayDragData(array, index);
                    currentEvent.Use();
                    break;
                case EventType.MouseDrag when s_PendingDrag != null:
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.SetGenericData(ArrayDragDataKey, s_PendingDrag);
                    DragAndDrop.StartDrag("Reorder element");
                    s_PendingDrag = null;
                    currentEvent.Use();
                    break;
                case EventType.MouseUp:
                    s_PendingDrag = null;
                    break;
            }

            var dragData = DragAndDrop.GetGenericData(ArrayDragDataKey) as SerializedArrayDragData;

            if (dragData == null || !dragData.Matches(array) || !rect.Contains(currentEvent.mousePosition))
            {
                return;
            }

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                    currentEvent.Use();
                    break;
                case EventType.DragPerform:
                    DragAndDrop.AcceptDrag();
                    array.MoveArrayElement(dragData.Index, index);
                    DragAndDrop.SetGenericData(ArrayDragDataKey, null);
                    currentEvent.Use();
                    break;
            }
        }

        public static void DrawSearchHeader(ref string searchValue)
        {
            EnsureStyles();

            var searchBarRect = EditorGUILayout.GetControlRect(
                false,
                SearchBarHeight,
                GUILayout.MinWidth(0f),
                GUILayout.ExpandWidth(true));
            DrawRoundedBackground(searchBarRect);

            var iconRect = new Rect(
                searchBarRect.x + SearchHorizontalPadding,
                CenterVertically(searchBarRect, SearchIconSize),
                SearchIconSize,
                SearchIconSize);
            var clearRect = new Rect(
                searchBarRect.xMax - SearchHorizontalPadding - SearchIconSize,
                CenterVertically(searchBarRect, SearchIconSize),
                SearchIconSize,
                SearchIconSize);
            var fieldX = iconRect.xMax + SearchControlSpacing;
            var fieldRect = new Rect(
                fieldX,
                CenterVertically(searchBarRect, SearchControlHeight),
                Mathf.Max(0f, clearRect.xMin - SearchControlSpacing - fieldX),
                SearchControlHeight);

            GUI.Label(
                iconRect,
                EditorGUIUtility.IconContent("Search Icon"),
                s_SearchIconStyle);

            searchValue = EditorGUI.TextField(
                fieldRect,
                searchValue ?? string.Empty,
                s_SearchFieldStyle);

            if (GUI.Button(clearRect, new GUIContent("×", "Clear search"), s_SearchClearStyle))
            {
                searchValue = string.Empty;
                GUI.FocusControl(null);
            }
        }

        private static void EnsureStyles()
        {
            if (ReferenceEquals(s_StylesSkin, GUI.skin) && s_SearchFieldStyle != null)
            {
                return;
            }

            s_StylesSkin = GUI.skin;
            s_SearchFieldStyle = new GUIStyle(EditorStyles.textField)
            {
                name = "Toolbox Search Field",
                alignment = TextAnchor.MiddleLeft,
                border = new RectOffset(),
                margin = new RectOffset(),
                padding = new RectOffset(0, 0, 0, 0),
                fixedHeight = 0f,
                stretchWidth = true,
            };
            ClearStyleBackgrounds(s_SearchFieldStyle);

            s_SearchIconStyle = new GUIStyle(GUIStyle.none)
            {
                name = "Toolbox Search Icon",
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(),
                padding = new RectOffset(),
            };

            var textColor = EditorStyles.label.normal.textColor;
            s_SearchClearStyle = new GUIStyle(GUIStyle.none)
            {
                name = "Toolbox Search Clear",
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                margin = new RectOffset(),
                padding = new RectOffset(0, 0, 0, 2),
            };
            s_SearchClearStyle.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, 0.65f);
            s_SearchClearStyle.hover.textColor = textColor;
            s_SearchClearStyle.active.textColor = new Color(textColor.r, textColor.g, textColor.b, 0.45f);
            s_SearchClearStyle.focused.textColor = textColor;

            s_SectionLabelStyle = new GUIStyle(EditorStyles.label)
            {
                name = "Toolbox Section Label",
                fontSize = 16,
                fontStyle = FontStyle.Bold,
            };
            s_SectionLabelStyle.normal.textColor = new Color(0.7294118f, 0.7294118f, 0.7294118f, 1f);

            s_FoldoutLabelStyle = new GUIStyle(GUIStyle.none)
            {
                name = "Toolbox Foldout Label",
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                margin = new RectOffset(),
                padding = new RectOffset(),
            };
            var foldoutTextColor = new Color(0.8301887f, 0.8301887f, 0.8301887f, 1f);
            var foldoutInteractionColor = new Color(0.6431373f, 0.6431373f, 0.6431373f, 1f);
            s_FoldoutIconStyle = new GUIStyle(GUIStyle.none)
            {
                name = "Toolbox Foldout Icon",
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter,
                margin = new RectOffset(),
                padding = new RectOffset(0, 0, 0, 0),
            };
            s_FoldoutIconStyle.normal.textColor = foldoutTextColor;
            s_FoldoutIconStyle.hover.textColor = Color.white;
            s_FoldoutIconStyle.active.textColor = foldoutInteractionColor;
            s_FoldoutIconStyle.focused.textColor = foldoutTextColor;

            s_FoldoutLabelStyle.normal.textColor = foldoutTextColor;
            s_FoldoutLabelStyle.hover.textColor = foldoutInteractionColor;
            s_FoldoutLabelStyle.active.textColor = foldoutInteractionColor;
            s_FoldoutLabelStyle.focused.textColor = foldoutTextColor;

        }

        private static void ClearStyleBackgrounds(GUIStyle style)
        {
            style.normal.background = null;
            style.hover.background = null;
            style.active.background = null;
            style.focused.background = null;
            style.onNormal.background = null;
            style.onHover.background = null;
            style.onActive.background = null;
            style.onFocused.background = null;
        }

        private static float CenterVertically(Rect parent, float height)
        {
            return parent.y + (parent.height - height) * 0.5f;
        }

        public static void DrawSectionLabel(string label)
        {
            EnsureStyles();
            EditorGUILayout.LabelField(label, s_SectionLabelStyle);
        }

        public static GUIStyle SectionLabelStyle
        {
            get
            {
                EnsureStyles();
                return s_SectionLabelStyle;
            }
        }

        public static int DrawTabs(int selectedTab, GUIContent[] tabs)
        {
            if (tabs == null || tabs.Length == 0)
            {
                return selectedTab;
            }

            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < tabs.Length; i++)
            {
                var isSelected = GUILayout.Toggle(
                    i == selectedTab,
                    tabs[i],
                    GUI.skin.button,
                    GUILayout.Height(TabHeight),
                    GUILayout.ExpandWidth(true));

                if (isSelected)
                {
                    selectedTab = i;
                }
            }

            EditorGUILayout.EndHorizontal();
            return selectedTab;
        }

        public static Rect BeginRoundedVertical(params GUILayoutOption[] options)
        {
            var rect = EditorGUILayout.BeginVertical(GUIStyle.none, options);
            DrawRoundedBackground(rect);
            return rect;
        }

        public static Rect BeginRoundedVertical(int padding, params GUILayoutOption[] options)
        {
            if (s_RoundedVerticalLayoutStyle == null || s_RoundedVerticalPadding != padding)
            {
                s_RoundedVerticalPadding = padding;
                s_RoundedVerticalLayoutStyle = new GUIStyle(GUIStyle.none)
                {
                    padding = new RectOffset(padding, padding, padding, padding),
                };
            }

            var rect = EditorGUILayout.BeginVertical(s_RoundedVerticalLayoutStyle, options);
            DrawRoundedBackground(rect);
            return rect;
        }

        public static void EndRoundedVertical()
        {
            EditorGUILayout.EndVertical();
        }

        public static Rect BeginRoundedHorizontal(params GUILayoutOption[] options)
        {
            var rect = EditorGUILayout.BeginHorizontal(GUIStyle.none, options);
            DrawRoundedBackground(rect);
            return rect;
        }

        public static void EndRoundedHorizontal()
        {
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawRoundedBackground(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            GUI.DrawTexture(
                rect,
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                true,
                0f,
                RoundedBackgroundColor,
                0f,
                RoundedBackgroundRadius);
        }

        public static void DrawResponsiveLabel(string label, float maxWidth, GUIStyle style = null)
        {
            EditorGUILayout.LabelField(
                label,
                style ?? EditorStyles.label,
                GUILayout.MinWidth(0f),
                GUILayout.MaxWidth(maxWidth),
                GUILayout.ExpandWidth(true));
        }

        public static void DividerLine(float spacing = 4f)
        {
            EditorGUILayout.Space(spacing);

            var rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin
                ? new Color(0.1f, 0.1f, 0.1f, 1f)
                : new Color(0.6f, 0.6f, 0.6f, 1f));

            EditorGUILayout.Space(spacing);
        }

        public static Vector2 BeginVerticalScrollView(Vector2 scrollPosition)
        {
            scrollPosition.x = 0f;
            scrollPosition = EditorGUILayout.BeginScrollView(
                scrollPosition,
                GUIStyle.none,
                GUI.skin.verticalScrollbar);
            scrollPosition.x = 0f;
            return scrollPosition;
        }

        public static void DrawExpandCollapseButtons(SerializedProperty list)
        {
            if(list.arraySize > 0)
            {
                if (GUILayout.Button("▼", GUILayout.Width(20)))
                {
                    for (int j = 0; j < list.arraySize; j++)
                    {
                        var clip = list.GetArrayElementAtIndex(j);

                        clip.isExpanded = true;
                    }
                }

                if (GUILayout.Button("▲", GUILayout.Width(20)))
                {
                    for (int j = 0; j < list.arraySize; j++)
                    {
                        var clip = list.GetArrayElementAtIndex(j);

                        clip.isExpanded = false;
                    }
                }
            }
        }

        public static bool? ListItemFoldout(bool isExpanded, string label, SerializedProperty relatedList, int index, Func<bool> inline, Action content)
        {
            EnsureStyles();
            BeginRoundedVertical();
            GUILayout.Space(2);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(25));
            GUILayout.Space(4);
            ArrayDragHandle(relatedList, index);
            GUILayout.Space(4);
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            var foldoutRect = GUILayoutUtility.GetRect(
                GUIContent.none,
                GUIStyle.none,
                GUILayout.ExpandWidth(true),
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
            var currentExpansion = isExpanded;
            var iconRect = new Rect(
                foldoutRect.x,
                foldoutRect.y,
                FoldoutIconWidth,
                foldoutRect.height);
            var labelRect = new Rect(
                iconRect.xMax + FoldoutLabelSpacing,
                foldoutRect.y,
                Mathf.Max(0f, foldoutRect.xMax - iconRect.xMax - FoldoutLabelSpacing),
                foldoutRect.height);

            if (GUI.Button(
                iconRect,
                currentExpansion ? "▼" : "▶",
                s_FoldoutIconStyle))
            {
                currentExpansion = !currentExpansion;
            }

            if (GUI.Button(labelRect, label, s_FoldoutLabelStyle))
            {
                currentExpansion = !currentExpansion;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            var wasDeleted = inline();
            EditorGUILayout.EndHorizontal();

            if (wasDeleted)
            {
                EndRoundedVertical();
                return null;
            }

            if (currentExpansion)
            {
                content();
                GUILayout.Space(7);
            }
            else
            {
                GUILayout.Space(2);
            }
            EndRoundedVertical();

            return currentExpansion;
        }

        public static void DrawSearchableFoldoutsList(SerializedProperty list, Predicate<SerializedProperty> searchQuery, Action<SerializedProperty, int> entry, ref Vector2 scrollPos)
        {
            EditorGUILayout.BeginVertical();
            scrollPos = BeginVerticalScrollView(scrollPos);

            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);

                if (searchQuery == null || searchQuery(element))
                {
                    EditorGUILayout.BeginHorizontal();
                    entry(element, i);
                    EditorGUILayout.EndHorizontal();

                    GUILayout.Space(3);
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
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
