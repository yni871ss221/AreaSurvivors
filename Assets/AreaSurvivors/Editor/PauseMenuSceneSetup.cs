using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class PauseMenuSceneSetup
    {
        const string OptionsScenePath = "Assets/AreaSurvivors/Scenes/02_Options.unity";
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        static readonly Color PanelColor = new Color(0.035f, 0.04f, 0.05f, 0.88f);
        static readonly Color DialogColor = new Color(0.04f, 0.035f, 0.035f, 0.94f);
        static readonly Color ButtonColor = new Color(0.12f, 0.20f, 0.16f, 0.96f);
        static readonly Color ButtonEdge = new Color(0.56f, 0.65f, 0.42f, 0.96f);
        static readonly Color TextGold = new Color(0.96f, 0.90f, 0.68f);

        public static void ApplyAll()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            ApplyOptionsScene();
            ApplyGameScene();
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != OptionsScenePath && previousScenePath != GameScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Pause menu and scene-authored options UI were applied.");
        }

        [MenuItem("Area Survivors/UI/Validate Pause And Options UI")]
        public static void ValidateAll()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            bool valid = ValidateOptionsScene();
            valid &= ValidateGameScene();
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != OptionsScenePath && previousScenePath != GameScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            if (valid) Debug.Log("Pause menu and scene-authored options UI validation passed.");
        }

        static void ApplyOptionsScene()
        {
            var scene = EditorSceneManager.OpenScene(OptionsScenePath, OpenSceneMode.Single);
            DestroyRoot(scene, "Options UI");
            DestroyRoot(scene, "Main Camera");
            DestroyRoot(scene, "EventSystem");

            var camera = CreateCamera(scene, new Color(0.09f, 0.10f, 0.13f));
            var canvas = CreateCanvas(scene, "Options UI", 0);
            CreateEventSystem(scene);
            CreateBackground(canvas.transform, "Generated/UI/UpgradeBackground", new Color(0f, 0f, 0f, 0.24f));

            var panel = CreatePanel(canvas.transform, "Options Panel", new Vector2(0, 28), new Vector2(620, 430), PanelColor);
            var optionsPanel = panel.gameObject.AddComponent<AudioOptionsPanel>();
            ConfigureFullOptionsPanel(panel.transform, optionsPanel, true);

            var controller = FindRoot(scene, "02_Options Controller") ?? new GameObject("02_Options Controller");
            SceneManager.MoveGameObjectToScene(controller, scene);
            var optionsScreen = Ensure<OptionsScreen>(controller);
            var navigator = Ensure<SceneNavigator>(controller);
            optionsScreen.audioOptionsPanel = optionsPanel;
            optionsScreen.navigator = navigator;
            _ = camera;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ApplyGameScene()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            DestroyRoot(scene, "Pause Menu UI");

            var canvas = CreateCanvas(scene, "Pause Menu UI", 80);
            CreateEventSystem(scene);

            var menuPanel = CreatePanel(canvas.transform, "Pause Menu Panel", Vector2.zero, new Vector2(360, 330), PanelColor);
            CreateText(menuPanel.transform, "Title", "一時停止", 34, new Vector2(0, 112), new Vector2(300, 46), Color.white);
            var optionsButton = CreateButton(menuPanel.transform, "Options Button", "オプション", new Vector2(0, 34), new Vector2(240, 54));
            var abandonButton = CreateButton(menuPanel.transform, "Abandon Button", "諦める", new Vector2(0, -36), new Vector2(240, 54));
            var resumeButton = CreateButton(menuPanel.transform, "Resume Button", "再開", new Vector2(0, -106), new Vector2(240, 54));

            var optionsPanel = CreatePanel(canvas.transform, "Pause Options Panel", Vector2.zero, new Vector2(620, 430), PanelColor);
            var audioOptionsPanel = optionsPanel.gameObject.AddComponent<AudioOptionsPanel>();
            ConfigureFullOptionsPanel(optionsPanel.transform, audioOptionsPanel, true);

            var dialog = CreatePanel(canvas.transform, "Abandon Confirm Dialog", Vector2.zero, new Vector2(470, 250), DialogColor);
            CreateText(dialog.transform, "Message", "獲得したトークンを失いますが、諦めますか？", 24, new Vector2(0, 52), new Vector2(390, 76), Color.white);
            var confirmBackButton = CreateButton(dialog.transform, "Confirm Back Button", "戻る", new Vector2(-100, -70), new Vector2(160, 50));
            var confirmAbandonButton = CreateButton(dialog.transform, "Confirm Abandon Button", "諦める", new Vector2(100, -70), new Vector2(160, 50));

            menuPanel.gameObject.SetActive(false);
            optionsPanel.gameObject.SetActive(false);
            dialog.gameObject.SetActive(false);

            var manager = FindRoot(scene, "Game Manager");
            if (manager == null)
            {
                Debug.LogError("Game Manager was not found in 05_Game.unity. Pause menu references were not assigned.");
            }
            else
            {
                var pauseMenu = Ensure<InGamePauseMenu>(manager);
                pauseMenu.menuPanel = menuPanel.gameObject;
                pauseMenu.optionsPanel = optionsPanel.gameObject;
                pauseMenu.abandonDialog = dialog.gameObject;
                pauseMenu.audioOptionsPanel = audioOptionsPanel;
                pauseMenu.optionsButton = optionsButton;
                pauseMenu.abandonButton = abandonButton;
                pauseMenu.resumeButton = resumeButton;
                pauseMenu.confirmBackButton = confirmBackButton;
                pauseMenu.confirmAbandonButton = confirmAbandonButton;
                EditorUtility.SetDirty(manager);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static bool ValidateOptionsScene()
        {
            var scene = EditorSceneManager.OpenScene(OptionsScenePath, OpenSceneMode.Single);
            var controller = FindRoot(scene, "02_Options Controller");
            var screen = controller != null ? controller.GetComponent<OptionsScreen>() : null;
            bool valid = screen != null &&
                screen.audioOptionsPanel != null &&
                screen.navigator != null &&
                screen.audioOptionsPanel.bgmSlider != null &&
                screen.audioOptionsPanel.sfxSlider != null &&
                screen.audioOptionsPanel.backButton != null;
            if (!valid) Debug.LogError("02_Options scene-authored options references are incomplete.");
            return valid;
        }

        static bool ValidateGameScene()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var manager = FindRoot(scene, "Game Manager");
            var pauseMenu = manager != null ? manager.GetComponent<InGamePauseMenu>() : null;
            bool valid = pauseMenu != null &&
                pauseMenu.menuPanel != null &&
                pauseMenu.optionsPanel != null &&
                pauseMenu.abandonDialog != null &&
                pauseMenu.audioOptionsPanel != null &&
                pauseMenu.audioOptionsPanel.bgmSlider != null &&
                pauseMenu.audioOptionsPanel.sfxSlider != null &&
                pauseMenu.audioOptionsPanel.backButton != null &&
                pauseMenu.optionsButton != null &&
                pauseMenu.abandonButton != null &&
                pauseMenu.resumeButton != null &&
                pauseMenu.confirmBackButton != null &&
                pauseMenu.confirmAbandonButton != null;
            if (!valid) Debug.LogError("05_Game pause menu references are incomplete.");
            return valid;
        }

        static Camera CreateCamera(Scene scene, Color background)
        {
            var cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.tag = "MainCamera";
            return camera;
        }

        static Canvas CreateCanvas(Scene scene, string name, int sortingOrder)
        {
            var canvasObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static void CreateEventSystem(Scene scene)
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        static void CreateBackground(Transform parent, string spriteResource, Color shadeColor)
        {
            var sprite = Resources.Load<Sprite>(spriteResource);
            if (sprite != null)
            {
                var background = CreateImage(parent, "Background", Color.white);
                background.sprite = sprite;
                Stretch(background.rectTransform);
            }

            var shade = CreateImage(parent, "Vignette", shadeColor);
            Stretch(shade.rectTransform);
        }

        static Image CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var image = CreateImage(parent, name, color);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            UiBoxOutline.Apply(image.transform, ButtonEdge, 2f);
            return image;
        }

        static void ConfigureFullOptionsPanel(Transform parent, AudioOptionsPanel panel, bool includeControlsHelp)
        {
            CreateText(parent, "Title", "オプション", 40, new Vector2(0, 158), new Vector2(420, 52), Color.white);
            ConfigureAudioOptions(parent, panel, new Vector2(0, 30), true);
            if (includeControlsHelp)
            {
                CreateText(parent, "MoveTitle", "移動", 23, new Vector2(-205, -42), new Vector2(130, 36), TextGold);
                CreateText(parent, "MoveText", "WASD / 矢印キー", 23, new Vector2(80, -42), new Vector2(360, 36), Color.white);
                CreateText(parent, "AttackTitle", "攻撃", 23, new Vector2(-205, -104), new Vector2(130, 36), TextGold);
                CreateText(parent, "AttackText", "自動攻撃", 23, new Vector2(80, -104), new Vector2(360, 36), Color.white);
            }

            panel.backButton = CreateButton(parent, "Back Button", "戻る", new Vector2(0, -168), new Vector2(230, 54));
        }

        static void ConfigureAudioOptions(Transform parent, AudioOptionsPanel panel, Vector2 center, bool includeLabels)
        {
            if (includeLabels)
            {
                CreateText(parent, "BgmVolumeLabel", "BGM", 24, center + new Vector2(-154, 32), new Vector2(90, 36), TextGold);
                CreateText(parent, "SfxVolumeLabel", "効果音", 24, center + new Vector2(-154, -28), new Vector2(110, 36), TextGold);
            }

            panel.bgmSlider = CreateSlider(parent, "BGM Slider", center + new Vector2(75, 32), new Vector2(250, 28));
            panel.sfxSlider = CreateSlider(parent, "SFX Slider", center + new Vector2(75, -28), new Vector2(250, 28));
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
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
            CreateText(image.transform, "Label", label, 22, Vector2.zero, size, Color.white);
            return button;
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
            UiBoxOutline.Apply(root.transform, ButtonEdge, 2f);

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
            var handle = CreateImage(handleArea, "Handle", new Color(0.96f, 0.90f, 0.68f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(18, 34);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            return slider;
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

        static void DestroyRoot(Scene scene, string name)
        {
            var root = FindRoot(scene, name);
            if (root != null) Object.DestroyImmediate(root);
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            return null;
        }
    }
}
