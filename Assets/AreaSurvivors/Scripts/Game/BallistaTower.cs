using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class BallistaTower : MonoBehaviour
    {
        public GameConfig config;
        public TileGrid grid;
        public GameObject arrowPrefab;
        public Collider2D blockingCollider;
        public PaperMeshVisual ghostRenderer;
        public PaperMeshVisual buildRenderer;
        public PaperMeshVisual completeRenderer;
        public PaperMeshVisual hammerRenderer;
        public PaperMeshVisual sparkleRenderer;
        public Sprite ballistaSprite;
        public Vector2 spriteVisualSize = new Vector2(1.34f, 1.65f);
        public GameObject ghostObject;
        public GameObject buildObject;
        public GameObject completeObject;
        public Slider buildGauge;
        public float buildSeconds = 2.2f;
        public float attackRange = 7.5f;
        public float attackCooldown = 1.15f;
        public int damage = 5;
        public int maxHp = 90;

        Health health;
        GridObjectMarker marker;
        float buildProgress;
        float attackTimer;
        float visualHeight = 1f;
        float sparkleTimer;
        Vector3 buildVisualScale = Vector3.one;
        Vector3 completeVisualScale = Vector3.one;
        Vector3 buildObjectScale = Vector3.one;
        Vector3 completeObjectScale = Vector3.one;
        Renderer[] completeObjectRenderers;
        Color[][] completeObjectColors;
        int touchingPlayers;
        Transform activeBuilder;
        bool completed;
        bool usingSpriteVisuals;
        bool breaking;
        bool hasRegisteredCell;
        Vector3Int registeredCell;
        readonly float sparkleDuration = 0.75f;
        const float BuildDecaySecondsMultiplier = 3f;

        public bool IsBuilt => completed;

        void Awake()
        {
            health = GetComponent<Health>();
            if (health == null) health = gameObject.AddComponent<Health>();
            marker = GetComponent<GridObjectMarker>();
            health.Died += _ => Break();
            EnsureBlockingCollider();
            EnsureSpriteVisuals();
            ConfigureHammerVisual();
        }

        public void RegisterBuildPlacement(TileGrid tileGrid, Vector3Int originCell)
        {
            grid = tileGrid;
            registeredCell = originCell;
            hasRegisteredCell = true;
        }

        void Start()
        {
            EnsureSpriteVisuals();
            ConfigureHammerVisual();

            if (config != null)
            {
                buildSeconds = config.ballistaBuildSeconds;
                attackRange = config.ballistaRange + ProgressionStore.GetLevel(UpgradeType.BallistaRange) * config.ballistaRangePerUpgradeLevel;
                attackCooldown = config.ballistaCooldown;
                damage = config.ballistaDamage;
                maxHp = config.ballistaMaxHp;
            }

            if (completeRenderer != null && completeRenderer.sprite != null)
            {
                completeVisualScale = completeRenderer.transform.localScale;
                visualHeight = completeRenderer.sprite.bounds.size.y * completeVisualScale.y;
            }
            if (buildRenderer != null) buildVisualScale = buildRenderer.transform.localScale;
            if (buildObject != null) buildObjectScale = buildObject.transform.localScale;
            if (completeObject != null)
            {
                completeObjectScale = completeObject.transform.localScale;
                completeObjectRenderers = completeObject.GetComponentsInChildren<Renderer>(true);
                completeObjectColors = CaptureColors(completeObjectRenderers);
            }
            if (blockingCollider != null) blockingCollider.enabled = completed;
            ApplyBuildVisuals();
        }

        void OnDestroy()
        {
            SetPlayerPassThrough(false);
        }

        void EnsureBlockingCollider()
        {
            if (blockingCollider != null) return;
            var colliders = GetComponents<Collider2D>();
            foreach (var col in colliders)
            {
                if (col != null && !col.isTrigger)
                {
                    blockingCollider = col;
                    return;
                }
            }

            var blocker = gameObject.AddComponent<BoxCollider2D>();
            blocker.size = new Vector2(1.28f, 1f);
            blocker.offset = new Vector2(0f, -0.1f);
            blocker.isTrigger = false;
            blockingCollider = blocker;
        }

        void EnsureSpriteVisuals()
        {
            if (ballistaSprite == null) return;
            if (ghostRenderer != null && buildRenderer != null && completeRenderer != null)
            {
                usingSpriteVisuals = true;
                DestroyLegacyObject(ghostObject, ghostRenderer.gameObject);
                DestroyLegacyObject(buildObject, buildRenderer.gameObject);
                DestroyLegacyObject(completeObject, completeRenderer.gameObject);
                ConfigureSpriteVisual(ghostRenderer, new Color(1f, 1f, 1f, 0.34f));
                ConfigureSpriteVisual(buildRenderer, Color.white);
                ConfigureSpriteVisual(completeRenderer, Color.white);
                ghostObject = ghostRenderer.gameObject;
                buildObject = buildRenderer.gameObject;
                completeObject = completeRenderer.gameObject;
                RefreshSortRenderers();
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
            buildObject = buildRenderer.gameObject;
            completeObject = completeRenderer.gameObject;
            RefreshSortRenderers();
            usingSpriteVisuals = true;
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

        PaperMeshVisual CreateSpriteVisual(string objectName, Color color, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            go.AddComponent<PaperBillboard>();
            var visual = go.AddComponent<PaperMeshVisual>();
            visual.Configure(ballistaSprite, color, sortingOrder);
            ConfigureSpriteVisual(visual, color);
            return visual;
        }

        void ConfigureSpriteVisual(PaperMeshVisual visual, Color color)
        {
            if (visual == null || ballistaSprite == null) return;
            visual.sprite = ballistaSprite;
            visual.color = color;
            if (visual.GetComponent<OcclusionMaskSource>() == null)
                visual.gameObject.AddComponent<OcclusionMaskSource>();
            ConfigureOutline(visual.gameObject);
            var bounds = ballistaSprite.bounds.size;
            float x = Mathf.Abs(bounds.x) > 0.001f ? spriteVisualSize.x / bounds.x : 1f;
            float y = Mathf.Abs(bounds.y) > 0.001f ? spriteVisualSize.y / bounds.y : 1f;
            visual.transform.localScale = new Vector3(x, y, 1f);
            visual.visible = false;
        }

        static void ConfigureOutline(GameObject target)
        {
            if (target == null) return;
            var outline = target.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) outline = target.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.035f;
        }

        void ConfigureHammerVisual()
        {
            if (hammerRenderer == null) return;
            var hammer = Resources.Load<Sprite>("Generated/Hammer");
            if (hammer != null) hammerRenderer.sprite = hammer;
            hammerRenderer.order = 22020;
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
                    buildProgress = Mathf.Clamp01(buildProgress + Time.deltaTime * WorkSpeedMultiplier() / Mathf.Max(0.1f, buildSeconds));
                    if (buildProgress >= 1f) CompleteBuild();
                }
                else if (buildProgress > 0f)
                {
                    buildProgress = Mathf.Clamp01(buildProgress - Time.deltaTime / Mathf.Max(0.1f, buildSeconds * BuildDecaySecondsMultiplier));
                }

                AnimateHammer();
                ApplyBuildVisuals();
                return;
            }

            attackTimer -= Time.deltaTime;
            AnimateCompletionSparkle();
            if (attackTimer <= 0f)
            {
                TryShoot();
                attackTimer = attackCooldown;
            }
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

        static float WorkSpeedMultiplier()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            return player != null ? Mathf.Max(0.05f, player.Stats.workSpeedMultiplier) : 1f;
        }

        void ApplyBuildVisuals()
        {
            if (ghostRenderer != null) ghostRenderer.visible = !completed;
            SetActive(ghostObject, !completed);
            if (buildRenderer != null)
            {
                buildRenderer.visible = !completed && buildProgress > 0f;
                buildRenderer.transform.localScale = new Vector3(buildVisualScale.x, buildVisualScale.y * Mathf.Max(0.02f, buildProgress), buildVisualScale.z);
                buildRenderer.transform.localPosition = new Vector3(0f, -visualHeight * (1f - buildProgress) * 0.5f, 0f);
            }
            if (!usingSpriteVisuals && buildObject != null)
            {
                buildObject.SetActive(!completed && buildProgress > 0f);
                buildObject.transform.localScale = new Vector3(buildObjectScale.x, buildObjectScale.y, buildObjectScale.z * Mathf.Max(0.02f, buildProgress));
            }
            if (completeRenderer != null) completeRenderer.visible = completed;
            if (!usingSpriteVisuals || completeObject == null || (completeRenderer != null && completeObject == completeRenderer.gameObject))
            {
                SetActive(completeObject, completed);
            }
            if (sparkleRenderer != null && !completed) sparkleRenderer.visible = false;
            if (buildGauge != null)
            {
                buildGauge.gameObject.SetActive(!completed && (touchingPlayers > 0 || buildProgress > 0f));
                buildGauge.value = buildProgress;
            }
            if (hammerRenderer != null) hammerRenderer.visible = !completed && touchingPlayers > 0;
            if (blockingCollider != null) blockingCollider.enabled = completed;
        }

        void AnimateHammer()
        {
            if (hammerRenderer == null || !hammerRenderer.visible) return;
            float swing = Mathf.Sin(Time.time * 16f);
            hammerRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -35f + swing * 32f);
            Vector3 contact = activeBuilder != null && blockingCollider != null
                ? blockingCollider.ClosestPoint(activeBuilder.position)
                : transform.position + new Vector3(0.28f, -0.12f, 0f);
            hammerRenderer.transform.localPosition = transform.InverseTransformPoint(contact) + new Vector3(0f, 0.2f + Mathf.Abs(swing) * 0.08f, 0f);
        }

        void CompleteBuild()
        {
            completed = true;
            buildProgress = 1f;
            attackTimer = 0.25f;
            sparkleTimer = sparkleDuration;
            health.SetMax(maxHp);
            SetPlayerPassThrough(true);
            ApplyBuildVisuals();
            AnimateCompletionSparkle();
            if (sparkleRenderer != null)
            {
                PixelBurstEffect.Spawn(sparkleRenderer.sprite, transform.position + new Vector3(0f, 0.62f, 0f), new Color(1f, 0.96f, 0.52f, 0.72f), 7, 0.24f, 0.28f, 3400);
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
            SetPlayerPassThrough(false);
            Destroy(gameObject);
        }

        void SetPlayerPassThrough(bool ignore)
        {
            if (blockingCollider == null) return;
            var players = FindObjectsOfType<PlayerController>();
            foreach (var player in players)
            {
                var playerCollider = player.GetComponent<Collider2D>();
                if (playerCollider != null) Physics2D.IgnoreCollision(blockingCollider, playerCollider, ignore);
            }
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
            SetColor(completeObjectRenderers, completeObjectColors, Color.Lerp(Color.white, new Color(1f, 0.96f, 0.52f, 1f), pulse));
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
            go.GetComponent<Projectile>().Launch(direction.normalized, damage, speed, false);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
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
