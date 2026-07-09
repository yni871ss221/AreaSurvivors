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
        const string Prefix = "AreaSurvivors.Input.KeyboardMouse.";

        public static KeyCode MoveUp => GetKey(KeyboardMouseAction.MoveUp);
        public static KeyCode MoveDown => GetKey(KeyboardMouseAction.MoveDown);
        public static KeyCode MoveLeft => GetKey(KeyboardMouseAction.MoveLeft);
        public static KeyCode MoveRight => GetKey(KeyboardMouseAction.MoveRight);
        public static KeyCode MoveUpAlternate => GetAlternateKey(KeyboardMouseAction.MoveUp);
        public static KeyCode MoveDownAlternate => GetAlternateKey(KeyboardMouseAction.MoveDown);
        public static KeyCode MoveLeftAlternate => GetAlternateKey(KeyboardMouseAction.MoveLeft);
        public static KeyCode MoveRightAlternate => GetAlternateKey(KeyboardMouseAction.MoveRight);

        public static Vector2 MoveVector()
        {
            float x = Axis(MoveRight, MoveRightAlternate) - Axis(MoveLeft, MoveLeftAlternate);
            float y = Axis(MoveUp, MoveUpAlternate) - Axis(MoveDown, MoveDownAlternate);
            var input = new Vector2(x, y);
            input += ControllerInputSettingsStore.MoveVector();
            if (input.sqrMagnitude > 1f) input.Normalize();
            return input;
        }

        public static KeyCode GetKey(KeyboardMouseAction action)
        {
            return (KeyCode)PlayerPrefs.GetInt(Key(action), (int)DefaultKey(action));
        }

        public static void SetKey(KeyboardMouseAction action, KeyCode keyCode)
        {
            PlayerPrefs.SetInt(Key(action), (int)keyCode);
            PlayerPrefs.Save();
        }

        public static KeyCode GetAlternateKey(KeyboardMouseAction action)
        {
            return (KeyCode)PlayerPrefs.GetInt(AlternateKey(action), (int)DefaultAlternateKey(action));
        }

        public static void SetAlternateKey(KeyboardMouseAction action, KeyCode keyCode)
        {
            PlayerPrefs.SetInt(AlternateKey(action), (int)keyCode);
            PlayerPrefs.Save();
        }

        public static void ResetDefaults()
        {
            foreach (KeyboardMouseAction action in System.Enum.GetValues(typeof(KeyboardMouseAction)))
            {
                PlayerPrefs.DeleteKey(Key(action));
                PlayerPrefs.DeleteKey(AlternateKey(action));
            }

            PlayerPrefs.Save();
            ControllerInputSettingsStore.ResetDefaults();
        }

        public static string MovementLabel()
        {
            return $"上:{KeyLabel(MoveUp)}/{KeyLabel(MoveUpAlternate)}  下:{KeyLabel(MoveDown)}/{KeyLabel(MoveDownAlternate)}  左:{KeyLabel(MoveLeft)}/{KeyLabel(MoveLeftAlternate)}  右:{KeyLabel(MoveRight)}/{KeyLabel(MoveRightAlternate)}";
        }

        public static string KeyLabel(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.Space: return "Space";
                case KeyCode.Return: return "Enter";
                case KeyCode.LeftShift: return "LShift";
                case KeyCode.RightShift: return "RShift";
                case KeyCode.LeftControl: return "LCtrl";
                case KeyCode.RightControl: return "RCtrl";
                case KeyCode.LeftAlt: return "LAlt";
                case KeyCode.RightAlt: return "RAlt";
                case KeyCode.JoystickButton0: return "□";
                case KeyCode.JoystickButton1: return "×";
                case KeyCode.JoystickButton2: return "〇";
                case KeyCode.JoystickButton3: return "△";
                default: return keyCode.ToString();
            }
        }

        static float Axis(KeyCode primary, KeyCode alternate)
        {
            bool primaryPressed = primary != KeyCode.None && Input.GetKey(primary);
            bool alternatePressed = alternate != KeyCode.None && Input.GetKey(alternate);
            return primaryPressed || alternatePressed ? 1f : 0f;
        }

        static KeyCode DefaultKey(KeyboardMouseAction action)
        {
            switch (action)
            {
                case KeyboardMouseAction.MoveUp: return KeyCode.W;
                case KeyboardMouseAction.MoveDown: return KeyCode.S;
                case KeyboardMouseAction.MoveLeft: return KeyCode.A;
                case KeyboardMouseAction.MoveRight: return KeyCode.D;
                default: return KeyCode.None;
            }
        }

        static KeyCode DefaultAlternateKey(KeyboardMouseAction action)
        {
            switch (action)
            {
                case KeyboardMouseAction.MoveUp: return KeyCode.UpArrow;
                case KeyboardMouseAction.MoveDown: return KeyCode.DownArrow;
                case KeyboardMouseAction.MoveLeft: return KeyCode.LeftArrow;
                case KeyboardMouseAction.MoveRight: return KeyCode.RightArrow;
                default: return KeyCode.None;
            }
        }

        static string Key(KeyboardMouseAction action)
        {
            return Prefix + action;
        }

        static string AlternateKey(KeyboardMouseAction action)
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
        public readonly KeyCode button;

        public ControllerInputBinding(ControllerInputKind kind, KeyCode button = KeyCode.None)
        {
            this.kind = kind;
            this.button = button;
        }
    }

    public static class ControllerInputSettingsStore
    {
        const string Prefix = "AreaSurvivors.Input.Controller.";
        const string SubmitKindKey = Prefix + "Submit.Kind";
        const string SubmitButtonKey = Prefix + "Submit.Button";
        const string CancelKindKey = Prefix + "Cancel.Kind";
        const string CancelButtonKey = Prefix + "Cancel.Button";
        const string ControllerHorizontalAxis = "ControllerHorizontal";
        const string ControllerVerticalAxis = "ControllerVertical";
        const string ControllerDPadHorizontalAxis = "ControllerDPadHorizontal";
        const string ControllerDPadVerticalAxis = "ControllerDPadVertical";
        const string ControllerDPadVerticalFallbackAxis = "ControllerDPadVerticalFallback";
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
            var button = (KeyCode)PlayerPrefs.GetInt(ButtonKey(action), (int)DefaultBinding(action).button);
            return new ControllerInputBinding(kind, button);
        }

        public static ControllerInputBinding GetAlternateBinding(KeyboardMouseAction action)
        {
            var kind = (ControllerInputKind)PlayerPrefs.GetInt(AlternateKindKey(action), (int)DefaultAlternateBinding(action).kind);
            var button = (KeyCode)PlayerPrefs.GetInt(AlternateButtonKey(action), (int)DefaultAlternateBinding(action).button);
            return new ControllerInputBinding(kind, button);
        }

        public static ControllerInputBinding GetSubmitBinding()
        {
            var kind = (ControllerInputKind)PlayerPrefs.GetInt(SubmitKindKey, (int)DefaultSubmitBinding().kind);
            var button = (KeyCode)PlayerPrefs.GetInt(SubmitButtonKey, (int)DefaultSubmitBinding().button);
            return new ControllerInputBinding(kind, button);
        }

        public static ControllerInputBinding GetCancelBinding()
        {
            var kind = (ControllerInputKind)PlayerPrefs.GetInt(CancelKindKey, (int)DefaultCancelBinding().kind);
            var button = (KeyCode)PlayerPrefs.GetInt(CancelButtonKey, (int)DefaultCancelBinding().button);
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
                case ControllerInputKind.Button: return InputSettingsStore.KeyLabel(binding.button);
                default: return "-";
            }
        }

        public static ControllerInputBinding PressedBinding()
        {
            if (Input.GetAxisRaw(ControllerVerticalAxis) > AxisThreshold) return new ControllerInputBinding(ControllerInputKind.LeftStickUp);
            if (Input.GetAxisRaw(ControllerVerticalAxis) < -AxisThreshold) return new ControllerInputBinding(ControllerInputKind.LeftStickDown);
            if (Input.GetAxisRaw(ControllerHorizontalAxis) < -AxisThreshold) return new ControllerInputBinding(ControllerInputKind.LeftStickLeft);
            if (Input.GetAxisRaw(ControllerHorizontalAxis) > AxisThreshold) return new ControllerInputBinding(ControllerInputKind.LeftStickRight);
            if (DPadVerticalRaw() > AxisThreshold) return new ControllerInputBinding(ControllerInputKind.DPadUp);
            if (DPadVerticalRaw() < -AxisThreshold) return new ControllerInputBinding(ControllerInputKind.DPadDown);
            if (Input.GetAxisRaw(ControllerDPadHorizontalAxis) < -AxisThreshold) return new ControllerInputBinding(ControllerInputKind.DPadLeft);
            if (Input.GetAxisRaw(ControllerDPadHorizontalAxis) > AxisThreshold) return new ControllerInputBinding(ControllerInputKind.DPadRight);

            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (key < KeyCode.JoystickButton0 || key > KeyCode.Joystick8Button19) continue;
                if (Input.GetKeyDown(key)) return new ControllerInputBinding(ControllerInputKind.Button, key);
            }

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
                    return Input.GetAxisRaw(ControllerVerticalAxis) > AxisThreshold ? 1f : 0f;
                case ControllerInputKind.LeftStickDown:
                    return Input.GetAxisRaw(ControllerVerticalAxis) < -AxisThreshold ? 1f : 0f;
                case ControllerInputKind.LeftStickLeft:
                    return Input.GetAxisRaw(ControllerHorizontalAxis) < -AxisThreshold ? 1f : 0f;
                case ControllerInputKind.LeftStickRight:
                    return Input.GetAxisRaw(ControllerHorizontalAxis) > AxisThreshold ? 1f : 0f;
                case ControllerInputKind.DPadUp:
                    return DPadVerticalRaw() > AxisThreshold ? 1f : 0f;
                case ControllerInputKind.DPadDown:
                    return DPadVerticalRaw() < -AxisThreshold ? 1f : 0f;
                case ControllerInputKind.DPadLeft:
                    return Input.GetAxisRaw(ControllerDPadHorizontalAxis) < -AxisThreshold ? 1f : 0f;
                case ControllerInputKind.DPadRight:
                    return Input.GetAxisRaw(ControllerDPadHorizontalAxis) > AxisThreshold ? 1f : 0f;
                case ControllerInputKind.Button:
                    return binding.button != KeyCode.None && Input.GetKey(binding.button) ? 1f : 0f;
                default:
                    return 0f;
            }
        }

        static bool BindingPressed(ControllerInputBinding binding)
        {
            switch (binding.kind)
            {
                case ControllerInputKind.Button:
                    return binding.button != KeyCode.None && Input.GetKeyDown(binding.button);
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

        static float DPadVerticalRaw()
        {
            float primary = Input.GetAxisRaw(ControllerDPadVerticalAxis);
            if (Mathf.Abs(primary) > AxisThreshold) return primary;
            return Input.GetAxisRaw(ControllerDPadVerticalFallbackAxis);
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
            return new ControllerInputBinding(ControllerInputKind.Button, KeyCode.JoystickButton1);
        }

        static ControllerInputBinding DefaultCancelBinding()
        {
            return new ControllerInputBinding(ControllerInputKind.Button, KeyCode.JoystickButton2);
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
