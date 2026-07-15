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
        PaperMeshVisual upgradedCompleteVisual;
        PaperMeshVisual upgradeSparkleVisual;
        Sprite upgradedSprite;
        Vector3 groundAnchorWorld;
        bool collapsing;
        bool isUpgraded;
        bool hasGroundAnchor;
        bool upgradeVisualsPrepared;
        bool baseTowerUsesPrefabLayout;
        bool upgradeUsesPrefabLayout;
        Vector3 upgradedCompleteBaseScale = Vector3.one;
        const float UpgradeSparkleDuration = 0.75f;
        float upgradeSparkleTimer;

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
        public Health Health => health;
        public Vector3 GroundAnchorWorld => hasGroundAnchor ? groundAnchorWorld : enemyTargetPoint != null ? enemyTargetPoint.position : transform.position;

        public Transform EnemyTarget => enemyTargetPoint != null ? enemyTargetPoint : transform;

        public Sprite GetConfiguredUpgradeSprite()
        {
            BindPrefabUpgradeVisuals();
            if (upgradedCompleteVisual != null && upgradedCompleteVisual.sprite != null) return upgradedCompleteVisual.sprite;
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
            return !isUpgraded && !collapsing && health != null && !health.IsDead;
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

        public void ShowUpgradePreview(Sprite sprite, bool allowed)
        {
            if (isUpgraded) return;
            upgradedSprite = sprite != null ? sprite : GetConfiguredUpgradeSprite();
            if (upgradedSprite == null) return;
            EnsureUpgradeVisuals(upgradedSprite);
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

        public void CompleteUpgrade(GameConfig config, TileGrid grid, Sprite sprite)
        {
            if (isUpgraded) return;
            isUpgraded = true;
            upgradedSprite = sprite != null ? sprite : GetConfiguredUpgradeSprite();
            if (upgradedSprite == null) return;
            EnsureUpgradeVisuals(upgradedSprite);
            SetBaseTowerVisible(false);
            if (upgradedCompleteVisual != null)
            {
                upgradedCompleteVisual.visible = true;
                upgradedCompleteVisual.color = Color.white;
                upgradedCompleteVisual.transform.localScale = upgradedCompleteBaseScale;
            }

            if (health != null) health.SetMax(config != null ? config.upgradedTowerMaxHp : 900);
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
            gridVisual.blockingColliderBottomInset = Mathf.Max(gridVisual.blockingColliderBottomInset, 0.1f);
            gridVisual.blockingColliderEdgeRadius = Mathf.Max(gridVisual.blockingColliderEdgeRadius, 0.04f);
        }

        void EnsureFootprintCollider()
        {
            EnsureGridObjectVisual();
            var box = default(BoxCollider2D);
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

            var sprite = baseTowerVisual.sprite;
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
            baseTowerVisual.visible = !isUpgraded;
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
                ConfigureUpgradeVisual(upgradedCompleteVisual, sprite, upgradedCompleteVisual != null ? upgradedCompleteVisual.color : Color.white);
                return;
            }

            if (upgradeUsesPrefabLayout)
            {
                ConfigureUpgradeVisual(upgradedCompleteVisual, sprite, Color.white);
            }
            else
            {
                upgradedCompleteVisual = CreateUpgradeVisual("Upgraded Tower Image", sprite, Color.white, 22002);
                upgradeSparkleVisual = CreateOverlayVisual("Upgrade Sparkle", null, 22030);
            }
            if (upgradedCompleteVisual != null) upgradedCompleteBaseScale = upgradedCompleteVisual.transform.localScale;
            if (upgradedCompleteVisual != null) upgradedCompleteVisual.visible = false;
            if (upgradeSparkleVisual != null) upgradeSparkleVisual.visible = false;
            upgradeVisualsPrepared = true;
        }

        void BindPrefabUpgradeVisuals()
        {
            if (upgradedCompleteVisual == null) upgradedCompleteVisual = FindVisual("Upgraded Tower Image");
            if (upgradeSparkleVisual == null) upgradeSparkleVisual = FindVisual("Upgrade Sparkle");
            upgradeUsesPrefabLayout = upgradedCompleteVisual != null;
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
            return mesh;
        }

        void ConfigureUpgradeVisual(PaperMeshVisual mesh, Sprite sprite, Color color)
        {
            if (mesh == null) return;
            mesh.useBottomCenterAnchor = true;
            if (!upgradeUsesPrefabLayout)
            {
                if (sprite == null) return;
                mesh.sprite = sprite;
            }
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

        void StartCollapse()
        {
            if (collapsing) return;
            GameManager.Instance?.BeginTowerCollapseCutscene(this);
            StartCoroutine(CollapseRoutine());
        }

        IEnumerator CollapseRoutine()
        {
            collapsing = true;
            foreach (var col in colliders) col.enabled = false;
            if (hpBar != null) hpBar.gameObject.SetActive(false);
            if (GameManager.Instance != null)
            {
                yield return GameManager.Instance.WaitForEndingCutsceneCamera(EnemyTarget);
            }
            AudioManager.PlaySfx(SfxTrack.TowerCollapse);

            var startPosition = transform.position;
            var startScale = transform.localScale;
            var billboard = visual != null ? visual.GetComponent<PaperBillboard>() : null;
            float elapsed = 0f;
            const float duration = 2.3f;
            while (elapsed < duration)
            {
                elapsed += Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
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
