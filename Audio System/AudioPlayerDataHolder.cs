using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace VolumeBox.Toolbox
{
    public class AudioPlayerDataHolder : ScriptableObject
    {
        [SerializeField] private List<AudioAlbum> albums;

        public List<AudioAlbum> Albums => albums;
    }

    [Serializable]
    public class AudioAlbum
    {
        [SerializeField] public string albumName;
        [HideInInspector, SerializeField] public AudioSource source;
        [SerializeField] public bool useSeparateSource;
        [SerializeField] public AudioMixerGroup mixerGroup;
        [SerializeField] public List<AudioClipInfo> clips;
    }
    
    [Serializable]
    public class AudioClipInfo
    {
        public string id;
#if UNITY_2023_2_OR_NEWER
        public AudioResource clip;
#else
        public AudioClip clip;
#endif
    }
}
