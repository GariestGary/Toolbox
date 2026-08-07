#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#else
using System;

#endif
using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class MonoCached : MonoBehaviour
    {
        [SerializeField, HideInInspector] private bool processIfInactiveSelf = false;
        [SerializeField, HideInInspector] private bool processIfInactiveInHierarchy = false;
        [SerializeField, HideInInspector] private bool ignoreTimeScale = false;

        protected float delta;
        protected float fixedDelta;
        protected float interval;
        private RectTransform rect;
        private bool pausedByActiveState = false;
        private bool pausedManual = false;
        private bool raised;
        private bool ready;

        [HideInInspector] private float IntervalTimer;
        [HideInInspector] private float TimeStack;
        [HideInInspector] private float FixedTimeStack;

        #region Properties
        public bool Paused => pausedByActiveState || pausedManual;

        public bool ProcessIfInactiveSelf
        {
            get => processIfInactiveSelf;

            set => processIfInactiveSelf = value;
        }

        public bool ProcessIfInactiveInHierarchy
        {
            get => processIfInactiveInHierarchy;

            set => processIfInactiveInHierarchy = value;
        }

        public bool IgnoreTimeScale
        {
            get => ignoreTimeScale;

            set => ignoreTimeScale = value;
        }

        public float Interval
        {
            get => interval;
            set
            {
                if (value < 0)
                {
                    interval = 0;
                }
                else
                {
                    interval = value;
                }
            }
        }

        public RectTransform Rect
        {
            get
            {
                if(rect == null)
                {
                    if (transform is RectTransform)
                    {
                        rect = transform as RectTransform;
                    }
                }

                return rect;
            }
            private set { }
        }
        #endregion
        
        internal void OnRise()
        {
            if (raised) return;

            Rise();

            raised = true;
        }

        internal void OnReady()
        {
            if (ready) return;

            Ready();

            ready = true;
        }

        internal void ProcessControl(float extDelta)
        {
            if (Interval > 0)
            {
                if (IntervalTimer >= Interval)
                {
                    Process(TimeStack);
                    TimeStack = 0;
                    IntervalTimer -= Interval;
                }
                IntervalTimer += extDelta;
                TimeStack += extDelta;
            }
            else
            {
               Process(extDelta);
            }
        }

        internal void FixedProcessControl(float extFixedDelta)
        {
            if (Interval > 0)
            {
                if (IntervalTimer >= Interval)
                {
                    FixedProcess(FixedTimeStack);
                    FixedTimeStack = 0;
                }
                FixedTimeStack += extFixedDelta;
            }
            else
            {
                FixedProcess(extFixedDelta);
            }
        }

        internal void LateProcessControl(float extDelta)
        {
            if (Interval > 0)
            {
                if (IntervalTimer >= Interval)
                {
                    //Time stack counting in Process method
                    LateProcess(TimeStack);
                }
            }
            else
            {
                LateProcess(delta);
            }
        }

        #region Virtual Process Methods
        /// <summary>
        /// Calls when scene which this MonoCached part of loaded
        /// </summary>
        protected virtual void OnSceneLoaded(){}

        /// <summary>
        /// Alternative to Awake()
        /// </summary>
        protected virtual void Rise(){}

        /// <summary>
        /// Alternative to Start()
        /// </summary>
        protected virtual void Ready(){}

        /// <summary>
        /// Alternative to Update()
        /// </summary>
        protected virtual void Tick(){}

        /// <summary>
        /// Alternative to FixedUpdate()
        /// </summary>
        protected virtual void FixedTick(){}

        /// <summary>
        /// Alternative to LateUpdate()
        /// </summary>
        protected virtual void LateTick(){}
        #endregion

        #region Lifetime Methods
        protected virtual void Destroyed(){}

        protected virtual void OnPause(){}

        protected virtual void OnResume(){}

        protected virtual void OnActivate(){}

        protected virtual void OnDeactivate(){}
        #endregion

        #region Process Methods
        private void Process(float delta)
        {
            this.delta = delta;

            if(Paused) return;

            Tick();
        }

        private void FixedProcess(float fixedDelta)
        {
            this.fixedDelta = fixedDelta;

            if(Paused) return;

            FixedTick();
        }

        private void LateProcess(float delta)
        {
            if(Paused) return;

            LateTick();
        }
        #endregion

        #region Lifetime Control Methods
        public void Pause()
        {
            if(pausedManual) return;
            pausedManual = true;
            OnPause();
        }

        public void Resume()
        {
            if(!pausedManual) return;
            pausedManual = false;
            OnResume();
        }
        
        public void EnableGameObject()
        {
            gameObject.SetActive(true);
        }

        public void DisableGameObject()
        {
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            pausedByActiveState = false;

            OnActivate();
        }

        private void OnDisable()
        {
            if (gameObject.activeSelf)
            {
                if (!gameObject.activeInHierarchy && !processIfInactiveInHierarchy)
                {
                    pausedByActiveState = true;
                }
            }
            else
            {
                if (!processIfInactiveSelf)
                {
                    pausedByActiveState = true;
                }
            }

            OnDeactivate();
        }

        private void OnDestroy()
        {
            if(Toolbox.HasInstance)
            {
                var updater = Toolbox.Updater;
                if (updater != null)
                {
                    updater.RemoveMonoFromUpdate(this);
                }
            }

            if (raised)
            {
                Destroyed();
            }
        }
        #endregion
    }

    public static class GameObjectExtensions
    {
        public static void Enable(this GameObject gameObject)
        {
            gameObject.SetActive(true);
        }

        public static void Disable(this GameObject gameObject)
        {
            gameObject.SetActive(false);
        }
    }
}
