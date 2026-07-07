using UnityEngine;

namespace AreaSurvivors
{
    public sealed class OptionsScreen : MonoBehaviour
    {
        public GeneralOptionsPanel generalOptionsPanel;
        public AudioOptionsPanel audioOptionsPanel;
        public DisplayOptionsPanel displayOptionsPanel;
        public SceneNavigator navigator;

        void Start()
        {
            AudioManager.PlayBgm(BgmTrack.TitleOptions);

            if (navigator == null) navigator = GetComponent<SceneNavigator>();
            if (generalOptionsPanel == null || audioOptionsPanel == null || displayOptionsPanel == null || navigator == null)
            {
                Debug.LogError("OptionsScreen requires Scene-authored option panel and SceneNavigator references.");
                enabled = false;
                return;
            }

            generalOptionsPanel.Bind();
            audioOptionsPanel.Bind(navigator.LoadTitle);
            displayOptionsPanel.Bind();
        }
    }
}
