#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;

namespace VolumeBox.Toolbox.Editor
{
    [CustomEditor(typeof(SceneAlbumsHolder))]
    public class SceneAlbumHolderEditor : UnityEditor.Editor
    {
        private GUISkin m_Skin;

        private SerializedProperty _AlbumsList;
        private string _AlbumSearchValue;
        private Vector2 _CurrentScrollPosition;

        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            _AlbumsList = serializedObject.FindProperty("_Albums");
            m_Skin = ResourcesUtils.GetOrLoadAsset(m_Skin, "toolbox_styles.guiskin");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            AudioPlayerEditor.DrawAlbums(
                _AlbumsList,
                ref _AlbumSearchValue,
                ref _CurrentScrollPosition,
                m_Skin,
                true
            );

            serializedObject.ApplyModifiedProperties();

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
                AudioPlayerClipPropertyDrawer.IsClipsChanged = true;
            }
        }

        private void OnDisable()
        {
            AudioUtils.StopAllPreviewClips();
        }
    }
}
#endif
