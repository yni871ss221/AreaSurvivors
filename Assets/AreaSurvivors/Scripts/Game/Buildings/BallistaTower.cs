using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class BallistaTower : MonoBehaviour, IBuildableConstruction
    {
        public GameConfig config;
        public TileGrid grid;
        public GameObject arrowPrefab;
        public Collider2D blockingCollider;
        public PaperMeshVisual completeRenderer;
        public PaperMeshVisual sparkleRenderer;
        public Sprite ballistaSprite;
        public Vector2 spriteVisualSize = new Vector2(1.34f, 1.65f);
        public GameObject completeObject;
        public float attackRange = 7.5f;
        public float attackCooldown = 1.15f;
        public int damage = 5;
        public int maxHp = 90;

        Health health;
        GridObjectMarker marker;
        GridObjectVisual gridVisual;
        BuildingPrefabVisualSet prefabVisualSet;
        float attackTimer;
        float visualHeight = 1f;
        float sparkleTimer;
        Vector3 completeVisualScale = Vector3.one;
        Vector3 completeObjectScale = Vector3.one;
        Renderer[] completeObjectRenderers;
        Color[][] completeObjectColors;
        MaterialPropertyBlock[][] completeObjectColorBlocks;
        Color appliedCompleteObjectTint;
        bool hasAppliedCompleteObjectTint;
        bool completed;
        bool usingSpriteVisuals;
        bool usingPrefabLayout;
        bool breaking;
        bool hasRegisteredCell;
        Vector3Int registeredCell;
        readonly float sparkleDuration = 0.75f;

        public bool IsBuilt => completed;
        public TileGrid Grid => grid;
        public Vector3Int OriginCell => hasRegisteredCell ? registeredCell : grid != null ? grid.WorldToCell(transform.position) : Vector3Int.zero;
        public Vector2Int Footprint => marker != null ? marker.footprint : new Vector2Int(2, 2);

        void Awake()
        {
            health = GetComponent<Health>();
            if (health == null) health = gameObject.AddComponent<Health>();
            marker = GetComponent<GridObjectMarker>();
            EnsureGridObjectVisual();
            health.Died += _ => Break();
            EnsureBlockingCollider();
            UsePrefabVisualSetIfAvailable();
            EnsureSpriteVisuals();
            EnsureUpgradeTarget();
        }

        public void RegisterBuildPlacement(TileGrid tileGrid, Vector3Int originCell)
        {
            grid = tileGrid;
            registeredCell = originCell;
            hasRegisteredCell = true;
            EnsureGridObjectVisual();
        }

        public void ApplyBuildingUpgrade(Sprite upgradedSprite, int hpBonus, int damageBonus)
        {
            maxHp += Mathf.Max(0, hpBonus);
            damage += Mathf.Max(0, damageBonus);
            if (health != null) health.SetMax(maxHp);
            if (upgradedSprite != null) ballistaSprite = upgradedSprite;
            EnsureSpriteVisuals();
            CacheVisualScales();
            ApplyBuildVisuals();
        }

        public void SetCompletedVisualVisible(bool visible)
        {
            if (completeRenderer != null) completeRenderer.visible = completed && visible;
            SetActive(completeObject, completed && visible);
        }

        void Start()
        {
            EnsureSpriteVisuals();

            var upgradeTarget = GetComponent<BuildingUpgradeTarget>();
            if (config != null && (upgradeTarget == null || !upgradeTarget.IsUpgraded))
            {
                attackRange = config.ballistaRange + ProgressionStore.GetLevel(UpgradeType.BallistaRange) * config.ballistaRangePerUpgradeLevel;
                attackCooldown = config.ballistaCooldown;
                damage = config.ballistaDamage + ProgressionStore.GetLevel(UpgradeType.BallistaDamage) * config.ballistaDamagePerUpgradeLevel;
                maxHp = config.ballistaMaxHp;
            }
            BuildingSkillEffects.ConfigureAutoRegeneration(gameObject, config);

            CacheVisualScales();
            if (completeObject != null)
            {
                completeObjectScale = completeObject.transform.localScale;
                completeObjectRenderers = completeObject.GetComponentsInChildren<Renderer>(true);
                completeObjectColors = CaptureColors(completeObjectRenderers);
            }
            if (blockingCollider != null) blockingCollider.enabled = completed;
            ApplyBuildVisuals();
        }

        void EnsureBlockingCollider()
        {
            EnsureGridObjectVisual();
            var trigger = default(BoxCollider2D);
            var blocker = blockingCollider as BoxCollider2D;
            foreach (var circle in GetComponents<CircleCollider2D>())
            {
                if (circle != null && circle.isTrigger) Destroy(circle);
            }

            foreach (var box in GetComponents<BoxCollider2D>())
            {
                if (box.isTrigger && trigger == null) trigger = box;
                else if (!box.isTrigger && blocker == null) blocker = box;
            }

            gridVisual.ConfigureFootprintBox(trigger, true);
            blocker = gridVisual.ConfigureFootprintBox(blocker, false);
            blockingCollider = blocker;
        }

        void EnsureSpriteVisuals()
        {
            UsePrefabVisualSetIfAvailable();
            if (usingPrefabLayout && prefabVisualSet != null && prefabVisualSet.HasBaseVisuals)
            {
                usingSpriteVisuals = true;
                ConfigureSpriteVisual(completeRenderer, Color.white);
                completeObject = completeRenderer.gameObject;
                RefreshSortRenderers();
                return;
            }
            if (ballistaSprite == null) return;
            if (completeRenderer != null)
            {
                usingSpriteVisuals = true;
                DestroyLegacyObject(completeObject, completeRenderer.gameObject);
                ConfigureSpriteVisual(completeRenderer, Color.white);
                completeObject = completeRenderer.gameObject;
                RefreshSortRenderers();
                return;
            }

            var legacyCompleteObject = completeObject;
            SetActive(completeObject, false);
            completeRenderer = CreateSpriteVisual("Complete Image", Color.white, 1002);
            DestroyLegacyObject(legacyCompleteObject, completeRenderer.gameObject);
            completeObject = completeRenderer.gameObject;
            RefreshSortRenderers();
            usingSpriteVisuals = true;
        }

        void EnsureUpgradeTarget()
        {
            var target = GetComponent<BuildingUpgradeTarget>();
            if (target == null) target = gameObject.AddComponent<BuildingUpgradeTarget>();
            target.Configure(BuildingUpgradeKind.Ballista, 20, 50, "BallistaUpgrade", 100, 5);
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

        PaperMeshVisual CreateSpriteVisual(string objectName, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<PaperBillboard>().faceCamera = false;
            var visual = go.AddComponent<PaperMeshVisual>();
            visual.Configure(ballistaSprite, color, sortingOrder);
            ConfigureSpriteVisual(visual, color);
            return visual;
        }

        void ConfigureSpriteVisual(PaperMeshVisual visual, Color color)
        {
            if (visual == null) return;
            if (!usingPrefabLayout)
            {
                if (ballistaSprite == null) return;
                visual.sprite = ballistaSprite;
            }
            visual.color = color;
            if (visual.GetComponent<OcclusionMaskSource>() == null)
                visual.gameObject.AddComponent<OcclusionMaskSource>();
            ConfigureOutline(visual.gameObject);
            EnsureGridObjectVisual();
            if (!usingPrefabLayout)
            {
                visual.useBottomCenterAnchor = true;
                gridVisual.ApplyFootprintWidthPreserveAspect(visual, ballistaSprite);
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

        static void ConfigureOutline(GameObject target)
        {
            if (target == null) return;
            var outline = target.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) outline = target.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.035f;
        }

        void Update()
        {
            if (!completed) return;

            GameManager.Instance?.MarkBuildingDamageSourceActive(RunDamageBuildingSource.Ballista);
            attackTimer -= Time.deltaTime;
            AnimateCompletionSparkle();
            if (attackTimer <= 0f)
            {
                TryShoot();
                attackTimer = attackCooldown;
            }
        }

        public void CompleteImmediately()
        {
            if (completed) return;
            CompleteBuild();
        }

        void ApplyBuildVisuals()
        {
            var upgradeTarget = GetComponent<BuildingUpgradeTarget>();
            bool hideBaseVisual = upgradeTarget != null && upgradeTarget.IsUpgraded;
            if (completeRenderer != null) completeRenderer.SetVerticalFill(1f);
            if (!usingSpriteVisuals || completeObject == null || (completeRenderer != null && completeObject == completeRenderer.gameObject))
            {
                SetActive(completeObject, completed && !hideBaseVisual);
            }
            if (completeRenderer != null) completeRenderer.visible = completed && !hideBaseVisual;
            if (sparkleRenderer != null && !completed) sparkleRenderer.visible = false;
            if (blockingCollider != null) blockingCollider.enabled = completed;
        }

        void CompleteBuild()
        {
            completed = true;
            attackTimer = 0.25f;
            sparkleTimer = sparkleDuration;
            health.SetMax(maxHp);
            ApplyBuildVisuals();
            AnimateCompletionSparkle();
            CompletionSparkleEffect.Spawn(sparkleRenderer != null ? sparkleRenderer.sprite : null, transform.position + new Vector3(0f, 0.62f, 0f), 0.7f);
            if (sparkleRenderer != null)
            {
                PixelBurstEffect.Spawn(sparkleRenderer.sprite, transform.position + new Vector3(0f, 0.62f, 0f), new Color(1f, 0.96f, 0.52f, 0.72f), 7, 0.24f, 0.28f, 3400);
            }
        }

        void Break()
        {
            if (breaking) return;
            breaking = true;
            var cell = hasRegisteredCell ? registeredCell : grid != null ? grid.WorldToCell(transform.position) : OriginCell;
            if (BuildingPersistentState.TryMarkDestroyed(gameObject, grid, cell)) return;
            if (grid != null)
            {
                grid.ClearObject(cell);
            }
            Destroy(gameObject);
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
                if (completeObject != null) completeObject.transform.localScale = completeObjectScale;
                return;
            }

            sparkleTimer = Mathf.Max(0f, sparkleTimer - Time.deltaTime);
            float t = 1f - sparkleTimer / sparkleDuration;
            float pulse = Mathf.Sin(t * Mathf.PI);
            if (completeRenderer != null)
            {
                completeRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.96f, 0.52f, 1f), pulse);
                completeRenderer.transform.localScale = completeVisualScale * (1f + pulse * 0.14f);
            }
            SetCompleteObjectTint(Color.Lerp(Color.white, new Color(1f, 0.96f, 0.52f, 1f), pulse));
            if (completeObject != null) completeObject.transform.localScale = completeObjectScale * (1f + pulse * 0.1f);
            if (sparkleRenderer != null)
            {
                sparkleRenderer.visible = true;
                sparkleRenderer.color = new Color(1f, 1f, 1f, pulse);
                sparkleRenderer.transform.localScale = Vector3.one * (0.35f + pulse * 1.1f);
                sparkleRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, t * 210f);
                sparkleRenderer.transform.localPosition = new Vector3(0.18f, 0.62f + pulse * 0.12f, 0f);
            }
        }

        void TryShoot()
        {
            if (arrowPrefab == null) return;
            var enemies = FindObjectsOfType<EnemyController>();
            EnemyController nearest = null;
            float best = attackRange * attackRange;
            foreach (var enemy in enemies)
            {
                float distance = (enemy.transform.position - transform.position).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = enemy;
                }
            }

            if (nearest == null) return;
            var direction = (Vector2)(nearest.transform.position - transform.position);
            var go = Instantiate(arrowPrefab, transform.position + (Vector3)(direction.normalized * 0.35f), Quaternion.identity);
            float speed = config != null ? config.projectileSpeed * 1.15f : 10f;
            var projectile = go.GetComponent<Projectile>();
            if (projectile == null) return;
            projectile.paintsTerritory = false;
            projectile.SetDamageSource(RunDamageSource.ForBuilding(RunDamageBuildingSource.Ballista));
            projectile.Launch(direction.normalized, RelicEffects.ApplyBallistaDamage(damage, grid), speed, false);
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
