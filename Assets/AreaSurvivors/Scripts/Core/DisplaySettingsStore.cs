using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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

        public static void ResetDefaults()
        {
            PlayerPrefs.DeleteKey(ModeKey);
            PlayerPrefs.DeleteKey(PresetKey);
            PlayerPrefs.DeleteKey(CustomWidthKey);
            PlayerPrefs.DeleteKey(CustomHeightKey);
            PlayerPrefs.Save();
            ApplySaved();
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

    public enum KeyboardMouseAction
    {
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight
    }

    public static class InputSettingsStore
    {
        const string Prefix = "AreaSurvivors.Input2.KeyboardMouse.";

        public static Key MoveUp => GetKey(KeyboardMouseAction.MoveUp);
        public static Key MoveDown => GetKey(KeyboardMouseAction.MoveDown);
        public static Key MoveLeft => GetKey(KeyboardMouseAction.MoveLeft);
        public static Key MoveRight => GetKey(KeyboardMouseAction.MoveRight);
        public static Key MoveUpAlternate => GetAlternateKey(KeyboardMouseAction.MoveUp);
        public static Key MoveDownAlternate => GetAlternateKey(KeyboardMouseAction.MoveDown);
        public static Key MoveLeftAlternate => GetAlternateKey(KeyboardMouseAction.MoveLeft);
        public static Key MoveRightAlternate => GetAlternateKey(KeyboardMouseAction.MoveRight);

        public static Vector2 MoveVector()
        {
            float x = Axis(MoveRight, MoveRightAlternate) - Axis(MoveLeft, MoveLeftAlternate);
            float y = Axis(MoveUp, MoveUpAlternate) - Axis(MoveDown, MoveDownAlternate);
            var input = new Vector2(x, y);
            input += ControllerInputSettingsStore.MoveVector();
            if (input.sqrMagnitude > 1f) input.Normalize();
            return input;
        }

        public static Key GetKey(KeyboardMouseAction action)
        {
            return (Key)PlayerPrefs.GetInt(KeyName(action), (int)DefaultKey(action));
        }

        public static void SetKey(KeyboardMouseAction action, Key key)
        {
            PlayerPrefs.SetInt(KeyName(action), (int)key);
            PlayerPrefs.Save();
        }

        public static Key GetAlternateKey(KeyboardMouseAction action)
        {
            return (Key)PlayerPrefs.GetInt(AlternateKeyName(action), (int)DefaultAlternateKey(action));
        }

        public static void SetAlternateKey(KeyboardMouseAction action, Key key)
        {
            PlayerPrefs.SetInt(AlternateKeyName(action), (int)key);
            PlayerPrefs.Save();
        }

        public static void ResetDefaults()
        {
            foreach (KeyboardMouseAction action in System.Enum.GetValues(typeof(KeyboardMouseAction)))
            {
                PlayerPrefs.DeleteKey(KeyName(action));
                PlayerPrefs.DeleteKey(AlternateKeyName(action));
            }

            PlayerPrefs.Save();
            ControllerInputSettingsStore.ResetDefaults();
        }

        public static string MovementLabel()
        {
            return $"上:{KeyLabel(MoveUp)}/{KeyLabel(MoveUpAlternate)}  下:{KeyLabel(MoveDown)}/{KeyLabel(MoveDownAlternate)}  左:{KeyLabel(MoveLeft)}/{KeyLabel(MoveLeftAlternate)}  右:{KeyLabel(MoveRight)}/{KeyLabel(MoveRightAlternate)}";
        }

        public static string KeyLabel(Key key) => AreaInput.KeyLabel(key);

        static float Axis(Key primary, Key alternate)
        {
            bool primaryPressed = AreaInput.KeyIsPressed(primary);
            bool alternatePressed = AreaInput.KeyIsPressed(alternate);
            return primaryPressed || alternatePressed ? 1f : 0f;
        }

        static Key DefaultKey(KeyboardMouseAction action)
        {
            switch (action)
            {
                case KeyboardMouseAction.MoveUp: return Key.W;
                case KeyboardMouseAction.MoveDown: return Key.S;
                case KeyboardMouseAction.MoveLeft: return Key.A;
                case KeyboardMouseAction.MoveRight: return Key.D;
                default: return Key.None;
            }
        }

        static Key DefaultAlternateKey(KeyboardMouseAction action)
        {
            switch (action)
            {
                case KeyboardMouseAction.MoveUp: return Key.UpArrow;
                case KeyboardMouseAction.MoveDown: return Key.DownArrow;
                case KeyboardMouseAction.MoveLeft: return Key.LeftArrow;
                case KeyboardMouseAction.MoveRight: return Key.RightArrow;
                default: return Key.None;
            }
        }

        static string KeyName(KeyboardMouseAction action)
        {
            return Prefix + action;
        }

        static string AlternateKeyName(KeyboardMouseAction action)
        {
            return Prefix + action + ".Alternate";
        }
    }

    public enum ControllerInputKind
    {
        None = 0,
        LeftStickUp = 1,
        LeftStickDown = 2,
        LeftStickLeft = 3,
        LeftStickRight = 4,
        DPadUp = 5,
        DPadDown = 6,
        DPadLeft = 7,
        DPadRight = 8,
        Button = 20
    }

    public readonly struct ControllerInputBinding
    {
        public readonly ControllerInputKind kind;
        public readonly GamepadButton button;

        public ControllerInputBinding(ControllerInputKind kind, GamepadButton button = GamepadButton.South)
        {
            this.kind = kind;
            this.button = button;
        }
    }

    public static class ControllerInputSettingsStore
    {
        const string Prefix = "AreaSurvivors.Input2.Controller.";
        const string SubmitKindKey = Prefix + "Submit.Kind";
        const string SubmitButtonKey = Prefix + "Submit.Button";
        const string CancelKindKey = Prefix + "Cancel.Kind";
        const string CancelButtonKey = Prefix + "Cancel.Button";
        const float AxisThreshold = 0.55f;

        public static Vector2 MoveVector()
        {
            float x = Axis(KeyboardMouseAction.MoveRight) - Axis(KeyboardMouseAction.MoveLeft);
            float y = Axis(KeyboardMouseAction.MoveUp) - Axis(KeyboardMouseAction.MoveDown);
            var input = new Vector2(x, y);
            if (input.sqrMagnitude > 1f) input.Normalize();
            return input;
        }

        public static ControllerInputBinding GetBinding(KeyboardMouseAction action)
        {
            var kind = (ControllerInputKind)PlayerPrefs.GetInt(KindKey(action), (int)DefaultBinding(action).kind);
            var button = (GamepadButton)PlayerPrefs.GetInt(ButtonKey(action), (int)DefaultBinding(action).button);
            return new ControllerInputBinding(kind, button);
        }

        public static ControllerInputBinding GetAlternateBinding(KeyboardMouseAction action)
        {
            var kind = (ControllerInputKind)PlayerPrefs.GetInt(AlternateKindKey(action), (int)DefaultAlternateBinding(action).kind);
            var button = (GamepadButton)PlayerPrefs.GetInt(AlternateButtonKey(action), (int)DefaultAlternateBinding(action).button);
            return new ControllerInputBinding(kind, button);
        }

        public static ControllerInputBinding GetSubmitBinding()
        {
            var kind = (ControllerInputKind)PlayerPrefs.GetInt(SubmitKindKey, (int)DefaultSubmitBinding().kind);
            var button = (GamepadButton)PlayerPrefs.GetInt(SubmitButtonKey, (int)DefaultSubmitBinding().button);
            return new ControllerInputBinding(kind, button);
        }

        public static ControllerInputBinding GetCancelBinding()
        {
            var kind = (ControllerInputKind)PlayerPrefs.GetInt(CancelKindKey, (int)DefaultCancelBinding().kind);
            var button = (GamepadButton)PlayerPrefs.GetInt(CancelButtonKey, (int)DefaultCancelBinding().button);
            return new ControllerInputBinding(kind, button);
        }

        public static void SetBinding(KeyboardMouseAction action, ControllerInputBinding binding)
        {
            PlayerPrefs.SetInt(KindKey(action), (int)binding.kind);
            PlayerPrefs.SetInt(ButtonKey(action), (int)binding.button);
            PlayerPrefs.Save();
        }

        public static void SetAlternateBinding(KeyboardMouseAction action, ControllerInputBinding binding)
        {
            PlayerPrefs.SetInt(AlternateKindKey(action), (int)binding.kind);
            PlayerPrefs.SetInt(AlternateButtonKey(action), (int)binding.button);
            PlayerPrefs.Save();
        }

        public static void SetSubmitBinding(ControllerInputBinding binding)
        {
            PlayerPrefs.SetInt(SubmitKindKey, (int)binding.kind);
            PlayerPrefs.SetInt(SubmitButtonKey, (int)binding.button);
            PlayerPrefs.Save();
        }

        public static void SetCancelBinding(ControllerInputBinding binding)
        {
            PlayerPrefs.SetInt(CancelKindKey, (int)binding.kind);
            PlayerPrefs.SetInt(CancelButtonKey, (int)binding.button);
            PlayerPrefs.Save();
        }

        public static void ResetDefaults()
        {
            foreach (KeyboardMouseAction action in System.Enum.GetValues(typeof(KeyboardMouseAction)))
            {
                PlayerPrefs.DeleteKey(KindKey(action));
                PlayerPrefs.DeleteKey(ButtonKey(action));
                PlayerPrefs.DeleteKey(AlternateKindKey(action));
                PlayerPrefs.DeleteKey(AlternateButtonKey(action));
            }

            PlayerPrefs.DeleteKey(SubmitKindKey);
            PlayerPrefs.DeleteKey(SubmitButtonKey);
            PlayerPrefs.DeleteKey(CancelKindKey);
            PlayerPrefs.DeleteKey(CancelButtonKey);
            PlayerPrefs.Save();
        }

        public static bool SubmitPressed()
        {
            return BindingPressed(GetSubmitBinding());
        }

        public static bool CancelPressed()
        {
            return BindingPressed(GetCancelBinding());
        }

        public static string BindingLabel(ControllerInputBinding binding)
        {
            switch (binding.kind)
            {
                case ControllerInputKind.LeftStickUp: return "左Stick↑";
                case ControllerInputKind.LeftStickDown: return "左Stick↓";
                case ControllerInputKind.LeftStickLeft: return "左Stick←";
                case ControllerInputKind.LeftStickRight: return "左Stick→";
                case ControllerInputKind.DPadUp: return "十字↑";
                case ControllerInputKind.DPadDown: return "十字↓";
                case ControllerInputKind.DPadLeft: return "十字←";
                case ControllerInputKind.DPadRight: return "十字→";
                case ControllerInputKind.Button: return AreaInput.GamepadButtonLabel(binding.button);
                default: return "-";
            }
        }

        public static ControllerInputBinding PressedBinding()
        {
            var stick = AreaInput.LeftStick;
            if (stick.y > AxisThreshold) return new ControllerInputBinding(ControllerInputKind.LeftStickUp);
            if (stick.y < -AxisThreshold) return new ControllerInputBinding(ControllerInputKind.LeftStickDown);
            if (stick.x < -AxisThreshold) return new ControllerInputBinding(ControllerInputKind.LeftStickLeft);
            if (stick.x > AxisThreshold) return new ControllerInputBinding(ControllerInputKind.LeftStickRight);
            var dpad = AreaInput.Dpad;
            if (dpad.y > AxisThreshold) return new ControllerInputBinding(ControllerInputKind.DPadUp);
            if (dpad.y < -AxisThreshold) return new ControllerInputBinding(ControllerInputKind.DPadDown);
            if (dpad.x < -AxisThreshold) return new ControllerInputBinding(ControllerInputKind.DPadLeft);
            if (dpad.x > AxisThreshold) return new ControllerInputBinding(ControllerInputKind.DPadRight);

            var button = AreaInput.PressedGamepadButton();
            if (button.HasValue) return new ControllerInputBinding(ControllerInputKind.Button, button.Value);

            return new ControllerInputBinding(ControllerInputKind.None);
        }

        static float Axis(KeyboardMouseAction action)
        {
            return Mathf.Max(BindingAxis(GetBinding(action)), BindingAxis(GetAlternateBinding(action)));
        }

        static float BindingAxis(ControllerInputBinding binding)
        {
            switch (binding.kind)
            {
                case ControllerInputKind.LeftStickUp:
                    return AreaInput.LeftStick.y > AxisThreshold ? 1f : 0f;
                case ControllerInputKind.LeftStickDown:
                    return AreaInput.LeftStick.y < -AxisThreshold ? 1f : 0f;
                case ControllerInputKind.LeftStickLeft:
                    return AreaInput.LeftStick.x < -AxisThreshold ? 1f : 0f;
                case ControllerInputKind.LeftStickRight:
                    return AreaInput.LeftStick.x > AxisThreshold ? 1f : 0f;
                case ControllerInputKind.DPadUp:
                    return AreaInput.Dpad.y > AxisThreshold ? 1f : 0f;
                case ControllerInputKind.DPadDown:
                    return AreaInput.Dpad.y < -AxisThreshold ? 1f : 0f;
                case ControllerInputKind.DPadLeft:
                    return AreaInput.Dpad.x < -AxisThreshold ? 1f : 0f;
                case ControllerInputKind.DPadRight:
                    return AreaInput.Dpad.x > AxisThreshold ? 1f : 0f;
                case ControllerInputKind.Button:
                    return AreaInput.GamepadButtonIsPressed(binding.button) ? 1f : 0f;
                default:
                    return 0f;
            }
        }

        static bool BindingPressed(ControllerInputBinding binding)
        {
            switch (binding.kind)
            {
                case ControllerInputKind.Button:
                    return AreaInput.GamepadButtonPressedThisFrame(binding.button);
                case ControllerInputKind.LeftStickUp:
                case ControllerInputKind.LeftStickDown:
                case ControllerInputKind.LeftStickLeft:
                case ControllerInputKind.LeftStickRight:
                case ControllerInputKind.DPadUp:
                case ControllerInputKind.DPadDown:
                case ControllerInputKind.DPadLeft:
                case ControllerInputKind.DPadRight:
                    return BindingAxis(binding) > 0f;
                default:
                    return false;
            }
        }

        static ControllerInputBinding DefaultBinding(KeyboardMouseAction action)
        {
            switch (action)
            {
                case KeyboardMouseAction.MoveUp: return new ControllerInputBinding(ControllerInputKind.LeftStickUp);
                case KeyboardMouseAction.MoveDown: return new ControllerInputBinding(ControllerInputKind.LeftStickDown);
                case KeyboardMouseAction.MoveLeft: return new ControllerInputBinding(ControllerInputKind.LeftStickLeft);
                case KeyboardMouseAction.MoveRight: return new ControllerInputBinding(ControllerInputKind.LeftStickRight);
                default: return new ControllerInputBinding(ControllerInputKind.None);
            }
        }

        static ControllerInputBinding DefaultAlternateBinding(KeyboardMouseAction action)
        {
            switch (action)
            {
                case KeyboardMouseAction.MoveUp: return new ControllerInputBinding(ControllerInputKind.DPadUp);
                case KeyboardMouseAction.MoveDown: return new ControllerInputBinding(ControllerInputKind.DPadDown);
                case KeyboardMouseAction.MoveLeft: return new ControllerInputBinding(ControllerInputKind.DPadLeft);
                case KeyboardMouseAction.MoveRight: return new ControllerInputBinding(ControllerInputKind.DPadRight);
                default: return new ControllerInputBinding(ControllerInputKind.None);
            }
        }

        static ControllerInputBinding DefaultSubmitBinding()
        {
            return new ControllerInputBinding(ControllerInputKind.Button, GamepadButton.South);
        }

        static ControllerInputBinding DefaultCancelBinding()
        {
            return new ControllerInputBinding(ControllerInputKind.Button, GamepadButton.East);
        }

        static string KindKey(KeyboardMouseAction action)
        {
            return Prefix + action + ".Kind";
        }

        static string ButtonKey(KeyboardMouseAction action)
        {
            return Prefix + action + ".Button";
        }

        static string AlternateKindKey(KeyboardMouseAction action)
        {
            return Prefix + action + ".Alternate.Kind";
        }

        static string AlternateButtonKey(KeyboardMouseAction action)
        {
            return Prefix + action + ".Alternate.Button";
        }
    }
}
