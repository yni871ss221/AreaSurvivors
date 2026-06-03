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
        public GameObject ghostObject;
        public GameObject buildObject;
        public GameObject completeObject;
        public Slider buildGauge;
        public float buildSeconds = 1.8f;
        public int maxHp = 70;
        public bool vertical;

        Health health;
        float buildProgress;
        float visualHeight = 1f;
        float sparkleTimer;
        Vector3 buildVisualScale = Vector3.one;
        Vector3 completeVisualScale = Vector3.one;
        Renderer[] completeObjectRenderers;
        Color[] completeObjectColors;
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
                completeVisualScale = completeRenderer.transform.localScale;
                visualHeight = completeRenderer.sprite.bounds.size.y * completeVisualScale.y;
            }
            if (buildRenderer != null) buildVisualScale = buildRenderer.transform.localScale;
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
            SetActive(ghostObject, !completed);
            if (buildRenderer != null)
            {
                buildRenderer.visible = !completed && buildProgress > 0f;
                buildRenderer.transform.localScale = new Vector3(buildVisualScale.x, buildVisualScale.y * Mathf.Max(0.02f, buildProgress), buildVisualScale.z);
                buildRenderer.transform.localPosition = new Vector3(0f, -visualHeight * (1f - buildProgress) * 0.5f, 0f);
            }
            if (buildObject != null)
            {
                buildObject.SetActive(!completed && buildProgress > 0f);
                buildObject.transform.localScale = new Vector3(buildVisualScale.x, buildVisualScale.y, buildVisualScale.z * Mathf.Max(0.02f, buildProgress));
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
            hammerRenderer.transform.localPosition = new Vector3(0.24f, -0.06f + Mathf.Abs(swing) * 0.05f, 0f);
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

        static Color[] CaptureColors(Renderer[] renderers)
        {
            if (renderers == null) return null;
            var colors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                colors[i] = renderers[i] != null ? renderers[i].material.color : Color.white;
            }

            return colors;
        }

        static void SetColor(Renderer[] renderers, Color[] baseColors, Color tint)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                var target = renderers[i];
                if (target == null) continue;
                var baseColor = baseColors != null && i < baseColors.Length ? baseColors[i] : Color.white;
                target.material.color = new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a * tint.a);
            }
        }
    }
}
