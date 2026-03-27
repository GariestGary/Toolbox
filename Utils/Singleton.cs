using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class Singleton<T> where T : class, new()
    {
        private static T instance;
        private static readonly object lockObject = new();

        private static bool applicationQuitting;
        private static bool subscribedToQuit;
        private static int observedPlaySessionId = -1;

        public static bool HasInstance
        {
            get
            {
                EnsureFreshState();
                return instance != null && !applicationQuitting;
            }
        }

        public static T Instance
        {
            get
            {
                EnsureFreshState();

                if (applicationQuitting)
                    return null;

                lock (lockObject)
                {
                    EnsureFreshState();

                    if (instance == null && !applicationQuitting)
                    {
                        instance = new T();

                        if (!subscribedToQuit)
                        {
                            Application.quitting += ClearInstance;
                            subscribedToQuit = true;
                        }
                    }

                    return instance;
                }
            }
        }

        private static void EnsureFreshState()
        {
            int currentSession = SingletonRuntime.PlaySessionId;
            if (observedPlaySessionId == currentSession)
                return;

            observedPlaySessionId = currentSession;

            applicationQuitting = false;
            instance = null;

            if (subscribedToQuit)
            {
                Application.quitting -= ClearInstance;
                subscribedToQuit = false;
            }
        }

        private static void ClearInstance()
        {
            if (applicationQuitting)
                return;

            applicationQuitting = true;
            instance = null;

            if (subscribedToQuit)
            {
                Application.quitting -= ClearInstance;
                subscribedToQuit = false;
            }
        }
    }
}