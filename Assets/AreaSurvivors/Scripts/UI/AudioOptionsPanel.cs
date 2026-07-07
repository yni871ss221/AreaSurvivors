using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class AudioOptionsPanel : MonoBehaviour
    {
        public Slider bgmSlider;
        public Slider sfxSlider;
        public Text bgmValueText;
        public Text sfxValueText;
        public Button backButton;

        UnityAction backAction;

        public void Bind(UnityAction onBack)
        {
            backAction = onBack;
            WireControls();
            Refresh();
        }

        void OnEnable()
        {
            Refresh();
        }

        void OnDestroy()
        {
            if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
        }

        void WireControls()
        {
            if (bgmSlider != null)
            {
                bgmSlider.onValueChanged.RemoveListener(OnBgmVolumeChanged);
                bgmSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
                sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnBackClicked);
                backButton.onClick.AddListener(OnBackClicked);
            }
        }

        void Refresh()
        {
            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(AudioManager.BgmVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AudioManager.SfxVolume);
            RefreshPercentTexts();
        }

        void OnBgmVolumeChanged(float value)
        {
            AudioManager.BgmVolume = value;
            RefreshPercentTexts();
        }

        void OnSfxVolumeChanged(float value)
        {
            AudioManager.SfxVolume = value;
            RefreshPercentTexts();
        }

        void RefreshPercentTexts()
        {
            if (bgmValueText != null) bgmValueText.text = Percent(AudioManager.BgmVolume);
            if (sfxValueText != null) sfxValueText.text = Percent(AudioManager.SfxVolume);
        }

        static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        void OnBackClicked()
        {
            AudioManager.PlayButtonConfirm();
            if (backAction != null) backAction();
        }
    }
}
