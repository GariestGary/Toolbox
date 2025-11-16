using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class CachedSingleton<T>: MonoCached where T: MonoCached
    {
        private static T instance;
        private static object lockObject = new();
        private static bool destroyed;
        private static bool reinstantiateIfDestroyed = true;
        private static bool initialized;
        private static bool applicationQuitting;

        public static bool HasInstance => instance != null && !applicationQuitting;

        public static bool ReinstantiateIfDestroyed
        {
            get => reinstantiateIfDestroyed;
            set => reinstantiateIfDestroyed = value;
        }

        public static T Instance
        {
            get
            {
                if (applicationQuitting) return null;
                
                if (!reinstantiateIfDestroyed && destroyed) return null;

                lock (lockObject)
                {
                    if (instance != null) return instance;
                    if (applicationQuitting) return null;
                    
#if UNITY_6000_0_OR_NEWER
                    instance = FindFirstObjectByType<T>();
#else
                    instance = FindObjectOfType<T>();
#endif

                    if (instance == null && !applicationQuitting)
                    {
                        var singleton = new GameObject("[SINGLETON] " + typeof(T));
                        destroyed = false;
                        instance = singleton.AddComponent<T>();
                    }

                    if (applicationQuitting) return instance;
                    
                    Application.quitting += ClearInstance;
                    initialized = true;

                    return instance;
                }
            }
        }

        public static void DontDestroy()
        {
            DontDestroyOnLoad(instance.gameObject);
        }

        private static void ClearInstance()
        {
            applicationQuitting = true;
            Application.quitting -= ClearInstance;
            instance = null;
            destroyed = true;
            initialized = false;
        }

        protected override void Destroyed()
        {
            if (initialized && !applicationQuitting)
            {
                ClearInstance();
            }
        }

        private void OnDestroy()
        {
            if (initialized && !applicationQuitting)
            {
                ClearInstance();
            }
        }

        [RuntimeInitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            applicationQuitting = false;
            destroyed = false;
            initialized = false;
        }
    }
}