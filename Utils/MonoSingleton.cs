using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class MonoSingleton<T> : MonoBehaviour where T: MonoBehaviour
    {
        private static T instance;
        private static object lockObject = new object();
        private static bool destroyed = false;
        private static bool reinstantiateIfDestroyed = true;
        private static bool initialized = false;
        private static bool applicationQuitting = false;

        public static bool HasInstance => initialized && instance != null && !applicationQuitting;
        
        public static bool ReinstantiateIfDestroyed
        {
            get => reinstantiateIfDestroyed;
            set
            {
                if(value)
                {
                    destroyed = false;
                }

                reinstantiateIfDestroyed = value;
            }
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

        private static void ClearInstance()
        {
            applicationQuitting = true;
            Application.quitting -= ClearInstance;
            instance = null;
            destroyed = true;
            initialized = false;
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
        }
    }
}