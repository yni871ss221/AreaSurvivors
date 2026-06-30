using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class BuildingPrefabLayoutBuilder
    {
        const string PrefabRoot = "Assets/AreaSurvivors/Prefabs";
        const float WoodenBarrierBaseHeightMultiplier = 89f / 87f;
        const float BallistaBaseHeightMultiplier = 221f / 190f;
        const float WatchTowerBaseHeightMultiplier = 419f / 367f;

        [MenuItem("AreaSurvivors/Setup Building Prefab Layouts")]
        public static void SetupBuildingPrefabLayouts()
        {
            NormalizeBuildingSpriteImports();

            SetupWoodenBarrierPrefab(
                $"{PrefabRoot}/WoodenWall.prefab",
                "WoodenWall",
                "WoodenWall",
                "WoodenWallUpgrade");

            SetupBallistaPrefab($"{PrefabRoot}/BallistaTower.prefab");
            SetupWatchTowerPrefab($"{PrefabRoot}/WatchTower.prefab");
            SetupCenterTowerPrefab($"{PrefabRoot}/CenterTower.prefab");
            UpdateGameSceneReferences();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void SetupWoodenBarrierPrefab(string path, string rootName, string baseSpriteName, string upgradeSpriteName, string sourcePrefabPath = null)
        {
            EnsurePrefabExists(path, sourcePrefabPath);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = rootName;
                var marker = Ensure<GridObjectMarker>(root);
                marker.footprint = new Vector2Int(3, 1);
                var gridVisual = Ensure<GridObjectVisual>(root);
                gridVisual.ConfigureFootprint(marker.footprint);
                gridVisual.footprint = marker.footprint;
                gridVisual.fitVisualWidthToFootprint = false;
                gridVisual.resetVisualOffset = false;

                var barrier = Ensure<WoodenBarrier>(root);
                barrier.barrierSprite = LoadGeneratedSprite(baseSpriteName);

                var set = Ensure<BuildingPrefabVisualSet>(root);
                ConfigureVisualSet(root, set, marker.footprint, barrier.barrierSprite, LoadGeneratedSprite(upgradeSpriteName), 0.026f, WoodenBarrierBaseHeightMultiplier);

                barrier.completeRenderer = set.completeVisual;
                barrier.sparkleRenderer = set.sparkleVisual;
                barrier.completeObject = set.completeVisual != null ? set.completeVisual.gameObject : null;
                barrier.blockingCollider = ConfigureCollider(root, marker.footprint, false);
                ConfigureCollider(root, marker.footprint, true);
                DestroyChild(root.transform, "Build Gauge");
                CleanMissingScripts(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetupBallistaPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var marker = Ensure<GridObjectMarker>(root);
                marker.footprint = new Vector2Int(2, 2);
                var gridVisual = Ensure<GridObjectVisual>(root);
                gridVisual.ConfigureFootprint(marker.footprint);
                gridVisual.footprint = marker.footprint;
                gridVisual.fitVisualWidthToFootprint = false;
                gridVisual.resetVisualOffset = false;

                var ballista = Ensure<BallistaTower>(root);
                ballista.ballistaSprite = LoadGeneratedSprite("Ballista");
                var set = Ensure<BuildingPrefabVisualSet>(root);
                ConfigureVisualSet(root, set, marker.footprint, ballista.ballistaSprite, LoadGeneratedSprite("BallistaUpgrade"), 0.035f, BallistaBaseHeightMultiplier);
                ballista.completeRenderer = set.completeVisual;
                ballista.sparkleRenderer = set.sparkleVisual;
                ballista.completeObject = set.completeVisual != null ? set.completeVisual.gameObject : null;
                ballista.blockingCollider = ConfigureCollider(root, marker.footprint, false);
                ConfigureCollider(root, marker.footprint, true);
                DestroyChild(root.transform, "Build Gauge");
                CleanMissingScripts(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetupWatchTowerPrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var marker = Ensure<GridObjectMarker>(root);
                marker.footprint = new Vector2Int(2, 2);
                var gridVisual = Ensure<GridObjectVisual>(root);
                gridVisual.ConfigureFootprint(marker.footprint);
                gridVisual.footprint = marker.footprint;
                gridVisual.fitVisualWidthToFootprint = false;
                gridVisual.resetVisualOffset = false;

                var watchTower = Ensure<WatchTower>(root);
                watchTower.towerSprite = LoadGeneratedSprite("WatchTower");
                var set = Ensure<BuildingPrefabVisualSet>(root);
                ConfigureVisualSet(root, set, marker.footprint, watchTower.towerSprite, LoadGeneratedSprite("WatchTowerUpgrade"), 0.03f, WatchTowerBaseHeightMultiplier);
                watchTower.completeRenderer = set.completeVisual;
                watchTower.sparkleRenderer = set.sparkleVisual;
                watchTower.completeObject = set.completeVisual != null ? set.completeVisual.gameObject : null;
                watchTower.blockingCollider = ConfigureCollider(root, marker.footprint, false);
                ConfigureCollider(root, marker.footprint, true);
                DestroyChild(root.transform, "Build Gauge");
                CleanMissingScripts(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetupCenterTowerPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject root;
            if (prefab == null)
            {
                root = new GameObject("CenterTower");
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
            }

            root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ConfigureCenterTowerObject(root);
                CleanMissingScripts(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ConfigureCenterTowerObject(GameObject root)
        {
            root.name = "CenterTower";
            var marker = Ensure<GridObjectMarker>(root);
            marker.type = GridObjectType.Tower;
            marker.flags = GridCellFlags.BlocksMovement | GridCellFlags.BlocksBuilding | GridCellFlags.Defensive;
            marker.footprint = new Vector2Int(3, 3);

            var gridVisual = Ensure<GridObjectVisual>(root);
            gridVisual.ConfigureFootprint(marker.footprint);
            gridVisual.footprint = marker.footprint;
            gridVisual.fitVisualWidthToFootprint = false;
            gridVisual.resetVisualOffset = false;

            var rb = Ensure<Rigidbody2D>(root);
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.bodyType = RigidbodyType2D.Static;
            ConfigureCollider(root, marker.footprint, false);
            ConfigureCollider(root, marker.footprint, true);

            Ensure<Health>(root);
            var tower = Ensure<TowerController>(root);
            tower.upgradedVisualScale = Vector3.one;
            tower.upgradedVisualOffset = Vector3.zero;

            var baseVisual = ConfigureVisual(root.transform, "Base Tower Image", LoadGeneratedSprite("Tower"), Color.white, 1003, marker.footprint, 0.018f);
            var complete = ConfigureVisual(root.transform, "Upgraded Tower Image", LoadGeneratedSprite("TowerUpgrade"), Color.white, 22002, marker.footprint, 0.018f);
            var sparkle = ConfigureOverlay(root.transform, "Upgrade Sparkle", LoadGeneratedSprite("Sparkle"), 22030, new Vector3(0.18f, 2.2f, 0f), 0.7f);

            if (baseVisual != null) baseVisual.visible = true;
            if (complete != null) complete.visible = false;
            if (sparkle != null) sparkle.visible = false;

            var ySort = Ensure<YSort>(root);
            ySort.baseOrder = 1000;
            ySort.sortPivotOffsetY = 0f;
            ySort.Refresh();
        }

        static void ConfigureVisualSet(GameObject root, BuildingPrefabVisualSet set, Vector2Int footprint, Sprite baseSprite, Sprite upgradeSprite, float outlineThickness, float baseHeightMultiplier = 1f)
        {
            set.usePrefabLayout = true;
            set.completeVisual = ConfigureVisual(root.transform, "Complete Image", baseSprite, Color.white, 1002, footprint, outlineThickness, baseHeightMultiplier);
            set.upgradedCompleteVisual = ConfigureVisual(root.transform, "Upgraded Building Image", upgradeSprite, Color.white, 22002, footprint, outlineThickness);
            set.sparkleVisual = ConfigureOverlay(root.transform, "Completion Sparkle", LoadGeneratedSprite("Sparkle"), 22030, new Vector3(0.32f, footprint.y * GridObjectVisual.CellHeight + 0.36f, 0f), 0.7f);
            set.ApplyInitialVisibility();
        }

        static PaperMeshVisual ConfigureVisual(Transform parent, string name, Sprite sprite, Color color, int sortingOrder, Vector2Int footprint, float outlineThickness, float heightMultiplier = 1f)
        {
            var go = FindOrCreateChild(parent, name);
            var billboard = Ensure<PaperBillboard>(go);
            billboard.faceCamera = false;
            var visual = Ensure<PaperMeshVisual>(go);
            visual.useBottomCenterAnchor = true;
            visual.Configure(sprite, color, sortingOrder);
            FitWidthPreserveAspect(go.transform, sprite, footprint, heightMultiplier);
            var outline = Ensure<RuntimeSpriteOutline>(go);
            outline.outlineColor = Color.black;
            outline.thickness = outlineThickness;
            Ensure<OcclusionMaskSource>(go);
            return visual;
        }

        static PaperMeshVisual ConfigureOverlay(Transform parent, string name, Sprite sprite, int sortingOrder, Vector3 localPosition, float scale)
        {
            var go = FindOrCreateChild(parent, name);
            go.transform.localPosition = localPosition;
            go.transform.localScale = Vector3.one * scale;
            var billboard = Ensure<PaperBillboard>(go);
            billboard.faceCamera = true;
            var visual = Ensure<PaperMeshVisual>(go);
            visual.Configure(sprite, Color.white, sortingOrder);
            Ensure<PreserveSortingOrder>(go);
            var outline = Ensure<RuntimeSpriteOutline>(go);
            outline.outlineColor = Color.black;
            outline.thickness = 0.022f;
            return visual;
        }

        static void FitWidthPreserveAspect(Transform transform, Sprite sprite, Vector2Int footprint, float heightMultiplier = 1f)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (sprite == null || Mathf.Abs(sprite.bounds.size.x) <= 0.001f)
            {
                transform.localScale = Vector3.one;
                return;
            }

            float targetWidth = Mathf.Max(0.01f, footprint.x * GridObjectVisual.CellWidth);
            float scale = targetWidth / sprite.bounds.size.x;
            float yScale = scale * Mathf.Max(0.01f, heightMultiplier);
            transform.localScale = new Vector3(scale, yScale, 1f);
        }

        static BoxCollider2D ConfigureCollider(GameObject root, Vector2Int footprint, bool trigger)
        {
            BoxCollider2D candidate = null;
            foreach (var box in root.GetComponents<BoxCollider2D>())
            {
                if (box.isTrigger == trigger)
                {
                    candidate = box;
                    break;
                }
            }

            if (candidate == null) candidate = root.AddComponent<BoxCollider2D>();
            var size = new Vector2(footprint.x * GridObjectVisual.CellWidth, footprint.y * GridObjectVisual.CellHeight);
            const float bottomInset = 0.1f;
            float resolvedInset = trigger ? 0f : Mathf.Clamp(bottomInset, 0f, Mathf.Max(0f, size.y - 0.01f));
            var colliderSize = new Vector2(size.x, size.y - resolvedInset);
            candidate.isTrigger = trigger;
            candidate.size = colliderSize;
            candidate.offset = new Vector2(0f, resolvedInset + colliderSize.y * 0.5f);
            if (!trigger)
            {
                candidate.edgeRadius = 0.04f;
                candidate.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/AreaSurvivors/Physics/CharacterSlide.physicsMaterial2D");
            }
            return candidate;
        }

        static GameObject FindOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        static Vector2 VisualSizeForWidth(Sprite sprite, float width)
        {
            if (sprite == null || sprite.bounds.size.x <= 0.001f) return new Vector2(width, width);
            return new Vector2(width, sprite.bounds.size.y * (width / sprite.bounds.size.x));
        }

        static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        static int CleanMissingScripts(GameObject root)
        {
            var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            foreach (Transform child in root.transform)
            {
                removed += CleanMissingScripts(child.gameObject);
            }

            return removed;
        }

        static Sprite LoadGeneratedSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/AreaSurvivors/Sprites/Generated/{name}.png");
        }

        static void NormalizeBuildingSpriteImports()
        {
            var generated = new[]
            {
                "Ballista",
                "BallistaUpgrade",
                "Tower",
                "TowerUpgrade",
                "WatchTower",
                "WatchTowerUpgrade",
                "WoodenWall",
                "WoodenWallUpgrade",
            };
            foreach (var name in generated)
            {
                NormalizeGeneratedSpriteImport(name);
            }

            var externalSources = new[]
            {
                "BallistaSource_20260616_122017",
                "BallistaUpgradeSource_20260615_154348",
                "TowerSquareSource_20260614_211704",
                "TowerUpgradeSquareSource_20260614_234350",
                "WatchTowerSquareSource_20260615_000239",
                "WatchTowerUpgradeSource_20260615_154952",
                "WoodenWallSource_20260616_204904",
                "WoodenWallUpgradeSource_20260616_221042",
            };
            foreach (var name in externalSources)
            {
                NormalizeExternalSpriteImport(name);
            }
        }

        static void NormalizeGeneratedSpriteImport(string name)
        {
            NormalizeSpriteImport($"Assets/AreaSurvivors/Sprites/Generated/{name}.png");
        }

        static void NormalizeExternalSpriteImport(string name)
        {
            NormalizeSpriteImport($"Assets/AreaSurvivors/Sprites/External/{name}.png");
        }

        static void NormalizeSpriteImport(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null && AssetImporter.GetAtPath(path) == null) return;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static void EnsurePrefabExists(string targetPath, string sourcePath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(targetPath) != null) return;
            if (!string.IsNullOrEmpty(sourcePath) && AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) != null)
            {
                AssetDatabase.CopyAsset(sourcePath, targetPath);
                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            }
        }

        static void UpdateGameSceneReferences()
        {
            var scene = EditorSceneManager.OpenScene("Assets/AreaSurvivors/Scenes/05_Game.unity", OpenSceneMode.Single);
            var placement = Object.FindObjectOfType<BuildPlacementController>();
            if (placement != null)
            {
                placement.woodenWallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/WoodenWall.prefab");
                placement.ballistaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/BallistaTower.prefab");
                placement.watchTowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/WatchTower.prefab");
                placement.woodenWallPreviewSprite = LoadGeneratedSprite("WoodenWall");
                EditorUtility.SetDirty(placement);
            }

            var tower = Object.FindObjectOfType<TowerController>();
            if (tower != null)
            {
                ConfigureCenterTowerObject(tower.gameObject);
                EditorUtility.SetDirty(tower.gameObject);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
