using UnityEngine;

namespace VolumeBox.Toolbox
{
    internal static class SingletonRuntime
    {
        private static int playSessionId;

        public static int PlaySessionId => playSessionId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode()
        {
            playSessionId++;
        }
    }
}