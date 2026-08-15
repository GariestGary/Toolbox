using System.Collections.Generic;
using UnityEngine;

namespace VolumeBox.Toolbox
{
    public class AudioVault : MonoCached
    {
        [SerializeField] private string m_AudioVaultName;
        [SerializeField] private List<AudioAlbum> m_Albums = new();
        [SerializeField] private int m_RunType;

        private bool m_Initialized;

        public string Name => m_AudioVaultName;
        public List<AudioAlbum> Albums => m_Albums;

        protected override void Rise()
        {
            if (m_RunType == 0)
            {
                InitializeVault();
            }
        }

        protected override void Ready()
        {
            if (m_RunType == 1)
            {
                InitializeVault();
            }
        }

        public void InitializeVault()
        {
            if (m_Initialized || Toolbox.AudioPlayer == null)
            {
                return;
            }

            foreach (var album in m_Albums)
            {
                Toolbox.AudioPlayer.AddAlbum(album);
            }

            m_Initialized = true;
        }

        protected override void Destroyed()
        {
            if (!m_Initialized || !Toolbox.HasInstance || Toolbox.AudioPlayer == null)
            {
                return;
            }

            foreach (var album in m_Albums)
            {
                Toolbox.AudioPlayer.TryRemoveAlbum(album);
            }
        }
    }
}
