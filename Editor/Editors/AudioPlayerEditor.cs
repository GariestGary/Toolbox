#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_2023_2_OR_NEWER
using UnityEngine.Audio;
#endif

namespace VolumeBox.Toolbox.Editor
{
    [CustomEditor(typeof(AudioPlayerDataHolder))]
    public class AudioPlayerEditor: UnityEditor.Editor
    {
        private SerializedProperty m_albums;
        private Vector2 currentScrollPosition;
        private string albumSearchValue;

        public static float LabelSize = 110;
        public static Color RedButtonColor = new Color(0.8705882352941176f, 0.3450980392156863f, 0.3450980392156863f);

        private void OnEnable()
        {
            m_albums = serializedObject.FindProperty("albums");
        }

        public void DrawIMGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawAlbums(m_albums, ref albumSearchValue, ref currentScrollPosition);

            serializedObject.ApplyModifiedProperties();

            if(EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }
        }

        public static void DrawPlayerHeader(ref string searchValue, SerializedProperty albumsList, ref Vector2 currentYScrollPos)
        {
            GUILayout.BeginHorizontal();
            ToolboxEditorGUI.DrawSearchHeader(ref searchValue);
            if (GUILayout.Button("Add Album", GUILayout.MinWidth(0f), GUILayout.MaxWidth(80f), GUILayout.Height(ToolboxEditorGUI.SearchBarHeight)))
            {
                AddAlbum(albumsList);
                currentYScrollPos.y = float.MaxValue;
                AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
            }

            if(GUILayout.Button(ToolboxEditorGUI.Icon("PreMatQuad", "■", "Stop all audio previews"), GUILayout.Width(24), GUILayout.Height(ToolboxEditorGUI.SearchBarHeight)))
            {
                AudioUtils.StopAllPreviewClips();
            }
            GUILayout.EndHorizontal();
        }

        public static void DrawAlbums(
            SerializedProperty albums,
            ref string searchValue,
            ref Vector2 scrollPosition)
        {
            DrawPlayerHeader(ref searchValue, albums, ref scrollPosition);
            var currentSearchValue = searchValue;

            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();
            ToolboxEditorGUI.DrawSectionLabel("Albums:");
            GUILayout.FlexibleSpace();
            ToolboxEditorGUI.DrawExpandCollapseButtons(albums);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(3);
            
            GUILayout.BeginHorizontal();
            GUILayout.Space(3f);
            ToolboxEditorGUI.DrawSearchableFoldoutsList(
                albums,
                element => string.IsNullOrEmpty(currentSearchValue) ||
                    element.FindPropertyRelative("albumName").stringValue.IndexOf(currentSearchValue, System.StringComparison.OrdinalIgnoreCase) >= 0,
                (element, index) => DrawAlbum(element, albums, index, RedButtonColor, LabelSize),
                ref scrollPosition);
            GUILayout.Space(3f);
            GUILayout.EndHorizontal();
        }

        private static void AddAlbum(SerializedProperty albums)
        {
            var index = albums.arraySize;
            albums.arraySize++;
            var album = albums.GetArrayElementAtIndex(index);
            album.isExpanded = false;
            album.FindPropertyRelative("albumName").stringValue = string.Empty;
            album.FindPropertyRelative("source").objectReferenceValue = null;
            album.FindPropertyRelative("useSeparateSource").boolValue = false;
            album.FindPropertyRelative("mixerGroup").objectReferenceValue = null;
            album.FindPropertyRelative("clips").arraySize = 0;
        }

        private static void AddClip(SerializedProperty clips)
        {
            var newIndex = clips.arraySize;
            clips.arraySize++;
            var newClip = clips.GetArrayElementAtIndex(newIndex);
            newClip.isExpanded = false;
            newClip.FindPropertyRelative("id").stringValue = string.Empty;
            newClip.FindPropertyRelative("clip").objectReferenceValue = null;
            AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
        }

        public static void DrawAlbum(SerializedProperty property, SerializedProperty list, int index, Color removeButtonColor, float labelSize)
        {
            var albumName = property.FindPropertyRelative("albumName");
            
            var expansion = ToolboxEditorGUI.ListItemFoldout(
                property.isExpanded,
                albumName.stringValue,
                list,
                index,
                () => DrawAlbumInline(removeButtonColor, albumName.stringValue, list, index),
                () => DrawAlbumContent(property, albumName, labelSize, removeButtonColor));

            if (expansion.HasValue)
            {
                property.isExpanded = expansion.Value;
            }
        }

        private static bool DrawAlbumInline(Color removeButtonColor, string albumName, SerializedProperty list, int index)
        {
            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = removeButtonColor;

            if (GUILayout.Button(ToolboxEditorGUI.Icon("TreeEditor.Trash", "×", "Delete album"), GUILayout.Width(25), GUILayout.ExpandHeight(true)))
            {
                if(EditorUtility.DisplayDialog("Confirm delete", $"Are you sure want to delete {albumName} album?", "Yes", "Cancel"))
                {
                    GUI.backgroundColor = oldColor;
                    list.DeleteArrayElementAtIndex(index);
                    AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
                    return true;
                }
            }
            
            GUILayout.Space(4);
            GUI.backgroundColor = oldColor;
            return false;
        }

        private static void DrawAlbumContent(SerializedProperty property, SerializedProperty albumName, float labelSize, Color removeButtonColor)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);

            EditorGUILayout.BeginVertical();
            var m_clips = property.FindPropertyRelative("clips");

            GUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            ToolboxEditorGUI.DrawResponsiveLabel("Album Name", labelSize);

            var prevAlbumName = albumName.stringValue;
            albumName.stringValue = EditorGUILayout.TextField(albumName.stringValue, GUILayout.MinWidth(0f));
            if(prevAlbumName != albumName.stringValue)
            {
                AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            var useSeparateSource = property.FindPropertyRelative("useSeparateSource");

            EditorGUILayout.BeginHorizontal();
            ToolboxEditorGUI.DrawResponsiveLabel("Use Separate Audio Source", labelSize + 50);
            useSeparateSource.boolValue = EditorGUILayout.Toggle(useSeparateSource.boolValue, GUILayout.Width(EditorGUIUtility.singleLineHeight));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            if(useSeparateSource.boolValue)
            {
                GUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();

                ToolboxEditorGUI.DrawResponsiveLabel("Mixer Group", labelSize);
                EditorGUILayout.PropertyField(
                    property.FindPropertyRelative("mixerGroup"),
                    GUIContent.none,
                    GUILayout.MinWidth(0f));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.BeginVertical();

            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            ToolboxEditorGUI.DrawResponsiveLabel("Clips:", labelSize, ToolboxEditorGUI.SectionLabelStyle);

            if (GUILayout.Button("Add Clip", GUILayout.MinWidth(0f), GUILayout.MaxWidth(75), GUILayout.ExpandWidth(false)))
            {
                AddClip(m_clips);
            }

            ToolboxEditorGUI.DrawExpandCollapseButtons(m_clips);

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(1);

            GUILayout.BeginHorizontal();
            GUILayout.Space(3f);
            GUILayout.BeginVertical();
            for (int i = 0; i < m_clips.arraySize; i++)
            {
                DrawClip(m_clips.GetArrayElementAtIndex(i), m_clips, i, removeButtonColor, labelSize);
                GUILayout.Space(3);
            }
            GUILayout.EndVertical();
            GUILayout.Space(3f);
            GUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        public static void DrawClip(SerializedProperty property, SerializedProperty list, int index, Color removeButtonColor, float labelSize)
        {
            var clipId = property.FindPropertyRelative("id");
            var clip = property.FindPropertyRelative("clip");

            var expansion = ToolboxEditorGUI.ListItemFoldout(
                property.isExpanded,
                clipId.stringValue,
                list,
                index,
                () => DrawClipInline(clip, list, index, removeButtonColor),
                () => DrawClipContent(clipId, clip, labelSize));

            if (expansion.HasValue)
            {
                property.isExpanded = expansion.Value;
            }
        }

        private static bool DrawClipInline(SerializedProperty clip, SerializedProperty list, int index, Color removeButtonColor)
        {
#if UNITY_2023_2_OR_NEWER
            var clipValue = clip.objectReferenceValue as AudioResource;
            var isRandomContainer = clipValue != null && clipValue.GetType().FullName == "UnityEngine.Audio.AudioRandomContainer";
#else
            var clipValue = clip.objectReferenceValue as AudioClip;
            const bool isRandomContainer = false;
#endif

            if (isRandomContainer)
            {
                if (GUILayout.Button(
                    new GUIContent("Open Container", "Open Audio Random Container editor"),
                    GUILayout.MinWidth(0f),
                    GUILayout.MaxWidth(110f),
                    GUILayout.ExpandWidth(true),
                    GUILayout.ExpandHeight(true)))
                {
                    AssetDatabase.OpenAsset(clip.objectReferenceValue);
                }
            }
            else
            {
                if (GUILayout.Button(ToolboxEditorGUI.Icon("PlayButton", "▶", "Play audio preview"), GUILayout.Width(25), GUILayout.ExpandHeight(true)))
                {
                    if (clipValue != null)
                    {
                        AudioUtils.PlayPreviewClip(clipValue);
                    }
                }

                if (GUILayout.Button(ToolboxEditorGUI.Icon("PreMatQuad", "■", "Stop audio preview"), GUILayout.Width(25), GUILayout.ExpandHeight(true)))
                {
                    AudioUtils.StopAllPreviewClips();
                }
            }

            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = removeButtonColor;

            if (GUILayout.Button(ToolboxEditorGUI.Icon("TreeEditor.Trash", "×", "Delete clip"), GUILayout.Width(25), GUILayout.ExpandHeight(true)))
            {
                GUI.backgroundColor = oldColor;
                list.DeleteArrayElementAtIndex(index);
                AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
                return true;
            }
            GUI.backgroundColor = oldColor;

            GUILayout.Space(4);
            return false;
        }

        private static void DrawClipContent(SerializedProperty clipId, SerializedProperty clip, float labelSize)
        {
#if UNITY_2023_2_OR_NEWER
            var clipValue = clip.objectReferenceValue as AudioResource;
            var isRandomContainer = clipValue != null && clipValue.GetType().FullName == "UnityEngine.Audio.AudioRandomContainer";
#else
            var clipValue = clip.objectReferenceValue as AudioClip;
            const bool isRandomContainer = false;
#endif
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);

            EditorGUILayout.BeginVertical();

            GUILayout.Space(8);

            //clip id draw
            EditorGUILayout.BeginHorizontal();
            ToolboxEditorGUI.DrawResponsiveLabel("Clip ID", labelSize);
            var prevClipId = clipId.stringValue;
            clipId.stringValue = EditorGUILayout.TextField(clipId.stringValue, GUILayout.MinWidth(0f));
            
            if(clipValue != null)
            {
                if(GUILayout.Button("Set As Clip", GUILayout.MinWidth(0f), GUILayout.MaxWidth(80f), GUILayout.ExpandWidth(true)))
                {
                    clipId.stringValue = clipValue.name;
                }
            }

            if(prevClipId != clipId.stringValue)
            {
                AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
            }

            EditorGUILayout.EndHorizontal();

            //clip reference draw
            EditorGUILayout.BeginHorizontal();
#if UNITY_2023_2_OR_NEWER
            ToolboxEditorGUI.DrawResponsiveLabel("Audio Resource", labelSize);
#else
            ToolboxEditorGUI.DrawResponsiveLabel("Audio Clip", labelSize);
#endif
            EditorGUILayout.PropertyField(clip, GUIContent.none, GUILayout.MinWidth(0f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            if (!isRandomContainer)
            {
                var previewWidth = 75f;
                var previewHeight = EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
                var preview = AssetPreview.GetAssetPreview(clip.objectReferenceValue);
                GUILayout.Label(
                    preview,
                    GUILayout.MinWidth(0f),
                    GUILayout.MaxWidth(previewWidth),
                    GUILayout.Height(previewHeight));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void OnDisable()
        {
            AudioUtils.StopAllPreviewClips();
        }
    }
}
#endif
