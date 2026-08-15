using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace VolumeBox.Toolbox.Editor
{
    public class AudioPlayerClipAdvancedDropdown : AdvancedDropdown
    {
        private readonly List<AudioDropdownGroup> m_Groups;
        private readonly Action<string> m_OnClipSelectedCallback;

        public AudioPlayerClipAdvancedDropdown(
            AdvancedDropdownState state,
            List<AudioDropdownGroup> groups,
            Action<string> onClipSelectedCallback) : base(state)
        {
            m_Groups = groups;
            m_OnClipSelectedCallback = onClipSelectedCallback;
            minimumSize = new Vector2(300f, 360f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Audio Clips");

            foreach (var group in m_Groups)
            {
                root.AddChild(BuildGroup(group));
            }

            return root;
        }

        private static AdvancedDropdownItem BuildGroup(AudioDropdownGroup group)
        {
            var root = new AdvancedDropdownItem(group.Name);

            foreach (var entry in group.Entries)
            {
                root.AddChild(new AudioClipDropdownItem(entry.Name, entry.FormattedId));
            }

            foreach (var child in group.Children)
            {
                root.AddChild(BuildGroup(child));
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            base.ItemSelected(item);

            if (item is AudioClipDropdownItem clipItem)
            {
                m_OnClipSelectedCallback?.Invoke(clipItem.FormattedId);
            }
        }
    }

    public class AudioDropdownGroup
    {
        public string Name { get; }
        public List<AudioDropdownGroup> Children { get; }
        public List<AudioDropdownEntry> Entries { get; }

        public AudioDropdownGroup(
            string name,
            List<AudioDropdownGroup> children = null,
            List<AudioDropdownEntry> entries = null)
        {
            Name = name;
            Children = children ?? new List<AudioDropdownGroup>();
            Entries = entries ?? new List<AudioDropdownEntry>();
        }
    }

    public readonly struct AudioDropdownEntry
    {
        public string Name { get; }
        public string FormattedId { get; }

        public AudioDropdownEntry(string name, string formattedId)
        {
            Name = name;
            FormattedId = formattedId;
        }
    }

    public class AudioClipDropdownItem : AdvancedDropdownItem
    {
        public string FormattedId { get; }

        public AudioClipDropdownItem(string name, string formattedId) : base(name)
        {
            FormattedId = formattedId;
        }
    }
}
