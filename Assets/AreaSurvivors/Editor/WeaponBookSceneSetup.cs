using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class WeaponBookSceneSetup
    {
        const string WeaponBookScenePath = "Assets/AreaSurvivors/Scenes/07_WeaponBook.unity";
        static readonly Color BackgroundColor = new Color(0.04f, 0.055f, 0.045f);
        static readonly Color PanelColor = new Color(0.02f, 0.05f, 0.04f, 0.88f);
        static readonly Color CardColor = new Color(0.08f, 0.17f, 0.12f, 0.94f);
        static readonly Color ButtonColor = new Color(0.12f, 0.2f, 0.16f, 0.96f);
        static readonly Color EdgeColor = new Color(0.56f, 0.65f, 0.42f, 0.96f);
        static readonly Color AccentColor = new Color(0.92f, 0.87f, 0.58f);

        [MenuItem("Area Survivors/UI/Apply Weapon Book Scene")]
        public static void Apply()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            ApplyWeaponBookScene();
            AddToBuildSettings();

            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != WeaponBookScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Weapon book scene was applied.");
        }

        static void ApplyWeaponBookScene()
        {
            var scene = System.IO.File.Exists(WeaponBookScenePath)
                ? EditorSceneManager.OpenScene(WeaponBookScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            DestroyRoot(scene, "Main Camera");
            DestroyRoot(scene, "EventSystem");
            DestroyRoot(scene, "Weapon Book UI");
            DestroyRoot(scene, "07_WeaponBook Controller");

            CreateCamera(scene);
            CreateEventSystem(scene);
            var canvas = CreateCanvas(scene, "Weapon Book UI");
            CreateBackground(canvas.transform);

            CreateText(canvas.transform, "Title Label", "武器図鑑", 44, new Vector2(0, 300), new Vector2(420, 58), Color.white, TextAnchor.MiddleCenter);
            CreateText(canvas.transform, "Subtitle Label", "解放した武器の性能と特徴を確認できます", 19, new Vector2(0, 258), new Vector2(640, 32), AccentColor, TextAnchor.MiddleCenter);

            var listPanel = CreatePanel(canvas.transform, "Weapon List Panel", new Vector2(-330, -20), new Vector2(420, 500), PanelColor);
            CreateText(listPanel.transform, "Weapon List Header", "武器一覧", 24, new Vector2(0, 220), new Vector2(360, 34), AccentColor, TextAnchor.MiddleCenter);
            var scrollRect = CreateScrollView(listPanel.transform);
            var entries = CreateWeaponCards(scrollRect.content);

            var detailPanel = CreatePanel(canvas.transform, "Weapon Detail Panel", new Vector2(220, -20), new Vector2(580, 500), PanelColor);
            var detailTitle = CreateText(detailPanel.transform, "Detail Title", "武器図鑑", 32, new Vector2(0, 206), new Vector2(500, 48), Color.white, TextAnchor.MiddleCenter);
            var detailTypeIcons = WeaponAttributeIconSceneUtility.EnsureIconSet(detailPanel.transform, "Detail Type Icons", new Vector2(-164, 206), new Vector2(34, 34), new Vector2(0.5f, 0.5f), WeaponAttributeType.None, false);
            var featureText = CreateSection(detailPanel.transform, "Feature", "特徴", new Vector2(0, 108), new Vector2(500, 116));
            var statsText = CreateSection(detailPanel.transform, "Initial Stats", "初期ステータス", new Vector2(0, -48), new Vector2(500, 164));
            var specialText = CreateSection(detailPanel.transform, "Special Effect", "特殊効果", new Vector2(0, -190), new Vector2(500, 92));
            var messageText = CreateText(detailPanel.transform, "Message Text", string.Empty, 18, new Vector2(0, -244), new Vector2(500, 28), new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter);

            var backButton = CreateButton(canvas.transform, "Back Button", "ロビーへ", new Vector2(0, -306), new Vector2(240, 54), GeneratedSpriteLoader.Load("Orb"));

            var controller = new GameObject("07_WeaponBook Controller");
            SceneManager.MoveGameObjectToScene(controller, scene);
            var navigator = controller.AddComponent<SceneNavigator>();
            var screen = controller.AddComponent<WeaponBookScreen>();
            screen.navigator = navigator;
            screen.backButton = backButton;
            screen.detailTitleText = detailTitle;
            screen.featureText = featureText;
            screen.statsText = statsText;
            screen.specialEffectText = specialText;
            screen.messageText = messageText;
            screen.detailTypeIcons = detailTypeIcons;
            screen.entries = entries;
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, WeaponBookScenePath);
        }

        static WeaponBookEntryView[] CreateWeaponCards(RectTransform content)
        {
            var specs = new[]
            {
                new WeaponSpec("slash", "スラッシュ", true, false, default, true, WeaponType.Slash, WeaponAttributeType.Melee, "Slash_0", "正面の近距離に三日月状の斬撃を放つ、最初から使える基本武器です。", "エリア占有率が50%以上の時、ノックバックが2倍になります。"),
                new WeaponSpec("arrow", "弓", false, false, UpgradeType.UnlockArrow, true, WeaponType.Arrow, WeaponAttributeType.Ranged, "ArrowHudIcon", "離れた敵を狙って矢を放つ遠距離武器です。射程が長く、敵に近づかれる前に削れます。", "エリア占有率が50%以上の時、射程が2倍になります。"),
                new WeaponSpec("fireball", "ファイアボール", false, false, UpgradeType.UnlockFireball, true, WeaponType.Fireball, WeaponAttributeType.Magic, "FireballHudIcon", "着弾地点で爆発する火球を放ち、まとまった敵に範囲ダメージを与えます。", "エリア占有率が50%以上の時、爆発範囲が2倍になります。"),
                new WeaponSpec("shield", "シールド", false, false, UpgradeType.UnlockShield, true, WeaponType.Shield, WeaponAttributeType.Defense, "Shield", "プレイヤーの周囲を回転するシールドを展開し、触れた敵にダメージとノックバックを与えます。", "エリア占有率が50%以上の時、回転速度が2倍になります。"),
                new WeaponSpec("flag", "旗", false, false, UpgradeType.UnlockFlag, true, WeaponType.Flag, WeaponAttributeType.Defense, "Flag", "プレイヤーの周囲に追従する円状の床を展開し、床上の敵にダメージと速度低下を与えます。", "エリア占有率が50%以上の時、エリア取得範囲に従って攻撃範囲が広がります。"),
                new WeaponSpec("boomerang-sword", "ブーメランソード", false, false, UpgradeType.UnlockBoomerangSword, true, WeaponType.BoomerangSword, WeaponAttributeType.Melee, "BoomerangSword", "進行方向に回転する剣を投げ、一定距離で反対方向へ戻る近接武器です。", "エリア占有率が70%以上の時、剣本数が2倍になります。"),
                new WeaponSpec("aura-sword", "オーラソード", false, false, UpgradeType.UnlockAuraSword, true, WeaponType.AuraSword, WeaponAttributeType.Melee, "AuraSword", "進行方向45度範囲のランダム方向に斬撃を飛ばします。", "エリア占有率が50%以上の時、エリア取得範囲に従って攻撃範囲が広がります。"),
                new WeaponSpec("arrow-rain", "アローレイン", false, false, UpgradeType.UnlockArrowRain, true, WeaponType.ArrowRain, WeaponAttributeType.Ranged, "ArrowRain", "進行方向の先に矢の雨を降らせ、範囲内の敵へ継続ダメージを与えます。", "エリア占有率が50%以上の時、エリア取得範囲に従って攻撃範囲が広がります。"),
                new WeaponSpec("gun", "銃", false, false, UpgradeType.UnlockGun, true, WeaponType.Gun, WeaponAttributeType.Ranged, "Gun", "進行方向へ敵を貫通する強力な銃弾を発射します。", "エリア占有率が70%以上の時、攻撃力が2倍になります。"),
                new WeaponSpec("frost", "フロスト", false, false, UpgradeType.UnlockFrost, true, WeaponType.Frost, WeaponAttributeType.Magic, "Frost", "進行方向の少し先に氷のエリアを作り、敵にダメージと速度低下を与えます。", "エリア占有率が50%以上の時、エリア取得範囲に従って攻撃範囲が広がります。"),
                new WeaponSpec("thunder-ball", "サンダーボール", false, false, UpgradeType.UnlockThunderBall, true, WeaponType.ThunderBall, WeaponAttributeType.Magic, "ThunderBall", "進行方向に近い敵をゆっくり追尾する雷球を放ち、持続中は周囲にダメージを与え続けます。", "エリア占有率が70%以上の時、攻撃範囲が2倍になります。"),
            };

            var entries = new WeaponBookEntryView[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                entries[i] = CreateWeaponCard(content, specs[i], i + 1);
            }

            return entries;
        }

        static WeaponBookEntryView CreateWeaponCard(Transform parent, WeaponSpec spec, int number)
        {
            var panel = CreateImage(parent, $"Weapon Card {number:00}", CardColor);
            var rect = panel.rectTransform;
            rect.sizeDelta = new Vector2(0, 92);
            var layout = panel.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 92;
            layout.preferredHeight = 92;
            UiBoxOutline.Apply(panel.transform, EdgeColor, 2f);

            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.14f, 0.35f, 0.22f, 0.98f);
            colors.pressedColor = new Color(0.07f, 0.14f, 0.1f, 0.98f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var icon = CreateImage(panel.transform, "Icon", Color.white);
            icon.sprite = GeneratedSpriteLoader.Load(spec.iconPath);
            icon.preserveAspect = true;
            icon.rectTransform.anchoredPosition = new Vector2(-126, 0);
            icon.rectTransform.sizeDelta = new Vector2(58, 58);

            var silhouette = CreateImage(icon.transform, "Silhouette Overlay", new Color(0f, 0f, 0f, 0.45f));
            silhouette.raycastTarget = false;
            Stretch(silhouette.rectTransform);

            var nameText = CreateText(panel.transform, "Name Text", spec.displayName, 22, new Vector2(34, 0), new Vector2(158, 42), Color.white, TextAnchor.MiddleLeft);
            if (spec.attributeType != WeaponAttributeType.None)
            {
                WeaponAttributeIconSceneUtility.EnsureIconSet(panel.transform, "Weapon Type Icons", new Vector2(132, 0), new Vector2(28, 28), new Vector2(0.5f, 0.5f), spec.attributeType, true);
            }

            var entry = panel.gameObject.AddComponent<WeaponBookEntryView>();
            entry.weaponId = spec.weaponId;
            entry.displayName = spec.displayName;
            entry.unlockedByDefault = spec.unlockedByDefault;
            entry.futureWeapon = spec.futureWeapon;
            entry.requiredUpgrade = spec.requiredUpgrade;
            entry.usesRuntimeStats = spec.usesRuntimeStats;
            entry.weaponType = spec.weaponType;
            entry.attributeType = spec.attributeType;
            entry.featureDescription = spec.featureDescription;
            entry.specialEffectDescription = spec.specialEffectDescription;
            entry.initialStatsText = spec.futureWeapon ? "未確認" : string.Empty;
            entry.button = button;
            entry.background = panel;
            entry.icon = icon;
            entry.silhouetteOverlay = silhouette;
            entry.nameText = nameText;
            return entry;
        }

        static WeaponSpec FutureSpec(int number)
        {
            return new WeaponSpec(
                $"future-{number:00}",
                $"未確認武器 {number:00}",
                false,
                true,
                default,
                false,
                WeaponType.Slash,
                WeaponAttributeType.None,
                "Orb",
                "まだ詳細が確認されていない武器です。今後の追加に備えた予約枠です。",
                "特殊効果は今後追加予定です。");
        }

        static ScrollRect CreateScrollView(Transform parent)
        {
            var scrollObject = CreateImage(parent, "Weapon List Scroll View", new Color(0f, 0f, 0f, 0.12f));
            scrollObject.rectTransform.anchoredPosition = new Vector2(0, -18);
            scrollObject.rectTransform.sizeDelta = new Vector2(356, 416);

            var viewport = CreateImage(scrollObject.transform, "Viewport", new Color(0f, 0f, 0f, 0.01f));
            viewport.rectTransform.anchorMin = Vector2.zero;
            viewport.rectTransform.anchorMax = Vector2.one;
            viewport.rectTransform.offsetMin = new Vector2(6, 6);
            viewport.rectTransform.offsetMax = new Vector2(-6, -6);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            var contentObject = new GameObject("Content", typeof(RectTransform));
            contentObject.transform.SetParent(viewport.transform, false);
            var content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 0);
            var layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollObject.gameObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.rectTransform;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 34f;
            return scrollRect;
        }

        static Text CreateSection(Transform parent, string name, string title, Vector2 position, Vector2 size)
        {
            var panel = CreatePanel(parent, name + " Panel", position, size, new Color(0.04f, 0.075f, 0.065f, 0.76f));
            CreateText(panel.transform, name + " Header", title, 18, new Vector2(0, size.y * 0.5f - 20), new Vector2(size.x - 28, 26), AccentColor, TextAnchor.MiddleLeft);
            var body = CreateText(panel.transform, name + " Text", "-", 18, new Vector2(0, -12), new Vector2(size.x - 32, size.y - 46), Color.white, TextAnchor.UpperLeft);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            return body;
        }

        static Camera CreateCamera(Scene scene)
        {
            var cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.tag = "MainCamera";
            return camera;
        }

        static Canvas CreateCanvas(Scene scene, string name)
        {
            var canvasObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static void CreateEventSystem(Scene scene)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        static void CreateBackground(Transform parent)
        {
            var background = CreateImage(parent, "Background", new Color(0.035f, 0.055f, 0.045f, 1f));
            Stretch(background.rectTransform);
            var glow = CreateImage(parent, "Center Glow", new Color(0.12f, 0.2f, 0.12f, 0.08f));
            Stretch(glow.rectTransform);
        }

        static Image CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var image = CreateImage(parent, name, color);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            UiBoxOutline.Apply(image.transform, EdgeColor, 2f);
            return image;
        }

        static Button CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Sprite icon)
        {
            var image = CreateImage(parent, name, ButtonColor);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            UiBoxOutline.Apply(image.transform, EdgeColor, 2f);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(0.12f, 0.38f, 0.22f, 0.98f);
            colors.pressedColor = new Color(0.08f, 0.14f, 0.11f, 0.98f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            if (icon != null)
            {
                var iconImage = CreateImage(image.transform, "Icon", Color.white);
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.rectTransform.anchoredPosition = new Vector2(-size.x * 0.34f, 0);
                iconImage.rectTransform.sizeDelta = new Vector2(32, 32);
                CreateText(image.transform, "Label", label, 20, new Vector2(18, 0), new Vector2(size.x - 58, size.y), Color.white, TextAnchor.MiddleCenter);
            }
            else
            {
                CreateText(image.transform, "Label", label, 20, Vector2.zero, size, Color.white, TextAnchor.MiddleCenter);
            }

            return button;
        }

        static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static Text CreateText(Transform parent, string name, string text, int fontSize, Vector2 position, Vector2 size, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            var rect = label.rectTransform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return label;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(scene => scene.path == WeaponBookScenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(WeaponBookScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
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

        static GameObject FindObjectInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var result = FindChild(root.transform, name);
                if (result != null) return result.gameObject;
                if (root.name == name) return root;
            }
            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = FindChild(root.GetChild(i), name);
                if (child != null) return child;
            }
            return null;
        }

        readonly struct WeaponSpec
        {
            public readonly string weaponId;
            public readonly string displayName;
            public readonly bool unlockedByDefault;
            public readonly bool futureWeapon;
            public readonly UpgradeType requiredUpgrade;
            public readonly bool usesRuntimeStats;
            public readonly WeaponType weaponType;
            public readonly WeaponAttributeType attributeType;
            public readonly string iconPath;
            public readonly string featureDescription;
            public readonly string specialEffectDescription;

            public WeaponSpec(
                string weaponId,
                string displayName,
                bool unlockedByDefault,
                bool futureWeapon,
                UpgradeType requiredUpgrade,
                bool usesRuntimeStats,
                WeaponType weaponType,
                WeaponAttributeType attributeType,
                string iconPath,
                string featureDescription,
                string specialEffectDescription)
            {
                this.weaponId = weaponId;
                this.displayName = displayName;
                this.unlockedByDefault = unlockedByDefault;
                this.futureWeapon = futureWeapon;
                this.requiredUpgrade = requiredUpgrade;
                this.usesRuntimeStats = usesRuntimeStats;
                this.weaponType = weaponType;
                this.attributeType = attributeType;
                this.iconPath = iconPath;
                this.featureDescription = featureDescription;
                this.specialEffectDescription = specialEffectDescription;
            }
        }
    }
}
