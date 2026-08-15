using System;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace VolumeBox.Toolbox.Editor
{
    public class SceneAdvancedDropdown: AdvancedDropdown
    {
        private Action<string> m_Callback;

        public SceneAdvancedDropdown(AdvancedDropdownState state) : base(state)
        {

        }

        public SceneAdvancedDropdown(AdvancedDropdownState state, Action<string> onSceneSelectedCallback) : base(state)
        {
            m_Callback = onSceneSelectedCallback;
        }

        public static string[] GetFormattedScenesList()
        {
            return BuildSettingsSceneUtils.GetScenes().Select(scene => scene.Value).ToArray();
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Scenes");

            var scenes = BuildSettingsSceneUtils.GetScenes();

            for (int i = 0; i < scenes.Count; i++)
            {
                root.AddChild(new SceneAdvancedDropdownItem(scenes[i].DisplayName, scenes[i].Value));
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is SceneAdvancedDropdownItem sceneItem)
            {
                m_Callback?.Invoke(sceneItem.Value);
            }
        }
    }

    public class SceneAdvancedDropdownItem : AdvancedDropdownItem
    {
        public string Value { get; }

        public SceneAdvancedDropdownItem(string name, string value) : base(name)
        {
            Value = value;
        }
    }
}
