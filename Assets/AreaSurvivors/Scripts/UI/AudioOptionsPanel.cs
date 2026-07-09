using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
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

        const float VolumeStep = 0.01f;
        const float RepeatDelay = 0.35f;
        const float RepeatInterval = 0.08f;
        static readonly Color SelectedSliderColor = new Color(0.23f, 0.34f, 0.22f, 0.95f);
        static readonly Color NormalSliderColor = new Color(0.08f, 0.12f, 0.1f, 0.92f);
        static readonly Color SelectedOutlineColor = new Color(0.98f, 0.9f, 0.38f, 1f);
        static readonly Color NormalOutlineColor = new Color(0.58f, 0.68f, 0.4f, 0.8f);

        UnityAction backAction;
        Image bgmSliderBackground;
        Image sfxSliderBackground;
        Outline bgmSliderOutline;
        Outline sfxSliderOutline;
        float nextHorizontalRepeatTime;
        float bgmLastValue;
        float sfxLastValue;
        int heldHorizontalDirection;

        public void Bind(UnityAction onBack)
        {
            backAction = onBack;
            WireControls();
            Refresh();
        }

        void OnEnable()
        {
            Refresh();
            RefreshSelectionVisuals();
        }

        void Update()
        {
            RefreshSelectionVisuals();
            TickSelectedVolumeInput();
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
                EnsureSelectionVisuals(bgmSlider, ref bgmSliderBackground, ref bgmSliderOutline);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
                sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
                EnsureSelectionVisuals(sfxSlider, ref sfxSliderBackground, ref sfxSliderOutline);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnBackClicked);
                backButton.onClick.AddListener(OnBackClicked);
            }
        }

        public void Refresh()
        {
            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(AudioManager.BgmVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AudioManager.SfxVolume);
            bgmLastValue = AudioManager.BgmVolume;
            sfxLastValue = AudioManager.SfxVolume;
            RefreshPercentTexts();
        }

        void OnBgmVolumeChanged(float value)
        {
            AudioManager.BgmVolume = value;
            if (ShouldAcceptSliderChange(bgmSlider)) bgmLastValue = value;
            RefreshPercentTexts();
        }

        void OnSfxVolumeChanged(float value)
        {
            AudioManager.SfxVolume = value;
            if (ShouldAcceptSliderChange(sfxSlider)) sfxLastValue = value;
            RefreshPercentTexts();
        }

        void RefreshPercentTexts()
        {
            if (bgmValueText != null) bgmValueText.text = Percent(AudioManager.BgmVolume);
            if (sfxValueText != null) sfxValueText.text = Percent(AudioManager.SfxVolume);
        }

        void RefreshSelectionVisuals()
        {
            bool bgmSelected = IsSelected(bgmSlider);
            bool sfxSelected = IsSelected(sfxSlider);
            ApplySelectionVisual(bgmSliderBackground, bgmSliderOutline, bgmSelected);
            ApplySelectionVisual(sfxSliderBackground, sfxSliderOutline, sfxSelected);
        }

        void TickSelectedVolumeInput()
        {
            var selectedSlider = SelectedSlider();
            if (selectedSlider == null)
            {
                heldHorizontalDirection = 0;
                return;
            }

            int direction = HorizontalInputDirection();
            if (direction == 0)
            {
                heldHorizontalDirection = 0;
                return;
            }

            if (direction != heldHorizontalDirection)
            {
                heldHorizontalDirection = direction;
                nextHorizontalRepeatTime = Time.unscaledTime + RepeatDelay;
                ChangeSliderValue(selectedSlider, direction);
                return;
            }

            if (Time.unscaledTime < nextHorizontalRepeatTime) return;
            nextHorizontalRepeatTime = Time.unscaledTime + RepeatInterval;
            ChangeSliderValue(selectedSlider, direction);
        }

        void ChangeSliderValue(Slider slider, int direction)
        {
            if (slider == bgmSlider)
            {
                bgmLastValue = SteppedValue(bgmLastValue, direction);
                bgmSlider.SetValueWithoutNotify(bgmLastValue);
                AudioManager.BgmVolume = bgmLastValue;
            }
            else if (slider == sfxSlider)
            {
                sfxLastValue = SteppedValue(sfxLastValue, direction);
                sfxSlider.SetValueWithoutNotify(sfxLastValue);
                AudioManager.SfxVolume = sfxLastValue;
            }

            RefreshPercentTexts();
        }

        Slider SelectedSlider()
        {
            if (IsSelected(bgmSlider)) return bgmSlider;
            if (IsSelected(sfxSlider)) return sfxSlider;
            return null;
        }

        static int HorizontalInputDirection()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) return -1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) return 1;

            float x = ControllerInputSettingsStore.MoveVector().x;
            if (x < -0.55f) return -1;
            if (x > 0.55f) return 1;
            return 0;
        }

        static float SteppedValue(float value, int direction)
        {
            float stepped = Mathf.Clamp01(value + direction * VolumeStep);
            return Mathf.Round(stepped * 100f) / 100f;
        }

        static bool ShouldAcceptSliderChange(Slider slider)
        {
            return !IsSelected(slider) || HorizontalInputDirection() == 0;
        }

        static bool IsSelected(Slider slider)
        {
            if (slider == null || EventSystem.current == null) return false;
            var selected = EventSystem.current.currentSelectedGameObject;
            return selected == slider.gameObject;
        }

        static void EnsureSelectionVisuals(Slider slider, ref Image background, ref Outline outline)
        {
            if (slider == null) return;
            background = slider.GetComponent<Image>();
            if (background != null)
            {
                background.raycastTarget = false;
                background.color = NormalSliderColor;
            }

            outline = slider.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = true;
                outline.effectColor = NormalOutlineColor;
            }
        }

        static void ApplySelectionVisual(Image background, Outline outline, bool selected)
        {
            if (background != null) background.color = selected ? SelectedSliderColor : NormalSliderColor;
            if (outline != null) outline.effectColor = selected ? SelectedOutlineColor : NormalOutlineColor;
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
