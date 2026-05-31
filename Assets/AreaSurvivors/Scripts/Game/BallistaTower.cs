using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class BallistaTower : MonoBehaviour
    {
        public GameConfig config;
        public GameObject arrowPrefab;
        public SpriteRenderer ghostRenderer;
        public SpriteRenderer buildRenderer;
        public SpriteRenderer completeRenderer;
        public SpriteRenderer hammerRenderer;
        public SpriteRenderer sparkleRenderer;
        public Slider buildGauge;
        public float buildSeconds = 2.2f;
        public float attackRange = 7.5f;
        public float attackCooldown = 1.15f;
        public int damage = 5;

        float buildProgress;
        float attackTimer;
        float visualHeight = 1f;
        float sparkleTimer;
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
            if (ghostRenderer != null) ghostRenderer.enabled = !completed;
            if (buildRenderer != null)
            {
                buildRenderer.enabled = !completed && buildProgress > 0f;
                buildRenderer.transform.localScale = new Vector3(1f, Mathf.Max(0.02f, buildProgress), 1f);
                buildRenderer.transform.localPosition = new Vector3(0f, -visualHeight * (1f - buildProgress) * 0.5f, 0f);
            }
            if (completeRenderer != null) completeRenderer.enabled = completed;
            if (sparkleRenderer != null && !completed) sparkleRenderer.enabled = false;
            if (buildGauge != null)
            {
                buildGauge.gameObject.SetActive(!completed && touchingPlayers > 0);
                buildGauge.value = buildProgress;
            }
            if (hammerRenderer != null) hammerRenderer.enabled = !completed && touchingPlayers > 0;
        }

        void AnimateHammer()
        {
            if (hammerRenderer == null || !hammerRenderer.enabled) return;
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
                if (sparkleRenderer != null) sparkleRenderer.enabled = false;
                if (completeRenderer != null)
                {
                    completeRenderer.color = Color.white;
                    completeRenderer.transform.localScale = Vector3.one;
                }
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
            if (sparkleRenderer != null)
            {
                sparkleRenderer.enabled = true;
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
    }
}
