using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace VolumeBox.Toolbox.Editor
{
    public static class BuildSettingsSceneUtils
    {
        public static IReadOnlyList<BuildSettingsSceneEntry> GetScenes()
        {
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path.IsValuable())
                .Where(scene => AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) != null)
                .Select(scene => new
                {
                    scene.path,
                    Name = Path.GetFileNameWithoutExtension(scene.path)
                })
                .ToArray();

            var duplicateNames = scenes
                .GroupBy(scene => scene.Name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return scenes
                .Select(scene => new BuildSettingsSceneEntry(
                    scene.path,
                    scene.Name,
                    duplicateNames.Contains(scene.Name) ? scene.path : scene.Name
                ))
                .ToArray();
        }
    }

    public readonly struct BuildSettingsSceneEntry
    {
        public string Path { get; }
        public string Name { get; }
        public string DisplayName { get; }
        public string Value => DisplayName;

        public BuildSettingsSceneEntry(string path, string name, string displayName)
        {
            Path = path;
            Name = name;
            DisplayName = displayName;
        }
    }
}
