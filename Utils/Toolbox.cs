using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class Toolbox: MonoSingleton<Toolbox>
    {
        [SerializeField] private Messenger _Messenger;
        [SerializeField] private AudioPlayer _AudioPlayer;
        [SerializeField] private Pooler _Pooler;
        [SerializeField] private Updater _Updater;
        [SerializeField] private Traveler _Traveler;
        
        public static Messenger Messenger => Instance?._Messenger;
        public static AudioPlayer AudioPlayer => Instance?._AudioPlayer;
        public static Pooler Pooler => Instance?._Pooler;
        public static Updater Updater => Instance?._Updater;
        public static Traveler Traveler => Instance?._Traveler;
    }
}
