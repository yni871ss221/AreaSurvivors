using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class TitleSceneSetup
    {
        const string TitleScenePath = "Assets/AreaSurvivors/Scenes/01_Title.unity";
        static readonly Color PanelColor = new Color(0.03f, 0.06f, 0.05f, 0.72f);
        static readonly Color ButtonColor = new Color(0.12f, 0.20f, 0.16f, 0.96f);
        static readonly Color ButtonEdge = new Color(0.56f, 0.65f, 0.42f, 0.96f);
        static readonly Color SubtitleColor = new Color(0.78f, 0.91f, 0.80f);

        public static void ApplyTitleScene()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);

            DestroyRoot(scene, "Title UI");
            DestroyRoot(scene, "Main Camera");
            DestroyRoot(scene, "EventSystem");

            CreateCamera(scene, new Color(0.08f, 0.11f, 0.09f));
            var canvas = CreateCanvas(scene, "Title UI", 0);
            CreateEventSystem(scene);
            CreateBackground(canvas.transform);

            var panel = CreatePanel(canvas.transform, "Title Panel", new Vector2(-330, 0), new Vector2(470, 470), PanelColor);
            var panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
            CreateText(panel.transform, "Title Label", "エリアサバイバー", 52, new Vector2(0, 130), new Vector2(430, 70), Color.white);
            CreateText(panel.transform, "Subtitle Label", "塗り広げた領地で塔を守り抜け", 21, new Vector2(0, 75), new Vector2(390, 36), SubtitleColor);

            var playButton = CreateButton(panel.transform, "Play Button", "プレイ", new Vector2(0, 0), new Vector2(300, 58), GeneratedSpriteLoader.Load("Tower"));
            var optionsButton = CreateButton(panel.transform, "Options Button", "オプション", new Vector2(0, -76), new Vector2(300, 58), GeneratedSpriteLoader.Load("Orb"));
            var quitButton = CreateButton(panel.transform, "Quit Button", "ゲーム終了", new Vector2(0, -152), new Vector2(300, 58), GeneratedSpriteLoader.Load("Slash_1"));

            var controller = FindRoot(scene, "01_Title Controller") ?? new GameObject("01_Title Controller");
            SceneManager.MoveGameObjectToScene(controller, scene);
            var titleScreen = Ensure<TitleScreen>(controller);
            var navigator = Ensure<SceneNavigator>(controller);
            titleScreen.navigator = navigator;
            titleScreen.playButton = playButton;
            titleScreen.optionsButton = optionsButton;
            titleScreen.quitButton = quitButton;
            var introAnimator = Ensure<TitleIntroAnimator>(controller);
            introAnimator.panel = panel.rectTransform;
            introAnimator.panelGroup = panelGroup;
            introAnimator.initialDelay = 0.3f;
            introAnimator.panelDuration = 1.35f;
            introAnimator.buttonDelay = 0.24f;
            introAnimator.buttonDuration = 0.8f;
            introAnimator.buttonStagger = 0.22f;
            introAnimator.buttons = new[]
            {
                playButton.GetComponent<RectTransform>(),
                optionsButton.GetComponent<RectTransform>(),
                quitButton.GetComponent<RectTransform>()
            };
            introAnimator.buttonGroups = new[]
            {
                playButton.GetComponent<CanvasGroup>(),
                optionsButton.GetComponent<CanvasGroup>(),
                quitButton.GetComponent<CanvasGroup>()
            };
            introAnimator.interactiveButtons = new[] { playButton, optionsButton, quitButton };
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != TitleScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Scene-authored title UI was applied.");
        }

        [MenuItem("Area Survivors/UI/Validate Title UI")]
        public static void ValidateTitleScene()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            bool valid = ValidateScene(scene);

            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != TitleScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            if (valid) Debug.Log("Scene-authored title UI validation passed.");
        }

        static bool ValidateScene(Scene scene)
        {
            var controller = FindRoot(scene, "01_Title Controller");
            var screen = controller != null ? controller.GetComponent<TitleScreen>() : null;
            var animator = controller != null ? controller.GetComponent<TitleIntroAnimator>() : null;
            bool valid = FindRoot(scene, "Title UI") != null &&
                screen != null &&
                screen.navigator != null &&
                screen.playButton != null &&
                screen.optionsButton != null &&
                screen.quitButton != null &&
                animator != null &&
                animator.panel != null &&
                animator.panelGroup != null &&
                AllAssigned(animator.buttons, 3) &&
                AllAssigned(animator.buttonGroups, 3) &&
                AllAssigned(animator.interactiveButtons, 3);
            if (!valid) Debug.LogError("01_Title scene-authored title UI references are incomplete.");
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

        static void CreateBackground(Transform parent)
        {
            var backgroundSprite = GeneratedSpriteLoader.Load("UI/TitleBackground");
            if (backgroundSprite != null)
            {
                var background = CreateImage(parent, "Background", Color.white);
                background.sprite = backgroundSprite;
                background.preserveAspect = false;
                Stretch(background.rectTransform);
            }

            var shade = CreateImage(parent, "Vignette", new Color(0f, 0f, 0f, 0.20f));
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

        static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Sprite icon)
        {
            var image = CreateImage(parent, name, ButtonColor);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            UiBoxOutline.Apply(image.transform, ButtonEdge, 2f);

            var button = image.gameObject.AddComponent<Button>();
            image.gameObject.AddComponent<CanvasGroup>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.114f, 0.529f, 0.298f, 0.98f);
            colors.pressedColor = new Color(0.08f, 0.14f, 0.11f, 0.98f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            if (icon != null)
            {
                var iconImage = CreateImage(image.transform, "Icon", Color.white);
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.rectTransform.anchoredPosition = new Vector2(-100, 0);
                iconImage.rectTransform.sizeDelta = new Vector2(34, 34);
                CreateText(image.transform, "Label", label, 22, new Vector2(20, 0), new Vector2(210, 58), Color.white);
            }
            else
            {
                CreateText(image.transform, "Label", label, 22, Vector2.zero, size, Color.white);
            }

            return button;
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

        static bool AllAssigned<T>(T[] values, int expectedLength) where T : Object
        {
            if (values == null || values.Length != expectedLength) return false;
            foreach (var value in values)
            {
                if (value == null) return false;
            }

            return true;
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
