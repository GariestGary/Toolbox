using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;

namespace VolumeBox.Toolbox.Editor
{
    public class PoolAdvancedDropdown: AdvancedDropdown
    {
        private Action<string> m_OnPoolSelectedCallback;
        private string[] m_PoolerEntries;
        private List<PoolDropdownGroup> m_ScenePoolGroups;

        public PoolAdvancedDropdown(AdvancedDropdownState state) : base(state)
        {
        }

        public PoolAdvancedDropdown(
            AdvancedDropdownState state,
            string[] poolerEntries,
            List<PoolDropdownGroup> scenePoolGroups,
            Action<string> onPoolSelectedCallback) : base(state)
        {
            m_OnPoolSelectedCallback = onPoolSelectedCallback;
            m_PoolerEntries = poolerEntries;
            m_ScenePoolGroups = scenePoolGroups;
            minimumSize = new UnityEngine.Vector2(260f, 320f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Pools");
            
            if(m_PoolerEntries.Length > 0)
            {
                var poolerRoot = new AdvancedDropdownItem("Main Pool");

                for (int i = 0; i < m_PoolerEntries.Length; i++)
                {
                    poolerRoot.AddChild(new PoolAdvancedDropdownItem(m_PoolerEntries[i], m_PoolerEntries[i]));
                }

                root.AddChild(poolerRoot);
            }

            for (int i = 0; i < m_ScenePoolGroups.Count; i++)
            {
                var scenePoolRoot = new AdvancedDropdownItem(m_ScenePoolGroups[i].Name);

                for (int j = 0; j < m_ScenePoolGroups[i].Entries.Length; j++)
                {
                    var poolName = m_ScenePoolGroups[i].Entries[j];
                    scenePoolRoot.AddChild(new PoolAdvancedDropdownItem(poolName, poolName));
                }

                root.AddChild(scenePoolRoot);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            base.ItemSelected(item);

            if (item is PoolAdvancedDropdownItem poolItem)
            {
                m_OnPoolSelectedCallback?.Invoke(poolItem.PoolName);
            }
        }

    }

    public class PoolDropdownGroup
    {
        public string Name { get; }
        public string[] Entries { get; }

        public PoolDropdownGroup(string name, string[] entries)
        {
            Name = name;
            Entries = entries;
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
