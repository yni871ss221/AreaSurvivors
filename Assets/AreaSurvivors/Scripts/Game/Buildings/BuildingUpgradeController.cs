using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class BuildingUpgradeController : MonoBehaviour
    {
        GameManager owner;
        GameConfig config;
        TileGrid grid;
        TowerController tower;
        Camera mainCamera;
        Image cursorIcon;
        Sprite upgradeIcon;
        Sprite upgradedTowerSprite;
        bool active;
        TowerController hoverTower;
        BuildingUpgradeTarget hoverBuilding;

        public bool IsActive => active;

        public void Initialize(GameManager gameManager, GameConfig gameConfig, TileGrid tileGrid, TowerController centerTower, Canvas hudCanvas)
        {
            owner = gameManager;
            config = gameConfig;
            grid = tileGrid;
            tower = centerTower;
            mainCamera = Camera.main;
            upgradeIcon = LoadGeneratedSprite("UpgradeBuildingIcon");
            upgradedTowerSprite = centerTower != null ? centerTower.GetConfiguredUpgradeSprite() : null;
            EnsureCursorIcon(hudCanvas);
            SetActive(false);
        }

        void Update()
        {
            if (!active) return;
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                SetActive(false);
                return;
            }

            FollowCursor();
            UpdateHover();
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUi()) ConfirmHover();
        }

        public void Toggle()
        {
            SetActive(!active);
        }

        public void SetActive(bool enabled)
        {
            active = enabled && IsAnyUpgradeUnlocked();
            if (active && owner != null && owner.buildPlacement != null) owner.buildPlacement.CancelActiveSelection();
            if (!active) ClearHover();
            if (cursorIcon != null) cursorIcon.gameObject.SetActive(active);
        }

        void UpdateHover()
        {
            var nextTower = FindTowerUnderCursor();
            var nextBuilding = nextTower == null ? FindBuildingUnderCursor() : null;
            if (nextTower != hoverTower || nextBuilding != hoverBuilding)
            {
                ClearHover();
                hoverTower = nextTower;
                hoverBuilding = nextBuilding;
            }

            if (hoverTower == null && hoverBuilding == null)
            {
                SetCursorSprite(upgradeIcon);
                return;
            }

            if (hoverTower != null)
            {
                bool canUpgrade = CanReserve(hoverTower);
                hoverTower.ShowUpgradePreview(upgradedTowerSprite, canUpgrade);
                SetCursorSprite(upgradeIcon);
                return;
            }

            hoverBuilding.ShowUpgradePreview(CanReserve(hoverBuilding));
            SetCursorSprite(upgradeIcon);
        }

        void ConfirmHover()
        {
            if (hoverTower == null && hoverBuilding == null) return;
            if (hoverTower != null)
            {
                if (!CanReserve(hoverTower)) return;
                if (owner == null || config == null) return;
                if (!SpendUpgradeResources(config.towerUpgradeWoodCost, config.towerUpgradeStoneCost)) return;
                hoverTower.CompleteUpgrade(config, grid, upgradedTowerSprite);
                SetActive(false);
                return;
            }

            if (!CanReserve(hoverBuilding)) return;
            if (owner == null || config == null) return;
            if (!SpendUpgradeResources(hoverBuilding.woodCost, hoverBuilding.stoneCost)) return;
            hoverBuilding.CompleteUpgrade();
            BuildingPersistentState.TryMarkUpgraded(hoverBuilding.gameObject);
            SetActive(false);
        }

        bool CanReserve(TowerController target)
        {
            if (target == null || config == null || owner == null) return false;
            return target.CanStartUpgrade() &&
                HasUpgradeResources(config.towerUpgradeWoodCost, config.towerUpgradeStoneCost) &&
                IsTowerUpgradeUnlocked();
        }

        bool CanReserve(BuildingUpgradeTarget target)
        {
            if (target == null || config == null || owner == null) return false;
            return target.CanStartUpgrade() &&
                HasUpgradeResources(target.woodCost, target.stoneCost) &&
                IsBuildingUpgradeUnlocked(target.kind);
        }

        bool HasUpgradeResources(int wood, int stone)
        {
            wood = Mathf.Max(0, wood);
            stone = Mathf.Max(0, stone);
            if (owner != null && owner.SessionMode == MapSessionMode.Build)
            {
                return ProgressionStore.HasPersistentResources(wood, stone);
            }

            return owner == null || owner.HasResources(wood, stone);
        }

        bool SpendUpgradeResources(int wood, int stone)
        {
            wood = Mathf.Max(0, wood);
            stone = Mathf.Max(0, stone);
            if (owner != null && owner.SessionMode == MapSessionMode.Build)
            {
                bool spent = ProgressionStore.TrySpendPersistentResources(wood, stone);
                if (spent) owner.SyncPersistentResources();
                return spent;
            }

            return owner == null || owner.TrySpendResources(wood, stone);
        }

        TowerController FindTowerUnderCursor()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return null;
            if (!TryGetPointerWorld(out var world)) return null;
            var hits = Physics2D.OverlapPointAll(world);
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var target = hit.GetComponentInParent<TowerController>();
                if (target != null && target.ContainsUpgradePointer(world)) return target;
            }

            if (tower != null && tower.ContainsUpgradePointer(world)) return tower;
            return null;
        }

        BuildingUpgradeTarget FindBuildingUnderCursor()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return null;
            if (!TryGetPointerWorld(out var world)) return null;
            var hits = Physics2D.OverlapPointAll(world);
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var target = hit.GetComponentInParent<BuildingUpgradeTarget>();
                if (target != null) return target;
            }

            if (grid != null)
            {
                var cell = grid.WorldToCell(world);
                var record = grid.GetObject(cell);
                var target = record != null && record.instance != null ? record.instance.GetComponentInParent<BuildingUpgradeTarget>() : null;
                if (target != null) return target;
            }

            return null;
        }

        bool TryGetPointerWorld(out Vector3 world)
        {
            world = Vector3.zero;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return false;
            var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.forward, Vector3.zero);
            if (!ground.Raycast(ray, out float distance)) return false;
            world = ray.GetPoint(distance);
            return true;
        }

        void ClearHover()
        {
            if (hoverTower != null) hoverTower.HideUpgradePreview();
            if (hoverBuilding != null) hoverBuilding.HideUpgradePreview();
            hoverTower = null;
            hoverBuilding = null;
        }

        static bool IsTowerUpgradeUnlocked()
        {
            return ProgressionStore.IsUnlocked(UpgradeType.UnlockTowerUpgrade);
        }

        static bool IsAnyUpgradeUnlocked()
        {
            return ProgressionStore.IsUnlocked(UpgradeType.WallUpgrade) ||
                ProgressionStore.IsUnlocked(UpgradeType.BallistaUpgrade) ||
                ProgressionStore.IsUnlocked(UpgradeType.WatchTowerUpgrade);
        }

        static bool IsBuildingUpgradeUnlocked(BuildingUpgradeKind kind)
        {
            switch (kind)
            {
                case BuildingUpgradeKind.WoodenWall:
                    return ProgressionStore.IsUnlocked(UpgradeType.WallUpgrade);
                case BuildingUpgradeKind.Ballista:
                    return ProgressionStore.IsUnlocked(UpgradeType.BallistaUpgrade);
                case BuildingUpgradeKind.WatchTower:
                    return ProgressionStore.IsUnlocked(UpgradeType.WatchTowerUpgrade);
                default:
                    return false;
            }
        }

        void EnsureCursorIcon(Canvas hudCanvas)
        {
            if (hudCanvas == null) return;
            var existing = hudCanvas.transform.Find("Upgrade Cursor Icon");
            cursorIcon = existing != null ? existing.GetComponent<Image>() : null;
            if (cursorIcon == null)
            {
                cursorIcon = new GameObject("Upgrade Cursor Icon").AddComponent<Image>();
                cursorIcon.transform.SetParent(hudCanvas.transform, false);
            }
            cursorIcon.sprite = upgradeIcon;
            cursorIcon.preserveAspect = true;
            cursorIcon.raycastTarget = false;
            cursorIcon.rectTransform.sizeDelta = new Vector2(44f, 44f);
            cursorIcon.transform.SetAsLastSibling();
        }

        void FollowCursor()
        {
            if (cursorIcon == null) return;
            cursorIcon.rectTransform.position = Input.mousePosition + new Vector3(18f, -18f, 0f);
        }

        void SetCursorSprite(Sprite sprite)
        {
            if (cursorIcon == null || sprite == null) return;
            cursorIcon.sprite = sprite;
        }

        static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        static Sprite LoadGeneratedSprite(string name)
        {
            var sprite = GeneratedSpriteLoader.Load(name);
            if (sprite != null) return sprite;
            var texture = GeneratedSpriteLoader.LoadTexture(name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 128f);
        }
    }

    public enum BuildingUpgradeKind
    {
        WoodenWall,
        Ballista,
        WatchTower
    }

    public sealed class BuildingUpgradeTarget : MonoBehaviour
    {
        public BuildingUpgradeKind kind;
        public int woodCost;
        public int stoneCost;
        public int hpBonus = 100;
        public int attackBonus;
        public int paintRadiusBonus;
        public string upgradedSpriteResource;

        Health health;
        GridObjectMarker marker;
        GridObjectVisual gridVisual;
        BuildingPrefabVisualSet prefabVisualSet;
        PaperMeshVisual upgradedCompleteVisual;
        PaperMeshVisual upgradeSparkleVisual;
        Sprite upgradedSprite;
        Vector3 upgradedCompleteBaseScale = Vector3.one;
        float sparkleTimer;
        bool isUpgraded;
        bool visualsPrepared;
        bool usingPrefabLayout;
        const float SparkleDuration = 0.75f;

        public bool IsUpgraded => isUpgraded;
        public Vector2Int Footprint => marker != null ? marker.footprint : Vector2Int.one;
        public bool IsBuilt
        {
            get
            {
                var construction = GetConstruction();
                return construction == null || construction.IsBuilt;
            }
        }

        void Awake()
        {
            health = GetComponent<Health>();
            marker = GetComponent<GridObjectMarker>();
            EnsureGridObjectVisual();
            UsePrefabVisualSetIfAvailable();
        }

        void Update()
        {
            AnimateUpgradeSparkle();
        }

        public void Configure(BuildingUpgradeKind upgradeKind, int wood, int stone, string spriteName, int hp = 100, int attack = 0, int paintRadius = 0)
        {
            kind = upgradeKind;
            woodCost = Mathf.Max(0, wood);
            stoneCost = Mathf.Max(0, stone);
            upgradedSpriteResource = spriteName;
            hpBonus = Mathf.Max(0, hp);
            attackBonus = Mathf.Max(0, attack);
            paintRadiusBonus = Mathf.Max(0, paintRadius);
            upgradedSprite = null;
            visualsPrepared = false;
            EnsureGridObjectVisual();
        }

        public bool CanStartUpgrade()
        {
            return IsBuilt && !isUpgraded && health != null && !health.IsDead;
        }

        public void ShowUpgradePreview(bool allowed)
        {
            if (isUpgraded) return;
            EnsureSprites();
            EnsureUpgradeVisuals();
            if (upgradedCompleteVisual == null) return;
            upgradedCompleteVisual.color = allowed ? new Color(0.30f, 0.82f, 1f, 0.42f) : new Color(1f, 0.20f, 0.16f, 0.42f);
            upgradedCompleteVisual.visible = true;
        }

        public void HideUpgradePreview()
        {
            if (!isUpgraded && upgradedCompleteVisual != null) upgradedCompleteVisual.visible = false;
        }

        public void HideUpgradeConstruction()
        {
            if (!isUpgraded && upgradedCompleteVisual != null) upgradedCompleteVisual.visible = false;
        }

        public void CompleteUpgrade()
        {
            if (isUpgraded) return;
            isUpgraded = true;
            HideUpgradeConstruction();
            EnsureSprites();
            EnsureUpgradeVisuals();
            ApplyUpgradeToBuilding();
            SetBaseVisualVisible(false);
            RefreshYSortRenderers();
            ShowUpgradedCompleteVisual();
            sparkleTimer = SparkleDuration;
            if (upgradeSparkleVisual != null)
            {
                PixelBurstEffect.Spawn(upgradeSparkleVisual.sprite, transform.position + new Vector3(0f, 0.7f, 0f), new Color(1f, 0.96f, 0.52f, 0.72f), 8, 0.32f, 0.3f, WeaponSortingOrders.ImpactBurst);
            }
        }

        IBuildableConstruction GetConstruction()
        {
            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour == this) continue;
                var construction = behaviour as IBuildableConstruction;
                if (construction != null) return construction;
            }
            return null;
        }

        void ApplyUpgradeToBuilding()
        {
            if (upgradedSprite == null)
            {
                Debug.LogWarning($"Upgrade sprite was not loaded for {name}: {upgradedSpriteResource}");
            }

            var barrier = GetComponent<WoodenBarrier>();
            if (barrier != null)
            {
                barrier.ApplyBuildingUpgrade(upgradedSprite, hpBonus);
                return;
            }

            var ballista = GetComponent<BallistaTower>();
            if (ballista != null)
            {
                ballista.ApplyBuildingUpgrade(upgradedSprite, hpBonus, attackBonus);
                return;
            }

            var watchTower = GetComponent<WatchTower>();
            if (watchTower != null)
            {
                watchTower.ApplyBuildingUpgrade(upgradedSprite, hpBonus, paintRadiusBonus);
            }
        }

        void SetBaseVisualVisible(bool visible)
        {
            var barrier = GetComponent<WoodenBarrier>();
            if (barrier != null)
            {
                barrier.SetCompletedVisualVisible(visible);
                return;
            }

            var ballista = GetComponent<BallistaTower>();
            if (ballista != null)
            {
                ballista.SetCompletedVisualVisible(visible);
                return;
            }

            var watchTower = GetComponent<WatchTower>();
            if (watchTower != null)
            {
                watchTower.SetCompletedVisualVisible(visible);
            }
        }

        public void SetUpgradedCompleteSprite(Sprite sprite)
        {
            if (!isUpgraded || sprite == null) return;
            EnsureUpgradeVisuals();
            if (upgradedCompleteVisual != null) upgradedCompleteVisual.sprite = sprite;
            ConfigureUpgradeVisual(upgradedCompleteVisual, sprite, Color.white);
            if (upgradedCompleteVisual != null)
            {
                upgradedCompleteVisual.visible = true;
                upgradedCompleteVisual.color = Color.white;
                upgradedCompleteBaseScale = upgradedCompleteVisual.transform.localScale;
            }
            RefreshYSortRenderers();
        }

        void EnsureSprites()
        {
            UsePrefabVisualSetIfAvailable();
            if (upgradedSprite == null && prefabVisualSet != null && prefabVisualSet.upgradedCompleteVisual != null)
            {
                upgradedSprite = prefabVisualSet.upgradedCompleteVisual.sprite;
            }
        }

        void EnsureGridObjectVisual()
        {
            if (gridVisual == null) gridVisual = GetComponent<GridObjectVisual>();
            if (gridVisual == null) gridVisual = gameObject.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureFootprint(Footprint);
            gridVisual.fitVisualWidthToFootprint = true;
            gridVisual.resetVisualOffset = true;
        }

        void EnsureUpgradeVisuals()
        {
            if (upgradedSprite == null) return;
            UsePrefabVisualSetIfAvailable();
            if (visualsPrepared)
            {
                ConfigureUpgradeVisual(upgradedCompleteVisual, upgradedSprite, upgradedCompleteVisual != null ? upgradedCompleteVisual.color : Color.white);
                return;
            }

            if (usingPrefabLayout && prefabVisualSet != null && prefabVisualSet.HasUpgradeVisuals)
            {
                upgradedCompleteVisual = prefabVisualSet.upgradedCompleteVisual;
                upgradeSparkleVisual = prefabVisualSet.sparkleVisual;
                ConfigureUpgradeVisual(upgradedCompleteVisual, upgradedSprite, Color.white);
            }
            else
            {
                upgradedCompleteVisual = CreateUpgradeVisual("Upgraded Building Image", upgradedSprite, Color.white, 22002);
                upgradeSparkleVisual = CreateOverlayVisual("Upgrade Sparkle", null, 22030);
            }
            if (upgradedCompleteVisual != null) upgradedCompleteBaseScale = upgradedCompleteVisual.transform.localScale;
            if (upgradedCompleteVisual != null) upgradedCompleteVisual.visible = false;
            if (upgradeSparkleVisual != null) upgradeSparkleVisual.visible = false;
            RefreshYSortRenderers();
            visualsPrepared = true;
        }

        void ShowUpgradedCompleteVisual()
        {
            EnsureUpgradeVisuals();
            if (upgradedCompleteVisual == null) return;
            upgradedCompleteVisual.visible = true;
            upgradedCompleteVisual.color = Color.white;
            upgradedCompleteVisual.transform.localScale = upgradedCompleteBaseScale;
            upgradedCompleteVisual.SetVerticalFill(1f);
        }

        PaperMeshVisual CreateUpgradeVisual(string objectName, Sprite sprite, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var billboard = go.AddComponent<PaperBillboard>();
            billboard.faceCamera = false;
            var mesh = go.AddComponent<PaperMeshVisual>();
            mesh.Configure(sprite, color, sortingOrder);
            ConfigureUpgradeVisual(mesh, sprite, color);
            return mesh;
        }

        PaperMeshVisual CreateOverlayVisual(string objectName, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<PaperBillboard>();
            var mesh = go.AddComponent<PaperMeshVisual>();
            mesh.Configure(sprite, Color.white, sortingOrder);
            mesh.visible = false;
            if (mesh.GetComponent<PreserveSortingOrder>() == null) mesh.gameObject.AddComponent<PreserveSortingOrder>();
            var outline = mesh.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) outline = mesh.gameObject.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.022f;
            return mesh;
        }

        void ConfigureUpgradeVisual(PaperMeshVisual mesh, Sprite sprite, Color color)
        {
            if (mesh == null) return;
            mesh.useBottomCenterAnchor = true;
            if (!usingPrefabLayout)
            {
                if (sprite == null) return;
                mesh.sprite = sprite;
            }
            mesh.color = color;
            mesh.transform.localRotation = Quaternion.identity;
            EnsureGridObjectVisual();
            if (!usingPrefabLayout)
            {
                gridVisual.ApplyFootprintWidthPreserveAspect(mesh, sprite);
            }
            var outline = mesh.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) outline = mesh.gameObject.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.018f;
            if (mesh.GetComponent<OcclusionMaskSource>() == null) mesh.gameObject.AddComponent<OcclusionMaskSource>();
        }

        void AnimateUpgradeSparkle()
        {
            if (sparkleTimer <= 0f)
            {
                if (upgradeSparkleVisual != null) upgradeSparkleVisual.visible = false;
                return;
            }

            sparkleTimer = Mathf.Max(0f, sparkleTimer - Time.deltaTime);
            float t = 1f - sparkleTimer / SparkleDuration;
            float pulse = Mathf.Sin(t * Mathf.PI);
            if (upgradeSparkleVisual != null)
            {
                upgradeSparkleVisual.visible = true;
                upgradeSparkleVisual.color = new Color(1f, 1f, 1f, pulse);
                upgradeSparkleVisual.transform.localScale = Vector3.one * (0.4f + pulse * 1.1f);
                upgradeSparkleVisual.transform.localRotation = Quaternion.Euler(0f, 0f, t * 210f);
            }
        }

        void UsePrefabVisualSetIfAvailable()
        {
            if (prefabVisualSet == null) prefabVisualSet = GetComponent<BuildingPrefabVisualSet>();
            usingPrefabLayout = prefabVisualSet != null && prefabVisualSet.usePrefabLayout && prefabVisualSet.HasUpgradeVisuals;
            if (!usingPrefabLayout) return;
            upgradedCompleteVisual = prefabVisualSet.upgradedCompleteVisual;
            if (prefabVisualSet.sparkleVisual != null) upgradeSparkleVisual = prefabVisualSet.sparkleVisual;
        }

        void RefreshYSortRenderers()
        {
            var ySort = GetComponent<YSort>();
            if (ySort == null) return;
            ySort.Refresh();
            ySort.Apply();
        }

    }
}
