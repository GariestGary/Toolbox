using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        private static readonly object lockObject = new();

        private static bool destroyed;
        private static bool reinstantiateIfDestroyed = true;
        private static bool applicationQuitting;
        private static bool subscribedToQuit;
        private static int observedPlaySessionId = -1;

        public static bool HasInstance
        {
            get
            {
                EnsureFreshState();

                if (instance == null)
                    return false;

                if (applicationQuitting)
                    return false;

                return true;
            }
        }

        public static bool ReinstantiateIfDestroyed
        {
            get => reinstantiateIfDestroyed;
            set
            {
                if (value)
                    destroyed = false;

                reinstantiateIfDestroyed = value;
            }
        }

        public static T Instance
        {
            get
            {
                EnsureFreshState();

                if (applicationQuitting)
                    return null;

                if (!reinstantiateIfDestroyed && destroyed)
                    return null;

                lock (lockObject)
                {
                    EnsureFreshState();

                    if (instance != null)
                        return instance;

                    if (applicationQuitting)
                        return null;

#if UNITY_6000_0_OR_NEWER
                    instance = FindFirstObjectByType<T>();
#else
                    instance = FindObjectOfType<T>();
#endif

                    if (instance == null && !applicationQuitting)
                    {
                        var go = new GameObject($"[SINGLETON] {typeof(T).Name}");
                        instance = go.AddComponent<T>();
                        destroyed = false;
                    }

                    if (instance != null && !subscribedToQuit)
                    {
                        Application.quitting += ClearInstance;
                        subscribedToQuit = true;
                    }

                    return applicationQuitting ? null : instance;
                }
            }
        }

        protected virtual void OnDestroy()
        {
            // Ignore stale callbacks from previous play sessions when domain reload is off.
            if (observedPlaySessionId != SingletonRuntime.PlaySessionId)
                return;

            if (ReferenceEquals(instance, this))
            {
                instance = null;
                destroyed = true;
            }
        }

        private static void EnsureFreshState()
        {
            int currentSession = SingletonRuntime.PlaySessionId;
            if (observedPlaySessionId == currentSession)
                return;

            observedPlaySessionId = currentSession;

            if (subscribedToQuit)
            {
                Application.quitting -= ClearInstance;
                subscribedToQuit = false;
            }

            instance = null;
            destroyed = false;
            applicationQuitting = false;
        }

        private static void ClearInstance()
        {
            if (applicationQuitting)
                return;

            applicationQuitting = true;
            instance = null;
            destroyed = true;

            if (subscribedToQuit)
            {
                Application.quitting -= ClearInstance;
                subscribedToQuit = false;
            }
        }
    }
}