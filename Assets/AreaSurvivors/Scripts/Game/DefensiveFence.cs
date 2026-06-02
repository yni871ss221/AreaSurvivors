using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class DefensiveFence : MonoBehaviour
    {
        public GameConfig config;
        public Collider2D blockingCollider;
        public PaperMeshVisual ghostRenderer;
        public PaperMeshVisual buildRenderer;
        public PaperMeshVisual completeRenderer;
        public PaperMeshVisual hammerRenderer;
        public PaperMeshVisual sparkleRenderer;
        public Slider buildGauge;
        public float buildSeconds = 1.8f;
        public int maxHp = 70;
        public bool vertical;

        Health health;
        float buildProgress;
        float visualHeight = 1f;
        float sparkleTimer;
        int touchingPlayers;
        bool completed;
        const float SparkleDuration = 0.75f;

        public bool IsBuilt => completed;

        void Awake()
        {
            health = GetComponent<Health>();
            health.Died += _ => Break();
        }

        void Start()
        {
            if (config != null)
            {
                buildSeconds = config.fenceBuildSeconds;
                maxHp = config.fenceMaxHp;
            }

            if (completeRenderer != null && completeRenderer.sprite != null)
            {
                visualHeight = completeRenderer.sprite.bounds.size.y;
            }

            ApplyVisuals();
        }

        void OnDestroy()
        {
            SetPlayerPassThrough(false);
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
                ApplyVisuals();
                return;
            }

            AnimateCompletionSparkle();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null) touchingPlayers++;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() != null) touchingPlayers = Mathf.Max(0, touchingPlayers - 1);
        }

        void CompleteBuild()
        {
            completed = true;
            buildProgress = 1f;
            sparkleTimer = SparkleDuration;
            health.SetMax(maxHp);
            SetPlayerPassThrough(true);
            ApplyVisuals();
            AnimateCompletionSparkle();
        }

        void Break()
        {
            completed = false;
            buildProgress = 0f;
            sparkleTimer = 0f;
            SetPlayerPassThrough(false);
            ApplyVisuals();
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

        void ApplyVisuals()
        {
            if (blockingCollider != null) blockingCollider.enabled = completed;
            if (ghostRenderer != null) ghostRenderer.visible = !completed;
            if (buildRenderer != null)
            {
                buildRenderer.visible = !completed && buildProgress > 0f;
                buildRenderer.transform.localScale = new Vector3(1f, Mathf.Max(0.02f, buildProgress), 1f);
                buildRenderer.transform.localPosition = new Vector3(0f, -visualHeight * (1f - buildProgress) * 0.5f, 0f);
            }
            if (completeRenderer != null) completeRenderer.visible = completed;
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
            hammerRenderer.transform.localPosition = new Vector3(0.54f, -0.12f + Mathf.Abs(swing) * 0.08f, 0f);
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
                return;
            }

            sparkleTimer = Mathf.Max(0f, sparkleTimer - Time.deltaTime);
            float t = 1f - sparkleTimer / SparkleDuration;
            float pulse = Mathf.Sin(t * Mathf.PI);
            if (completeRenderer != null)
            {
                completeRenderer.color = Color.Lerp(Color.white, new Color(1f, 0.96f, 0.52f, 1f), pulse);
                completeRenderer.transform.localScale = Vector3.one * (1f + pulse * 0.1f);
            }
            if (sparkleRenderer != null)
            {
                sparkleRenderer.visible = true;
                sparkleRenderer.color = new Color(1f, 1f, 1f, pulse);
                sparkleRenderer.transform.localScale = Vector3.one * (0.35f + pulse * 0.9f);
                sparkleRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, t * 210f);
                sparkleRenderer.transform.localPosition = new Vector3(0.4f, 0.48f + pulse * 0.08f, 0f);
            }
        }
    }
}
