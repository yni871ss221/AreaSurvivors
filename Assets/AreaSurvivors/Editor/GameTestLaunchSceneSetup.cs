using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class GameTestLaunchSceneSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/08_GameTestLauncher.unity";
        const string LobbyScenePath = "Assets/AreaSurvivors/Scenes/03_Lobby.unity";
        static readonly Color BackgroundColor = new Color(0.045f, 0.06f, 0.05f, 1f);
        static readonly Color PanelColor = new Color(0.03f, 0.06f, 0.05f, 0.72f);
        static readonly Color ButtonColor = new Color(0.12f, 0.20f, 0.16f, 0.94f);
        static readonly Color EdgeColor = new Color(0.58f, 0.68f, 0.40f, 0.9f);
        static readonly Color AccentText = new Color(0.96f, 0.90f, 0.68f);

        [MenuItem("AreaSurvivors/Setup/Apply Game Test Launcher Scene")]
        public static void Apply()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            CreateTestLauncherScene();
            UpdateLobbyScene();
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!string.IsNullOrEmpty(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        static void CreateTestLauncherScene()
        {
            var scene = System.IO.File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            CreateCamera(scene);
            var canvas = CreateCanvas(scene, "Game Test Launcher UI");
            CreateEventSystem(scene);

            CreateBackground(canvas.transform);
            Panel(canvas.transform, "Main Panel", new Vector2(0f, 12f), new Vector2(1080f, 560f), PanelColor);
            Label(canvas.transform, "Title", "ゲーム起動テスト", 38, new Vector2(0f, 244f), new Vector2(520f, 52f), Color.white);
            Label(canvas.transform, "Description", "ステージや初期武器を指定して動作確認できます", 20, new Vector2(0f, 204f), new Vector2(640f, 34f), AccentText);
            Panel(canvas.transform, "Stage Test Panel", new Vector2(-300f, 22f), new Vector2(420f, 346f), new Color(0.025f, 0.052f, 0.042f, 0.72f));
            Label(canvas.transform, "Stage Test Title", "ステージ開始", 25, new Vector2(-300f, 156f), new Vector2(320f, 34f), AccentText);
            Button(canvas.transform, "Start Stage 2 Test Button", "STAGE 2 テスト開始", new Vector2(-300f, 82f), new Vector2(300f, 58f));
            Button(canvas.transform, "Start Stage 3 Test Button", "STAGE 3 テスト開始", new Vector2(-300f, 8f), new Vector2(300f, 58f));
            Button(canvas.transform, "Start Stage 4 Test Button", "STAGE 4 テスト開始", new Vector2(-300f, -66f), new Vector2(300f, 58f));
            BuildWeaponTestScroll(canvas.transform);
            Button(canvas.transform, "Lobby Button", "ロビーへ", new Vector2(0f, -246f), new Vector2(260f, 52f));

            var controller = new GameObject("Game Test Launcher Controller");
            controller.AddComponent<SceneNavigator>();
            controller.AddComponent<GameTestLaunchScreen>();
            SceneManager.MoveGameObjectToScene(controller, scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void UpdateLobbyScene()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            var lobbyUi = GameObject.Find("Lobby UI");
            if (lobbyUi == null)
            {
                Debug.LogWarning("Lobby UI was not found. Skipped adding the game test launcher button.");
                return;
            }

            DestroyNamed(lobbyUi.transform, "Start Stage 2 Test Button");
            DestroyNamed(lobbyUi.transform, "Start Stage 3 Test Button");
            DestroyNamed(lobbyUi.transform, "Start Stage 4 Test Button");
            DestroyNamed(lobbyUi.transform, "Test Launch Button");

            Button(lobbyUi.transform, "Test Launch Button", "テスト起動", new Vector2(-509f, -105f), new Vector2(180f, 52f));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void UpdateBuildSettings()
        {
            var paths = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene == null || string.IsNullOrEmpty(scene.path)) continue;
                if (!paths.Contains(scene.path)) paths.Add(scene.path);
            }

            if (!paths.Contains(ScenePath)) paths.Add(ScenePath);
            EditorBuildSettings.scenes = paths.ConvertAll(path => new EditorBuildSettingsScene(path, true)).ToArray();
        }

        static void CreateCamera(Scene scene)
        {
            var cameraObject = new GameObject("Main Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.tag = "MainCamera";
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
        }

        static Canvas CreateCanvas(Scene scene, string name)
        {
            var canvasObject = new GameObject(name);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<GraphicRaycaster>();
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            return canvas;
        }

        static void CreateEventSystem(Scene scene)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        static void CreateBackground(Transform parent)
        {
            var background = Image(parent, "Background", new Color(0.035f, 0.055f, 0.045f, 1f), Vector2.zero, Vector2.zero);
            Stretch(background.rectTransform);
            var vignette = Image(parent, "Vignette", new Color(0f, 0f, 0f, 0.26f), Vector2.zero, Vector2.zero);
            Stretch(vignette.rectTransform);
        }

        static void BuildWeaponTestScroll(Transform parent)
        {
            Panel(parent, "Weapon Test Panel", new Vector2(272f, 22f), new Vector2(520f, 346f), new Color(0.025f, 0.052f, 0.042f, 0.72f));
            Label(parent, "Weapon Test Title", "武器別 STAGE 1 開始", 25, new Vector2(272f, 156f), new Vector2(360f, 34f), AccentText);

            var scrollRoot = new GameObject("Weapon Test Scroll View");
            scrollRoot.transform.SetParent(parent, false);
            var scrollRectTransform = scrollRoot.AddComponent<RectTransform>();
            scrollRectTransform.anchoredPosition = new Vector2(272f, -10f);
            scrollRectTransform.sizeDelta = new Vector2(452f, 250f);
            var scrollRect = scrollRoot.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 32f;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollRoot.transform, false);
            var viewportRect = viewport.AddComponent<RectTransform>();
            Stretch(viewportRect);
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.08f);
            viewportImage.raycastTarget = true;
            viewport.AddComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0f, WeaponCatalog.TestableWeapons.Length * 62f + 8f);
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            for (int i = 0; i < WeaponCatalog.TestableWeapons.Length; i++)
            {
                var weaponType = WeaponCatalog.TestableWeapons[i];
                string label = "STAGE 1: " + WeaponCatalog.DisplayName(weaponType);
                var y = -35f - i * 62f;
                var icon = LoadWeaponIcon(weaponType);
                Button(content.transform, GameTestLaunchScreen.WeaponTestButtonName(weaponType), label, new Vector2(0f, y), new Vector2(410f, 52f), icon);
            }
        }

        static Button Button(Transform parent, string objectName, string text, Vector2 pos, Vector2 size, Sprite icon = null)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = ButtonColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var rect = button.GetComponent<RectTransform>();
            if (parent != null && parent.name == "Content")
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
            }
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            UiBoxOutline.Apply(go.transform, EdgeColor, 2f);
            var highlight = go.AddComponent<UiSelectionHighlight>();
            highlight.padding = 6f;
            highlight.thickness = 4f;
            go.AddComponent<SelectOnPointerEnter>();
            if (icon != null)
            {
                var iconImage = Image(go.transform, "Icon", Color.white, new Vector2(-size.x * 0.38f, 0f), new Vector2(36f, 36f));
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
            }

            Label(go.transform, "Label", text, 22, icon != null ? new Vector2(20f, 0f) : Vector2.zero, new Vector2(size.x - (icon != null ? 78f : 20f), size.y), Color.white);
            return button;
        }

        static Sprite LoadWeaponIcon(WeaponType weaponType)
        {
            string key = WeaponCatalog.IconResource(weaponType);
            var sprite = GeneratedSpriteLoader.Load(key);
            if (sprite != null) return sprite;
            return Resources.Load<Sprite>(key);
        }

        static Image Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var image = Image(parent, name, color, pos, size);
            UiBoxOutline.Apply(image.transform, EdgeColor, 2f);
            var inset = Image(image.transform, "Inset Glow", new Color(1f, 0.92f, 0.58f, 0.08f), Vector2.zero, new Vector2(size.x - 8f, size.y - 8f));
            inset.raycastTarget = false;
            return image;
        }

        static Text Label(Transform parent, string name, string value, int fontSize, Vector2 pos, Vector2 size, Color color)
        {
            var text = new GameObject(name).AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.rectTransform.anchoredPosition = pos;
            text.rectTransform.sizeDelta = size;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.025f, 0.018f, 0.82f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        static Image Image(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.rectTransform.anchoredPosition = pos;
            image.rectTransform.sizeDelta = size;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void DestroyNamed(Transform root, string name)
        {
            var child = FindChild(root, name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
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
    }
}
