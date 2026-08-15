using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace AreaSurvivors
{
    public static class AreaInput
    {
        const float ScrollUnitsPerStep = 120f;

        static Gamepad CurrentGamepad
        {
            get
            {
                var current = Gamepad.current;
                if (current != null && current.enabled) return current;

                for (int i = 0; i < Gamepad.all.Count; i++)
                {
                    var candidate = Gamepad.all[i];
                    if (candidate != null && candidate.enabled) return candidate;
                }

                return null;
            }
        }

        public static Vector2 PointerPosition => Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : Vector2.zero;

        public static float ScrollY => Mouse.current != null
            ? Mouse.current.scroll.ReadValue().y / ScrollUnitsPerStep
            : 0f;

        public static bool MouseButtonPressedThisFrame(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;

            switch (button)
            {
                case 0: return mouse.leftButton.wasPressedThisFrame;
                case 1: return mouse.rightButton.wasPressedThisFrame;
                case 2: return mouse.middleButton.wasPressedThisFrame;
                default: return false;
            }
        }

        public static bool MouseButtonIsPressed(int button)
        {
            var mouse = Mouse.current;
            if (mouse == null) return false;

            switch (button)
            {
                case 0: return mouse.leftButton.isPressed;
                case 1: return mouse.rightButton.isPressed;
                case 2: return mouse.middleButton.isPressed;
                default: return false;
            }
        }

        public static bool KeyPressedThisFrame(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        public static bool KeyIsPressed(Key key)
        {
            var keyboard = Keyboard.current;
            return keyboard != null && key != Key.None && keyboard[key].isPressed;
        }

        public static bool AnyKeyboardKeyPressedThisFrame()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.anyKey.wasPressedThisFrame;
        }

        public static bool AnyInputPressedThisFrame()
        {
            if (AnyKeyboardKeyPressedThisFrame()) return true;
            if (MouseButtonPressedThisFrame(0) || MouseButtonPressedThisFrame(1) || MouseButtonPressedThisFrame(2)) return true;
            return AnyGamepadButtonPressedThisFrame();
        }

        public static Vector2 LeftStick
        {
            get
            {
                var gamepad = CurrentGamepad;
                return gamepad != null ? gamepad.leftStick.ReadValue() : Vector2.zero;
            }
        }

        public static Vector2 Dpad
        {
            get
            {
                var gamepad = CurrentGamepad;
                return gamepad != null ? gamepad.dpad.ReadValue() : Vector2.zero;
            }
        }

        public static bool GamepadButtonPressedThisFrame(GamepadButton button)
        {
            var gamepad = CurrentGamepad;
            return gamepad != null && gamepad[button].wasPressedThisFrame;
        }

        public static bool GamepadButtonIsPressed(GamepadButton button)
        {
            var gamepad = CurrentGamepad;
            return gamepad != null && gamepad[button].isPressed;
        }

        public static bool AnyGamepadButtonPressedThisFrame()
        {
            var gamepad = CurrentGamepad;
            if (gamepad != null)
            {
                foreach (var control in gamepad.allControls)
                {
                    if (control is ButtonControl button && button.wasPressedThisFrame) return true;
                }

                return false;
            }

            return false;
        }

        public static GamepadButton? PressedGamepadButton()
        {
            foreach (GamepadButton button in System.Enum.GetValues(typeof(GamepadButton)))
            {
                if (IsDirectionalButton(button)) continue;
                if (GamepadButtonPressedThisFrame(button)) return button;
            }

            return null;
        }

        public static string KeyLabel(Key key)
        {
            switch (key)
            {
                case Key.UpArrow: return "↑";
                case Key.DownArrow: return "↓";
                case Key.LeftArrow: return "←";
                case Key.RightArrow: return "→";
                case Key.Space: return "Space";
                case Key.Enter: return "Enter";
                case Key.NumpadEnter: return "Numpad Enter";
                case Key.LeftShift: return "LShift";
                case Key.RightShift: return "RShift";
                case Key.LeftCtrl: return "LCtrl";
                case Key.RightCtrl: return "RCtrl";
                case Key.LeftAlt: return "LAlt";
                case Key.RightAlt: return "RAlt";
                default: return key.ToString();
            }
        }

        public static string GamepadButtonLabel(GamepadButton button)
        {
            switch (button)
            {
                case GamepadButton.South: return "下ボタン";
                case GamepadButton.East: return "右ボタン";
                case GamepadButton.West: return "左ボタン";
                case GamepadButton.North: return "上ボタン";
                case GamepadButton.LeftShoulder: return "L1/LB";
                case GamepadButton.RightShoulder: return "R1/RB";
                case GamepadButton.LeftTrigger: return "L2/LT";
                case GamepadButton.RightTrigger: return "R2/RT";
                case GamepadButton.LeftStick: return "左Stick押込";
                case GamepadButton.RightStick: return "右Stick押込";
                case GamepadButton.Select: return "Select/View";
                case GamepadButton.Start: return "Start/Menu";
                default: return button.ToString();
            }
        }

        static bool IsDirectionalButton(GamepadButton button)
        {
            return button == GamepadButton.DpadUp
                || button == GamepadButton.DpadDown
                || button == GamepadButton.DpadLeft
                || button == GamepadButton.DpadRight;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallDeviceDiagnostics()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            LogDeviceSnapshot("startup");
        }

        static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            Debug.Log($"[AreaInput] device change={change}, name={device?.name}, layout={device?.layout}, enabled={device?.enabled}");
            LogDeviceSnapshot("device-change");
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            LogDeviceSnapshot($"scene-loaded:{scene.name}");
        }

        static void LogDeviceSnapshot(string reason)
        {
            Gamepad gamepad = CurrentGamepad;
            string route = gamepad != null ? "input-system" : "none";
            InputDeviceDescription description = gamepad?.description ?? default;
            Debug.Log($"[AreaInput] {reason}, devices={InputSystem.devices.Count}, gamepads={Gamepad.all.Count}, " +
                $"current={Gamepad.current?.name ?? "(none)"}, selected={gamepad?.name ?? "(none)"}, " +
                $"route={route}, " +
                $"layout={gamepad?.layout ?? "(none)"}, interface={description.interfaceName ?? "(none)"}, " +
                $"product={description.product ?? "(none)"}, disconnected={InputSystem.disconnectedDevices.Count}");
        }
    }
}
