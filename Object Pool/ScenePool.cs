using System;
using System.Collections.Generic;
using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class ScenePool : MonoCached
    {
        [SerializeField] private List<PoolData> m_Pools;
        [SerializeField] private int m_RunType = 0;
        
        private bool m_Initialized = false;
        private List<Pool> m_CurrentPools = new List<Pool>();
        
        public Func<GameObject, Vector3, Quaternion, Transform, GameObject> InstantiateFunc;
        public Action<GameObject> SpawnAction;

        public List<PoolData> Pools => m_Pools;

        protected override void Rise()
        {
            if (m_RunType == 0)
            {
                InitializePools();
            }
        }

        protected override void Ready()
        {
            if (m_RunType == 1)
            {
                InitializePools();
            }
        }
        
        public void InitializePools()
        {
            if (m_Initialized) return;

            m_CurrentPools = new List<Pool>();

            foreach (var pool in m_Pools) 
            {
                m_CurrentPools.Add(Toolbox.Pooler.TryAddPool(pool, InstantiateFunc, SpawnAction));
            }

            m_Initialized = true;
        }

        protected override void Destroyed()
        {
            if(Toolbox.HasInstance)
            {
                m_CurrentPools.ForEach(p => Toolbox.Pooler.TryRemovePool(p));
            }
        }
    }
}
