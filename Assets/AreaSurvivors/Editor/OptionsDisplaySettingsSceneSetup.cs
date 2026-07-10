using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class OptionsDisplaySettingsSceneSetup
    {
        const string OptionsScenePath = "Assets/AreaSurvivors/Scenes/02_Options.unity";
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        static readonly Color PanelColor = new Color(0.035f, 0.04f, 0.05f, 0.90f);
        static readonly Color GroupColor = new Color(0.025f, 0.052f, 0.042f, 0.78f);
        static readonly Color ButtonColor = new Color(0.12f, 0.20f, 0.16f, 0.96f);
        static readonly Color ButtonEdge = new Color(0.56f, 0.65f, 0.42f, 0.96f);
        static readonly Color TextGold = new Color(0.96f, 0.90f, 0.68f);

        public static void Apply()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;

            var optionsScene = EditorSceneManager.OpenScene(OptionsScenePath, OpenSceneMode.Single);
            ApplyOptionsScene(optionsScene);
            EditorSceneManager.MarkSceneDirty(optionsScene);
            EditorSceneManager.SaveScene(optionsScene);

            var gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            ApplyPauseOptionsScene(gameScene);
            EditorSceneManager.MarkSceneDirty(gameScene);
            EditorSceneManager.SaveScene(gameScene);

            if (!string.IsNullOrEmpty(previousScenePath) &&
                previousScenePath != OptionsScenePath &&
                previousScenePath != GameScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Grouped options UI was applied.");
        }

        [MenuItem("Area Survivors/UI/Validate Grouped Options UI")]
        public static void Validate()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            var optionsScene = EditorSceneManager.OpenScene(OptionsScenePath, OpenSceneMode.Single);
            bool valid = ValidateOptionsScene(optionsScene);
            var gameScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            valid &= ValidatePauseOptionsScene(gameScene);

            if (!string.IsNullOrEmpty(previousScenePath) &&
                previousScenePath != OptionsScenePath &&
                previousScenePath != GameScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            if (valid) Debug.Log("Grouped options UI validation passed.");
        }

        static void ApplyOptionsScene(Scene scene)
        {
            var controller = FindRoot(scene, "02_Options Controller");
            var screen = controller != null ? controller.GetComponent<OptionsScreen>() : null;
            var panel = FindChildInScene(scene, "Options Panel");
            if (screen == null || panel == null)
            {
                Debug.LogError("02_Options scene requires OptionsScreen and Options Panel.");
                return;
            }

            var components = RebuildGroupedOptionsPanel(panel, new Vector2(0f, -10f), new Vector2(860f, 950f), "戻る");
            screen.generalOptionsPanel = components.general;
            screen.audioOptionsPanel = components.audio;
            screen.displayOptionsPanel = components.display;
            AssignControlComponents(screen, components.control);
            EditorUtility.SetDirty(screen);
        }

        static void ApplyPauseOptionsScene(Scene scene)
        {
            var manager = FindRoot(scene, "Game Manager");
            var pauseMenu = manager != null ? manager.GetComponent<InGamePauseMenu>() : null;
            var panel = pauseMenu != null && pauseMenu.optionsPanel != null
                ? pauseMenu.optionsPanel.transform
                : FindChildInScene(scene, "Pause Options Panel");
            if (pauseMenu == null || panel == null)
            {
                Debug.LogError("05_Game scene requires InGamePauseMenu and Pause Options Panel.");
                return;
            }

            var components = RebuildGroupedOptionsPanel(panel, Vector2.zero, new Vector2(860f, 950f), "戻る");
            pauseMenu.optionsPanel = panel.gameObject;
            pauseMenu.generalOptionsPanel = components.general;
            pauseMenu.audioOptionsPanel = components.audio;
            pauseMenu.displayOptionsPanel = components.display;
            AssignControlComponents(pauseMenu, components.control);
            EditorUtility.SetDirty(pauseMenu);
        }

        static OptionPanelComponents RebuildGroupedOptionsPanel(Transform panel, Vector2 panelPosition, Vector2 panelSize, string backLabel)
        {
            var panelRect = panel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchoredPosition = panelPosition;
                panelRect.sizeDelta = panelSize;
            }

            var panelImage = panel.GetComponent<Image>();
            if (panelImage != null) panelImage.color = PanelColor;
            UiBoxOutline.Apply(panel, ButtonEdge, 2f);

            ClearChildren(panel);

            var general = Ensure<GeneralOptionsPanel>(panel.gameObject);
            var audio = Ensure<AudioOptionsPanel>(panel.gameObject);
            var display = Ensure<DisplayOptionsPanel>(panel.gameObject);
            EnsurePanelScrollController(panel.gameObject, panelRect, panelRect != null ? panelRect.parent as RectTransform : null);
            CreateText(panel, "Title", "オプション", 36, new Vector2(0f, 232f), new Vector2(420f, 46f), Color.white);
            BuildGeneralGroup(panel, general, 130f);
            BuildSoundGroup(panel, audio, -35f);
            BuildGraphicGroup(panel, display, -240f);
            var control = BuildControlGroup(panel, -430f);

            audio.backButton = CreateButton(panel, "Back Button", backLabel, new Vector2(0f, -800f), new Vector2(230f, 50f), 22);
            return new OptionPanelComponents(general, audio, display, control);
        }

        static void BuildGeneralGroup(Transform parent, GeneralOptionsPanel panel, float y)
        {
            var group = CreateGroup(parent, "General Group", "一般", y, 640f, 90f);
            CreateText(group, "Language Label", "言語", 21, new Vector2(-205f, -20f), new Vector2(130f, 34f), TextGold);
            panel.languageDropdown = CreateDropdown(group, "Language Dropdown", new[] { "日本語" }, new Vector2(95f, -20f), new Vector2(300f, 38f), 18);
        }

        static void BuildSoundGroup(Transform parent, AudioOptionsPanel panel, float y)
        {
            var group = CreateGroup(parent, "Sound Group", "サウンド", y, 640f, 120f);
            CreateText(group, "BgmVolumeLabel", "BGM", 21, new Vector2(-220f, 8f), new Vector2(120f, 34f), TextGold);
            panel.bgmSlider = CreateSlider(group, "BGM Slider", new Vector2(35f, 8f), new Vector2(250f, 20f));
            panel.bgmValueText = CreateText(group, "BGM Value Text", "100%", 20, new Vector2(225f, 8f), new Vector2(90f, 30f), Color.white);

            CreateText(group, "SfxVolumeLabel", "効果音", 21, new Vector2(-220f, -36f), new Vector2(120f, 34f), TextGold);
            panel.sfxSlider = CreateSlider(group, "SFX Slider", new Vector2(35f, -36f), new Vector2(250f, 20f));
            panel.sfxValueText = CreateText(group, "SFX Value Text", "100%", 20, new Vector2(225f, -36f), new Vector2(90f, 30f), Color.white);
        }

        static void BuildGraphicGroup(Transform parent, DisplayOptionsPanel panel, float y)
        {
            var group = CreateGroup(parent, "Graphic Group", "グラフィック", y, 640f, 140f);
            CreateText(group, "Display Mode Label", "表示モード", 21, new Vector2(-205f, 20f), new Vector2(150f, 34f), TextGold);
            panel.modeDropdown = CreateDropdown(group, "Display Mode Dropdown", new[] { "フルスクリーン", "ウィンドウ" }, new Vector2(95f, 20f), new Vector2(300f, 38f), 18);

            CreateText(group, "Window Size Label", "ウィンドウサイズ", 21, new Vector2(-205f, -34f), new Vector2(170f, 34f), TextGold);
            panel.windowSizeDropdown = CreateDropdown(group, "Window Size Dropdown", PresetLabels(), new Vector2(95f, -34f), new Vector2(300f, 38f), 18);
            panel.statusText = CreateText(group, "Display Status Text", "現在: フルスクリーン", 17, new Vector2(95f, -74f), new Vector2(360f, 28f), Color.white);
            panel.resolutionRoot = panel.windowSizeDropdown.gameObject;
            panel.fullscreenButton = null;
            panel.windowedButton = null;
            panel.resolutionButtons = null;
        }

        static ControlPanelComponents BuildControlGroup(Transform parent, float y)
        {
            var group = CreateGroup(parent, "Control Group", "コントロール", y, 640f, 180f);
            CreateText(group, "MoveTitle", "移動", 21, new Vector2(-205f, -8f), new Vector2(130f, 34f), TextGold);
            var up = CreateKeyInputRow(group, "MoveUp", "上", "W", 35f);
            var left = CreateKeyInputRow(group, "MoveLeft", "左", "A", 7f);
            var down = CreateKeyInputRow(group, "MoveDown", "下", "S", -21f);
            var right = CreateKeyInputRow(group, "MoveRight", "右", "D", -49f);
            return new ControlPanelComponents(up, left, down, right);
        }

        static Transform CreateGroup(Transform parent, string name, string title, float y, float width, float height)
        {
            var image = CreateImage(parent, name, GroupColor);
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(width, height);
            UiBoxOutline.Apply(image.transform, ButtonEdge, 2f);
            CreateText(image.transform, title + " Title", title, 24, new Vector2(0f, height * 0.5f - 26f), new Vector2(width - 40f, 34f), TextGold);
            return image.transform;
        }

        static void EnsurePanelScrollController(GameObject panel, RectTransform content, RectTransform viewport)
        {
            if (panel == null || content == null) return;

            var controllerType = Type.GetType("AreaSurvivors.OptionsPanelScrollController, Assembly-CSharp");
            if (controllerType == null)
            {
                Debug.LogWarning("OptionsPanelScrollController type was not found. Panel scroll setup was skipped.");
                return;
            }

            var controller = panel.GetComponent(controllerType);
            if (controller == null) controller = panel.AddComponent(controllerType);

            var serialized = new SerializedObject(controller);
            serialized.FindProperty("content").objectReferenceValue = content;
            serialized.FindProperty("viewport").objectReferenceValue = viewport;
            serialized.FindProperty("scrollSensitivity").floatValue = 64f;
            serialized.FindProperty("dragSensitivity").floatValue = 1f;
            serialized.FindProperty("bottomPadding").floatValue = 32f;
            serialized.FindProperty("resetOnEnable").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        static Dropdown CreateDropdown(Transform parent, string name, string[] options, Vector2 position, Vector2 size, int fontSize)
        {
            var image = CreateImage(parent, name, ButtonColor);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            UiBoxOutline.Apply(image.transform, ButtonEdge, 2f);
            var dropdown = image.gameObject.AddComponent<Dropdown>();
            dropdown.targetGraphic = image;
            dropdown.transition = Selectable.Transition.ColorTint;

            var label = CreateText(image.transform, "Label", options.Length > 0 ? options[0] : string.Empty, fontSize, new Vector2(-12f, 0f), new Vector2(size.x - 44f, size.y), Color.white);
            label.alignment = TextAnchor.MiddleLeft;
            var arrow = CreateText(image.transform, "Arrow", "▼", fontSize - 2, new Vector2(size.x * 0.5f - 22f, 0f), new Vector2(30f, size.y), TextGold);
            arrow.alignment = TextAnchor.MiddleCenter;
            dropdown.captionText = label;

            var template = CreateDropdownTemplate(image.transform, size, fontSize);
            dropdown.template = template;
            dropdown.itemText = FindChild(template, "Item Label").GetComponent<Text>();
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.value = 0;
            dropdown.RefreshShownValue();
            return dropdown;
        }

        static RectTransform CreateDropdownTemplate(Transform parent, Vector2 rootSize, int fontSize)
        {
            var templateImage = CreateImage(parent, "Template", new Color(0.05f, 0.08f, 0.065f, 0.98f));
            var template = templateImage.rectTransform;
            template.anchorMin = new Vector2(0f, 0f);
            template.anchorMax = new Vector2(1f, 0f);
            template.pivot = new Vector2(0.5f, 1f);
            template.anchoredPosition = new Vector2(0f, -2f);
            template.sizeDelta = new Vector2(0f, 156f);
            template.gameObject.SetActive(false);
            UiBoxOutline.Apply(templateImage.transform, ButtonEdge, 2f);

            var viewport = CreateImage(template, "Viewport", new Color(0f, 0f, 0f, 0.12f));
            Stretch(viewport.rectTransform);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, 38f);

            var itemImage = CreateImage(content.transform, "Item", ButtonColor);
            itemImage.rectTransform.anchorMin = new Vector2(0f, 1f);
            itemImage.rectTransform.anchorMax = new Vector2(1f, 1f);
            itemImage.rectTransform.pivot = new Vector2(0.5f, 1f);
            itemImage.rectTransform.anchoredPosition = Vector2.zero;
            itemImage.rectTransform.sizeDelta = new Vector2(0f, rootSize.y);
            var toggle = itemImage.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemImage;
            toggle.transition = Selectable.Transition.ColorTint;

            var itemLabel = CreateText(itemImage.transform, "Item Label", "Option", fontSize, new Vector2(-8f, 0f), new Vector2(rootSize.x - 24f, rootSize.y), Color.white);
            itemLabel.alignment = TextAnchor.MiddleLeft;
            return template;
        }

        static Slider CreateSlider(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var slider = root.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.transition = Selectable.Transition.None;

            var background = CreateImage(root.transform, "Background", new Color(0.05f, 0.07f, 0.08f, 0.95f));
            Stretch(background.rectTransform);
            UiBoxOutline.Apply(root.transform, ButtonEdge, 1f);

            var fillArea = new GameObject("Fill Area").AddComponent<RectTransform>();
            fillArea.SetParent(root.transform, false);
            Stretch(fillArea);
            var fill = CreateImage(fillArea, "Fill", new Color(0.39f, 0.78f, 0.47f, 0.95f));
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(1f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = new GameObject("Handle Slide Area").AddComponent<RectTransform>();
            handleArea.SetParent(root.transform, false);
            Stretch(handleArea);
            var handle = CreateImage(handleArea, "Handle", TextGold);
            handle.rectTransform.sizeDelta = new Vector2(12f, 26f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, int fontSize)
        {
            var image = CreateImage(parent, name, ButtonColor);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            UiBoxOutline.Apply(image.transform, ButtonEdge, 2f);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.114f, 0.529f, 0.298f, 0.98f);
            colors.pressedColor = new Color(0.08f, 0.14f, 0.11f, 0.98f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            CreateText(image.transform, "Label", label, fontSize, Vector2.zero, size, Color.white);
            return button;
        }

        static InputField CreateKeyInputRow(Transform parent, string prefix, string label, string value, float y)
        {
            CreateText(parent, prefix + "Label", label, 21, new Vector2(-20f, y), new Vector2(52f, 26f), TextGold);

            var image = CreateImage(parent, prefix + "InputField", ButtonColor);
            image.rectTransform.anchoredPosition = new Vector2(125f, y);
            image.rectTransform.sizeDelta = new Vector2(170f, 28f);
            UiBoxOutline.Apply(image.transform, ButtonEdge, 2f);

            var input = image.gameObject.AddComponent<InputField>();
            input.transition = Selectable.Transition.ColorTint;
            input.targetGraphic = image;
            input.characterLimit = 1;
            input.contentType = InputField.ContentType.Standard;
            input.lineType = InputField.LineType.SingleLine;
            input.text = value;

            var text = CreateText(image.transform, "Text", value, 21, Vector2.zero, new Vector2(150f, 28f), Color.white);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            input.textComponent = text;
            return input;
        }

        static Text CreateText(Transform parent, string name, string text, int fontSize, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            label.rectTransform.anchoredPosition = position;
            label.rectTransform.sizeDelta = size;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.025f, 0.018f, 0.82f);
            outline.effectDistance = new Vector2(1f, -1f);
            return label;
        }

        static Image CreateImage(Transform parent, string name, Color color)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            return image;
        }

        static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
            }
        }

        static string[] PresetLabels()
        {
            var labels = new string[DisplaySettingsStore.Presets.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = DisplaySettingsStore.Presets[i].label;
            }

            return labels;
        }

        static void AssignControlComponents(OptionsScreen screen, ControlPanelComponents control)
        {
            screen.controlMoveText = null;
            screen.controlAttackText = null;
            screen.controlMoveRebindButton = null;
            screen.controlMoveUpText = control.up != null ? control.up.textComponent : null;
            screen.controlMoveDownText = control.down != null ? control.down.textComponent : null;
            screen.controlMoveLeftText = control.left != null ? control.left.textComponent : null;
            screen.controlMoveRightText = control.right != null ? control.right.textComponent : null;
            screen.controlMoveUpButton = null;
            screen.controlMoveDownButton = null;
            screen.controlMoveLeftButton = null;
            screen.controlMoveRightButton = null;
            screen.controlMoveUpInput = control.up;
            screen.controlMoveDownInput = control.down;
            screen.controlMoveLeftInput = control.left;
            screen.controlMoveRightInput = control.right;
            screen.controlMoveUpAlternateInput = null;
            screen.controlMoveDownAlternateInput = null;
            screen.controlMoveLeftAlternateInput = null;
            screen.controlMoveRightAlternateInput = null;
        }

        static void AssignControlComponents(InGamePauseMenu pauseMenu, ControlPanelComponents control)
        {
            pauseMenu.controlMoveText = null;
            pauseMenu.controlAttackText = null;
            pauseMenu.controlMoveRebindButton = null;
            pauseMenu.controlMoveUpText = control.up != null ? control.up.textComponent : null;
            pauseMenu.controlMoveDownText = control.down != null ? control.down.textComponent : null;
            pauseMenu.controlMoveLeftText = control.left != null ? control.left.textComponent : null;
            pauseMenu.controlMoveRightText = control.right != null ? control.right.textComponent : null;
            pauseMenu.controlMoveUpButton = null;
            pauseMenu.controlMoveDownButton = null;
            pauseMenu.controlMoveLeftButton = null;
            pauseMenu.controlMoveRightButton = null;
            pauseMenu.controlMoveUpInput = control.up;
            pauseMenu.controlMoveDownInput = control.down;
            pauseMenu.controlMoveLeftInput = control.left;
            pauseMenu.controlMoveRightInput = control.right;
            pauseMenu.controlMoveUpAlternateInput = null;
            pauseMenu.controlMoveDownAlternateInput = null;
            pauseMenu.controlMoveLeftAlternateInput = null;
            pauseMenu.controlMoveRightAlternateInput = null;
        }

        static bool ValidateOptionsScene(Scene scene)
        {
            var controller = FindRoot(scene, "02_Options Controller");
            var screen = controller != null ? controller.GetComponent<OptionsScreen>() : null;
            bool valid = screen != null &&
                screen.generalOptionsPanel != null &&
                screen.audioOptionsPanel != null &&
                screen.displayOptionsPanel != null &&
                screen.navigator != null &&
                ValidateOptionComponents(screen.generalOptionsPanel, screen.audioOptionsPanel, screen.displayOptionsPanel) &&
                ValidateControlComponents(screen.controlMoveUpInput, screen.controlMoveDownInput, screen.controlMoveLeftInput, screen.controlMoveRightInput);
            if (!valid) Debug.LogError("02_Options grouped options references are incomplete.");
            return valid;
        }

        static bool ValidatePauseOptionsScene(Scene scene)
        {
            var manager = FindRoot(scene, "Game Manager");
            var pauseMenu = manager != null ? manager.GetComponent<InGamePauseMenu>() : null;
            bool valid = pauseMenu != null &&
                pauseMenu.optionsPanel != null &&
                pauseMenu.generalOptionsPanel != null &&
                pauseMenu.audioOptionsPanel != null &&
                pauseMenu.displayOptionsPanel != null &&
                ValidateOptionComponents(pauseMenu.generalOptionsPanel, pauseMenu.audioOptionsPanel, pauseMenu.displayOptionsPanel) &&
                ValidateControlComponents(pauseMenu.controlMoveUpInput, pauseMenu.controlMoveDownInput, pauseMenu.controlMoveLeftInput, pauseMenu.controlMoveRightInput);
            if (!valid) Debug.LogError("05_Game grouped pause options references are incomplete.");
            return valid;
        }

        static bool ValidateOptionComponents(GeneralOptionsPanel general, AudioOptionsPanel audio, DisplayOptionsPanel display)
        {
            return general.languageDropdown != null &&
                audio.bgmSlider != null &&
                audio.sfxSlider != null &&
                audio.bgmValueText != null &&
                audio.sfxValueText != null &&
                audio.backButton != null &&
                display.modeDropdown != null &&
                display.windowSizeDropdown != null &&
                display.statusText != null;
        }

        static bool ValidateControlComponents(InputField up, InputField down, InputField left, InputField right)
        {
            return up != null &&
                down != null &&
                left != null &&
                right != null &&
                up.textComponent != null &&
                down.textComponent != null &&
                left.textComponent != null &&
                right.textComponent != null;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static T Ensure<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        static Transform FindChildInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindChild(root.transform, name);
                if (found != null) return found;
            }

            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            return null;
        }

        readonly struct OptionPanelComponents
        {
            public readonly GeneralOptionsPanel general;
            public readonly AudioOptionsPanel audio;
            public readonly DisplayOptionsPanel display;
            public readonly ControlPanelComponents control;

            public OptionPanelComponents(GeneralOptionsPanel general, AudioOptionsPanel audio, DisplayOptionsPanel display, ControlPanelComponents control)
            {
                this.general = general;
                this.audio = audio;
                this.display = display;
                this.control = control;
            }
        }

        readonly struct ControlPanelComponents
        {
            public readonly InputField up;
            public readonly InputField left;
            public readonly InputField down;
            public readonly InputField right;

            public ControlPanelComponents(InputField up, InputField left, InputField down, InputField right)
            {
                this.up = up;
                this.left = left;
                this.down = down;
                this.right = right;
            }
        }
    }
}
