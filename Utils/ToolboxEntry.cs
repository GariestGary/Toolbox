#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;
using Cysharp.Threading.Tasks;

namespace VolumeBox.Toolbox
{
    public class ToolboxEntry : MonoBehaviour
    {
        [SerializeField] private bool _InitializeOnStart;

        private Messenger _Msg;
        private Pooler _Pool;
        private AudioPlayer _Audio;
        private Traveler _Travel;
        private Updater _Upd;
        
        private SettingsData m_Settings => StaticData.Settings;

        private void Start()
        {
            if (_InitializeOnStart)
            {
                Init().Forget();
            }
        }

        public async UniTask Init()
        {
            Application.targetFrameRate = m_Settings.TargetFrameRate;
            InitializeComponents();

            if(StaticData.Settings.AutoResolveScenesAtPlay)
            {
                if (!string.IsNullOrEmpty(m_Settings.InitialSceneName) && m_Settings.InitialSceneName != "MAIN")
                {
                    await _Travel.LoadScene(m_Settings.InitialSceneName, m_Settings.InitialSceneArgs);
                }
            }
        }

        public void InitializeComponents()
        {
            _Msg = GetComponent<Messenger>();
            _Pool = GetComponent<Pooler>();
            _Audio = GetComponent<AudioPlayer>();
            _Travel = GetComponent<Traveler>();
            _Upd = GetComponent<Updater>();
            
            _Pool.Initialize(_Msg, _Upd);
            _Msg.Initialize(_Pool);
            _Audio.Initialize(_Msg);
            _Travel.Initialize(_Msg, _Upd);
        }

        public static void UpdateTargetFramerate(int value)
        {
            Application.targetFrameRate = value;
        }

        private void ClearAll()
        {
            if (_Pool != null)
                _Pool.Clear();

            if (_Msg != null)
                _Msg.Clear();

            if (_Upd != null)
                _Upd.Clear();
        }

        private void OnApplicationQuit()
        {
            ClearAll();
        }
    }
}
