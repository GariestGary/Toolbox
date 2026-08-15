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
        private GUISkin m_Skin;

        private SerializedProperty m_albums;
        private Vector2 currentScrollPosition;
        private string albumSearchValue;

        public static float LabelSize = 110;
        public static Color RedButtonColor = new Color(0.8705882352941176f, 0.3450980392156863f, 0.3450980392156863f);

        private void OnEnable()
        {
            m_albums = serializedObject.FindProperty("albums");
            m_Skin = ResourcesUtils.GetOrLoadAsset(m_Skin, "toolbox_styles.guiskin");
        }

        public void DrawIMGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawAlbums(m_albums, ref albumSearchValue, ref currentScrollPosition, m_Skin, false);

            serializedObject.ApplyModifiedProperties();

            if(EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
            }
        }

        public static void DrawAlbums(
            SerializedProperty albums,
            ref string searchValue,
            ref Vector2 scrollPosition,
            GUISkin skin,
            bool isComponent)
        {
            GUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
            searchValue = GUILayout.TextField(searchValue, GUI.skin.FindStyle("ToolbarSearchTextField"));
            if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSearchCancelButton")))
            {
                searchValue = "";
                GUI.FocusControl(null);
            }

            if (GUILayout.Button("Add Album", GUILayout.Width(80), GUILayout.ExpandHeight(true)))
            {
                AddAlbum(albums);
                scrollPosition.y = float.MaxValue;
                AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
            }

            if(GUILayout.Button(ToolboxEditorGUI.Icon("PreMatQuad", "■", "Stop all audio previews"), GUILayout.Width(24), GUILayout.ExpandHeight(true)))
            {
                AudioUtils.StopAllPreviewClips();
            }

            GUILayout.EndHorizontal();

            EditorGUILayout.Space(3);
            EditorGUILayout.BeginHorizontal();


            if(albums.arraySize > 0)
            {
                EditorGUILayout.LabelField("Albums:", skin.GetStyle("Label"));
                
                if (GUILayout.Button("Expand All"))
                {
                    SetExpandedStateForAll(albums, true);
                }

                if (GUILayout.Button("Collapse All"))
                {
                    SetExpandedStateForAll(albums, false);
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginVertical();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Space(5);

            for (int i = 0; i < albums.arraySize; i++)
            {
                var album = albums.GetArrayElementAtIndex(i);

                if(searchValue.IsValuable())
                {
                    if(album.FindPropertyRelative("albumName").stringValue.IndexOf(searchValue, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        DrawAlbum(album, albums, i, skin, RedButtonColor, LabelSize, isComponent);
                    }
                }
                else
                {
                    DrawAlbum(album, albums, i, skin, RedButtonColor, LabelSize, isComponent);
                }
                GUILayout.Space(3);
            }

            EditorGUILayout.EndVertical();


            EditorGUILayout.EndScrollView();

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

        private static void SetExpandedStateForAll(SerializedProperty albums, bool value)
        {
            for (int i = 0; i < albums.arraySize; i++)
            {
                var album = albums.GetArrayElementAtIndex(i);
                album.isExpanded = value;
            }
        }

        public static void DrawAlbum(SerializedProperty property, SerializedProperty list, int index, GUISkin skin, Color removeButtonColor, float labelSize, bool isComponent)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(3);
            var oldSkin = GUI.skin;
            GUI.skin = skin;
            EditorGUILayout.BeginVertical(GUI.skin.FindStyle("Box"));
            GUILayout.Space(3);
            GUI.skin = oldSkin;

            EditorGUILayout.BeginHorizontal(GUILayout.Height(25));

            GUILayout.Space(5);
            ToolboxEditorGUI.ArrayDragHandle(list, index);
            var albumName = property.FindPropertyRelative("albumName");
            
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, albumName.stringValue, true, skin.GetStyle("Foldout"));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            var oldColor = GUI.backgroundColor;
            GUI.backgroundColor = removeButtonColor;

            if (GUILayout.Button(ToolboxEditorGUI.Icon("TreeEditor.Trash", "×", "Delete album"), GUILayout.Width(25), GUILayout.ExpandHeight(true)))
            {
                if(EditorUtility.DisplayDialog("Confirm delete", $"Are you sure want to delete {albumName.stringValue} album?", "Yes", "Cancel"))
                {
                    GUI.backgroundColor = oldColor;
                    list.DeleteArrayElementAtIndex(index);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                    AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
                    return;
                }
            }
            GUI.backgroundColor = oldColor;

            GUILayout.Space(5);
            EditorGUILayout.EndHorizontal();

            if (property.isExpanded)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);

                EditorGUILayout.BeginVertical();
                var m_clips = property.FindPropertyRelative("clips");

                GUILayout.Space(8);

                if(isComponent)
                {
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Album Name", GUILayout.Width(labelSize));

                var prevAlbumName = albumName.stringValue;
                albumName.stringValue = EditorGUILayout.TextField(albumName.stringValue);
                if(prevAlbumName != albumName.stringValue)
                {
                    AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
                }

                GUILayout.Space(25);

                var useSeparateSource = property.FindPropertyRelative("useSeparateSource");

                EditorGUILayout.LabelField("Use Separate Audio Source", GUILayout.Width(labelSize + 50));
                useSeparateSource.boolValue = EditorGUILayout.Toggle(useSeparateSource.boolValue, GUILayout.Width(EditorGUIUtility.singleLineHeight));

                GUILayout.Space(25);

                if (GUILayout.Button("Add Clip", GUILayout.Width(75)))
                {
                    var newIndex = m_clips.arraySize;
                    m_clips.arraySize++;
                    var newClip = m_clips.GetArrayElementAtIndex(newIndex);
                    newClip.isExpanded = false;
                    newClip.FindPropertyRelative("id").stringValue = string.Empty;
                    newClip.FindPropertyRelative("clip").objectReferenceValue = null;
                    AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
                }

                EditorGUILayout.EndHorizontal();
                
                if(useSeparateSource.boolValue)
                {
                    GUILayout.Space(5);

                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Mixer Group", GUILayout.Width(labelSize));
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("mixerGroup"), GUIContent.none);

                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.BeginVertical();

                GUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();


                if(m_clips.arraySize > 0)
                {
                    EditorGUILayout.LabelField("Clips:", skin.GetStyle("Label"));
                    
                    if (GUILayout.Button("Expand All", GUILayout.Width(100)))
                    {
                        for (int j = 0; j < m_clips.arraySize; j++)
                        {
                            var clip = m_clips.GetArrayElementAtIndex(j);

                            clip.isExpanded = true;
                        }
                    }

                    if (GUILayout.Button("Collapse All", GUILayout.Width(100)))
                    {
                        for (int j = 0; j < m_clips.arraySize; j++)
                        {
                            var clip = m_clips.GetArrayElementAtIndex(j);

                            clip.isExpanded = false;
                        }
                    }
                }

                if(isComponent)
                {
                    EditorGUI.indentLevel++;
                }

                EditorGUILayout.EndHorizontal();

                GUILayout.Space(1);

                for (int i = 0; i < m_clips.arraySize; i++)
                {
                    DrawClip(m_clips.GetArrayElementAtIndex(i), m_clips, i, skin, removeButtonColor, labelSize);
                    GUILayout.Space(3);
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndHorizontal();
            }
            GUILayout.Space(3);
            EditorGUILayout.EndVertical();
            GUILayout.Space(3);
            EditorGUILayout.EndHorizontal();
        }

        public static void DrawClip(SerializedProperty property, SerializedProperty list, int index, GUISkin skin, Color removeButtonColor, float labelSize)
        {
            EditorGUILayout.BeginHorizontal();
            var oldSkin = GUI.skin;
            GUI.skin = skin;
            EditorGUILayout.BeginVertical(GUI.skin.FindStyle("Box"));
            GUI.skin = oldSkin;
            GUILayout.Space(3);
            EditorGUILayout.BeginHorizontal(GUILayout.Height(25));
            GUILayout.Space(3);
            ToolboxEditorGUI.ArrayDragHandle(list, index);
            GUILayout.Space(6);
            var clipId = property.FindPropertyRelative("id");
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, clipId.stringValue, true, skin.GetStyle("Foldout"));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            var clip = property.FindPropertyRelative("clip");
#if UNITY_2023_2_OR_NEWER
            var clipValue = clip.objectReferenceValue as AudioResource;
            var isRandomContainer = clipValue != null && clipValue.GetType().FullName == "UnityEngine.Audio.AudioRandomContainer";
#else
            var clipValue = clip.objectReferenceValue as AudioClip;
            const bool isRandomContainer = false;
#endif

            if (isRandomContainer)
            {
                if (GUILayout.Button(new GUIContent("Open Container", "Open Audio Random Container editor"), GUILayout.Width(110), GUILayout.ExpandHeight(true)))
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
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
                AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
                return;
            }
            GUI.backgroundColor = oldColor;

            GUILayout.Space(3);
            EditorGUILayout.EndHorizontal();

            if(property.isExpanded)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20);

                EditorGUILayout.BeginVertical();

                GUILayout.Space(8);

                //clip id draw
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Clip ID", GUILayout.Width(labelSize));
                var prevClipId = clipId.stringValue;
                clipId.stringValue = EditorGUILayout.TextField(clipId.stringValue);
                
                if(clipValue != null)
                {
                    if(GUILayout.Button("Set As Clip", GUILayout.Width(80)))
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
                EditorGUILayout.LabelField("Audio Resource", GUILayout.Width(labelSize));
#else
                EditorGUILayout.LabelField("Audio Clip", GUILayout.Width(labelSize));
#endif
                EditorGUILayout.PropertyField(clip, GUIContent.none);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                GUILayout.Space(8);

                EditorGUILayout.EndVertical();

                if (!isRandomContainer)
                {
                    var previewWidth = 75f;
                    var previewHeight = EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
                    var preview = AssetPreview.GetAssetPreview(clip.objectReferenceValue);
                    GUILayout.Label(preview, GUILayout.Width(previewWidth), GUILayout.Height(previewHeight));
                }

                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(2);
            EditorGUILayout.EndVertical();
            GUILayout.Space(3);
            EditorGUILayout.EndHorizontal();
        }

        private void OnDisable()
        {
            AudioUtils.StopAllPreviewClips();
        }
    }
}
#endif
