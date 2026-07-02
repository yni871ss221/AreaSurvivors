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
        const string UpgradeScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";
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
            RemoveUpgradeTestingButtons();
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
            var mainPanel = Panel(canvas.transform, "Main Panel", new Vector2(0f, 0f), new Vector2(1120f, 650f), PanelColor);
            var scrollRect = CreateScrollView(mainPanel.transform, "Tool Scroll View", new Vector2(0f, 0f), new Vector2(1040f, 600f), 1720f);
            var content = scrollRect.content.transform;

            Label(content, "Title", "ゲーム起動テスト", 38, new Vector2(0f, -40f), new Vector2(520f, 52f), Color.white);
            Label(content, "Description", "ステージ、武器、レリック、強化データを指定して動作確認できます", 20, new Vector2(0f, -78f), new Vector2(760f, 34f), AccentText);
            BuildStageTestPanel(content, -210f);
            BuildDataToolPanel(content, -440f);
            BuildRelicTestPanel(content, -730f);
            BuildWeaponTestPanel(content, -1210f);
            Label(content, "Test Status Text", "テスト操作を選択できます", 17, new Vector2(0f, -1530f), new Vector2(760f, 48f), AccentText);
            Button(content, "Lobby Button", "ロビーへ", new Vector2(0f, -1585f), new Vector2(260f, 44f), null, 20);

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

        static void RemoveUpgradeTestingButtons()
        {
            var scene = EditorSceneManager.OpenScene(UpgradeScenePath, OpenSceneMode.Single);
            var upgradeUi = GameObject.Find("Upgrade UI");
            if (upgradeUi == null)
            {
                Debug.LogWarning("Upgrade UI was not found. Skipped removing testing buttons.");
                return;
            }

            DestroyNamed(upgradeUi.transform, "スキル初期化");
            DestroyNamed(upgradeUi.transform, "トークン+99999");
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

        static void BuildStageTestPanel(Transform parent, float y)
        {
            var panel = Panel(parent, "Stage Test Panel", new Vector2(0f, y), new Vector2(960f, 210f), new Color(0.025f, 0.052f, 0.042f, 0.72f));
            Label(panel.transform, "Stage Test Title", "ステージ開始", 25, new Vector2(0f, 72f), new Vector2(360f, 34f), AccentText);
            Button(panel.transform, "Start Stage 2 Test Button", "STAGE 2 テスト開始", new Vector2(-300f, -14f), new Vector2(280f, 54f));
            Button(panel.transform, "Start Stage 3 Test Button", "STAGE 3 テスト開始", new Vector2(0f, -14f), new Vector2(280f, 54f));
            Button(panel.transform, "Start Stage 4 Test Button", "STAGE 4 テスト開始", new Vector2(300f, -14f), new Vector2(280f, 54f));
        }

        static void BuildDataToolPanel(Transform parent, float y)
        {
            var panel = Panel(parent, "Data Tool Panel", new Vector2(0f, y), new Vector2(960f, 190f), new Color(0.025f, 0.052f, 0.042f, 0.72f));
            Label(panel.transform, "Data Tool Title", "テスト用データ操作", 25, new Vector2(0f, 62f), new Vector2(360f, 34f), AccentText);
            Button(panel.transform, "Add Test Tokens Button", "トークン +99999", new Vector2(-310f, -18f), new Vector2(280f, 54f), GeneratedSpriteLoader.Load("Orb"), 19);
            Button(panel.transform, "Reset Upgrades Button", "強化状態を初期化", new Vector2(0f, -18f), new Vector2(280f, 54f), GeneratedSpriteLoader.Load("Slash_0"), 19);
            Button(panel.transform, "Reset Stage Clear State Button", "クリア状態を初期化", new Vector2(310f, -18f), new Vector2(280f, 54f), GeneratedSpriteLoader.Load("Tower"), 19);
        }

        static void BuildRelicTestPanel(Transform parent, float y)
        {
            var panel = Panel(parent, "Relic Test Panel", new Vector2(0f, y), new Vector2(960f, 370f), new Color(0.025f, 0.052f, 0.042f, 0.72f));
            Label(panel.transform, "Relic Test Title", "レリック取得状態", 25, new Vector2(0f, 150f), new Vector2(360f, 34f), AccentText);

            var scrollRect = CreateScrollView(panel.transform, "Relic Test Scroll View", new Vector2(0f, 24f), new Vector2(820f, 210f), RelicCatalog.All.Length * 50f + 8f);
            var content = scrollRect.content.transform;
            var relics = RelicCatalog.All;
            for (int i = 0; i < relics.Length; i++)
            {
                var definition = relics[i];
                float rowY = -10f - i * 50f;
                var icon = LoadRelicIcon(definition);
                Button(content, GameTestLaunchScreen.RelicUnlockButtonName(definition.type), "獲得: " + definition.displayName, new Vector2(-180f, rowY), new Vector2(340f, 42f), icon, 16);
                Button(content, GameTestLaunchScreen.RelicLockButtonName(definition.type), "未取得: " + definition.displayName, new Vector2(180f, rowY), new Vector2(340f, 42f), icon, 16);
            }

            Button(panel.transform, "Reset All Relics Button", "全レリックを未取得へ", new Vector2(0f, -150f), new Vector2(340f, 44f), GeneratedSpriteLoader.Load("TreasureChest"), 18);
        }

        static void BuildWeaponTestPanel(Transform parent, float y)
        {
            var panel = Panel(parent, "Weapon Test Panel", new Vector2(0f, y), new Vector2(960f, 450f), new Color(0.025f, 0.052f, 0.042f, 0.72f));
            Label(panel.transform, "Weapon Test Title", "武器別 STAGE 1 開始", 25, new Vector2(0f, 190f), new Vector2(360f, 34f), AccentText);

            var scrollRect = CreateScrollView(panel.transform, "Weapon Test Scroll View", new Vector2(0f, -15f), new Vector2(820f, 330f), WeaponCatalog.TestableWeapons.Length * 54f + 8f);
            var content = scrollRect.content.transform;

            for (int i = 0; i < WeaponCatalog.TestableWeapons.Length; i++)
            {
                var weaponType = WeaponCatalog.TestableWeapons[i];
                string label = "STAGE 1: " + WeaponCatalog.DisplayName(weaponType);
                var rowY = -10f - i * 54f;
                var icon = LoadWeaponIcon(weaponType);
                Button(content, GameTestLaunchScreen.WeaponTestButtonName(weaponType), label, new Vector2(0f, rowY), new Vector2(720f, 46f), icon, 18);
            }
        }

        static ScrollRect CreateScrollView(Transform parent, string objectName, Vector2 pos, Vector2 size, float contentHeight)
        {
            var scrollRoot = new GameObject(objectName);
            scrollRoot.transform.SetParent(parent, false);
            var scrollRectTransform = scrollRoot.AddComponent<RectTransform>();
            scrollRectTransform.anchoredPosition = pos;
            scrollRectTransform.sizeDelta = size;
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
            contentRect.sizeDelta = new Vector2(0f, contentHeight);
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            return scrollRect;
        }

        static Button Button(Transform parent, string objectName, string text, Vector2 pos, Vector2 size, Sprite icon = null, int fontSize = 22)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = ButtonColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var rect = button.GetComponent<RectTransform>();
            PlaceRect(rect, parent, pos, size);
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

            Label(go.transform, "Label", text, fontSize, icon != null ? new Vector2(20f, 0f) : Vector2.zero, new Vector2(size.x - (icon != null ? 78f : 20f), size.y), Color.white);
            return button;
        }

        static Sprite LoadWeaponIcon(WeaponType weaponType)
        {
            string key = WeaponCatalog.IconResource(weaponType);
            var sprite = GeneratedSpriteLoader.Load(key);
            if (sprite != null) return sprite;
            return Resources.Load<Sprite>(key);
        }

        static Sprite LoadRelicIcon(RelicDefinition definition)
        {
            if (definition == null) return null;
            var sprite = GeneratedSpriteLoader.Load(definition.iconPath);
            if (sprite != null) return sprite;
            return Resources.Load<Sprite>(definition.iconPath);
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
            PlaceRect(text.rectTransform, parent, pos, size);
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
            PlaceRect(image.rectTransform, parent, pos, size);
            image.raycastTarget = false;
            return image;
        }

        static void PlaceRect(RectTransform rect, Transform parent, Vector2 pos, Vector2 size)
        {
            if (parent != null && parent.name == "Content")
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }

            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
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
