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
        Sprite cancelIcon;
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
            cancelIcon = LoadGeneratedSprite("CancelUpgradeIcon");
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
            active = enabled && IsUnlocked();
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
                if (hoverTower.HasPendingUpgrade)
                {
                    hoverTower.HideUpgradePreview();
                    SetCursorSprite(cancelIcon != null ? cancelIcon : upgradeIcon);
                    return;
                }

                bool canUpgrade = CanReserve(hoverTower);
                hoverTower.ShowUpgradePreview(upgradedTowerSprite, canUpgrade);
                SetCursorSprite(upgradeIcon);
                return;
            }

            if (hoverBuilding.HasPendingUpgrade)
            {
                hoverBuilding.HideUpgradePreview();
                SetCursorSprite(cancelIcon != null ? cancelIcon : upgradeIcon);
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
                if (hoverTower.HasPendingUpgrade)
                {
                    hoverTower.CancelUpgradeReservation();
                    SetActive(false);
                    return;
                }
                if (!CanReserve(hoverTower)) return;
                if (owner == null || config == null) return;
                if (!owner.TrySpendResources(config.towerUpgradeWoodCost, config.towerUpgradeStoneCost)) return;
                if (!hoverTower.BeginUpgradeReservation(config, grid, owner, upgradedTowerSprite))
                {
                    owner.AddResource(ResourceType.Wood, config.towerUpgradeWoodCost);
                    owner.AddResource(ResourceType.Stone, config.towerUpgradeStoneCost);
                    return;
                }
                SetActive(false);
                return;
            }

            if (hoverBuilding.HasPendingUpgrade)
            {
                hoverBuilding.CancelUpgradeReservation();
                SetActive(false);
                return;
            }
            if (!CanReserve(hoverBuilding)) return;
            if (owner == null || config == null) return;
            if (!owner.TrySpendResources(hoverBuilding.woodCost, hoverBuilding.stoneCost)) return;
            if (!hoverBuilding.BeginUpgradeReservation(config, grid, owner))
            {
                owner.AddResource(ResourceType.Wood, hoverBuilding.woodCost);
                owner.AddResource(ResourceType.Stone, hoverBuilding.stoneCost);
                return;
            }
            SetActive(false);
        }

        bool CanReserve(TowerController target)
        {
            if (target == null || config == null || owner == null) return false;
            return target.CanStartUpgrade() &&
                owner.HasResources(config.towerUpgradeWoodCost, config.towerUpgradeStoneCost) &&
                IsUnlocked();
        }

        bool CanReserve(BuildingUpgradeTarget target)
        {
            if (target == null || config == null || owner == null) return false;
            return target.CanStartUpgrade() &&
                owner.HasResources(target.woodCost, target.stoneCost) &&
                IsUnlocked();
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

        bool IsUnlocked()
        {
            return ProgressionStore.IsUnlocked(UpgradeType.UnlockTowerUpgrade);
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
        WoodenGate,
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
        public string upgradedOpenSpriteResource;

        Health health;
        GridObjectMarker marker;
        GridObjectVisual gridVisual;
        BuildingPrefabVisualSet prefabVisualSet;
        PaperMeshVisual upgradeGhostVisual;
        PaperMeshVisual upgradeBuildVisual;
        PaperMeshVisual upgradedCompleteVisual;
        PaperMeshVisual upgradeHammerVisual;
        PaperMeshVisual upgradeSparkleVisual;
        BuildingUpgradeConstruction pendingUpgrade;
        Sprite upgradedSprite;
        Sprite upgradedOpenSprite;
        Vector3 upgradeBuildBaseScale = Vector3.one;
        Vector3 upgradedCompleteBaseScale = Vector3.one;
        float sparkleTimer;
        bool isUpgraded;
        bool visualsPrepared;
        bool usingPrefabLayout;
        const float SparkleDuration = 0.75f;
        static readonly Vector3 ToolVisualScale = Vector3.one * 0.58f;

        public bool IsUpgraded => isUpgraded;
        public bool HasPendingUpgrade => pendingUpgrade != null;
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

        public void Configure(BuildingUpgradeKind upgradeKind, int wood, int stone, string spriteName, string openSpriteName = null, int hp = 100, int attack = 0, int paintRadius = 0)
        {
            kind = upgradeKind;
            woodCost = Mathf.Max(0, wood);
            stoneCost = Mathf.Max(0, stone);
            upgradedSpriteResource = spriteName;
            upgradedOpenSpriteResource = openSpriteName;
            hpBonus = Mathf.Max(0, hp);
            attackBonus = Mathf.Max(0, attack);
            paintRadiusBonus = Mathf.Max(0, paintRadius);
            upgradedSprite = null;
            upgradedOpenSprite = null;
            visualsPrepared = false;
            EnsureGridObjectVisual();
        }

        public bool CanStartUpgrade()
        {
            return IsBuilt && !isUpgraded && pendingUpgrade == null && health != null && !health.IsDead;
        }

        public bool BeginUpgradeReservation(GameConfig config, TileGrid grid, GameManager owner)
        {
            if (!CanStartUpgrade()) return false;
            EnsureSprites();
            UsePrefabVisualSetIfAvailable();
            pendingUpgrade = gameObject.AddComponent<BuildingUpgradeConstruction>();
            pendingUpgrade.Configure(this, config, grid, owner, woodCost, stoneCost);
            SetBaseVisualVisible(false);
            ShowUpgradeConstruction(0f, true, false);
            return true;
        }

        public void CancelUpgradeReservation()
        {
            pendingUpgrade?.CancelAndRefund();
        }

        public void ClearPendingUpgrade(BuildingUpgradeConstruction construction)
        {
            if (pendingUpgrade == construction) pendingUpgrade = null;
            HideUpgradeConstruction();
            if (!isUpgraded) SetBaseVisualVisible(true);
        }

        public void ShowUpgradePreview(bool allowed)
        {
            if (isUpgraded || pendingUpgrade != null) return;
            EnsureSprites();
            EnsureUpgradeVisuals();
            if (upgradeGhostVisual == null) return;
            upgradeGhostVisual.color = allowed ? new Color(0.30f, 0.82f, 1f, 0.42f) : new Color(1f, 0.20f, 0.16f, 0.42f);
            upgradeGhostVisual.visible = true;
        }

        public void HideUpgradePreview()
        {
            if (pendingUpgrade != null) return;
            if (upgradeGhostVisual != null) upgradeGhostVisual.visible = false;
        }

        public void ShowUpgradeConstruction(float progress, bool active, bool showHammer)
        {
            EnsureSprites();
            EnsureUpgradeVisuals();
            progress = Mathf.Clamp01(progress);
            if (upgradeGhostVisual != null)
            {
                upgradeGhostVisual.color = new Color(0.30f, 0.82f, 1f, 0.36f);
                upgradeGhostVisual.visible = active && !isUpgraded;
            }
            if (upgradeBuildVisual != null)
            {
                upgradeBuildVisual.visible = active && !isUpgraded && progress > 0f;
                upgradeBuildVisual.transform.localScale = upgradeBuildBaseScale;
                upgradeBuildVisual.SetVerticalFill(progress);
            }
            if (upgradeHammerVisual != null)
            {
                if (!isUpgraded && showHammer && !upgradeHammerVisual.gameObject.activeSelf) upgradeHammerVisual.gameObject.SetActive(true);
                upgradeHammerVisual.visible = !isUpgraded && showHammer;
            }
            if (!isUpgraded && showHammer) AnimateUpgradeHammer();
        }

        public void HideUpgradeConstruction()
        {
            if (upgradeGhostVisual != null) upgradeGhostVisual.visible = false;
            if (upgradeBuildVisual != null) upgradeBuildVisual.visible = false;
            if (upgradeBuildVisual != null) upgradeBuildVisual.SetVerticalFill(1f);
            if (upgradeHammerVisual != null) upgradeHammerVisual.visible = false;
        }

        public void CompleteUpgrade()
        {
            if (isUpgraded) return;
            isUpgraded = true;
            pendingUpgrade = null;
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
                barrier.ApplyBuildingUpgrade(upgradedSprite, upgradedOpenSprite, hpBonus);
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

        public void SetUpgradedGateOpen(bool open)
        {
            if (!isUpgraded) return;
            EnsureSprites();
            var sprite = open && upgradedOpenSprite != null ? upgradedOpenSprite : upgradedSprite;
            SetUpgradedCompleteSprite(sprite);
        }

        void EnsureSprites()
        {
            UsePrefabVisualSetIfAvailable();
            if (upgradedSprite == null && prefabVisualSet != null && prefabVisualSet.upgradedCompleteVisual != null)
            {
                upgradedSprite = prefabVisualSet.upgradedCompleteVisual.sprite;
            }
            if (upgradedOpenSprite == null && prefabVisualSet != null && prefabVisualSet.upgradedOpenSprite != null)
            {
                upgradedOpenSprite = prefabVisualSet.upgradedOpenSprite;
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
                ConfigureUpgradeVisual(upgradeGhostVisual, upgradedSprite, upgradeGhostVisual != null ? upgradeGhostVisual.color : Color.white);
                ConfigureUpgradeVisual(upgradeBuildVisual, upgradedSprite, Color.white);
                return;
            }

            if (usingPrefabLayout && prefabVisualSet != null && prefabVisualSet.HasUpgradeVisuals)
            {
                upgradeGhostVisual = prefabVisualSet.upgradedGhostVisual;
                upgradeBuildVisual = prefabVisualSet.upgradedBuildFillVisual;
                upgradedCompleteVisual = prefabVisualSet.upgradedCompleteVisual;
                upgradeHammerVisual = prefabVisualSet.hammerVisual;
                upgradeSparkleVisual = prefabVisualSet.sparkleVisual;
                ConfigureUpgradeVisual(upgradeGhostVisual, upgradedSprite, new Color(0.30f, 0.82f, 1f, 0.36f));
                ConfigureUpgradeVisual(upgradeBuildVisual, upgradedSprite, Color.white);
                ConfigureUpgradeVisual(upgradedCompleteVisual, upgradedSprite, Color.white);
            }
            else
            {
                upgradeGhostVisual = CreateUpgradeVisual("Upgrade Ghost", upgradedSprite, new Color(0.30f, 0.82f, 1f, 0.36f), 22000);
                upgradeBuildVisual = CreateUpgradeVisual("Upgrade Build Fill", upgradedSprite, Color.white, 22001);
                upgradedCompleteVisual = CreateUpgradeVisual("Upgraded Building Image", upgradedSprite, Color.white, 22002);
                upgradeHammerVisual = CreateOverlayVisual("Upgrade Hammer", null, 22020);
                upgradeSparkleVisual = CreateOverlayVisual("Upgrade Sparkle", null, 22030);
            }
            if (upgradeBuildVisual != null) upgradeBuildBaseScale = upgradeBuildVisual.transform.localScale;
            if (upgradedCompleteVisual != null) upgradedCompleteBaseScale = upgradedCompleteVisual.transform.localScale;
            if (upgradeGhostVisual != null) upgradeGhostVisual.visible = false;
            if (upgradeBuildVisual != null) upgradeBuildVisual.visible = false;
            if (upgradedCompleteVisual != null) upgradedCompleteVisual.visible = false;
            if (upgradeHammerVisual != null) upgradeHammerVisual.visible = false;
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
            ApplyToolVisualScale(mesh.transform);
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

        void AnimateUpgradeHammer()
        {
            if (upgradeHammerVisual == null) return;
            float swing = Mathf.Sin(Time.time * 16f);
            upgradeHammerVisual.transform.localRotation = Quaternion.Euler(0f, 0f, -35f + swing * 32f);
            if (!usingPrefabLayout)
            {
                var size = gridVisual != null ? gridVisual.FootprintWorldSize : Vector2.one;
                upgradeHammerVisual.transform.localPosition = new Vector3(size.x * 0.18f, size.y + 0.36f + Mathf.Abs(swing) * 0.08f, 0f);
            }
            ApplyToolVisualScale(upgradeHammerVisual.transform);
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

        static void ApplyToolVisualScale(Transform target)
        {
            if (target == null) return;
            var parentScale = target.parent != null ? target.parent.lossyScale : Vector3.one;
            target.localScale = new Vector3(
                ToolVisualScale.x / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
                ToolVisualScale.y / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
                ToolVisualScale.z);
        }

        void UsePrefabVisualSetIfAvailable()
        {
            if (prefabVisualSet == null) prefabVisualSet = GetComponent<BuildingPrefabVisualSet>();
            usingPrefabLayout = prefabVisualSet != null && prefabVisualSet.usePrefabLayout && prefabVisualSet.HasUpgradeVisuals;
            if (!usingPrefabLayout) return;
            upgradeGhostVisual = prefabVisualSet.upgradedGhostVisual;
            upgradeBuildVisual = prefabVisualSet.upgradedBuildFillVisual;
            upgradedCompleteVisual = prefabVisualSet.upgradedCompleteVisual;
            if (prefabVisualSet.hammerVisual != null) upgradeHammerVisual = prefabVisualSet.hammerVisual;
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

    public sealed class BuildingUpgradeConstruction : MonoBehaviour, IBuildableConstruction
    {
        BuildingUpgradeTarget target;
        GameConfig config;
        TileGrid grid;
        GameManager owner;
        float progress;
        float assistedBuildTimer;
        int touchingPlayers;
        bool completed;
        bool canceling;
        int woodCost;
        int stoneCost;
        Collider2D[] targetColliders;
        Collider2D playerCollider;
        Vector3Int originCell;
        Vector2Int footprint = Vector2Int.one;
        const float BuildDecaySecondsMultiplier = 3f;
        const float ColliderContactTolerance = 0.02f;

        public bool IsBuilt => completed;
        public TileGrid Grid => grid;
        public Vector3Int OriginCell => originCell;
        public Vector2Int Footprint => footprint;

        public void Configure(BuildingUpgradeTarget upgradeTarget, GameConfig gameConfig, TileGrid tileGrid, GameManager gameManager, int wood, int stone)
        {
            target = upgradeTarget;
            config = gameConfig;
            grid = tileGrid;
            owner = gameManager;
            woodCost = Mathf.Max(0, wood);
            stoneCost = Mathf.Max(0, stone);
            targetColliders = GetComponents<Collider2D>();
            var player = owner != null ? owner.Player : GameManager.Instance != null ? GameManager.Instance.Player : null;
            playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
            var sourceConstruction = FindSourceConstruction();
            if (sourceConstruction != null)
            {
                originCell = sourceConstruction.OriginCell;
                footprint = sourceConstruction.Footprint;
            }
            else
            {
                var marker = GetComponent<GridObjectMarker>();
                if (marker != null) footprint = marker.footprint;
                if (grid != null) originCell = grid.WorldToCell(transform.position);
            }
            target?.ShowUpgradeConstruction(progress, true, false);
        }

        IBuildableConstruction FindSourceConstruction()
        {
            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour == this) continue;
                var construction = behaviour as IBuildableConstruction;
                if (construction != null) return construction;
            }
            return null;
        }

        void Update()
        {
            if (completed || canceling) return;
            var contactBuilder = ColliderContactBuilder();
            if (contactBuilder != null)
            {
                AddBuildWork(WorkSpeedMultiplier(), contactBuilder);
            }
            else if (progress > 0f)
            {
                if (assistedBuildTimer > 0f) assistedBuildTimer = Mathf.Max(0f, assistedBuildTimer - Time.deltaTime);
                else
                {
                    progress = Mathf.Clamp01(progress - Time.deltaTime / Mathf.Max(0.1f, BuildSeconds() * BuildDecaySecondsMultiplier));
                    target?.ShowUpgradeConstruction(progress, true, false);
                }
            }

            target?.ShowUpgradeConstruction(progress, true, ShouldShowHammer());
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider.GetComponent<PlayerController>() == null) return;
            touchingPlayers++;
            playerCollider = collision.collider;
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.collider.GetComponent<PlayerController>() == null) return;
            touchingPlayers = Mathf.Max(0, touchingPlayers - 1);
            if (playerCollider == collision.collider) playerCollider = null;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            touchingPlayers++;
            playerCollider = other;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            touchingPlayers = Mathf.Max(0, touchingPlayers - 1);
            if (playerCollider == other) playerCollider = null;
        }

        public void AddBuildWork(float workSpeedMultiplier, Transform builder = null)
        {
            if (completed || canceling) return;
            progress = Mathf.Clamp01(progress + Time.deltaTime * Mathf.Max(0f, workSpeedMultiplier) / Mathf.Max(0.1f, BuildSeconds()));
            assistedBuildTimer = 0.18f;
            target?.ShowUpgradeConstruction(progress, true, ShouldShowHammer());
            if (progress >= 1f) CompleteBuild();
        }

        public void CancelAndRefund()
        {
            if (completed || canceling) return;
            canceling = true;
            if (owner != null)
            {
                owner.AddResource(ResourceType.Wood, woodCost);
                owner.AddResource(ResourceType.Stone, stoneCost);
            }
            target?.ClearPendingUpgrade(this);
            Destroy(this);
        }

        void CompleteBuild()
        {
            if (completed) return;
            completed = true;
            progress = 1f;
            target?.CompleteUpgrade();
            Destroy(this);
        }

        float BuildSeconds()
        {
            return config != null ? Mathf.Max(0.1f, config.towerUpgradeBuildSeconds) : 5f;
        }

        bool ShouldShowHammer()
        {
            return ColliderContactBuilder() != null || assistedBuildTimer > 0f;
        }

        Transform ColliderContactBuilder()
        {
            var player = owner != null ? owner.Player : GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return null;
            if (playerCollider == null) playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider == null) return touchingPlayers > 0 ? player.transform : null;
            if (targetColliders == null || targetColliders.Length == 0) targetColliders = GetComponents<Collider2D>();
            foreach (var targetCollider in targetColliders)
            {
                if (targetCollider == null || !targetCollider.enabled) continue;
                var distance = targetCollider.Distance(playerCollider);
                if (distance.isOverlapped || distance.distance <= ColliderContactTolerance) return player.transform;
            }
            return null;
        }

        static float WorkSpeedMultiplier()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            return player != null ? Mathf.Max(0.05f, player.Stats.workSpeedMultiplier) : 1f;
        }
    }
}
