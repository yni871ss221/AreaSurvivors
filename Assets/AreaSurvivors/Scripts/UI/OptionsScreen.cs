using UnityEngine;

namespace AreaSurvivors
{
    public sealed class OptionsScreen : MonoBehaviour
    {
        public AudioOptionsPanel audioOptionsPanel;
        public SceneNavigator navigator;

        void Start()
        {
            AudioManager.PlayBgm(BgmTrack.TitleOptions);

            if (navigator == null) navigator = GetComponent<SceneNavigator>();
            if (audioOptionsPanel == null || navigator == null)
            {
                Debug.LogError("OptionsScreen requires Scene-authored AudioOptionsPanel and SceneNavigator references.");
                enabled = false;
                return;
            }

            audioOptionsPanel.Bind(navigator.LoadTitle);
        }
    }
}
