using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class Singleton<T> where T: class, new()
    {
        private static T instance;
        private static object lockObject = new();
        private static bool applicationQuitting;
        private static bool initialized;

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
                        Application.quitting += ClearInstance;
                        initialized = true;
                    }
                    return instance;
                }
            }
        }

        private static void ClearInstance()
        {
            if (applicationQuitting) return;
            
            applicationQuitting = true;
            Application.quitting -= ClearInstance;
            instance = null;
            initialized = false;
        }

        [RuntimeInitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            applicationQuitting = false;
        }
    }
}