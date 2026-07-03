using System;
using UnityEditor.IMGUI.Controls;

namespace VolumeBox.Toolbox.Editor
{
    public class PoolAdvancedDropdown: AdvancedDropdown
    {
        private Action<string> m_OnPoolSelectedCallback;
        private string[] m_PoolerEntries;
        private string[] m_SceneEntries;
        private string m_SceneName;

        public PoolAdvancedDropdown(AdvancedDropdownState state) : base(state)
        {
        }

        public PoolAdvancedDropdown(AdvancedDropdownState state, string[] poolerEntries, string[] sceneEntries, string sceneName, Action<string> onPoolSelectedCallback) : base(state)
        {
            m_OnPoolSelectedCallback = onPoolSelectedCallback;
            m_PoolerEntries = poolerEntries;
            m_SceneEntries = sceneEntries;
            m_SceneName = sceneName;
            minimumSize = new UnityEngine.Vector2(260f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Pools");
            
            if(m_PoolerEntries.Length > 0)
            {
                var poolerRoot = new AdvancedDropdownItem("Pooler");

                for (int i = 0; i < m_PoolerEntries.Length; i++)
                {
                    poolerRoot.AddChild(new PoolAdvancedDropdownItem(m_PoolerEntries[i], m_PoolerEntries[i]));
                }

                root.AddChild(poolerRoot);
            }

            if(m_SceneEntries.Length > 0)
            {
                var sceneRoot = new AdvancedDropdownItem(m_SceneName);

                for (int i = 0; i < m_SceneEntries.Length; i++)
                {
                    sceneRoot.AddChild(new PoolAdvancedDropdownItem(m_SceneEntries[i], m_SceneEntries[i]));
                }

                root.AddChild(sceneRoot);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            base.ItemSelected(item);

            var poolItem = item as PoolAdvancedDropdownItem;

            m_OnPoolSelectedCallback?.Invoke(poolItem.PoolName);
        }

    }

    public class PoolAdvancedDropdownItem: AdvancedDropdownItem
    {
        private string m_PoolName;

        public string PoolName => m_PoolName;

        public PoolAdvancedDropdownItem(string name) : base(name)
        {
        }

        public PoolAdvancedDropdownItem(string name, string poolName) : base(name)
        {
            m_PoolName = poolName;
        }
    }


}
