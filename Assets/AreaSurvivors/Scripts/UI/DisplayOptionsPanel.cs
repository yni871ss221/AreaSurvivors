using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class DisplayOptionsPanel : MonoBehaviour
    {
        public Button fullscreenButton;
        public Button windowedButton;
        public Button[] resolutionButtons;
        public Dropdown modeDropdown;
        public Dropdown windowSizeDropdown;
        public GameObject resolutionRoot;
        public Text statusText;

        readonly Color selectedColor = new Color(0.96f, 0.90f, 0.34f, 1f);
        readonly Color normalColor = Color.white;
        readonly Color disabledColor = new Color(0.65f, 0.70f, 0.62f, 0.72f);
        float ignoreResizeUntil;

        public void Bind()
        {
            WireControls();
            Refresh();
        }

        void OnEnable()
        {
            Refresh();
            ignoreResizeUntil = Time.unscaledTime + 0.35f;
        }

        void Update()
        {
            if (DisplaySettingsStore.Mode != DisplayWindowMode.Windowed) return;
            if (Time.unscaledTime < ignoreResizeUntil) return;
            if (Screen.fullScreenMode != FullScreenMode.Windowed && Screen.fullScreen) return;

            int currentWidth = Mathf.Max(1, Screen.width);
            int currentHeight = Mathf.Max(1, Screen.height);
            if (currentWidth == DisplaySettingsStore.CustomWidth &&
                currentHeight == DisplaySettingsStore.CustomHeight &&
                DisplaySettingsStore.IsCustomWindowSize)
            {
                return;
            }

            if (MatchesSelectedPreset(currentWidth, currentHeight)) return;

            DisplaySettingsStore.SetCustomWindowSize(currentWidth, currentHeight, false);
            Refresh();
        }

        void OnDestroy()
        {
            if (fullscreenButton != null) fullscreenButton.onClick.RemoveListener(OnFullscreenClicked);
            if (windowedButton != null) windowedButton.onClick.RemoveListener(OnWindowedClicked);
            if (modeDropdown != null) modeDropdown.onValueChanged.RemoveListener(OnModeDropdownChanged);
            if (windowSizeDropdown != null) windowSizeDropdown.onValueChanged.RemoveListener(OnWindowSizeDropdownChanged);
        }

        void WireControls()
        {
            if (fullscreenButton != null)
            {
                fullscreenButton.onClick.RemoveListener(OnFullscreenClicked);
                fullscreenButton.onClick.AddListener(OnFullscreenClicked);
            }

            if (windowedButton != null)
            {
                windowedButton.onClick.RemoveListener(OnWindowedClicked);
                windowedButton.onClick.AddListener(OnWindowedClicked);
            }

            if (resolutionButtons != null)
            {
                for (int i = 0; i < resolutionButtons.Length; i++)
                {
                    int index = i;
                    var button = resolutionButtons[i];
                    if (button == null) continue;
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => OnResolutionClicked(index));
                }
            }

            if (modeDropdown != null)
            {
                modeDropdown.onValueChanged.RemoveListener(OnModeDropdownChanged);
                modeDropdown.onValueChanged.AddListener(OnModeDropdownChanged);
            }

            if (windowSizeDropdown != null)
            {
                windowSizeDropdown.onValueChanged.RemoveListener(OnWindowSizeDropdownChanged);
                windowSizeDropdown.onValueChanged.AddListener(OnWindowSizeDropdownChanged);
            }
        }

        void Refresh()
        {
            bool windowed = DisplaySettingsStore.Mode == DisplayWindowMode.Windowed;
            if (resolutionRoot != null) resolutionRoot.SetActive(windowed);
            if (statusText != null) statusText.text = "現在: " + DisplaySettingsStore.CurrentResolutionLabel();
            RefreshDropdowns(windowed);

            SetButtonLabelColor(fullscreenButton, !windowed ? selectedColor : normalColor);
            SetButtonLabelColor(windowedButton, windowed ? selectedColor : normalColor);

            if (resolutionButtons == null) return;
            for (int i = 0; i < resolutionButtons.Length; i++)
            {
                var button = resolutionButtons[i];
                if (button == null) continue;
                button.interactable = windowed;
                bool selected = windowed && !DisplaySettingsStore.IsCustomWindowSize && DisplaySettingsStore.PresetIndex == i;
                SetButtonLabelColor(button, windowed ? (selected ? selectedColor : normalColor) : disabledColor);
            }
        }

        void OnFullscreenClicked()
        {
            AudioManager.PlayButtonConfirm();
            DisplaySettingsStore.SetFullscreen();
            ignoreResizeUntil = Time.unscaledTime + 0.7f;
            Refresh();
        }

        void OnWindowedClicked()
        {
            AudioManager.PlayButtonConfirm();
            if (DisplaySettingsStore.IsCustomWindowSize)
            {
                DisplaySettingsStore.SetCustomWindowSize(DisplaySettingsStore.CustomWidth, DisplaySettingsStore.CustomHeight, true);
            }
            else
            {
                int index = Mathf.Clamp(DisplaySettingsStore.PresetIndex, 0, DisplaySettingsStore.Presets.Length - 1);
                DisplaySettingsStore.SetWindowedPreset(index);
            }

            ignoreResizeUntil = Time.unscaledTime + 0.7f;
            Refresh();
        }

        void OnResolutionClicked(int index)
        {
            AudioManager.PlayButtonConfirm();
            DisplaySettingsStore.SetWindowedPreset(index);
            ignoreResizeUntil = Time.unscaledTime + 0.7f;
            Refresh();
        }

        void OnModeDropdownChanged(int value)
        {
            if (value == (int)DisplayWindowMode.Fullscreen)
            {
                OnFullscreenClicked();
                return;
            }

            OnWindowedClicked();
        }

        void OnWindowSizeDropdownChanged(int value)
        {
            if (value < 0 || value >= DisplaySettingsStore.Presets.Length) return;
            OnResolutionClicked(value);
        }

        void RefreshDropdowns(bool windowed)
        {
            if (modeDropdown != null)
            {
                if (modeDropdown.options == null || modeDropdown.options.Count != 2)
                {
                    modeDropdown.ClearOptions();
                    modeDropdown.AddOptions(new List<string> { "フルスクリーン", "ウィンドウ" });
                }

                modeDropdown.SetValueWithoutNotify(windowed ? (int)DisplayWindowMode.Windowed : (int)DisplayWindowMode.Fullscreen);
                modeDropdown.RefreshShownValue();
            }

            if (windowSizeDropdown == null) return;
            var labels = new List<string>();
            for (int i = 0; i < DisplaySettingsStore.Presets.Length; i++)
            {
                labels.Add(DisplaySettingsStore.Presets[i].label);
            }

            int selectedIndex = Mathf.Clamp(DisplaySettingsStore.PresetIndex, 0, DisplaySettingsStore.Presets.Length - 1);
            if (DisplaySettingsStore.IsCustomWindowSize)
            {
                labels.Add("カスタム " + DisplaySettingsStore.CustomWidth + " x " + DisplaySettingsStore.CustomHeight);
                selectedIndex = labels.Count - 1;
            }

            windowSizeDropdown.ClearOptions();
            windowSizeDropdown.AddOptions(labels);
            windowSizeDropdown.interactable = windowed;
            windowSizeDropdown.SetValueWithoutNotify(selectedIndex);
            windowSizeDropdown.RefreshShownValue();
        }

        bool MatchesSelectedPreset(int width, int height)
        {
            if (DisplaySettingsStore.IsCustomWindowSize) return false;
            int index = Mathf.Clamp(DisplaySettingsStore.PresetIndex, 0, DisplaySettingsStore.Presets.Length - 1);
            var preset = DisplaySettingsStore.Presets[index];
            return width == preset.width && height == preset.height;
        }

        static void SetButtonLabelColor(Button button, Color color)
        {
            if (button == null) return;
            var labels = button.GetComponentsInChildren<Text>(true);
            foreach (var label in labels)
            {
                label.color = color;
            }
        }
    }
}
