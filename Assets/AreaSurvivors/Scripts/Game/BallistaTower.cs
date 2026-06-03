using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class BallistaTower : MonoBehaviour
    {
        public GameConfig config;
        public GameObject arrowPrefab;
        public PaperMeshVisual ghostRenderer;
        public PaperMeshVisual buildRenderer;
        public PaperMeshVisual completeRenderer;
        public PaperMeshVisual hammerRenderer;
        public PaperMeshVisual sparkleRenderer;
        public GameObject ghostObject;
        public GameObject buildObject;
        public GameObject completeObject;
        public Slider buildGauge;
        public float buildSeconds = 2.2f;
        public float attackRange = 7.5f;
        public float attackCooldown = 1.15f;
        public int damage = 5;

        float buildProgress;
        float attackTimer;
        float visualHeight = 1f;
        float sparkleTimer;
        Vector3 buildObjectScale = Vector3.one;
        Vector3 completeObjectScale = Vector3.one;
        Renderer[] completeObjectRenderers;
        Color[][] completeObjectColors;
        int touchingPlayers;
        bool completed;
        readonly float sparkleDuration = 0.75f;

        void Start()
        {
            if (config != null)
            {
                buildSeconds = config.ballistaBuildSeconds;
                attackRange = config.ballistaRange;
                attackCooldown = config.ballistaCooldown;
                damage = config.ballistaDamage;
            }

            if (completeRenderer != null && completeRenderer.sprite != null)
            {
                visualHeight = completeRenderer.sprite.bounds.size.y;
            }
            if (buildObject != null) buildObjectScale = buildObject.transform.localScale;
            if (completeObject != null)
            {
                completeObjectScale = completeObject.transform.localScale;
                completeObjectRenderers = completeObject.GetComponentsInChildren<Renderer>(true);
                completeObjectColors = CaptureColors(completeObjectRenderers);
            }
            ApplyBuildVisuals();
        }

        void Update()
        {
            if (!completed)
            {
                if (touchingPlayers > 0)
                {
                    buildProgress = Mathf.Clamp01(buildProgress + Time.deltaTime / Mathf.Max(0.1f, buildSeconds));
                    if (buildProgress >= 1f) CompleteBuild();
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
            if (other.GetComponent<PlayerController>() != null) touchingPlayers++;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null) touchingPlayers = Mathf.Max(0, touchingPlayers - 1);
        }

        void ApplyBuildVisuals()
        {
            if (ghostRenderer != null) ghostRenderer.visible = !completed;
            SetActive(ghostObject, !completed);
            if (buildRenderer != null)
            {
                buildRenderer.visible = !completed && buildProgress > 0f;
                buildRenderer.transform.localScale = new Vector3(1f, Mathf.Max(0.02f, buildProgress), 1f);
                buildRenderer.transform.localPosition = new Vector3(0f, -visualHeight * (1f - buildProgress) * 0.5f, 0f);
            }
            if (buildObject != null)
            {
                buildObject.SetActive(!completed && buildProgress > 0f);
                buildObject.transform.localScale = new Vector3(buildObjectScale.x, buildObjectScale.y, buildObjectScale.z * Mathf.Max(0.02f, buildProgress));
            }
            if (completeRenderer != null) completeRenderer.visible = completed;
            SetActive(completeObject, completed);
            if (sparkleRenderer != null && !completed) sparkleRenderer.visible = false;
            if (buildGauge != null)
            {
                buildGauge.gameObject.SetActive(!completed && touchingPlayers > 0);
                buildGauge.value = buildProgress;
            }
            if (hammerRenderer != null) hammerRenderer.visible = !completed && touchingPlayers > 0;
        }

        void AnimateHammer()
        {
            if (hammerRenderer == null || !hammerRenderer.visible) return;
            float swing = Mathf.Sin(Time.time * 16f);
            hammerRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -35f + swing * 32f);
            hammerRenderer.transform.localPosition = new Vector3(0.28f, -0.12f + Mathf.Abs(swing) * 0.08f, 0f);
        }

        void CompleteBuild()
        {
            completed = true;
            buildProgress = 1f;
            attackTimer = 0.25f;
            sparkleTimer = sparkleDuration;
            ApplyBuildVisuals();
            AnimateCompletionSparkle();
        }

        void AnimateCompletionSparkle()
        {
            if (sparkleTimer <= 0f)
            {
                if (sparkleRenderer != null) sparkleRenderer.visible = false;
                if (completeRenderer != null)
                {
                    completeRenderer.color = Color.white;
                    completeRenderer.transform.localScale = Vector3.one;
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
                completeRenderer.transform.localScale = Vector3.one * (1f + pulse * 0.14f);
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
            go.transform.localScale *= 0.5f;
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
