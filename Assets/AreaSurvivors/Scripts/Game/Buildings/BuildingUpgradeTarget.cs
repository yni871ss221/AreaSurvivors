using UnityEngine;

namespace AreaSurvivors
{
    public enum BuildingUpgradeKind
    {
        WoodenWall,
        Ballista,
        WatchTower
    }

        public sealed class BuildingUpgradeTarget : MonoBehaviour
    {
        public BuildingUpgradeKind kind;
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

        public void Configure(BuildingUpgradeKind upgradeKind, string spriteName, int hp = 100, int attack = 0, int paintRadius = 0)
        {
            kind = upgradeKind;
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
