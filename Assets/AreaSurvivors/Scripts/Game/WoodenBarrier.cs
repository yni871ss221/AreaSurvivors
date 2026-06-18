using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class WoodenBarrier : MonoBehaviour, IBuildableConstruction
    {
        public GameConfig config;
        public Collider2D blockingCollider;
        public PaperMeshVisual ghostRenderer;
        public PaperMeshVisual buildRenderer;
        public PaperMeshVisual completeRenderer;
        public PaperMeshVisual hammerRenderer;
        public PaperMeshVisual sparkleRenderer;
        public GameObject ghostObject;
        public GameObject buildObject;
        public GameObject completeObject;
        public float buildSeconds = 1.8f;
        public int maxHp = 70;
        public bool gate;
        public Sprite barrierSprite;
        public Sprite openGateSprite;
        public Vector2 spriteVisualSize = new Vector2(1.4f, 0.86f);

        Health health;
        TileGrid grid;
        GridObjectVisual gridVisual;
        BuildingPrefabVisualSet prefabVisualSet;
        float buildProgress;
        float assistedBuildTimer;
        float visualHeight = 1f;
        float sparkleTimer;
        Vector3 buildVisualScale = Vector3.one;
        Vector3 completeVisualScale = Vector3.one;
        Renderer[] completeObjectRenderers;
        Color[][] completeObjectColors;
        int touchingPlayers;
        Transform activeBuilder;
        bool completed;
        bool spriteVisualsPrepared;
        bool usingSpriteVisuals;
        bool usingPrefabLayout;
        bool breaking;
        bool hasRegisteredCell;
        Vector3Int registeredCell;
        readonly HashSet<Collider2D> ignoredPlayerColliders = new HashSet<Collider2D>();
        const float SparkleDuration = 0.75f;
        const float BuildDecaySecondsMultiplier = 3f;
        static readonly Vector3 ToolVisualScale = Vector3.one * 0.58f;

        public bool IsBuilt => completed;
        public TileGrid Grid => grid;
        public Vector3Int OriginCell => hasRegisteredCell ? registeredCell : grid != null ? grid.WorldToCell(transform.position) : Vector3Int.zero;
        public Vector2Int Footprint
        {
            get
            {
                var marker = GetComponent<GridObjectMarker>();
                return marker != null ? marker.footprint : new Vector2Int(3, 1);
            }
        }

        void Awake()
        {
            health = GetComponent<Health>();
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

        public void RefreshConfiguredSprites()
        {
            spriteVisualsPrepared = false;
            UsePrefabVisualSetIfAvailable();
            EnsureSpriteVisuals();
            ConfigureHammerVisual();
            CacheVisualScales();
            ApplyVisuals();
            EnsureUpgradeTarget();
        }

        public void ApplyBuildingUpgrade(Sprite upgradedSprite, Sprite upgradedOpenSprite, int hpBonus)
        {
            maxHp += Mathf.Max(0, hpBonus);
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
                buildSeconds = config.woodenWallBuildSeconds;
                maxHp = config.woodenWallMaxHp;
            }

            EnsureSpriteVisuals();
            ConfigureHammerVisual();

            CacheVisualScales();
            if (buildObject != null)
            {
                buildVisualScale = buildObject.transform.localScale;
            }
            if (completeObject != null)
            {
                completeVisualScale = completeObject.transform.localScale;
                completeObjectRenderers = completeObject.GetComponentsInChildren<Renderer>(true);
                completeObjectColors = CaptureColors(completeObjectRenderers);
            }

            ApplyVisuals();
        }

        void OnDestroy()
        {
            if (blockingCollider == null) return;
            foreach (var playerCollider in ignoredPlayerColliders)
            {
                if (playerCollider != null) Physics2D.IgnoreCollision(blockingCollider, playerCollider, false);
            }
            ignoredPlayerColliders.Clear();
        }

        void EnsureSpriteVisuals()
        {
            if (spriteVisualsPrepared) return;
            usingSpriteVisuals = true;
            UsePrefabVisualSetIfAvailable();
            if (usingPrefabLayout && prefabVisualSet != null && prefabVisualSet.HasBaseVisuals)
            {
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
            if (barrierSprite == null) return;
            if (ghostRenderer != null && buildRenderer != null && completeRenderer != null)
            {
                DestroyLegacyObject(ghostObject, ghostRenderer.gameObject);
                DestroyLegacyObject(buildObject, buildRenderer.gameObject);
                DestroyLegacyObject(completeObject, completeRenderer.gameObject);
                ConfigureSpriteVisual(ghostRenderer, new Color(1f, 1f, 1f, 0.34f));
                ConfigureSpriteVisual(buildRenderer, Color.white);
                ConfigureSpriteVisual(completeRenderer, Color.white);
                ghostObject = ghostRenderer.gameObject;
                buildObject = null;
                completeObject = completeRenderer.gameObject;
                RefreshSortRenderers();
                spriteVisualsPrepared = true;
                return;
            }

            var legacyGhostObject = ghostObject;
            var legacyBuildObject = buildObject;
            var legacyCompleteObject = completeObject;
            SetActive(ghostObject, false);
            SetActive(buildObject, false);
            SetActive(completeObject, false);

            ghostRenderer = CreateSpriteVisual("Ghost Image", new Color(1f, 1f, 1f, 0.34f), 1000);
            buildRenderer = CreateSpriteVisual("Build Fill Image", Color.white, 1001);
            completeRenderer = CreateSpriteVisual("Complete Image", Color.white, 1002);
            DestroyLegacyObject(legacyGhostObject, ghostRenderer.gameObject);
            DestroyLegacyObject(legacyBuildObject, buildRenderer.gameObject);
            DestroyLegacyObject(legacyCompleteObject, completeRenderer.gameObject);
            ghostObject = ghostRenderer.gameObject;
            buildObject = null;
            completeObject = completeRenderer.gameObject;
            completeObjectRenderers = null;
            completeObjectColors = null;
            RefreshSortRenderers();
            spriteVisualsPrepared = true;
        }

        void EnsureUpgradeTarget()
        {
            var target = GetComponent<BuildingUpgradeTarget>();
            if (target == null) target = gameObject.AddComponent<BuildingUpgradeTarget>();
            if (gate)
            {
                target.Configure(BuildingUpgradeKind.WoodenGate, 0, 30, "WoodenGateUpgradeClosed", "WoodenGateUpgradeOpen", 100);
            }
            else
            {
                target.Configure(BuildingUpgradeKind.WoodenWall, 0, 20, "WoodenWallUpgrade", null, 100);
            }
        }

        void ApplyConfiguredSpriteToVisuals()
        {
            ConfigureSpriteVisual(ghostRenderer, ghostRenderer != null ? ghostRenderer.color : new Color(1f, 1f, 1f, 0.34f));
            ConfigureSpriteVisual(buildRenderer, Color.white);
            ConfigureSpriteVisual(completeRenderer, Color.white);
            ApplyGateVisualState();
        }

        void DestroyLegacyObject(GameObject legacyObject, GameObject replacementObject)
        {
            if (legacyObject == null || legacyObject == replacementObject) return;
            legacyObject.SetActive(false);
            Destroy(legacyObject);
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

        PaperMeshVisual CreateSpriteVisual(string name, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<PaperBillboard>().faceCamera = false;
            var visual = go.AddComponent<PaperMeshVisual>();
            visual.Configure(barrierSprite, color, sortingOrder);
            ConfigureSpriteVisual(visual, color);
            return visual;
        }

        void ConfigureSpriteVisual(PaperMeshVisual visual, Color color)
        {
            if (visual == null) return;
            if (!usingPrefabLayout)
            {
                if (barrierSprite == null) return;
                visual.sprite = barrierSprite;
            }
            visual.color = color;
            if (visual.GetComponent<OcclusionMaskSource>() == null)
                visual.gameObject.AddComponent<OcclusionMaskSource>();
            ConfigureOutline(visual.gameObject);
            EnsureGridObjectVisual();
            if (!usingPrefabLayout)
            {
                visual.useBottomCenterAnchor = true;
                gridVisual.ApplyFootprintWidthPreserveAspect(visual, barrierSprite);
            }
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

        Vector3 SpriteVisualOffset()
        {
            EnsureGridObjectVisual();
            return gridVisual != null ? gridVisual.visualOffset : Vector3.zero;
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

        void ConfigureHammerVisual()
        {
            if (hammerRenderer == null) return;
            hammerRenderer.order = 22020;
            ApplyToolVisualScale(hammerRenderer.transform, false);
            var outline = hammerRenderer.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) outline = hammerRenderer.gameObject.AddComponent<RuntimeSpriteOutline>();
            if (hammerRenderer.GetComponent<PreserveSortingOrder>() == null) hammerRenderer.gameObject.AddComponent<PreserveSortingOrder>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.022f;
        }

        void Update()
        {
            if (!completed)
            {
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
                return;
            }

            AnimateCompletionSparkle();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            touchingPlayers++;
            activeBuilder = other.transform;
            if (completed && gate) SetPlayerGateCollision(other, true);
            ApplyGateVisualState();
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            touchingPlayers = Mathf.Max(0, touchingPlayers - 1);
            if (touchingPlayers == 0 || activeBuilder == other.transform) activeBuilder = null;
            if (gate) SetPlayerGateCollision(other, false);
            ApplyGateVisualState();
        }

        static float WorkSpeedMultiplier()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            return player != null ? Mathf.Max(0.05f, player.Stats.workSpeedMultiplier) : 1f;
        }

        public void AddBuildWork(float workSpeedMultiplier, Transform builder = null)
        {
            if (completed) return;
            buildProgress = Mathf.Clamp01(buildProgress + Time.deltaTime * Mathf.Max(0f, workSpeedMultiplier) / Mathf.Max(0.1f, buildSeconds));
            assistedBuildTimer = 0.18f;
            if (builder != null) activeBuilder = builder;
            if (buildProgress >= 1f) CompleteBuild();
        }

        void CompleteBuild()
        {
            completed = true;
            buildProgress = 1f;
            sparkleTimer = SparkleDuration;
            health.SetMax(maxHp);
            if (gate && activeBuilder != null)
            {
                var playerCollider = activeBuilder.GetComponent<Collider2D>();
                if (playerCollider != null) SetPlayerGateCollision(playerCollider, true);
            }
            ApplyVisuals();
            AnimateCompletionSparkle();
            CompletionSparkleEffect.Spawn(sparkleRenderer != null ? sparkleRenderer.sprite : null, transform.position + new Vector3(0f, 0.5f, 0f), 0.65f);
            if (sparkleRenderer != null)
            {
                PixelBurstEffect.Spawn(sparkleRenderer.sprite, transform.position + new Vector3(0f, 0.5f, 0f), new Color(1f, 0.96f, 0.52f, 0.66f), 6, 0.22f, 0.26f, 3400);
            }
        }

        void Break()
        {
            if (breaking) return;
            breaking = true;
            if (grid != null)
            {
                grid.ClearObject(hasRegisteredCell ? registeredCell : grid.WorldToCell(transform.position));
            }
            Destroy(gameObject);
        }

        void ApplyVisuals()
        {
            if (blockingCollider != null) blockingCollider.enabled = completed;
            if (ghostRenderer != null) ghostRenderer.visible = !completed;
            SetActive(ghostObject, !completed);
            if (buildRenderer != null)
            {
                buildRenderer.visible = !completed && buildProgress > 0f;
                buildRenderer.transform.localScale = buildVisualScale;
                buildRenderer.SetVerticalFill(buildProgress);
                if (!usingPrefabLayout) buildRenderer.transform.localPosition = SpriteVisualOffset();
            }
            if (!usingSpriteVisuals && buildObject != null)
            {
                buildObject.SetActive(!completed && buildProgress > 0f);
            }
            if (completeRenderer != null) completeRenderer.visible = completed;
            if (completeRenderer != null) completeRenderer.SetVerticalFill(1f);
            if (!usingSpriteVisuals || completeObject == null || (completeRenderer != null && completeObject == completeRenderer.gameObject))
            {
                SetActive(completeObject, completed);
            }
            if (sparkleRenderer != null && !completed) sparkleRenderer.visible = false;
            if (hammerRenderer != null) hammerRenderer.visible = ShouldShowHammer();
            ApplyGateVisualState();
        }

        void AnimateHammer()
        {
            if (hammerRenderer == null || !ShouldShowHammer()) return;
            float swing = Mathf.Sin(Time.time * 16f);
            hammerRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -35f + swing * 32f);
            if (!usingPrefabLayout)
            {
                hammerRenderer.transform.localPosition = new Vector3(0.18f, 0.22f + Mathf.Abs(swing) * 0.05f, 0f);
            }
            ApplyToolVisualScale(hammerRenderer.transform, false);
        }

        void ApplyGateVisualState()
        {
            if (!gate || completeRenderer == null || openGateSprite == null || barrierSprite == null) return;
            var upgradeTarget = GetComponent<BuildingUpgradeTarget>();
            if (upgradeTarget != null && upgradeTarget.IsUpgraded)
            {
                upgradeTarget.SetUpgradedGateOpen(completed && touchingPlayers > 0);
                return;
            }

            var sprite = completed && touchingPlayers > 0 ? openGateSprite : barrierSprite;
            completeRenderer.sprite = sprite;
        }

        void SetPlayerGateCollision(Collider2D playerCollider, bool ignored)
        {
            if (blockingCollider == null || playerCollider == null) return;
            Physics2D.IgnoreCollision(blockingCollider, playerCollider, ignored);
            if (ignored) ignoredPlayerColliders.Add(playerCollider);
            else ignoredPlayerColliders.Remove(playerCollider);
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
                SetColor(completeObjectRenderers, completeObjectColors, Color.white);
                if (completeObject != null) completeObject.transform.localScale = completeVisualScale;
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
            SetColor(completeObjectRenderers, completeObjectColors, Color.Lerp(Color.white, new Color(1f, 0.96f, 0.52f, 1f), pulse));
            if (completeObject != null) completeObject.transform.localScale = completeVisualScale * (1f + pulse * 0.08f);
            if (sparkleRenderer != null)
            {
                sparkleRenderer.visible = true;
                sparkleRenderer.color = new Color(1f, 1f, 1f, pulse);
                sparkleRenderer.transform.localScale = Vector3.one * (0.35f + pulse * 0.9f);
                sparkleRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, t * 210f);
                sparkleRenderer.transform.localPosition = new Vector3(0.4f, 0.48f + pulse * 0.08f, 0f);
            }
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }

        static void ApplyToolVisualScale(Transform target, bool _)
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
            if (prefabVisualSet != null && prefabVisualSet.usePrefabLayout)
            {
                prefabVisualSet.BindMissingVisualsFromChildren();
                prefabVisualSet.DisableBillboardsForBuildingVisuals();
            }
            usingPrefabLayout = prefabVisualSet != null && prefabVisualSet.usePrefabLayout && prefabVisualSet.HasBaseVisuals;
            if (!usingPrefabLayout) return;
            ghostRenderer = prefabVisualSet.ghostVisual;
            buildRenderer = prefabVisualSet.buildFillVisual;
            completeRenderer = prefabVisualSet.completeVisual;
            if (prefabVisualSet.hammerVisual != null) hammerRenderer = prefabVisualSet.hammerVisual;
            if (prefabVisualSet.sparkleVisual != null) sparkleRenderer = prefabVisualSet.sparkleVisual;
        }

        static Color[][] CaptureColors(Renderer[] renderers)
        {
            if (renderers == null) return null;
            var colors = new Color[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                {
                    colors[i] = new[] { Color.white };
                    continue;
                }

                var materials = renderers[i].materials;
                colors[i] = new Color[materials.Length];
                for (int j = 0; j < materials.Length; j++)
                {
                    colors[i][j] = materials[j] != null ? materials[j].color : Color.white;
                }
            }

            return colors;
        }

        static void SetColor(Renderer[] renderers, Color[][] baseColors, Color tint)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                var target = renderers[i];
                if (target == null) continue;
                var materials = target.materials;
                for (int j = 0; j < materials.Length; j++)
                {
                    var baseColor = baseColors != null && i < baseColors.Length && baseColors[i] != null && j < baseColors[i].Length ? baseColors[i][j] : Color.white;
                    materials[j].color = new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a * tint.a);
                }

                target.materials = materials;
            }
        }
    }
}
