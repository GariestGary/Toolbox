using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class Singleton<T> where T: class, new()
    {
        private static T instance;
        private static object lockObject = new();
        private static bool applicationQuitting;

        public static bool HasInstance => instance != null && !applicationQuitting;

        public static T Instance
        {
            get
            {
                if (applicationQuitting) return null;
                
                lock (lockObject)
                {
                    if (instance == null && !applicationQuitting)
                    {
                        instance = new T();
                    }
                    return instance;
                }
            }
        }

        public void ClearInstance()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            applicationQuitting = false;
        }
    }
}