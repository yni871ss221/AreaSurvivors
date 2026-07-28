using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class WatchTower : MonoBehaviour, IBuildableConstruction
    {
        public GameConfig config;
        public TileGrid grid;
        public AttackBounceAnimator attackBounce;
        public Collider2D blockingCollider;
        public PaperMeshVisual completeRenderer;
        public PaperMeshVisual sparkleRenderer;
        public PaperMeshVisual rangeFillRenderer;
        public EllipseOutlineMeshVisual rangeOutlineRenderer;
        public Color rangeFillColor = new Color(0.22f, 0.78f, 0.58f, 100f / 255f);
        public int rangeFillSortingOrder = 100;
        public int rangeOutlineSortingOrder = 101;
        public Sprite towerSprite;
        public Vector2 spriteVisualSize = new Vector2(1.22f, 2.55f);
        public Vector3 spriteVisualOffset = Vector3.zero;
        public GameObject completeObject;
        public int maxHp = 100;
        public int autoPaintRadiusCells = 10;

        Health health;
        GridObjectMarker marker;
        GridObjectVisual gridVisual;
        BuildingPrefabVisualSet prefabVisualSet;
        float visualHeight = 1f;
        float sparkleTimer;
        Vector3 completeVisualScale = Vector3.one;
        bool completed;
        bool spriteVisualsPrepared;
        bool usingPrefabLayout;
        bool breaking;
        bool hasRegisteredCell;
        float autoPaintTimer;
        Vector3Int registeredCell;
        readonly HashSet<Health> damagedTargets = new HashSet<Health>();
        const float SparkleDuration = 0.75f;
        const float DefaultAutoPaintIntervalSeconds = 2f;

        public bool IsBuilt => completed;
        public TileGrid Grid => grid;
        public Vector3Int OriginCell => hasRegisteredCell ? registeredCell : grid != null ? grid.WorldToCell(transform.position) : Vector3Int.zero;
        public Vector2Int Footprint => marker != null ? marker.footprint : new Vector2Int(2, 2);

        void Awake()
        {
            health = GetComponent<Health>();
            if (attackBounce == null) attackBounce = GetComponent<AttackBounceAnimator>();
            marker = GetComponent<GridObjectMarker>();
            EnsureGridObjectVisual();
            EnsureFootprintColliders();
            UsePrefabVisualSetIfAvailable();
            health.Died += _ => Break();
            EnsureSpriteVisuals();
            EnsureUpgradeTarget();
        }

        void OnValidate()
        {
            ApplyRangeSortOrders();
            if (Application.isPlaying) ApplyRangeVisual();
        }

        public void RegisterBuildPlacement(TileGrid tileGrid, Vector3Int originCell)
        {
            grid = tileGrid;
            registeredCell = originCell;
            hasRegisteredCell = true;
            EnsureGridObjectVisual();
            EnsureFootprintColliders();
            ApplyRangeVisual();
        }

        public void ApplyBuildingUpgrade(Sprite upgradedSprite, int hpBonus, int paintRadiusBonus)
        {
            maxHp += Mathf.Max(0, hpBonus);
            autoPaintRadiusCells += Mathf.Max(0, paintRadiusBonus);
            if (health != null) health.SetMax(maxHp);
            if (upgradedSprite != null) towerSprite = upgradedSprite;
            spriteVisualsPrepared = false;
            EnsureSpriteVisuals();
            CacheVisualScales();
            ApplyVisuals();
        }

        public void SetCompletedVisualVisible(bool visible)
        {
            if (completeRenderer != null) completeRenderer.visible = completed && visible;
            SetActive(completeObject, completed && visible);
        }

        void Start()
        {
            var upgradeTarget = GetComponent<BuildingUpgradeTarget>();
            if (config != null && (upgradeTarget == null || !upgradeTarget.IsUpgraded))
            {
                maxHp = config.watchTowerMaxHp;
                autoPaintRadiusCells = config.watchTowerAutoPaintRadiusCells + ProgressionStore.GetLevel(UpgradeType.WatchTowerRange) * config.watchTowerRangePerUpgradeLevel;
            }
            BuildingSkillEffects.ConfigureAutoRegeneration(gameObject, config);

            EnsureSpriteVisuals();

            CacheVisualScales();
            ApplyVisuals();
        }

        void Update()
        {
            if (!completed) return;
            GameManager.Instance?.MarkBuildingDamageSourceActive(RunDamageBuildingSource.WatchTower);
            AnimateCompletionSparkle();
            TickAutoPaint();
        }

        public void CompleteImmediately()
        {
            if (completed) return;
            CompleteBuild();
        }

        public void RestoreAfterRevive()
        {
            breaking = false;
            completed = true;
            ApplyVisuals();
        }

        public void AutoPaintNearestCell()
        {
            if (!completed || grid == null || grid.groundTilemap == null) return;
            Vector3Int target;
            if (!TryFindNearestNonPlayerCell(out target)) return;
            grid.Paint(grid.groundTilemap.GetCellCenterWorld(target), TileOwner.Player, 0);
        }

        void TickAutoPaint()
        {
            if (grid == null || grid.groundTilemap == null) return;

            float interval = AutoPaintIntervalSeconds();
            if (interval <= 0f)
            {
                AutoPaintNearestCell();
                DamageEnemiesInAutoPaintRange();
                return;
            }

            autoPaintTimer -= Time.deltaTime;
            if (autoPaintTimer > 0f) return;

            AutoPaintNearestCell();
            DamageEnemiesInAutoPaintRange();
            autoPaintTimer = interval;
        }

        float AutoPaintIntervalSeconds()
        {
            return config != null ? config.watchTowerAutoPaintIntervalSeconds : DefaultAutoPaintIntervalSeconds;
        }

        bool TryFindNearestNonPlayerCell(out Vector3Int target)
        {
            Vector3Int centerCell = RangeCenterCell();
            target = centerCell;
            int radius = Mathf.Max(0, autoPaintRadiusCells);
            var candidates = new List<Vector3Int>();
            int bestDistance = int.MaxValue;

            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int distance = Mathf.Abs(x) + Mathf.Abs(y);
                    if (!IsCellOffsetInAutoPaintEllipse(x, y)) continue;

                    var cell = centerCell + new Vector3Int(x, y, 0);
                    if (!grid.ContainsCell(cell) || grid.IsOwnedBy(cell, TileOwner.Player)) continue;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        candidates.Clear();
                        candidates.Add(cell);
                    }
                    else if (distance == bestDistance)
                    {
                        candidates.Add(cell);
                    }
                }
            }

            if (candidates.Count == 0) return false;
            target = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        void DamageEnemiesInAutoPaintRange()
        {
            int damage = BuildingSkillEffects.WatchTowerDamage(config, IsUpgraded());
            if (damage <= 0) return;

            float searchRadius = AutoPaintSearchRadiusWorld();
            var colliders = Physics2D.OverlapCircleAll(RangeCenterWorld(), searchRadius);
            damagedTargets.Clear();
            bool dealtDamage = false;
            for (int i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                var enemy = collider != null ? collider.GetComponentInParent<EnemyController>() : null;
                if (enemy == null) continue;

                var health = enemy.GetComponent<Health>();
                if (health == null || health.IsDead || damagedTargets.Contains(health)) continue;
                if (!IsWorldPointInAutoPaintEllipse(enemy.AttackTargetPosition)) continue;

                damagedTargets.Add(health);
                int creditedDamage = health.DamageAmount(damage);
                health.Damage(damage, enemy.AttackTargetPosition);
                GameManager.Instance?.RegisterBuildingDamage(RunDamageBuildingSource.WatchTower, creditedDamage);
                if (creditedDamage > 0) dealtDamage = true;
            }

            if (dealtDamage) attackBounce?.PlayBounce();
        }

        bool IsCellOffsetInAutoPaintEllipse(int x, int y)
        {
            int radius = Mathf.Max(0, autoPaintRadiusCells);
            if (radius <= 0) return x == 0 && y == 0;

            float radiusX = Mathf.Max(0.5f, radius);
            float radiusY = Mathf.Max(0.5f, radius);
            float normalized = (x * x) / (radiusX * radiusX) + (y * y) / (radiusY * radiusY);
            return normalized <= 1f;
        }

        bool IsWorldPointInAutoPaintEllipse(Vector3 worldPoint)
        {
            if (grid == null) return false;
            Vector3 offset = worldPoint - RangeCenterWorld();
            Vector2 radius = AutoPaintRadiusWorld();
            float radiusX = Mathf.Max(0.1f, radius.x);
            float radiusY = Mathf.Max(0.1f, radius.y);
            float normalized = (offset.x * offset.x) / (radiusX * radiusX) + (offset.y * offset.y) / (radiusY * radiusY);
            return normalized <= 1f;
        }

        Vector3Int RangeCenterCell()
        {
            if (grid == null) return OriginCell;
            return grid.WorldToCell(RangeCenterWorld());
        }

        Vector3 RangeCenterWorld()
        {
            if (grid == null || grid.groundTilemap == null) return transform.position;
            return grid.FootprintCenterToWorld(OriginCell, Footprint);
        }

        Vector2 AutoPaintRadiusWorld()
        {
            if (grid == null) return Vector2.one * Mathf.Max(0.1f, autoPaintRadiusCells);
            Vector2 cellSize = grid.WorldCellSize();
            return new Vector2(
                Mathf.Max(0.1f, autoPaintRadiusCells * Mathf.Max(0.01f, cellSize.x)),
                Mathf.Max(0.1f, autoPaintRadiusCells * Mathf.Max(0.01f, cellSize.y)));
        }

        float AutoPaintSearchRadiusWorld()
        {
            if (grid == null) return Mathf.Max(0.1f, autoPaintRadiusCells);
            Vector2 cellSize = grid.WorldCellSize();
            float maxCellSize = Mathf.Max(0.01f, cellSize.x, cellSize.y);
            return Mathf.Max(0.1f, autoPaintRadiusCells * maxCellSize + maxCellSize);
        }

        bool IsUpgraded()
        {
            var upgradeTarget = GetComponent<BuildingUpgradeTarget>();
            return upgradeTarget != null && upgradeTarget.IsUpgraded;
        }

        void CompleteBuild()
        {
            completed = true;
            autoPaintTimer = AutoPaintIntervalSeconds();
            sparkleTimer = SparkleDuration;
            health.SetMax(maxHp);
            ApplyVisuals();
            AnimateCompletionSparkle();
            CompletionSparkleEffect.Spawn(sparkleRenderer != null ? sparkleRenderer.sprite : null, transform.position + new Vector3(0f, 1.0f, 0f), 0.75f);
            if (sparkleRenderer != null)
            {
                PixelBurstEffect.Spawn(sparkleRenderer.sprite, transform.position + new Vector3(0f, 1.0f, 0f), new Color(1f, 0.96f, 0.52f, 0.66f), 7, 0.24f, 0.28f, 3400);
            }
        }

        void Break()
        {
            if (breaking) return;
            breaking = true;
            SetRangeVisualVisible(false);
            var cell = hasRegisteredCell ? registeredCell : grid != null ? grid.WorldToCell(transform.position) : OriginCell;
            if (BuildingRevivalState.TryHandleDestroyed(gameObject, grid, cell)) return;
            if (grid != null) grid.ClearObject(cell);
            Destroy(gameObject);
        }

        void EnsureSpriteVisuals()
        {
            if (spriteVisualsPrepared) return;
            UsePrefabVisualSetIfAvailable();
            if (usingPrefabLayout && prefabVisualSet != null && prefabVisualSet.HasBaseVisuals)
            {
                ConfigureSpriteVisual(completeRenderer, Color.white);
                completeObject = completeRenderer.gameObject;
                RefreshSortRenderers();
                spriteVisualsPrepared = true;
                return;
            }
            if (towerSprite == null) return;
            if (completeRenderer == null) completeRenderer = CreateSpriteVisual("Complete Image", Color.white, 1002);
            if (sparkleRenderer == null) sparkleRenderer = CreateOverlayVisual("Completion Sparkle", null, 22030);
            ConfigureSpriteVisual(completeRenderer, Color.white);
            completeObject = completeRenderer.gameObject;
            RefreshSortRenderers();
            spriteVisualsPrepared = true;
        }

        void EnsureUpgradeTarget()
        {
            var target = GetComponent<BuildingUpgradeTarget>();
            if (target == null) target = gameObject.AddComponent<BuildingUpgradeTarget>();
            target.Configure(BuildingUpgradeKind.WatchTower, "WatchTowerUpgrade", 200, 0, 5);
        }

        PaperMeshVisual CreateSpriteVisual(string objectName, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<PaperBillboard>().faceCamera = false;
            var visual = go.AddComponent<PaperMeshVisual>();
            visual.Configure(towerSprite, color, sortingOrder);
            ConfigureSpriteVisual(visual, color);
            return visual;
        }

        PaperMeshVisual CreateOverlayVisual(string objectName, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<PaperBillboard>();
            var visual = go.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, sortingOrder);
            visual.visible = false;
            return visual;
        }

        void ConfigureSpriteVisual(PaperMeshVisual visual, Color color)
        {
            if (visual == null) return;
            if (!usingPrefabLayout)
            {
                if (towerSprite == null) return;
                visual.sprite = towerSprite;
            }
            visual.color = color;
            if (visual.GetComponent<OcclusionMaskSource>() == null) visual.gameObject.AddComponent<OcclusionMaskSource>();
            ConfigureOutline(visual.gameObject);
            EnsureGridObjectVisual();
            if (!usingPrefabLayout)
            {
                visual.useBottomCenterAnchor = true;
                gridVisual.ApplyFootprintWidthPreserveAspect(visual, towerSprite);
            }
            visual.visible = false;
        }

        void CacheVisualScales()
        {
            if (completeRenderer != null && completeRenderer.sprite != null)
            {
                completeVisualScale = completeRenderer.transform.localScale;
                visualHeight = completeRenderer.sprite.bounds.size.y * completeVisualScale.y;
            }
        }

        void EnsureGridObjectVisual()
        {
            if (gridVisual == null) gridVisual = GetComponent<GridObjectVisual>();
            if (gridVisual == null) gridVisual = gameObject.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureFootprint(Footprint);
            gridVisual.blockingColliderBottomInset = Mathf.Max(gridVisual.blockingColliderBottomInset, 0.1f);
            gridVisual.blockingColliderEdgeRadius = Mathf.Max(gridVisual.blockingColliderEdgeRadius, 0.04f);
            gridVisual.fitVisualWidthToFootprint = true;
            gridVisual.resetVisualOffset = true;
        }

        void EnsureFootprintColliders()
        {
            EnsureGridObjectVisual();
            var trigger = default(BoxCollider2D);
            var blocker = blockingCollider as BoxCollider2D;
            foreach (var box in GetComponents<BoxCollider2D>())
            {
                if (box.isTrigger && trigger == null) trigger = box;
                else if (!box.isTrigger && blocker == null) blocker = box;
            }

            gridVisual.ConfigureFootprintBox(trigger, true);
            blocker = gridVisual.ConfigureFootprintBox(blocker, false);
            blockingCollider = blocker;
        }

        static void ConfigureOutline(GameObject target)
        {
            if (target == null) return;
            var outline = target.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) outline = target.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.03f;
        }

        void RefreshSortRenderers()
        {
            var ySort = GetComponent<YSort>();
            if (ySort == null) return;
            ySort.renderers = new[]
            {
                completeRenderer != null ? completeRenderer.Renderer : null
            };
            ySort.Apply();
        }

        void ApplyVisuals()
        {
            var upgradeTarget = GetComponent<BuildingUpgradeTarget>();
            bool hideBaseVisual = upgradeTarget != null && upgradeTarget.IsUpgraded;
            if (blockingCollider != null) blockingCollider.enabled = completed;
            if (completeRenderer != null) completeRenderer.visible = completed && !hideBaseVisual;
            if (completeRenderer != null) completeRenderer.SetVerticalFill(1f);
            SetActive(completeObject, completed && !hideBaseVisual);
            if (sparkleRenderer != null && !completed) sparkleRenderer.visible = false;
            ApplyRangeVisual();
        }

        void ApplyRangeVisual()
        {
            ApplyRangeSortOrders();
            bool visible = completed && !breaking;
            ApplyRangeFillVisual(rangeFillRenderer, rangeFillColor, visible);
            ApplyRangeOutlineVisual(visible);
        }

        void ApplyRangeSortOrders()
        {
            if (rangeFillRenderer != null) rangeFillRenderer.order = rangeFillSortingOrder;
            if (rangeOutlineRenderer != null) rangeOutlineRenderer.order = rangeOutlineSortingOrder;
        }

        void ApplyRangeFillVisual(PaperMeshVisual visual, Color color, bool visible)
        {
            if (visual == null) return;
            visual.color = color;
            visual.visible = visible;
            if (!visible) return;

            Vector3 center = RangeCenterWorld();
            visual.transform.localPosition = transform.InverseTransformPoint(center);
            Vector2 radius = AutoPaintRadiusWorld();
            float aspectY = visual.UsesEllipseShape ? Mathf.Max(0.05f, visual.EllipseShapeAspectY) : 1f;
            visual.transform.localScale = new Vector3(radius.x, radius.y / aspectY, 1f);
        }

        void ApplyRangeOutlineVisual(bool visible)
        {
            if (rangeOutlineRenderer == null) return;
            if (!visible)
            {
                rangeOutlineRenderer.SetVisible(false);
                return;
            }

            rangeOutlineRenderer.transform.localPosition = transform.InverseTransformPoint(RangeCenterWorld());
            rangeOutlineRenderer.Configure(AutoPaintRadiusWorld(), true);
        }

        void SetRangeVisualVisible(bool visible)
        {
            if (rangeFillRenderer != null) rangeFillRenderer.visible = visible;
            if (rangeOutlineRenderer != null) rangeOutlineRenderer.SetVisible(visible);
        }

        void AnimateCompletionSparkle()
        {
            if (sparkleTimer <= 0f)
            {
                if (sparkleRenderer != null) sparkleRenderer.visible = false;
                if (completeRenderer != null)
                {
                    completeRenderer.color = Color.white;
                    completeRenderer.transform.localScale = completeVisualScale;
                }
                return;
            }

            sparkleTimer = Mathf.Max(0f, sparkleTimer - Time.deltaTime);
            float t = 1f - sparkleTimer / SparkleDuration;
            float pulse = Mathf.Sin(t * Mathf.PI);
            if (completeRenderer != null)
            {
                completeRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.96f, 0.52f, 1f), pulse);
                completeRenderer.transform.localScale = completeVisualScale * (1f + pulse * 0.1f);
            }
            if (sparkleRenderer != null)
            {
                sparkleRenderer.visible = true;
                sparkleRenderer.color = new Color(1f, 1f, 1f, pulse);
                sparkleRenderer.transform.localScale = Vector3.one * (0.35f + pulse * 1.2f);
                sparkleRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, t * 210f);
                sparkleRenderer.transform.localPosition = new Vector3(0.3f, 1.0f + pulse * 0.12f, 0f);
            }
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }

        void UsePrefabVisualSetIfAvailable()
        {
            if (prefabVisualSet == null) prefabVisualSet = GetComponent<BuildingPrefabVisualSet>();
            usingPrefabLayout = prefabVisualSet != null && prefabVisualSet.usePrefabLayout && prefabVisualSet.HasBaseVisuals;
            if (!usingPrefabLayout) return;
            completeRenderer = prefabVisualSet.completeVisual;
            if (prefabVisualSet.sparkleVisual != null) sparkleRenderer = prefabVisualSet.sparkleVisual;
        }
    }
}
