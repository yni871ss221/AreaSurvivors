using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class WoodenBarrier : MonoBehaviour, IBuildableConstruction
    {
        public GameConfig config;
        public Collider2D blockingCollider;
        public PaperMeshVisual completeRenderer;
        public PaperMeshVisual sparkleRenderer;
        public GameObject completeObject;
        public int maxHp = 70;
        public Sprite barrierSprite;
        public Vector2 spriteVisualSize = new Vector2(1.4f, 0.86f);

        Health health;
        TileGrid grid;
        GridObjectVisual gridVisual;
        BuildingPrefabVisualSet prefabVisualSet;
        float visualHeight = 1f;
        float sparkleTimer;
        Vector3 completeVisualScale = Vector3.one;
        Renderer[] completeObjectRenderers;
        Color[][] completeObjectColors;
        MaterialPropertyBlock[][] completeObjectColorBlocks;
        Color appliedCompleteObjectTint;
        bool hasAppliedCompleteObjectTint;
        bool completed;
        bool spriteVisualsPrepared;
        bool usingSpriteVisuals;
        bool usingPrefabLayout;
        bool breaking;
        bool hasRegisteredCell;
        Vector3Int registeredCell;
        const float SparkleDuration = 0.75f;

        public bool IsBuilt => completed;
        public TileGrid Grid => grid;
        public Vector3Int OriginCell => hasRegisteredCell ? registeredCell : grid != null ? grid.WorldToCell(transform.position) : Vector3Int.zero;
        public Vector2Int Footprint
        {
            get
            {
                var marker = GetComponent<GridObjectMarker>();
                return marker != null ? marker.footprint : Vector2Int.one;
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
            CacheVisualScales();
            ApplyVisuals();
            EnsureUpgradeTarget();
        }

        public void ApplyBuildingUpgrade(Sprite upgradedSprite, int hpBonus)
        {
            maxHp += Mathf.Max(0, hpBonus);
            if (health != null) health.SetMax(maxHp);
            if (upgradedSprite != null) barrierSprite = upgradedSprite;
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
                maxHp = config.woodenWallMaxHp + BuildingSkillEffects.WallMaxHpBonus(config);
            }
            BuildingSkillEffects.ConfigureAutoRegeneration(gameObject, config);

            EnsureSpriteVisuals();

            CacheVisualScales();
            if (completeObject != null)
            {
                completeVisualScale = completeObject.transform.localScale;
                completeObjectRenderers = completeObject.GetComponentsInChildren<Renderer>(true);
                completeObjectColors = CaptureColors(completeObjectRenderers);
            }

            ApplyVisuals();
        }

        void EnsureSpriteVisuals()
        {
            if (spriteVisualsPrepared) return;
            usingSpriteVisuals = true;
            UsePrefabVisualSetIfAvailable();
            if (usingPrefabLayout && prefabVisualSet != null && prefabVisualSet.HasBaseVisuals)
            {
                ConfigureSpriteVisual(completeRenderer, Color.white);
                completeObject = completeRenderer.gameObject;
                RefreshSortRenderers();
                spriteVisualsPrepared = true;
                return;
            }
            if (barrierSprite == null) return;
            if (completeRenderer != null)
            {
                DestroyLegacyObject(completeObject, completeRenderer.gameObject);
                ConfigureSpriteVisual(completeRenderer, Color.white);
                completeObject = completeRenderer.gameObject;
                RefreshSortRenderers();
                spriteVisualsPrepared = true;
                return;
            }

            var legacyCompleteObject = completeObject;
            SetActive(completeObject, false);

            completeRenderer = CreateSpriteVisual("Complete Image", Color.white, 1002);
            DestroyLegacyObject(legacyCompleteObject, completeRenderer.gameObject);
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
            target.Configure(BuildingUpgradeKind.WoodenWall, "WoodenWallUpgrade", 200);
        }

        void ApplyConfiguredSpriteToVisuals()
        {
            ConfigureSpriteVisual(completeRenderer, Color.white);
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
            outline.thickness = 0.026f;
        }

        void Update()
        {
            if (!completed) return;
            AnimateCompletionSparkle();
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

        void CompleteBuild()
        {
            completed = true;
            sparkleTimer = SparkleDuration;
            health.SetMax(maxHp);
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
            var cell = hasRegisteredCell ? registeredCell : grid != null ? grid.WorldToCell(transform.position) : OriginCell;
            if (BuildingRevivalState.TryHandleDestroyed(gameObject, grid, cell)) return;
            if (grid != null)
            {
                grid.ClearObject(cell);
            }
            Destroy(gameObject);
        }

        void ApplyVisuals()
        {
            if (blockingCollider != null) blockingCollider.enabled = completed;
            var upgradeTarget = GetComponent<BuildingUpgradeTarget>();
            bool hideBaseVisual = upgradeTarget != null && upgradeTarget.IsUpgraded;
            if (completeRenderer != null) completeRenderer.visible = completed && !hideBaseVisual;
            if (completeRenderer != null) completeRenderer.SetVerticalFill(1f);
            if (!usingSpriteVisuals || completeObject == null || (completeRenderer != null && completeObject == completeRenderer.gameObject))
            {
                SetActive(completeObject, completed && !hideBaseVisual);
            }
            if (sparkleRenderer != null) sparkleRenderer.visible = completed && !hideBaseVisual ? sparkleRenderer.visible : false;
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
                SetCompleteObjectTint(Color.white);
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
            SetCompleteObjectTint(Color.Lerp(Color.white, new Color(1f, 0.96f, 0.52f, 1f), pulse));
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
            completeRenderer = prefabVisualSet.completeVisual;
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

                var materials = renderers[i].sharedMaterials;
                colors[i] = new Color[materials.Length];
                for (int j = 0; j < materials.Length; j++)
                {
                    colors[i][j] = materials[j] != null ? materials[j].color : Color.white;
                }
            }

            return colors;
        }

        void SetCompleteObjectTint(Color tint)
        {
            if (hasAppliedCompleteObjectTint && Approximately(appliedCompleteObjectTint, tint)) return;
            SetColor(completeObjectRenderers, completeObjectColors, tint, ref completeObjectColorBlocks);
            appliedCompleteObjectTint = tint;
            hasAppliedCompleteObjectTint = true;
        }

        static bool Approximately(Color a, Color b)
        {
            const float tolerance = 0.001f;
            return Mathf.Abs(a.r - b.r) <= tolerance
                && Mathf.Abs(a.g - b.g) <= tolerance
                && Mathf.Abs(a.b - b.b) <= tolerance
                && Mathf.Abs(a.a - b.a) <= tolerance;
        }

        static void SetColor(Renderer[] renderers, Color[][] baseColors, Color tint, ref MaterialPropertyBlock[][] propertyBlocks)
        {
            if (renderers == null) return;
            if (propertyBlocks == null || propertyBlocks.Length != renderers.Length)
            {
                propertyBlocks = new MaterialPropertyBlock[renderers.Length][];
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                var target = renderers[i];
                if (target == null) continue;
                var materials = target.sharedMaterials;
                if (propertyBlocks[i] == null || propertyBlocks[i].Length != materials.Length)
                {
                    propertyBlocks[i] = new MaterialPropertyBlock[materials.Length];
                }

                for (int j = 0; j < materials.Length; j++)
                {
                    if (propertyBlocks[i][j] == null) propertyBlocks[i][j] = new MaterialPropertyBlock();
                    var baseColor = baseColors != null && i < baseColors.Length && baseColors[i] != null && j < baseColors[i].Length ? baseColors[i][j] : Color.white;
                    var color = new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a * tint.a);
                    var block = propertyBlocks[i][j];
                    target.GetPropertyBlock(block, j);
                    block.SetColor("_Color", color);
                    block.SetColor("_BaseColor", color);
                    target.SetPropertyBlock(block, j);
                }
            }
        }
    }
}
