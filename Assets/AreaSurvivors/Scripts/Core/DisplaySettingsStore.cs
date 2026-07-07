using UnityEngine;

namespace AreaSurvivors
{
    public enum DisplayWindowMode
    {
        Fullscreen = 0,
        Windowed = 1
    }

    public static class DisplaySettingsStore
    {
        public struct ResolutionPreset
        {
            public readonly int width;
            public readonly int height;
            public readonly string label;

            public ResolutionPreset(int width, int height, string label)
            {
                this.width = width;
                this.height = height;
                this.label = label;
            }
        }

        const string ModeKey = "AreaSurvivors.Display.Mode";
        const string PresetKey = "AreaSurvivors.Display.Preset";
        const string CustomWidthKey = "AreaSurvivors.Display.CustomWidth";
        const string CustomHeightKey = "AreaSurvivors.Display.CustomHeight";
        const int CustomPresetIndex = -1;
        const int DefaultPresetIndex = 1;

        public static readonly ResolutionPreset[] Presets =
        {
            new ResolutionPreset(960, 540, "960 x 540"),
            new ResolutionPreset(1280, 720, "1280 x 720"),
            new ResolutionPreset(1600, 900, "1600 x 900"),
            new ResolutionPreset(1920, 1080, "1920 x 1080")
        };

        public static DisplayWindowMode Mode => (DisplayWindowMode)PlayerPrefs.GetInt(ModeKey, (int)DisplayWindowMode.Fullscreen);
        public static int PresetIndex => PlayerPrefs.GetInt(PresetKey, DefaultPresetIndex);
        public static bool IsCustomWindowSize => PresetIndex == CustomPresetIndex;
        public static int CustomWidth => Mathf.Max(320, PlayerPrefs.GetInt(CustomWidthKey, Presets[DefaultPresetIndex].width));
        public static int CustomHeight => Mathf.Max(240, PlayerPrefs.GetInt(CustomHeightKey, Presets[DefaultPresetIndex].height));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySavedOnStartup()
        {
            ApplySaved();
        }

        public static void ApplySaved()
        {
            if (Mode == DisplayWindowMode.Fullscreen)
            {
                ApplyFullscreen();
                return;
            }

            GetSavedWindowSize(out int width, out int height);
            ApplyWindowed(width, height);
        }

        public static void SetFullscreen()
        {
            PlayerPrefs.SetInt(ModeKey, (int)DisplayWindowMode.Fullscreen);
            PlayerPrefs.Save();
            ApplyFullscreen();
        }

        public static void SetWindowedPreset(int presetIndex)
        {
            presetIndex = Mathf.Clamp(presetIndex, 0, Presets.Length - 1);
            var preset = Presets[presetIndex];
            PlayerPrefs.SetInt(ModeKey, (int)DisplayWindowMode.Windowed);
            PlayerPrefs.SetInt(PresetKey, presetIndex);
            PlayerPrefs.SetInt(CustomWidthKey, preset.width);
            PlayerPrefs.SetInt(CustomHeightKey, preset.height);
            PlayerPrefs.Save();
            ApplyWindowed(preset.width, preset.height);
        }

        public static void SetCustomWindowSize(int width, int height, bool apply)
        {
            width = Mathf.Max(320, width);
            height = Mathf.Max(240, height);
            PlayerPrefs.SetInt(ModeKey, (int)DisplayWindowMode.Windowed);
            PlayerPrefs.SetInt(PresetKey, CustomPresetIndex);
            PlayerPrefs.SetInt(CustomWidthKey, width);
            PlayerPrefs.SetInt(CustomHeightKey, height);
            PlayerPrefs.Save();
            if (apply) ApplyWindowed(width, height);
        }

        public static string CurrentResolutionLabel()
        {
            if (Mode == DisplayWindowMode.Fullscreen) return "フルスクリーン";
            if (IsCustomWindowSize) return $"カスタム {CustomWidth} x {CustomHeight}";

            int index = Mathf.Clamp(PresetIndex, 0, Presets.Length - 1);
            return Presets[index].label;
        }

        static void GetSavedWindowSize(out int width, out int height)
        {
            if (IsCustomWindowSize)
            {
                width = CustomWidth;
                height = CustomHeight;
                return;
            }

            int index = Mathf.Clamp(PresetIndex, 0, Presets.Length - 1);
            width = Presets[index].width;
            height = Presets[index].height;
        }

        static void ApplyFullscreen()
        {
            var resolution = Screen.currentResolution;
            int width = Mathf.Max(640, resolution.width);
            int height = Mathf.Max(360, resolution.height);
            Screen.SetResolution(width, height, FullScreenMode.FullScreenWindow);
        }

        static void ApplyWindowed(int width, int height)
        {
            Screen.SetResolution(Mathf.Max(320, width), Mathf.Max(240, height), FullScreenMode.Windowed);
        }
    }
}
