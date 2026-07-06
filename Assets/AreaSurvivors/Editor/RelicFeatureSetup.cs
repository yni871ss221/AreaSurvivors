using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class RelicFeatureSetup
    {
        const string ExternalTreasureChestSourcePath = "C:/Develop/unity_workspace/MultiSurvivors2/Assets/KnightSurvivorsPack/TreasureChests_32px.png";
        const string TreasureChestSourcePath = "Assets/AreaSurvivors/Sprites/External/TreasureChestSource.png";
        const string TreasureChestGeneratedPath = "Assets/AreaSurvivors/Sprites/Generated/TreasureChest.png";
        const string TreasureChestOpenGeneratedPath = "Assets/AreaSurvivors/Sprites/Generated/TreasureChestOpen.png";
        const string RelicBackFxPath = "Assets/AreaSurvivors/Sprites/Generated/RelicBackFx.png";
        const string RelicBackFxShinyPath = "Assets/AreaSurvivors/Sprites/Generated/RelicBackFxShiny.png";
        const string RarityBadgeSpritePath = "Assets/AreaSurvivors/Sprites/Generated/RelicRarityBadge.png";
        const string GeneratedSpriteCatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string RelicChestPrefabPath = "Assets/AreaSurvivors/Prefabs/RelicChest.prefab";
        const string RelicAcquisitionPanelPrefabPath = "Assets/AreaSurvivors/Prefabs/UI/RelicAcquisitionPanel.prefab";
        const string RelicScenePath = "Assets/AreaSurvivors/Scenes/09_Relics.unity";
        const string LobbyScenePath = "Assets/AreaSurvivors/Scenes/03_Lobby.unity";
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        static readonly Color BackgroundColor = new Color(0.04f, 0.05f, 0.04f);
        static readonly Color PanelColor = new Color(0.02f, 0.05f, 0.04f, 0.88f);
        static readonly Color CardColor = new Color(0.08f, 0.17f, 0.12f, 0.94f);
        static readonly Color ButtonColor = new Color(0.12f, 0.2f, 0.16f, 0.96f);
        static readonly Color EdgeColor = new Color(0.56f, 0.65f, 0.42f, 0.96f);
        static readonly Color AccentColor = new Color(0.92f, 0.87f, 0.58f);

        [MenuItem("Area Survivors/Relics/Apply Relic Feature")]
        public static void Apply()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;

            ApplyTreasureChestAssets();
            ApplyRarityBadgeAsset();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ConfigureRelicIconImports();
            UpdateGeneratedSpriteCatalog();
            CreateRelicChestPrefab();
            CreateRelicAcquisitionPanelPrefab();
            ApplyRelicScene();
            ApplyLobbyRelicButton();
            AssignGameSceneChestPrefab();
            AddToBuildSettings();

            if (!string.IsNullOrEmpty(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Relic feature assets and scenes were applied.");
        }

        static void ApplyTreasureChestAssets()
        {
            if (!File.Exists(ExternalTreasureChestSourcePath))
            {
                Debug.LogError("Treasure chest source was not found: " + ExternalTreasureChestSourcePath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(TreasureChestSourcePath));
            Directory.CreateDirectory(Path.GetDirectoryName(TreasureChestGeneratedPath));
            File.Copy(ExternalTreasureChestSourcePath, TreasureChestSourcePath, true);

            var sourceBytes = File.ReadAllBytes(ExternalTreasureChestSourcePath);
            var sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sourceTexture.LoadImage(sourceBytes);

            var crop = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            crop.SetPixels(sourceTexture.GetPixels(0, sourceTexture.height - 32, 32, 32));
            crop.Apply();
            File.WriteAllBytes(TreasureChestGeneratedPath, crop.EncodeToPNG());
            crop.SetPixels(sourceTexture.GetPixels(32, sourceTexture.height - 32, 32, 32));
            crop.Apply();
            File.WriteAllBytes(TreasureChestOpenGeneratedPath, crop.EncodeToPNG());
            Object.DestroyImmediate(sourceTexture);
            Object.DestroyImmediate(crop);

            AssetDatabase.ImportAsset(TreasureChestSourcePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TreasureChestGeneratedPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TreasureChestOpenGeneratedPath, ImportAssetOptions.ForceUpdate);
            CreateFxSprite(RelicBackFxPath, new Color(1f, 0.83f, 0.16f, 0.42f), 16);
            CreateFxSprite(RelicBackFxShinyPath, new Color(0.55f, 0.95f, 1f, 0.32f), 10);
            ConfigureSpriteImporter(TreasureChestSourcePath, 32f);
            ConfigureSpriteImporter(TreasureChestGeneratedPath, 32f);
            ConfigureSpriteImporter(TreasureChestOpenGeneratedPath, 32f);
            ConfigureSpriteImporter(RelicBackFxPath, 128f);
            ConfigureSpriteImporter(RelicBackFxShinyPath, 128f);
        }

        static void ApplyRarityBadgeAsset()
        {
            const int width = 64;
            const int height = 28;
            const float cornerRadius = 7f;
            Directory.CreateDirectory(Path.GetDirectoryName(RarityBadgeSpritePath));

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float alpha = RoundedRectAlpha(x + 0.5f, y + 0.5f, width, height, cornerRadius);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            File.WriteAllBytes(RarityBadgeSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(RarityBadgeSpritePath, ImportAssetOptions.ForceUpdate);
            ConfigureSpriteImporter(RarityBadgeSpritePath, 100f, new Vector4(9f, 9f, 9f, 9f));
        }

        static float RoundedRectAlpha(float x, float y, int width, int height, float radius)
        {
            float dx = Mathf.Max(radius - x, 0f, x - (width - radius));
            float dy = Mathf.Max(radius - y, 0f, y - (height - radius));
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            return Mathf.Clamp01(radius + 0.5f - distance);
        }

        static void CreateFxSprite(string path, Color color, int rayCount)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var delta = new Vector2(x, y) - center;
                    float radius = delta.magnitude / (size * 0.5f);
                    if (radius > 1f)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    float angle = Mathf.Atan2(delta.y, delta.x);
                    float rays = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle * rayCount)), 4f);
                    float ring = Mathf.Exp(-Mathf.Pow((radius - 0.62f) * 7f, 2f));
                    float alpha = Mathf.Clamp01((rays * 0.85f + ring * 0.45f) * (1f - radius * 0.2f));
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }

            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        static void ConfigureSpriteImporter(string path, float pixelsPerUnit)
        {
            ConfigureSpriteImporter(path, pixelsPerUnit, Vector4.zero);
        }

        static void ConfigureSpriteImporter(string path, float pixelsPerUnit, Vector4 spriteBorder)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = spriteBorder;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static void ConfigureRelicIconImports()
        {
            var definitions = RelicCatalog.GetDisplayOrdered();
            for (int i = 0; i < definitions.Length; i++)
            {
                if (string.IsNullOrEmpty(definitions[i].iconPath)) continue;
                ConfigureSpriteImporter("Assets/AreaSurvivors/Sprites/Generated/" + definitions[i].iconPath + ".png", 128f);
            }
        }

        static void UpdateGeneratedSpriteCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GeneratedSpriteCatalog>(GeneratedSpriteCatalogPath);
            if (catalog == null)
            {
                Debug.LogWarning("GeneratedSpriteCatalog was not found. Relic icons will still load in Editor, but not in builds: " + GeneratedSpriteCatalogPath);
                return;
            }

            var entries = catalog.entries != null
                ? catalog.entries.ToList()
                : new List<GeneratedSpriteCatalog.Entry>();

            var definitions = RelicCatalog.All;
            for (int i = 0; i < definitions.Length; i++)
            {
                if (string.IsNullOrEmpty(definitions[i].iconPath)) continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AreaSurvivors/Sprites/Generated/" + definitions[i].iconPath + ".png");
                if (sprite == null) continue;

                int index = entries.FindIndex(entry => entry.name == definitions[i].iconPath);
                var next = new GeneratedSpriteCatalog.Entry { name = definitions[i].iconPath, sprite = sprite };
                if (index >= 0) entries[index] = next;
                else entries.Add(next);
            }

            catalog.entries = entries.OrderBy(entry => entry.name).ToArray();
            EditorUtility.SetDirty(catalog);
        }

        static void CreateRelicChestPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RelicChestPrefabPath));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TreasureChestGeneratedPath);
            if (sprite == null)
            {
                Debug.LogError("Treasure chest sprite was not found: " + TreasureChestGeneratedPath);
                return;
            }

            var root = new GameObject("RelicChest");
            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 0;

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            var collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.78f, 0.62f);
            collider.offset = new Vector2(0f, -0.08f);

            root.AddComponent<RelicChest>();
            var ySort = root.AddComponent<YSort>();
            ySort.renderers = new Renderer[] { renderer };
            ySort.Apply();

            PrefabUtility.SaveAsPrefabAsset(root, RelicChestPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void CreateRelicAcquisitionPanelPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RelicAcquisitionPanelPrefabPath));
            var closedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TreasureChestGeneratedPath);
            var openSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TreasureChestOpenGeneratedPath);
            var backFxSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RelicBackFxPath);
            var backFxShinySprite = AssetDatabase.LoadAssetAtPath<Sprite>(RelicBackFxShinyPath);
            var rarityBadgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RarityBadgeSpritePath);

            var root = new GameObject("RelicAcquisitionPanel", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            root.AddComponent<GraphicRaycaster>();
            var rootGroup = root.AddComponent<CanvasGroup>();

            var shade = CreateImage(root.transform, "Shade", new Color(0f, 0f, 0f, 0.72f));
            Stretch(shade.rectTransform);

            var panel = CreatePanel(root.transform, "Panel", new Vector2(0f, 0f), new Vector2(620f, 520f), new Color(0.025f, 0.05f, 0.04f, 0.96f));
            var title = CreateText(panel.transform, "Title", "レリック獲得", 36, new Vector2(0f, 214f), new Vector2(520f, 54f), Color.white, TextAnchor.MiddleCenter);

            var backFx = CreateImage(panel.transform, "Back FX", Color.white);
            backFx.sprite = backFxSprite;
            backFx.preserveAspect = true;
            backFx.rectTransform.anchoredPosition = new Vector2(0f, 70f);
            backFx.rectTransform.sizeDelta = new Vector2(250f, 250f);

            var backFxShiny = CreateImage(panel.transform, "Back FX Shiny", Color.white);
            backFxShiny.sprite = backFxShinySprite;
            backFxShiny.preserveAspect = true;
            backFxShiny.rectTransform.anchoredPosition = new Vector2(0f, 70f);
            backFxShiny.rectTransform.sizeDelta = new Vector2(210f, 210f);

            var openChest = CreateImage(panel.transform, "Open Chest", Color.white);
            openChest.sprite = openSprite != null ? openSprite : closedSprite;
            openChest.preserveAspect = true;
            openChest.rectTransform.anchoredPosition = new Vector2(0f, -74f);
            openChest.rectTransform.sizeDelta = new Vector2(128f, 128f);

            var closedChest = CreateImage(panel.transform, "Closed Chest", Color.white);
            closedChest.sprite = closedSprite;
            closedChest.preserveAspect = true;
            closedChest.rectTransform.anchoredPosition = new Vector2(0f, -74f);
            closedChest.rectTransform.sizeDelta = new Vector2(128f, 128f);

            var item = CreateImage(panel.transform, "Relic Icon", Color.white);
            item.preserveAspect = true;
            item.rectTransform.anchoredPosition = new Vector2(0f, -58f);
            item.rectTransform.sizeDelta = new Vector2(184f, 184f);

            var headerRow = CreateRelicHeaderRow(panel.transform, "Relic Header Row", new Vector2(0f, -50f), new Vector2(440f, 34f));
            var rarityBadge = CreateRarityBadge(headerRow, "Rarity Badge", rarityBadgeSprite);
            var rarityText = CreateText(rarityBadge.transform, "Rarity Text", "アンコモン", 13, Vector2.zero, new Vector2(82f, 20f), RelicRarityVisuals.GetBadgeTextColor(RelicRarity.Uncommon), TextAnchor.MiddleCenter);
            var relicName = CreateRelicNameText(headerRow, "Relic Name", "", 23, RelicRarityVisuals.GetColor(RelicRarity.Uncommon));

            var description = CreateText(panel.transform, "Description", "", 19, new Vector2(0f, -126f), new Vector2(500f, 72f), Color.white, TextAnchor.UpperCenter);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Overflow;

            var effect = CreateText(panel.transform, "Effect", "", 22, new Vector2(0f, -174f), new Vector2(500f, 36f), AccentColor, TextAnchor.MiddleCenter);

            var openButton = CreateButton(panel.transform, "Open Button", "宝箱を開ける", new Vector2(0f, -224f), new Vector2(240f, 50f), closedSprite);
            var closeButton = CreateButton(panel.transform, "Close Button", "閉じる", new Vector2(0f, -224f), new Vector2(220f, 50f), GeneratedSpriteLoader.Load("Orb"));

            var controller = root.AddComponent<RelicAcquisitionPanel>();
            controller.rootGroup = rootGroup;
            controller.closedChestImage = closedChest;
            controller.openChestImage = openChest;
            controller.itemImage = item;
            controller.backFxImage = backFx;
            controller.backFxShinyImage = backFxShiny;
            controller.titleText = title;
            controller.relicNameText = relicName;
            controller.rarityBadgeImage = rarityBadge;
            controller.rarityText = rarityText;
            controller.descriptionText = description;
            controller.effectText = effect;
            controller.openButton = openButton;
            controller.closeButton = closeButton;

            PrefabUtility.SaveAsPrefabAsset(root, RelicAcquisitionPanelPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void ApplyRelicScene()
        {
            var scene = File.Exists(RelicScenePath)
                ? EditorSceneManager.OpenScene(RelicScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            DestroyRoot(scene, "Main Camera");
            DestroyRoot(scene, "EventSystem");
            DestroyRoot(scene, "Relic Book UI");
            DestroyRoot(scene, "09_Relics Controller");

            CreateCamera(scene);
            CreateEventSystem(scene);
            var canvas = CreateCanvas(scene, "Relic Book UI");
            CreateBackground(canvas.transform);

            CreateText(canvas.transform, "Title Label", "所持レリック", 44, new Vector2(0, 300), new Vector2(480, 58), Color.white, TextAnchor.MiddleCenter);
            CreateText(canvas.transform, "Subtitle Label", "宝箱から獲得した永続強化を確認できます", 19, new Vector2(0, 258), new Vector2(640, 32), AccentColor, TextAnchor.MiddleCenter);

            var listPanel = CreatePanel(canvas.transform, "Relic List Panel", new Vector2(-292, -20), new Vector2(520, 500), PanelColor);
            CreateText(listPanel.transform, "Relic List Header", "レリック一覧", 24, new Vector2(0, 220), new Vector2(420, 34), AccentColor, TextAnchor.MiddleCenter);
            var scrollRect = CreateScrollView(listPanel.transform);
            var entries = CreateRelicCards(scrollRect.content);

            var detailPanel = CreatePanel(canvas.transform, "Relic Detail Panel", new Vector2(284, -20), new Vector2(500, 500), PanelColor);
            var rarityBadgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RarityBadgeSpritePath);
            var detailHeaderRow = CreateRelicHeaderRow(detailPanel.transform, "Relic Header Row", new Vector2(0, 206), new Vector2(386, 48));
            var rarityBadge = CreateRarityBadge(detailHeaderRow, "Rarity Badge", rarityBadgeSprite);
            var rarityText = CreateText(rarityBadge.transform, "Rarity Text", "アンコモン", 13, Vector2.zero, new Vector2(82, 20), RelicRarityVisuals.GetBadgeTextColor(RelicRarity.Uncommon), TextAnchor.MiddleCenter);
            var detailTitle = CreateRelicNameText(detailHeaderRow, "Detail Title", "所持レリック", 32, Color.white);
            var descriptionText = CreateSection(detailPanel.transform, "Description", "説明", new Vector2(0, 78), new Vector2(430, 164));
            var effectText = CreateSection(detailPanel.transform, "Effect", "強化内容", new Vector2(0, -118), new Vector2(430, 164));
            var messageText = CreateText(detailPanel.transform, "Message Text", string.Empty, 18, new Vector2(0, -244), new Vector2(430, 28), new Color(1f, 0.9f, 0.55f), TextAnchor.MiddleCenter);

            var backButton = CreateButton(canvas.transform, "Back Button", "ロビーへ", new Vector2(0, -306), new Vector2(240, 54), GeneratedSpriteLoader.Load("Orb"));

            var controller = new GameObject("09_Relics Controller");
            SceneManager.MoveGameObjectToScene(controller, scene);
            var navigator = controller.AddComponent<SceneNavigator>();
            var screen = controller.AddComponent<RelicBookScreen>();
            screen.navigator = navigator;
            screen.backButton = backButton;
            screen.detailTitleText = detailTitle;
            screen.rarityBadgeImage = rarityBadge;
            screen.rarityText = rarityText;
            screen.descriptionText = descriptionText;
            screen.effectText = effectText;
            screen.messageText = messageText;
            screen.entries = entries;
            EditorUtility.SetDirty(controller);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, RelicScenePath);
        }

        static RelicBookEntryView[] CreateRelicCards(RectTransform content)
        {
            var definitions = RelicCatalog.All;
            var entries = new RelicBookEntryView[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                entries[i] = CreateRelicCard(content, definitions[i], i + 1);
            }

            return entries;
        }

        static RelicBookEntryView CreateRelicCard(Transform parent, RelicDefinition definition, int number)
        {
            var panel = CreateImage(parent, $"Relic Card {number:00}", CardColor);
            var rect = panel.rectTransform;
            rect.sizeDelta = new Vector2(92, 108);
            var layout = panel.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 92;
            layout.minHeight = 108;
            layout.preferredWidth = 92;
            layout.preferredHeight = 108;
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
            icon.sprite = GeneratedSpriteLoader.Load(definition.iconPath);
            if (icon.sprite == null) icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TreasureChestGeneratedPath);
            icon.preserveAspect = true;
            icon.rectTransform.anchoredPosition = new Vector2(0, 18);
            icon.rectTransform.sizeDelta = new Vector2(54, 54);
            icon.rectTransform.localScale = Vector3.one * RelicCatalog.IconScale(definition);

            var silhouette = CreateImage(icon.transform, "Silhouette Overlay", new Color(0f, 0f, 0f, 0.45f));
            silhouette.raycastTarget = false;
            Stretch(silhouette.rectTransform);

            var nameText = CreateText(panel.transform, "Name Text", definition.displayName, 13, new Vector2(0, -36), new Vector2(76, 40), Color.white, TextAnchor.MiddleCenter);
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var entry = panel.gameObject.AddComponent<RelicBookEntryView>();
            entry.relicType = definition.type;
            entry.button = button;
            entry.background = panel;
            entry.icon = icon;
            entry.silhouetteOverlay = silhouette;
            entry.nameText = nameText;
            return entry;
        }

        static ScrollRect CreateScrollView(Transform parent)
        {
            var scrollObject = CreateImage(parent, "Relic List Scroll View", new Color(0f, 0f, 0f, 0.12f));
            scrollObject.rectTransform.anchoredPosition = new Vector2(0, -18);
            scrollObject.rectTransform.sizeDelta = new Vector2(456, 416);

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

            var layout = contentObject.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = new Vector2(10, 12);
            layout.cellSize = new Vector2(78, 108);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 5;
            layout.childAlignment = TextAnchor.UpperCenter;

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

        static void AssignGameSceneChestPrefab()
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RelicChestPrefabPath);
            var panelPrefab = AssetDatabase.LoadAssetAtPath<RelicAcquisitionPanel>(RelicAcquisitionPanelPrefabPath);
            if (chestPrefab == null)
            {
                Debug.LogError("Relic chest prefab was not found: " + RelicChestPrefabPath);
                return;
            }

            var managers = Object.FindObjectsOfType<GameManager>(true);
            foreach (var manager in managers)
            {
                if (manager == null || manager.gameObject.scene != scene) continue;
                manager.relicChestPrefab = chestPrefab;
                manager.relicAcquisitionPanelPrefab = panelPrefab;
                EditorUtility.SetDirty(manager);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ApplyLobbyRelicButton()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            var lobbyRoot = FindRoot(scene, "Lobby UI");
            if (lobbyRoot == null)
            {
                Debug.LogError("Lobby UI was not found. Relic setup does not rebuild the lobby scene to avoid overwriting hand-authored layout.");
                return;
            }

            var weaponBookButton = FindChild(lobbyRoot.transform, "Weapon Book Button");
            if (weaponBookButton == null)
            {
                Debug.LogError("Weapon Book Button was not found. Place Relic Button manually near the character loadout.");
                return;
            }

            var relicButton = FindChild(lobbyRoot.transform, "Relic Button");
            var weaponBookRect = weaponBookButton.GetComponent<RectTransform>();
            Vector2 relicPosition = weaponBookRect != null ? weaponBookRect.anchoredPosition + new Vector2(240f, 0f) : new Vector2(30f, -170f);
            Vector2 relicSize = weaponBookRect != null ? weaponBookRect.sizeDelta : new Vector2(220f, 58f);
            if (relicButton == null)
            {
                relicButton = Object.Instantiate(weaponBookButton.gameObject, weaponBookButton.transform.parent).transform;
                relicButton.name = "Relic Button";
            }
            else
            {
                var relicRect = relicButton.GetComponent<RectTransform>();
                if (relicRect != null)
                {
                    relicPosition = relicRect.anchoredPosition;
                    relicSize = relicRect.sizeDelta;
                }
            }

            ConfigureButton(relicButton, "所持レリック", "TreasureChest", relicPosition, relicSize);
            ConfigureExistingButton(lobbyRoot.transform, "Start Game Button", "Arrow");
            ConfigureExistingButton(lobbyRoot.transform, "Upgrade Button", "Orb");
            ConfigureExistingButton(lobbyRoot.transform, "Weapon Book Button", "Slash_0");
            ConfigureExistingButton(lobbyRoot.transform, "Title Button", "Slash_0");

            var controller = FindRoot(scene, "03_Lobby Controller");
            var lobbyScreen = controller != null ? controller.GetComponent<LobbyScreen>() : null;
            if (lobbyScreen != null)
            {
                lobbyScreen.relicButton = relicButton.GetComponent<Button>();
                EditorUtility.SetDirty(lobbyScreen);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ConfigureExistingButton(Transform root, string buttonName, string iconPath)
        {
            var button = FindChild(root, buttonName);
            if (button == null) return;
            var rect = button.GetComponent<RectTransform>();
            ConfigureButton(button, null, iconPath, rect != null ? rect.anchoredPosition : Vector2.zero, rect != null ? rect.sizeDelta : new Vector2(220f, 58f));
        }

        static void ConfigureButton(Transform buttonTransform, string label, string iconPath, Vector2 position, Vector2 size)
        {
            var rect = buttonTransform.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }

            var text = FindChild(buttonTransform, "Label")?.GetComponent<Text>();
            if (text == null) text = buttonTransform.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                if (!string.IsNullOrEmpty(label)) text.text = label;
                text.rectTransform.anchoredPosition = new Vector2(18f, 0f);
                text.rectTransform.sizeDelta = new Vector2(Mathf.Max(40f, size.x - 58f), size.y);
                EditorUtility.SetDirty(text);
            }

            var icon = FindChild(buttonTransform, "Icon")?.GetComponent<Image>();
            if (icon == null)
            {
                var iconObject = new GameObject("Icon", typeof(RectTransform));
                iconObject.transform.SetParent(buttonTransform, false);
                icon = iconObject.AddComponent<Image>();
            }

            icon.sprite = GeneratedSpriteLoader.Load(iconPath);
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.rectTransform.anchoredPosition = new Vector2(-size.x * 0.34f, 0f);
            icon.rectTransform.sizeDelta = new Vector2(38f, 38f);
            icon.transform.SetAsFirstSibling();
            EditorUtility.SetDirty(icon);
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
            var glow = CreateImage(parent, "Relic Glow", new Color(0.28f, 0.22f, 0.08f, 0.1f));
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

        static Transform CreateRelicHeaderRow(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rowObject = new GameObject(name, typeof(RectTransform));
            rowObject.transform.SetParent(parent, false);
            var rect = rowObject.GetComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var layout = rowObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.spacing = 10f;
            return rowObject.transform;
        }

        static Image CreateRarityBadge(Transform parent, string name, Sprite sprite)
        {
            var image = CreateImage(parent, name, RelicRarityVisuals.GetColor(RelicRarity.Uncommon));
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.rectTransform.sizeDelta = new Vector2(86f, 24f);

            var layout = image.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 86f;
            layout.preferredWidth = 86f;
            layout.minHeight = 24f;
            layout.preferredHeight = 24f;
            return image;
        }

        static Text CreateRelicNameText(Transform parent, string name, string text, int fontSize, Color color)
        {
            var label = CreateText(parent, name, text, fontSize, Vector2.zero, Vector2.zero, color, TextAnchor.MiddleLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            var layout = label.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 0f;
            layout.flexibleWidth = 1f;
            layout.minHeight = fontSize + 8f;
            layout.preferredHeight = fontSize + 8f;
            return label;
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
            if (!scenes.Any(scene => scene.path == RelicScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(RelicScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
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
