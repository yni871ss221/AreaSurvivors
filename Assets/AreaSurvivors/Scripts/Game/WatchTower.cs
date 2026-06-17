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
        public Collider2D blockingCollider;
        public PaperMeshVisual ghostRenderer;
        public PaperMeshVisual buildRenderer;
        public PaperMeshVisual completeRenderer;
        public PaperMeshVisual hammerRenderer;
        public PaperMeshVisual sparkleRenderer;
        public Sprite towerSprite;
        public Vector2 spriteVisualSize = new Vector2(1.22f, 2.55f);
        public Vector3 spriteVisualOffset = Vector3.zero;
        public GameObject ghostObject;
        public GameObject buildObject;
        public GameObject completeObject;
        public Slider buildGauge;
        public float buildSeconds = 3.2f;
        public int maxHp = 100;
        public int autoPaintRadiusCells = 10;

        Health health;
        GridObjectMarker marker;
        GridObjectVisual gridVisual;
        BuildingPrefabVisualSet prefabVisualSet;
        float buildProgress;
        float assistedBuildTimer;
        float visualHeight = 1f;
        float sparkleTimer;
        Vector3 buildVisualScale = Vector3.one;
        Vector3 completeVisualScale = Vector3.one;
        int touchingPlayers;
        Transform activeBuilder;
        bool completed;
        bool spriteVisualsPrepared;
        bool usingPrefabLayout;
        bool breaking;
        bool hasRegisteredCell;
        Vector3Int registeredCell;
        const float SparkleDuration = 0.75f;
        const float BuildDecaySecondsMultiplier = 3f;
        static readonly Vector3 ToolVisualScale = Vector3.one * 0.58f;

        public bool IsBuilt => completed;
        public TileGrid Grid => grid;
        public Vector3Int OriginCell => hasRegisteredCell ? registeredCell : grid != null ? grid.WorldToCell(transform.position) : Vector3Int.zero;
        public Vector2Int Footprint => marker != null ? marker.footprint : new Vector2Int(2, 2);

        void Awake()
        {
            health = GetComponent<Health>();
            marker = GetComponent<GridObjectMarker>();
            EnsureGridObjectVisual();
            EnsureFootprintColliders();
            UsePrefabVisualSetIfAvailable();
            health.Died += _ => Break();
            EnsureSpriteVisuals();
            ConfigureHammerVisual();
            EnsureUpgradeTarget();
        }

        public void RegisterBuildPlacement(TileGrid tileGrid, Vector3Int originCell)
        {
            grid = tileGrid;
            registeredCell = originCell;
            hasRegisteredCell = true;
            EnsureGridObjectVisual();
            EnsureFootprintColliders();
        }

        public void ApplyBuildingUpgrade(Sprite upgradedSprite, int hpBonus, int paintRadiusBonus)
        {
            if (upgradedSprite != null) towerSprite = upgradedSprite;
            maxHp += Mathf.Max(0, hpBonus);
            autoPaintRadiusCells += Mathf.Max(0, paintRadiusBonus);
            if (health != null) health.SetMax(maxHp);
            spriteVisualsPrepared = false;
            EnsureSpriteVisuals();
            ConfigureHammerVisual();
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
            if (config != null)
            {
                buildSeconds = config.watchTowerBuildSeconds;
                maxHp = config.watchTowerMaxHp;
                autoPaintRadiusCells = config.watchTowerAutoPaintRadiusCells;
            }

            EnsureSpriteVisuals();
            ConfigureHammerVisual();

            CacheVisualScales();
            ApplyVisuals();
        }

        void Update()
        {
            if (completed)
            {
                AnimateCompletionSparkle();
                return;
            }

            if (touchingPlayers > 0)
            {
                AddBuildWork(WorkSpeedMultiplier(), activeBuilder);
            }
            else if (buildProgress > 0f)
            {
                if (assistedBuildTimer > 0f)
                {
                    assistedBuildTimer = Mathf.Max(0f, assistedBuildTimer - Time.deltaTime);
                }
                else
                {
                    buildProgress = Mathf.Clamp01(buildProgress - Time.deltaTime / Mathf.Max(0.1f, buildSeconds * BuildDecaySecondsMultiplier));
                }
            }

            ApplyVisuals();
            AnimateHammer();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            touchingPlayers++;
            activeBuilder = other.transform;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            touchingPlayers = Mathf.Max(0, touchingPlayers - 1);
            if (touchingPlayers == 0 || activeBuilder == other.transform) activeBuilder = null;
        }

        public void AddBuildWork(float workSpeedMultiplier, Transform builder = null)
        {
            if (completed) return;
            buildProgress = Mathf.Clamp01(buildProgress + Time.deltaTime * Mathf.Max(0f, workSpeedMultiplier) / Mathf.Max(0.1f, buildSeconds));
            assistedBuildTimer = 0.18f;
            if (builder != null) activeBuilder = builder;
            if (buildProgress >= 1f) CompleteBuild();
        }

        public void AutoPaintNearestCell()
        {
            if (!completed || grid == null || grid.groundTilemap == null) return;
            Vector3Int target;
            if (!TryFindNearestNonPlayerCell(out target)) return;
            grid.Paint(grid.groundTilemap.GetCellCenterWorld(target), TileOwner.Player, 0);
        }

        bool TryFindNearestNonPlayerCell(out Vector3Int target)
        {
            target = OriginCell;
            int radius = Mathf.Max(0, autoPaintRadiusCells);
            var candidates = new List<Vector3Int>();
            int bestDistance = int.MaxValue;

            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    int distance = Mathf.Abs(x) + Mathf.Abs(y);
                    if (distance > radius) continue;

                    var cell = OriginCell + new Vector3Int(x, y, 0);
                    if (!grid.ContainsCell(cell) || grid.GetOwner(cell) == TileOwner.Player) continue;
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

        static float WorkSpeedMultiplier()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            return player != null ? Mathf.Max(0.05f, player.Stats.workSpeedMultiplier) : 1f;
        }

        void CompleteBuild()
        {
            completed = true;
            buildProgress = 1f;
            sparkleTimer = SparkleDuration;
            health.SetMax(maxHp);
            ApplyVisuals();
            AnimateCompletionSparkle();
            if (sparkleRenderer != null)
            {
                PixelBurstEffect.Spawn(sparkleRenderer.sprite, transform.position + new Vector3(0f, 1.0f, 0f), new Color(1f, 0.96f, 0.52f, 0.66f), 7, 0.24f, 0.28f, 3400);
            }
        }

        void Break()
        {
            if (breaking) return;
            breaking = true;
            if (grid != null) grid.ClearObject(hasRegisteredCell ? registeredCell : grid.WorldToCell(transform.position));
            Destroy(gameObject);
        }

        void EnsureSpriteVisuals()
        {
            if (towerSprite == null || spriteVisualsPrepared) return;
            UsePrefabVisualSetIfAvailable();
            if (usingPrefabLayout && prefabVisualSet != null && prefabVisualSet.HasBaseVisuals)
            {
                prefabVisualSet.ApplySpriteToBase(towerSprite);
                ConfigureSpriteVisual(ghostRenderer, new Color(1f, 1f, 1f, 0.34f));
                ConfigureSpriteVisual(buildRenderer, Color.white);
                ConfigureSpriteVisual(completeRenderer, Color.white);
                ghostObject = ghostRenderer.gameObject;
                buildObject = buildRenderer.gameObject;
                completeObject = completeRenderer.gameObject;
                RefreshSortRenderers();
                spriteVisualsPrepared = true;
                return;
            }
            if (ghostRenderer == null) ghostRenderer = CreateSpriteVisual("Ghost Image", new Color(1f, 1f, 1f, 0.34f), 1000);
            if (buildRenderer == null) buildRenderer = CreateSpriteVisual("Build Fill Image", Color.white, 1001);
            if (completeRenderer == null) completeRenderer = CreateSpriteVisual("Complete Image", Color.white, 1002);
            if (hammerRenderer == null) hammerRenderer = CreateOverlayVisual("Hammer", GeneratedSpriteLoader.Load("Hammer"), 22020);
            if (sparkleRenderer == null) sparkleRenderer = CreateOverlayVisual("Completion Sparkle", GeneratedSpriteLoader.Load("Sparkle"), 22030);
            ConfigureSpriteVisual(ghostRenderer, new Color(1f, 1f, 1f, 0.34f));
            ConfigureSpriteVisual(buildRenderer, Color.white);
            ConfigureSpriteVisual(completeRenderer, Color.white);
            ghostObject = ghostRenderer.gameObject;
            buildObject = buildRenderer.gameObject;
            completeObject = completeRenderer.gameObject;
            RefreshSortRenderers();
            spriteVisualsPrepared = true;
        }

        void EnsureUpgradeTarget()
        {
            var target = GetComponent<BuildingUpgradeTarget>();
            if (target == null) target = gameObject.AddComponent<BuildingUpgradeTarget>();
            target.Configure(BuildingUpgradeKind.WatchTower, 20, 50, "WatchTowerUpgrade", null, 100, 0, 5);
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
            if (visual == null || towerSprite == null) return;
            visual.sprite = towerSprite;
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
            if (buildRenderer != null) buildVisualScale = buildRenderer.transform.localScale;
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
            outline.thickness = 0.03f;
        }

        void ConfigureHammerVisual()
        {
            if (hammerRenderer == null) return;
            var hammer = GeneratedSpriteLoader.Load("Hammer");
            if (hammer != null) hammerRenderer.sprite = hammer;
            hammerRenderer.order = 22020;
            ApplyToolVisualScale(hammerRenderer.transform);
            var outline = hammerRenderer.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) outline = hammerRenderer.gameObject.AddComponent<RuntimeSpriteOutline>();
            if (hammerRenderer.GetComponent<PreserveSortingOrder>() == null) hammerRenderer.gameObject.AddComponent<PreserveSortingOrder>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.022f;
        }

        void RefreshSortRenderers()
        {
            var ySort = GetComponent<YSort>();
            if (ySort == null) return;
            ySort.renderers = new[]
            {
                ghostRenderer != null ? ghostRenderer.Renderer : null,
                buildRenderer != null ? buildRenderer.Renderer : null,
                completeRenderer != null ? completeRenderer.Renderer : null
            };
            ySort.Apply();
        }

        void ApplyVisuals()
        {
            if (blockingCollider != null) blockingCollider.enabled = completed;
            if (ghostRenderer != null) ghostRenderer.visible = !completed;
            SetActive(ghostObject, !completed);
            if (buildRenderer != null)
            {
                buildRenderer.visible = !completed && buildProgress > 0f;
                buildRenderer.transform.localScale = usingPrefabLayout
                    ? buildVisualScale
                    : buildVisualScale;
                buildRenderer.SetVerticalFill(buildProgress);
                if (!usingPrefabLayout) buildRenderer.transform.localPosition = gridVisual != null ? gridVisual.visualOffset : spriteVisualOffset;
            }
            if (completeRenderer != null) completeRenderer.visible = completed;
            if (completeRenderer != null) completeRenderer.SetVerticalFill(1f);
            SetActive(completeObject, completed);
            if (sparkleRenderer != null && !completed) sparkleRenderer.visible = false;
            if (buildGauge != null)
            {
                buildGauge.gameObject.SetActive(!completed && (touchingPlayers > 0 || buildProgress > 0f));
                buildGauge.value = buildProgress;
            }
            if (hammerRenderer != null) hammerRenderer.visible = ShouldShowHammer();
        }

        void AnimateHammer()
        {
            if (hammerRenderer == null || !ShouldShowHammer()) return;
            float swing = Mathf.Sin(Time.time * 16f);
            hammerRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -35f + swing * 32f);
            var offset = gridVisual != null ? gridVisual.visualOffset : spriteVisualOffset;
            if (!usingPrefabLayout)
            {
                hammerRenderer.transform.localPosition = offset + new Vector3(0.18f, spriteVisualSize.y * 0.5f + 0.42f + Mathf.Abs(swing) * 0.08f, 0f);
            }
            ApplyToolVisualScale(hammerRenderer.transform);
        }

        bool ShouldShowHammer()
        {
            return !completed && (touchingPlayers > 0 || assistedBuildTimer > 0f);
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
            usingPrefabLayout = prefabVisualSet != null && prefabVisualSet.usePrefabLayout && prefabVisualSet.HasBaseVisuals;
            if (!usingPrefabLayout) return;
            ghostRenderer = prefabVisualSet.ghostVisual;
            buildRenderer = prefabVisualSet.buildFillVisual;
            completeRenderer = prefabVisualSet.completeVisual;
            if (prefabVisualSet.hammerVisual != null) hammerRenderer = prefabVisualSet.hammerVisual;
            if (prefabVisualSet.sparkleVisual != null) sparkleRenderer = prefabVisualSet.sparkleVisual;
        }
    }
}
