using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class TowerController : MonoBehaviour
    {
        public Slider hpBar;
        public Transform enemyTargetPoint;
        public Vector3 upgradedVisualScale = Vector3.one;
        public Vector3 upgradedVisualOffset = Vector3.zero;
        public event System.Action<Sprite> Upgraded;
        Health health;
        Collider2D[] colliders;
        BoxCollider2D footprintCollider;
        GridObjectVisual gridVisual;
        PaperMeshVisual visual;
        PaperMeshVisual baseTowerVisual;
        PaperMeshVisual upgradeGhostVisual;
        PaperMeshVisual upgradeBuildVisual;
        PaperMeshVisual upgradedCompleteVisual;
        PaperMeshVisual upgradeHammerVisual;
        PaperMeshVisual upgradeSparkleVisual;
        TowerUpgradeConstruction pendingUpgrade;
        Sprite upgradedSprite;
        Vector3 groundAnchorWorld;
        bool collapsing;
        bool isUpgraded;
        bool hasGroundAnchor;
        bool upgradeVisualsPrepared;
        bool baseTowerUsesPrefabLayout;
        bool upgradeUsesPrefabLayout;
        Vector3 upgradeBuildBaseScale = Vector3.one;
        Vector3 upgradedCompleteBaseScale = Vector3.one;
        const float UpgradeSparkleDuration = 0.75f;
        float upgradeSparkleTimer;
        static readonly Vector3 ToolVisualScale = Vector3.one * 0.58f;

        void Awake()
        {
            health = GetComponent<Health>();
            EnsureGridObjectVisual();
            EnsureFootprintCollider();
            colliders = GetComponents<Collider2D>();
            EnsureBaseTowerVisual();
            visual = baseTowerVisual != null ? baseTowerVisual : GetComponentInChildren<PaperMeshVisual>();
            health.Died += _ => StartCollapse();
        }

        public void Configure(int maxHp)
        {
            health.SetMax(maxHp);
        }

        public bool IsUpgraded => isUpgraded;
        public bool HasPendingUpgrade => pendingUpgrade != null;
        public Health Health => health;
        public Vector3 GroundAnchorWorld => hasGroundAnchor ? groundAnchorWorld : enemyTargetPoint != null ? enemyTargetPoint.position : transform.position;

        public Transform EnemyTarget => enemyTargetPoint != null ? enemyTargetPoint : transform;

        public Sprite GetConfiguredUpgradeSprite()
        {
            BindPrefabUpgradeVisuals();
            if (upgradedCompleteVisual != null && upgradedCompleteVisual.sprite != null) return upgradedCompleteVisual.sprite;
            if (upgradeGhostVisual != null && upgradeGhostVisual.sprite != null) return upgradeGhostVisual.sprite;
            if (upgradeBuildVisual != null && upgradeBuildVisual.sprite != null) return upgradeBuildVisual.sprite;
            return upgradedSprite;
        }

        public void AlignToGridFootprint(TileGrid grid, Vector3Int originCell)
        {
            EnsureGridObjectVisual();
            gridVisual.AlignRootToFootprint(grid, originCell);
            ConfigureEnemyTarget(GridObjectVisual.FootprintOriginToWorld(grid, originCell));
            EnsureFootprintCollider();
            EnsureBaseTowerVisual();
        }

        public void ConfigureEnemyTarget(Vector3 worldPosition)
        {
            groundAnchorWorld = worldPosition;
            hasGroundAnchor = true;
            if (enemyTargetPoint == null)
            {
                var target = new GameObject("Enemy Target");
                target.transform.SetParent(transform, true);
                enemyTargetPoint = target.transform;
            }

            enemyTargetPoint.position = worldPosition;
        }

        void Update()
        {
            if (hpBar != null) hpBar.value = health.Normalized;
            AnimateUpgradeSparkle();
        }

        public bool CanStartUpgrade()
        {
            return !isUpgraded && pendingUpgrade == null && !collapsing && health != null && !health.IsDead;
        }

        public bool ContainsUpgradePointer(Vector3 world)
        {
            EnsureBaseTowerVisual();
            var renderer = ActiveTowerVisualRenderer();
            if (renderer != null)
            {
                var bounds = renderer.bounds;
                return world.x >= bounds.min.x &&
                    world.x <= bounds.max.x &&
                    world.y >= bounds.min.y &&
                    world.y <= bounds.max.y;
            }

            return footprintCollider != null && footprintCollider.OverlapPoint(world);
        }

        public bool BeginUpgradeReservation(GameConfig config, TileGrid grid, GameManager owner, Sprite sprite)
        {
            if (!CanStartUpgrade()) return false;
            upgradedSprite = sprite != null ? sprite : GetConfiguredUpgradeSprite();
            if (upgradedSprite == null) upgradedSprite = LoadGeneratedSprite("TowerUpgrade");
            pendingUpgrade = gameObject.AddComponent<TowerUpgradeConstruction>();
            pendingUpgrade.Configure(this, config, grid, owner, upgradedSprite);
            SetBaseTowerVisible(false);
            ShowUpgradeConstruction(0f, true, false);
            return true;
        }

        public void CancelUpgradeReservation()
        {
            if (pendingUpgrade == null) return;
            pendingUpgrade.CancelAndRefund();
        }

        public void ClearPendingUpgrade(TowerUpgradeConstruction construction)
        {
            if (pendingUpgrade == construction) pendingUpgrade = null;
            HideUpgradeConstruction();
            if (!isUpgraded) SetBaseTowerVisible(true);
        }

        public void ShowUpgradePreview(Sprite sprite, bool allowed)
        {
            if (isUpgraded || pendingUpgrade != null) return;
            upgradedSprite = sprite != null ? sprite : GetConfiguredUpgradeSprite();
            if (upgradedSprite == null) upgradedSprite = LoadGeneratedSprite("TowerUpgrade");
            EnsureUpgradeVisuals(upgradedSprite);
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
            EnsureUpgradeVisuals(upgradedSprite != null ? upgradedSprite : GetConfiguredUpgradeSprite() ?? LoadGeneratedSprite("TowerUpgrade"));
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

        public void CompleteUpgrade(GameConfig config, TileGrid grid, Sprite sprite)
        {
            if (isUpgraded) return;
            isUpgraded = true;
            pendingUpgrade = null;
            upgradedSprite = sprite != null ? sprite : GetConfiguredUpgradeSprite();
            if (upgradedSprite == null) upgradedSprite = LoadGeneratedSprite("TowerUpgrade");
            EnsureUpgradeVisuals(upgradedSprite);
            SetBaseTowerVisible(false);
            if (upgradeGhostVisual != null) upgradeGhostVisual.visible = false;
            if (upgradeBuildVisual != null) upgradeBuildVisual.visible = false;
            if (upgradeHammerVisual != null)
            {
                upgradeHammerVisual.visible = false;
                upgradeHammerVisual.gameObject.SetActive(false);
            }
            if (upgradedCompleteVisual != null)
            {
                upgradedCompleteVisual.visible = true;
                upgradedCompleteVisual.color = Color.white;
                upgradedCompleteVisual.transform.localScale = upgradedCompleteBaseScale;
            }

            if (health != null) health.SetMax(config != null ? config.upgradedTowerMaxHp : 450);
            var regeneration = GetComponent<AutoRegeneration>();
            if (regeneration != null && config != null) regeneration.amount += Mathf.Max(0, config.upgradedTowerRegenBonus);
            var cannon = GetComponent<TowerCannonController>();
            if (cannon != null && config != null)
            {
                cannon.ApplyTowerUpgrade(config.upgradedTowerCannonDamageBonus, config.upgradedTowerCannonExplosionRadiusMultiplier);
            }
            if (grid != null && config != null)
            {
                grid.PaintImmediate(GroundAnchorWorld, TileOwner.Player, Mathf.Max(0, config.upgradedTowerImmediatePaintRadiusCells));
            }

            upgradeSparkleTimer = UpgradeSparkleDuration;
            if (upgradeSparkleVisual != null)
            {
                PixelBurstEffect.Spawn(upgradeSparkleVisual.sprite, transform.position + new Vector3(0f, 0.5f, 0f), new Color(1f, 0.96f, 0.52f, 0.72f), 12, 0.55f, 0.35f, WeaponSortingOrders.ImpactBurst);
            }
            Upgraded?.Invoke(upgradedSprite);
        }

        void SetBaseTowerVisible(bool visible)
        {
            if (baseTowerVisual != null) baseTowerVisual.visible = visible;
        }

        Renderer ActiveTowerVisualRenderer()
        {
            if (!isUpgraded && baseTowerVisual != null) return baseTowerVisual.Renderer;
            if (upgradedCompleteVisual != null && upgradedCompleteVisual.visible) return upgradedCompleteVisual.Renderer;
            if (upgradeGhostVisual != null && upgradeGhostVisual.visible) return upgradeGhostVisual.Renderer;
            return baseTowerVisual != null ? baseTowerVisual.Renderer : null;
        }

        void EnsureGridObjectVisual()
        {
            if (gridVisual == null) gridVisual = GetComponent<GridObjectVisual>();
            if (gridVisual == null) gridVisual = gameObject.AddComponent<GridObjectVisual>();
            var marker = GetComponent<GridObjectMarker>();
            gridVisual.ConfigureFootprint(marker != null ? marker.footprint : new Vector2Int(3, 3));
            bool hasPrefabVisuals = transform.Find("Base Tower Image") != null;
            gridVisual.fitVisualWidthToFootprint = !hasPrefabVisuals;
            gridVisual.resetVisualOffset = !hasPrefabVisuals;
            if (!hasPrefabVisuals) gridVisual.visualOffset = Vector3.zero;
        }

        void EnsureFootprintCollider()
        {
            EnsureGridObjectVisual();
            var box = default(BoxCollider2D);
            foreach (var circle in GetComponents<CircleCollider2D>())
            {
                if (circle != null) Destroy(circle);
            }
            foreach (var candidate in GetComponents<BoxCollider2D>())
            {
                if (!candidate.isTrigger)
                {
                    box = candidate;
                    break;
                }
            }

            footprintCollider = gridVisual.ConfigureFootprintBox(box, false);
        }

        void EnsureBaseTowerVisual()
        {
            EnsureGridObjectVisual();
            if (baseTowerVisual == null)
            {
                var existing = transform.Find("Base Tower Image");
                if (existing != null)
                {
                    baseTowerVisual = existing.GetComponent<PaperMeshVisual>();
                    baseTowerUsesPrefabLayout = baseTowerVisual != null;
                }
                if (baseTowerVisual == null)
                {
                    var go = new GameObject("Base Tower Image");
                    go.transform.SetParent(transform, false);
                    var billboard = go.AddComponent<PaperBillboard>();
                    billboard.faceCamera = false;
                    baseTowerVisual = go.AddComponent<PaperMeshVisual>();
                }
            }

            var sprite = baseTowerVisual.sprite != null ? baseTowerVisual.sprite : LoadGeneratedSprite("Tower");
            if (sprite == null) return;
            baseTowerVisual.Configure(sprite, Color.white, 1003);
            if (baseTowerUsesPrefabLayout)
            {
                baseTowerVisual.useBottomCenterAnchor = true;
                baseTowerVisual.transform.localRotation = Quaternion.identity;
            }
            else
            {
                gridVisual.ApplyFootprintWidthPreserveAspect(baseTowerVisual, sprite);
            }
            baseTowerVisual.visible = !isUpgraded && pendingUpgrade == null;
            var preserveSortingOrder = baseTowerVisual.GetComponent<PreserveSortingOrder>();
            if (preserveSortingOrder != null) Destroy(preserveSortingOrder);
            if (baseTowerVisual.GetComponent<OcclusionMaskSource>() == null) baseTowerVisual.gameObject.AddComponent<OcclusionMaskSource>();
            var outline = baseTowerVisual.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) outline = baseTowerVisual.gameObject.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.018f;
        }

        void EnsureUpgradeVisuals(Sprite sprite)
        {
            if (sprite == null) return;
            BindPrefabUpgradeVisuals();
            if (upgradeVisualsPrepared)
            {
                ConfigureUpgradeVisual(upgradeGhostVisual, sprite, upgradeGhostVisual != null ? upgradeGhostVisual.color : Color.white);
                ConfigureUpgradeVisual(upgradeBuildVisual, sprite, Color.white);
                ConfigureUpgradeVisual(upgradedCompleteVisual, sprite, Color.white);
                return;
            }

            if (upgradeUsesPrefabLayout)
            {
                ConfigureUpgradeVisual(upgradeGhostVisual, sprite, new Color(0.30f, 0.82f, 1f, 0.36f));
                ConfigureUpgradeVisual(upgradeBuildVisual, sprite, Color.white);
                ConfigureUpgradeVisual(upgradedCompleteVisual, sprite, Color.white);
            }
            else
            {
                upgradeGhostVisual = CreateUpgradeVisual("Upgrade Ghost", sprite, new Color(0.30f, 0.82f, 1f, 0.36f), 22000);
                upgradeBuildVisual = CreateUpgradeVisual("Upgrade Build Fill", sprite, Color.white, 22001);
                upgradedCompleteVisual = CreateUpgradeVisual("Upgraded Tower Image", sprite, Color.white, 22002);
                upgradeHammerVisual = CreateOverlayVisual("Upgrade Hammer", LoadGeneratedSprite("Hammer"), 22020);
                upgradeSparkleVisual = CreateOverlayVisual("Upgrade Sparkle", LoadGeneratedSprite("Sparkle"), 22030);
            }
            if (upgradeBuildVisual != null) upgradeBuildBaseScale = upgradeBuildVisual.transform.localScale;
            if (upgradedCompleteVisual != null) upgradedCompleteBaseScale = upgradedCompleteVisual.transform.localScale;
            if (upgradeGhostVisual != null) upgradeGhostVisual.visible = false;
            if (upgradeBuildVisual != null) upgradeBuildVisual.visible = false;
            if (upgradedCompleteVisual != null) upgradedCompleteVisual.visible = false;
            if (upgradeHammerVisual != null) upgradeHammerVisual.visible = false;
            if (upgradeSparkleVisual != null) upgradeSparkleVisual.visible = false;
            upgradeVisualsPrepared = true;
        }

        void BindPrefabUpgradeVisuals()
        {
            if (upgradeGhostVisual == null) upgradeGhostVisual = FindVisual("Upgrade Ghost");
            if (upgradeBuildVisual == null) upgradeBuildVisual = FindVisual("Upgrade Build Fill");
            if (upgradedCompleteVisual == null) upgradedCompleteVisual = FindVisual("Upgraded Tower Image");
            if (upgradeHammerVisual == null) upgradeHammerVisual = FindVisual("Upgrade Hammer");
            if (upgradeSparkleVisual == null) upgradeSparkleVisual = FindVisual("Upgrade Sparkle");
            upgradeUsesPrefabLayout = upgradeGhostVisual != null && upgradeBuildVisual != null && upgradedCompleteVisual != null;
        }

        PaperMeshVisual FindVisual(string childName)
        {
            var child = transform.Find(childName);
            return child != null ? child.GetComponent<PaperMeshVisual>() : null;
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
            if (mesh == null || sprite == null) return;
            mesh.useBottomCenterAnchor = true;
            mesh.sprite = sprite;
            mesh.color = color;
            mesh.order = mesh.order;
            mesh.transform.localRotation = Quaternion.identity;
            EnsureGridObjectVisual();
            mesh.useBottomCenterAnchor = true;
            if (!upgradeUsesPrefabLayout)
            {
                gridVisual.ApplyFootprintWidthPreserveAspect(mesh, sprite);
                mesh.transform.localScale = new Vector3(
                    mesh.transform.localScale.x * upgradedVisualScale.x,
                    mesh.transform.localScale.y * upgradedVisualScale.y,
                    mesh.transform.localScale.z * upgradedVisualScale.z);
                mesh.transform.localPosition = UpgradeVisualOffset;
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
            upgradeHammerVisual.transform.localPosition = UpgradeVisualOffset + new Vector3(0.42f, 1.4f + Mathf.Abs(swing) * 0.08f, 0f);
            ApplyToolVisualScale(upgradeHammerVisual.transform);
        }

        void AnimateUpgradeSparkle()
        {
            if (upgradeSparkleTimer <= 0f)
            {
                if (upgradeSparkleVisual != null) upgradeSparkleVisual.visible = false;
                return;
            }

            upgradeSparkleTimer = Mathf.Max(0f, upgradeSparkleTimer - Time.deltaTime);
            float t = 1f - upgradeSparkleTimer / UpgradeSparkleDuration;
            float pulse = Mathf.Sin(t * Mathf.PI);
            if (upgradedCompleteVisual != null)
            {
                upgradedCompleteVisual.color = Color.Lerp(Color.white, new Color(1f, 0.96f, 0.52f, 1f), pulse);
                upgradedCompleteVisual.transform.localScale = upgradedCompleteBaseScale * (1f + pulse * 0.08f);
            }
            if (upgradeSparkleVisual != null)
            {
                upgradeSparkleVisual.visible = true;
                upgradeSparkleVisual.color = new Color(1f, 1f, 1f, pulse);
                upgradeSparkleVisual.transform.localScale = Vector3.one * (0.45f + pulse * 1.25f);
                upgradeSparkleVisual.transform.localRotation = Quaternion.Euler(0f, 0f, t * 210f);
                upgradeSparkleVisual.transform.localPosition = UpgradeVisualOffset + new Vector3(0.18f, 2.2f + pulse * 0.12f, 0f);
            }
        }

        Vector3 UpgradeVisualOffset
        {
            get
            {
                return upgradedVisualOffset;
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

        void StartCollapse()
        {
            if (collapsing) return;
            StartCoroutine(CollapseRoutine());
        }

        IEnumerator CollapseRoutine()
        {
            collapsing = true;
            foreach (var col in colliders) col.enabled = false;
            if (hpBar != null) hpBar.gameObject.SetActive(false);

            var startPosition = transform.position;
            var startScale = transform.localScale;
            var billboard = visual != null ? visual.GetComponent<PaperBillboard>() : null;
            float elapsed = 0f;
            const float duration = 1.15f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float shake = Mathf.Sin(elapsed * 42f) * Mathf.Lerp(0.08f, 0.01f, t);
                transform.position = startPosition + new Vector3(shake, -0.35f * t, 0f);
                if (billboard != null) billboard.rollDegrees = Mathf.Sin(elapsed * 30f) * Mathf.Lerp(5f, 14f, t);
                transform.localScale = new Vector3(startScale.x * Mathf.Lerp(1f, 1.08f, t), startScale.y * Mathf.Lerp(1f, 0.35f, t), startScale.z);
                if (visual != null)
                {
                    var color = visual.color;
                    color.a = Mathf.Lerp(1f, 0.18f, t);
                    visual.color = color;
                }
                yield return null;
            }

            GameManager.Instance?.GameOver();
        }
    }
}
