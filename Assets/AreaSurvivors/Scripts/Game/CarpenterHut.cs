using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class CarpenterHut : MonoBehaviour, IBuildableConstruction
    {
        static readonly bool DisableCarpenterHutRepairForPhase1 = true;
        public GameConfig config;
        public TileGrid grid;
        public Collider2D blockingCollider;
        public PaperMeshVisual completeRenderer;
        public PaperMeshVisual sparkleRenderer;
        public Sprite hutSprite;
        public Vector2 spriteVisualSize = new Vector2(0.66f, 0.66f);
        public Vector3 spriteVisualOffset = Vector3.zero;
        public GameObject completeObject;
        public int maxHp = 50;
        public float repairIntervalSeconds = 2f;
        public int repairAmount = 1;

        Health health;
        GridObjectMarker marker;
        GridObjectVisual gridVisual;
        float repairTimer;
        float visualHeight = 1f;
        float sparkleTimer;
        Vector3 completeVisualScale = Vector3.one;
        bool completed;
        bool spriteVisualsPrepared;
        bool breaking;
        bool hasRegisteredCell;
        Vector3Int registeredCell;
        const float SparkleDuration = 0.75f;

        public bool IsBuilt => completed;
        public TileGrid Grid => grid;
        public Vector3Int OriginCell => hasRegisteredCell ? registeredCell : grid != null ? grid.WorldToCell(transform.position) : Vector3Int.zero;
        public Vector2Int Footprint => marker != null ? marker.footprint : Vector2Int.one;

        void Awake()
        {
            health = GetComponent<Health>();
            marker = GetComponent<GridObjectMarker>();
            EnsureGridObjectVisual();
            EnsureFootprintColliders();
            health.Died += _ => Break();
            EnsureSpriteVisuals();
        }

        public void RegisterBuildPlacement(TileGrid tileGrid, Vector3Int originCell)
        {
            grid = tileGrid;
            registeredCell = originCell;
            hasRegisteredCell = true;
            EnsureGridObjectVisual();
            EnsureFootprintColliders();
        }

        void Start()
        {
            if (config != null)
            {
                maxHp = config.carpenterHutMaxHp;
                int speedLevel = ProgressionStore.GetLevel(UpgradeType.UnlockAutoBuild);
                int amountLevel = ProgressionStore.GetLevel(UpgradeType.AutoBuildSpeed);
                repairIntervalSeconds = Mathf.Max(
                    config.carpenterHutRepairMinIntervalSeconds,
                    config.carpenterHutRepairIntervalSeconds - speedLevel * config.carpenterHutRepairIntervalReductionPerUpgradeLevel);
                repairAmount = Mathf.Max(1, config.carpenterHutRepairAmount + amountLevel * config.carpenterHutRepairAmountPerUpgradeLevel);
            }

            EnsureSpriteVisuals();

            if (completeRenderer != null && completeRenderer.sprite != null)
            {
                completeVisualScale = completeRenderer.transform.localScale;
                visualHeight = completeRenderer.sprite.bounds.size.y * completeVisualScale.y;
            }
            ApplyVisuals();
        }

        void Update()
        {
            if (!completed || DisableCarpenterHutRepairForPhase1) return;

            RepairConnectedBuildings();
            AnimateCompletionSparkle();
        }

        public void CompleteImmediately()
        {
            if (completed) return;
            CompleteBuild();
        }

        void RepairConnectedBuildings()
        {
            if (DisableCarpenterHutRepairForPhase1) return;
            if (grid == null || repairAmount <= 0) return;
            repairTimer -= Time.deltaTime;
            if (repairTimer > 0f) return;
            repairTimer = Mathf.Max(0.1f, repairIntervalSeconds);

            foreach (var targetHealth in FindObjectsOfType<Health>())
            {
                if (!CanRepair(targetHealth)) continue;
                if (!IsConnectedByPlayerTerritory(targetHealth)) continue;
                targetHealth.Heal(repairAmount);
            }
        }

        bool CanRepair(Health targetHealth)
        {
            if (targetHealth == null || targetHealth.IsDead || targetHealth.currentHp >= targetHealth.maxHp) return false;
            var target = targetHealth.gameObject;
            return target.GetComponent<TowerController>() != null ||
                target.GetComponent<BallistaTower>() != null ||
                target.GetComponent<WoodenBarrier>() != null ||
                target.GetComponent<CarpenterHut>() != null ||
                target.GetComponent<WorkerHut>() != null ||
                target.GetComponent<WatchTower>() != null;
        }

        bool IsConnectedByPlayerTerritory(Health targetHealth)
        {
            if (!grid.IsOwnedBy(OriginCell, TileOwner.Player)) return false;

            var targetCells = new HashSet<Vector3Int>();
            var targetOrigin = TargetOriginCell(targetHealth);
            var targetFootprint = TargetFootprint(targetHealth);
            foreach (var cell in FootprintCells(targetOrigin, targetFootprint))
            {
                if (grid.ContainsCell(cell) && grid.IsOwnedBy(cell, TileOwner.Player)) targetCells.Add(cell);
            }
            if (targetCells.Count == 0) return false;
            if (targetCells.Contains(OriginCell)) return true;

            var visited = new HashSet<Vector3Int>();
            var open = new Queue<Vector3Int>();
            visited.Add(OriginCell);
            open.Enqueue(OriginCell);
            int safety = Mathf.Max(1, grid.width * grid.height);

            while (open.Count > 0 && safety-- > 0)
            {
                var cell = open.Dequeue();
                foreach (var next in Neighbors(cell))
                {
                    if (visited.Contains(next) || !grid.ContainsCell(next) || !grid.IsOwnedBy(next, TileOwner.Player)) continue;
                    if (targetCells.Contains(next)) return true;
                    visited.Add(next);
                    open.Enqueue(next);
                }
            }

            return false;
        }

        Vector3Int TargetOriginCell(Health targetHealth)
        {
            if (targetHealth == null) return Vector3Int.zero;
            var construction = targetHealth.GetComponent<IBuildableConstruction>();
            if (construction != null && construction.Grid == grid) return construction.OriginCell;
            return grid != null ? grid.WorldToCell(targetHealth.transform.position) : Vector3Int.zero;
        }

        Vector2Int TargetFootprint(Health targetHealth)
        {
            if (targetHealth == null) return Vector2Int.one;
            var construction = targetHealth.GetComponent<IBuildableConstruction>();
            if (construction != null && construction.Grid == grid) return construction.Footprint;
            var marker = targetHealth.GetComponent<GridObjectMarker>();
            return marker != null ? marker.footprint : Vector2Int.one;
        }

        static IEnumerable<Vector3Int> Neighbors(Vector3Int cell)
        {
            yield return cell + Vector3Int.left;
            yield return cell + Vector3Int.right;
            yield return cell + Vector3Int.up;
            yield return cell + Vector3Int.down;
        }

        static IEnumerable<Vector3Int> FootprintCells(Vector3Int originCell, Vector2Int footprint)
        {
            footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            int minX = originCell.x - (footprint.x - 1) / 2;
            int minY = originCell.y - (footprint.y - 1) / 2;
            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    yield return new Vector3Int(minX + x, minY + y, originCell.z);
                }
            }
        }

        void CompleteBuild()
        {
            completed = true;
            sparkleTimer = SparkleDuration;
            health.SetMax(maxHp);
            ApplyVisuals();
            AnimateCompletionSparkle();
            CompletionSparkleEffect.Spawn(sparkleRenderer != null ? sparkleRenderer.sprite : null, transform.position + new Vector3(0f, 0.48f, 0f), 0.6f);
            if (sparkleRenderer != null)
            {
                PixelBurstEffect.Spawn(sparkleRenderer.sprite, transform.position + new Vector3(0f, 0.48f, 0f), new Color(1f, 0.96f, 0.52f, 0.66f), 6, 0.22f, 0.26f, 3400);
            }
        }

        void Break()
        {
            if (breaking) return;
            breaking = true;
            var cell = hasRegisteredCell ? registeredCell : grid != null ? grid.WorldToCell(transform.position) : OriginCell;
            if (BuildingPersistentState.TryMarkDestroyed(gameObject, grid, cell)) return;
            if (grid != null) grid.ClearObject(cell);
            Destroy(gameObject);
        }

        void EnsureSpriteVisuals()
        {
            if (spriteVisualsPrepared) return;
            bool hasPrefabVisuals = completeRenderer != null;
            if (!hasPrefabVisuals && hutSprite == null) return;
            if (completeRenderer == null) completeRenderer = CreateSpriteVisual("Complete Image", Color.white, 1002);
            if (sparkleRenderer == null) sparkleRenderer = CreateOverlayVisual("Completion Sparkle", null, 22030);
            ConfigureSpriteVisual(completeRenderer, Color.white);
            completeObject = completeRenderer.gameObject;
            RefreshSortRenderers();
            spriteVisualsPrepared = true;
        }

        PaperMeshVisual CreateSpriteVisual(string objectName, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<PaperBillboard>().faceCamera = false;
            var visual = go.AddComponent<PaperMeshVisual>();
            visual.Configure(hutSprite, color, sortingOrder);
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
            visual.useBottomCenterAnchor = true;
            if (visual.sprite == null && hutSprite != null) visual.sprite = hutSprite;
            visual.color = color;
            if (visual.GetComponent<OcclusionMaskSource>() == null) visual.gameObject.AddComponent<OcclusionMaskSource>();
            ConfigureOutline(visual.gameObject);
            EnsureGridObjectVisual();
            var sprite = visual.sprite != null ? visual.sprite : hutSprite;
            if (sprite != null) gridVisual.ApplyToVisual(visual, sprite, spriteVisualSize);
            visual.visible = false;
        }

        void EnsureGridObjectVisual()
        {
            if (gridVisual == null) gridVisual = GetComponent<GridObjectVisual>();
            if (gridVisual == null) gridVisual = gameObject.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureFootprint(Footprint);
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
            outline.thickness = 0.026f;
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
            if (blockingCollider != null) blockingCollider.enabled = completed;
            if (completeRenderer != null) completeRenderer.visible = completed;
            if (completeRenderer != null) completeRenderer.SetVerticalFill(1f);
            SetActive(completeObject, completed);
            if (sparkleRenderer != null && !completed) sparkleRenderer.visible = false;
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
                sparkleRenderer.transform.localScale = Vector3.one * (0.35f + pulse * 0.9f);
                sparkleRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, t * 210f);
                sparkleRenderer.transform.localPosition = new Vector3(0.3f, 0.48f + pulse * 0.08f, 0f);
            }
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }

    }
}
