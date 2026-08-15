#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if UNITY_2023_2_OR_NEWER
using UnityEngine.Audio;
#endif

namespace VolumeBox.Toolbox.Editor
{
    public static class AudioUtils
    {
        private static MethodInfo m_PlayMethod;
        private static MethodInfo m_StopMethod;

        private static void ValidateMethods()
        {
            if(m_PlayMethod == null || m_StopMethod == null)
            {
                Assembly unityEditorAssembly = typeof(AudioImporter).Assembly;
                Type audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
                m_PlayMethod = audioUtilClass.GetMethod(
                    "PlayPreviewClip",
                    BindingFlags.Static | BindingFlags.Public
                );

                m_StopMethod = audioUtilClass.GetMethod(
                    "StopAllPreviewClips",
                    BindingFlags.Static | BindingFlags.Public
                );
            }
        }

        public static void PlayPreviewClip(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            ValidateMethods();

            m_PlayMethod.Invoke(
                null,
                new object[]
                {
                        clip,
                        0,
                        false
                }
            );
        }

#if UNITY_2023_2_OR_NEWER
        public static void PlayPreviewClip(AudioResource resource)
        {
            if (resource is AudioClip clip)
            {
                PlayPreviewClip(clip);
                return;
            }

            var resourceType = resource.GetType();

            if (resourceType.FullName != "UnityEngine.Audio.AudioRandomContainer")
            {
                return;
            }

            var elementsProperty = resourceType.GetProperty(
                "elements",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            var elements = elementsProperty?.GetValue(resource) as Array;

            if (elements == null || elements.Length == 0)
            {
                return;
            }

            var elementType = elements.GetType().GetElementType();
            var clipProperty = elementType?.GetProperty("audioClip", BindingFlags.Instance | BindingFlags.NonPublic);
            var enabledProperty = elementType?.GetProperty("enabled", BindingFlags.Instance | BindingFlags.NonPublic);
            var availableClips = new System.Collections.Generic.List<AudioClip>();

            foreach (var element in elements)
            {
                var isEnabled = enabledProperty == null || (bool)enabledProperty.GetValue(element);
                var elementClip = clipProperty?.GetValue(element) as AudioClip;

                if (isEnabled && elementClip != null)
                {
                    availableClips.Add(elementClip);
                }
            }

            if (availableClips.Count > 0)
            {
                PlayPreviewClip(availableClips[UnityEngine.Random.Range(0, availableClips.Count)]);
            }
        }
#endif

        public static void StopAllPreviewClips()
        {
            ValidateMethods();

            m_StopMethod.Invoke(
                null,
                new object[] { }
            );
        }
    }
}
#endif
