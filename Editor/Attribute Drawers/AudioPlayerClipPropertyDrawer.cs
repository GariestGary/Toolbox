using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VolumeBox.Toolbox.Editor
{
    [CustomPropertyDrawer(typeof(AudioPlayerClipAttribute))]
    public class AudioPlayerClipPropertyDrawer : PropertyDrawer
    {
        private AudioPlayerDataHolder m_AudioPlayerDataHolder;
        private List<AudioDropdownGroup> m_Groups = new();
        private bool m_ManualEnabled;

        private static List<AudioDropdownGroup> s_SceneGroups;

        public static bool IsClipsChanged { get; set; }

        static AudioPlayerClipPropertyDrawer()
        {
            EditorBuildSettings.sceneListChanged += InvalidateSceneCache;
            EditorApplication.hierarchyChanged += InvalidateSceneCache;
            EditorApplication.projectChanged += InvalidateSceneCache;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "AudioPlayerClip requires a string field");
                return;
            }

            ValidateEntries();
            EditorGUI.BeginProperty(position, label, property);

            var labelRect = position;
            labelRect.width = EditorGUIUtility.labelWidth;
            EditorGUI.LabelField(labelRect, label);

            var fieldRect = position;
            fieldRect.x += labelRect.width;
            fieldRect.width -= labelRect.width + 5;

            if (m_ManualEnabled)
            {
                property.stringValue = EditorGUI.TextField(fieldRect, property.stringValue);
            }
            else if (m_Groups.Count == 0)
            {
                EditorGUI.LabelField(fieldRect, "There are no audio clips available", EditorStyles.popup);
            }
            else
            {
                var split = property.stringValue.Split('/');
                var albumName = split.Length > 0 ? split[0] : string.Empty;
                var clipName = split.Length > 1 ? split[1] : string.Empty;
                var caption = property.stringValue.IsValuable()
                    ? $"Album: {albumName} | Clip: {clipName}"
                    : "Select Audio Clip";

                if (GUI.Button(fieldRect, new GUIContent(caption, "Select an album and audio clip"), EditorStyles.popup))
                {
                    var dropdown = new AudioPlayerClipAdvancedDropdown(
                        new AdvancedDropdownState(),
                        m_Groups,
                        formattedId => OnClipSelected(formattedId, property)
                    );
                    dropdown.Show(fieldRect);
                }
            }

            var manualRect = fieldRect;
            manualRect.x += manualRect.width;
            manualRect.width = 20;
            m_ManualEnabled = GUI.Toggle(
                manualRect,
                m_ManualEnabled,
                ToolboxEditorGUI.Icon("editicon.sml", "✎", "Toggle manual audio ID editing"),
                "Button"
            );

            EditorGUI.EndProperty();
        }

        private void ValidateEntries()
        {
            if (m_AudioPlayerDataHolder == null)
            {
                m_AudioPlayerDataHolder = ResourcesUtils.ResolveScriptable<AudioPlayerDataHolder>(SettingsData.audioPlayerResourcesDataPath);
            }

            if (s_SceneGroups == null || IsClipsChanged)
            {
                s_SceneGroups = BuildSceneGroups();
                IsClipsChanged = false;
            }

            m_Groups = new List<AudioDropdownGroup>();
            var mainAlbums = BuildAlbumGroups(m_AudioPlayerDataHolder?.Albums);

            if (mainAlbums.Count > 0)
            {
                m_Groups.Add(new AudioDropdownGroup("Main Audio", mainAlbums));
            }

            m_Groups.AddRange(s_SceneGroups);
        }

        private static List<AudioDropdownGroup> BuildSceneGroups()
        {
            var groups = new List<AudioDropdownGroup>();

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
                    var vaultGroups = new List<AudioDropdownGroup>();
                    var roots = scene.GetRootGameObjects();

                    foreach (var vault in roots.SelectMany(root => root.GetComponentsInChildren<AudioVault>(true)))
                    {
                        var albums = BuildAlbumGroups(vault.Albums);

                        if (albums.Count > 0)
                        {
                            vaultGroups.Add(new AudioDropdownGroup(
                                vault.Name.IsValuable() ? vault.Name : vault.gameObject.name,
                                albums
                            ));
                        }
                    }

                    foreach (var holder in roots.SelectMany(root => root.GetComponentsInChildren<AudioVault>(true)))
                    {
                        var albums = BuildAlbumGroups(holder.Albums);

                        if (albums.Count > 0)
                        {
                            vaultGroups.Add(new AudioDropdownGroup(holder.gameObject.name, albums));
                        }
                    }

                    if (vaultGroups.Count > 0)
                    {
                        groups.Add(new AudioDropdownGroup(buildScene.DisplayName, vaultGroups));
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

        private static List<AudioDropdownGroup> BuildAlbumGroups(IEnumerable<AudioAlbum> albums)
        {
            if (albums == null)
            {
                return new List<AudioDropdownGroup>();
            }

            return albums
                .Where(album => album != null && album.albumName.IsValuable())
                .GroupBy(album => album.albumName)
                .Select(group => new AudioDropdownGroup(
                    group.Key,
                    entries: group
                        .Where(album => album.clips != null)
                        .SelectMany(album => album.clips)
                        .Where(clip => clip != null && clip.id.IsValuable())
                        .Select(clip => clip.id)
                        .Distinct()
                        .Select(clipId => new AudioDropdownEntry(clipId, $"{group.Key}/{clipId}"))
                        .ToList()
                ))
                .Where(group => group.Entries.Count > 0)
                .ToList();
        }

        private static void OnClipSelected(string formattedId, SerializedProperty property)
        {
            property.serializedObject.Update();
            property.stringValue = formattedId;
            property.serializedObject.ApplyModifiedProperties();
        }

        private static void InvalidateSceneCache()
        {
            s_SceneGroups = null;
        }
    }
}
