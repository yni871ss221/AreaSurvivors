using AreaSurvivors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class WatchTowerSetup
    {
        const string SpritePath = "Assets/AreaSurvivors/Sprites/Generated/WatchTower.png";
        const string HammerSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Hammer.png";
        const string SparkleSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Sparkle.png";
        const string TilePath = "Assets/AreaSurvivors/TilePalette/WatchTower.asset";
        const string PrefabPath = "Assets/AreaSurvivors/Prefabs/WatchTower.prefab";
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";

        [MenuItem("Area Survivors/Setup Watch Tower")]
        public static void Run()
        {
            ConfigureSpriteImporter();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var tile = CreateTile(sprite);
            var prefab = CreatePrefab(sprite);
            UpdateConfig();
            UpdateGameScene(sprite, tile, prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Watch tower setup completed.");
        }

        static void ConfigureSpriteImporter()
        {
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static Tile CreateTile(Sprite sprite)
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, TilePath);
            }

            tile.name = "WatchTower";
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        static GameObject CreatePrefab(Sprite sprite)
        {
            var go = new GameObject("WatchTower");
            var marker = go.AddComponent<GridObjectMarker>();
            marker.type = GridObjectType.WatchTower;
            marker.flags = GridCellFlags.BlocksMovement | GridCellFlags.BlocksBuilding | GridCellFlags.Defensive;
            marker.footprint = new Vector2Int(2, 2);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.42f, 1.05f);
            trigger.offset = new Vector2(0f, -0.05f);

            var blocker = go.AddComponent<BoxCollider2D>();
            blocker.size = trigger.size;
            blocker.offset = trigger.offset;
            blocker.enabled = false;

            go.AddComponent<Health>();
            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;

            var tower = go.AddComponent<WatchTower>();
            tower.blockingCollider = blocker;
            tower.towerSprite = sprite;
            tower.spriteVisualSize = VisualSizeForWidth(sprite, 1.22f);
            tower.spriteVisualOffset = new Vector3(0f, -1f, 0f);
            tower.hammerRenderer = CreateOverlayVisual(go.transform, "Hammer", AssetDatabase.LoadAssetAtPath<Sprite>(HammerSpritePath), 22020);
            tower.sparkleRenderer = CreateOverlayVisual(go.transform, "Completion Sparkle", AssetDatabase.LoadAssetAtPath<Sprite>(SparkleSpritePath), 22030);

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static PaperMeshVisual CreateOverlayVisual(Transform parent, string name, Sprite sprite, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<PaperBillboard>();
            var visual = child.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, sortingOrder);
            visual.visible = false;
            var outline = child.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.022f;
            child.AddComponent<PreserveSortingOrder>();
            return visual;
        }

        static Vector2 VisualSizeForWidth(Sprite sprite, float width)
        {
            if (sprite == null || sprite.bounds.size.x <= 0.001f) return new Vector2(width, width);
            float height = sprite.bounds.size.y * (width / sprite.bounds.size.x);
            return new Vector2(width, height);
        }

        static void UpdateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/AreaSurvivors/Resources/Config/GameConfig.asset");
            if (config == null) return;
            config.watchTowerBuildSeconds = 3.2f;
            config.watchTowerMaxHp = 100;
            config.watchTowerWoodCost = 50;
            config.watchTowerStoneCost = 50;
            config.watchTowerAutoPaintIntervalSeconds = 2f;
            config.watchTowerAutoPaintRadiusCells = 10;
            EditorUtility.SetDirty(config);
        }

        static void UpdateGameScene(Sprite sprite, Tile tile, GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var placement = Object.FindObjectOfType<BuildPlacementController>(true);
            if (placement != null)
            {
                placement.watchTowerPrefab = prefab;
                placement.watchTowerPreviewSprite = sprite;
                placement.watchTowerTile = tile;
                EditorUtility.SetDirty(placement);
            }

            var menuObject = GameObject.Find("Construction Menu");
            if (menuObject != null)
            {
                UpdateConstructionMenu(menuObject.transform, sprite);
                EditorUtility.SetDirty(menuObject);
            }

            EditorSceneManager.SaveScene(scene);
        }

        static void UpdateConstructionMenu(Transform menu, Sprite sprite)
        {
            var menuRect = menu.GetComponent<RectTransform>();
            var slotPositions = BuildSlotPositions(menu);
            var statusPosition = new Vector2(slotPositions[5].x + 70f, slotPositions[5].y);
            if (menuRect != null) EnsureMenuBounds(menuRect, slotPositions, statusPosition);

            var status = menu.Find("Build Status") as RectTransform;
            if (status != null)
            {
                status.anchoredPosition = statusPosition;
                status.sizeDelta = new Vector2(58f, 58f);
                var text = status.GetComponent<Text>();
                if (text != null) text.fontSize = 13;
            }

            var existing = menu.Find("Build Slot 4");
            if (existing == null)
            {
                var source = menu.Find("Build Slot 3");
                if (source == null) return;
                existing = Object.Instantiate(source.gameObject, menu).transform;
                existing.name = "Build Slot 4";
            }

            var slotRect = existing.GetComponent<RectTransform>();
            if (slotRect != null)
            {
                slotRect.anchorMin = Vector2.zero;
                slotRect.anchorMax = Vector2.zero;
                slotRect.pivot = Vector2.zero;
                slotRect.anchoredPosition = slotPositions[3];
            }
            SetText(existing, "Key", "4");
            SetText(existing, "Stock", "ロック");

            var icon = existing.Find("Icon");
            if (icon != null)
            {
                var image = icon.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = sprite;
                    image.preserveAspect = true;
                    image.rectTransform.sizeDelta = new Vector2(34f, 50f);
                }
            }

            EditorUtility.SetDirty(existing);
        }

        static Vector2[] BuildSlotPositions(Transform menu)
        {
            var result = new[]
            {
                new Vector2(41f, 15f),
                new Vector2(111f, 15f),
                new Vector2(181f, 15f),
                new Vector2(251f, 15f),
                new Vector2(321f, 15f),
                new Vector2(391f, 15f)
            };

            for (int i = 0; i < result.Length; i++)
            {
                var slot = menu.Find("Build Slot " + (i + 1)) as RectTransform;
                if (slot != null) result[i] = slot.anchoredPosition;
            }

            var spacing = result[2].x - result[1].x;
            if (Mathf.Abs(spacing) < 1f) spacing = result[1].x - result[0].x;
            if (Mathf.Abs(spacing) < 1f) spacing = 70f;
            if (menu.Find("Build Slot 4") == null) result[3] = new Vector2(result[2].x + spacing, result[2].y);
            if (menu.Find("Build Slot 5") == null) result[4] = new Vector2(result[3].x + spacing, result[3].y);
            if (menu.Find("Build Slot 6") == null) result[5] = new Vector2(result[4].x + spacing, result[4].y);
            return result;
        }

        static void EnsureMenuBounds(RectTransform menuRect, Vector2[] slotPositions, Vector2 statusPosition)
        {
            const float slotHeight = 66f;
            const float statusWidth = 58f;
            const float statusHeight = 58f;
            const float margin = 12f;

            var size = menuRect.sizeDelta;
            size.x = Mathf.Max(size.x, statusPosition.x + statusWidth + margin);
            size.y = Mathf.Max(size.y, Mathf.Max(slotPositions[0].y + slotHeight, statusPosition.y + statusHeight) + margin);
            menuRect.sizeDelta = size;
        }

        static void SetText(Transform root, string childName, string value)
        {
            var child = root.Find(childName);
            if (child == null) return;
            var text = child.GetComponent<Text>();
            if (text != null) text.text = value;
        }
    }
}
