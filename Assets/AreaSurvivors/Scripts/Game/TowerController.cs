using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class TowerController : MonoBehaviour
    {
        public Slider hpBar;
        Health health;
        Collider2D[] colliders;
        PaperMeshVisual visual;
        Renderer[] modelRenderers;
        Color[][] modelColors;
        bool collapsing;

        void Awake()
        {
            health = GetComponent<Health>();
            colliders = GetComponents<Collider2D>();
            visual = GetComponentInChildren<PaperMeshVisual>();
            modelRenderers = GetComponentsInChildren<Renderer>(true);
            modelColors = CaptureColors(modelRenderers);
            health.Died += _ => StartCollapse();
        }

        public void Configure(int maxHp)
        {
            health.SetMax(maxHp);
        }

        void Update()
        {
            if (hpBar != null) hpBar.value = health.Normalized;
        }

        void StartCollapse()
        {
            if (collapsing) return;
            StartCoroutine(CollapseRoutine());
        }

        IEnumerator CollapseRoutine()
        {
            collapsing = true;
            foreach (var col in colliders) col.enabled = false;
            if (hpBar != null) hpBar.gameObject.SetActive(false);

            var startPosition = transform.position;
            var startScale = transform.localScale;
            var billboard = visual != null ? visual.GetComponent<PaperBillboard>() : null;
            float elapsed = 0f;
            const float duration = 1.15f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float shake = Mathf.Sin(elapsed * 42f) * Mathf.Lerp(0.08f, 0.01f, t);
                transform.position = startPosition + new Vector3(shake, -0.35f * t, 0f);
                if (billboard != null) billboard.rollDegrees = Mathf.Sin(elapsed * 30f) * Mathf.Lerp(5f, 14f, t);
                transform.localScale = new Vector3(startScale.x * Mathf.Lerp(1f, 1.08f, t), startScale.y * Mathf.Lerp(1f, 0.35f, t), startScale.z);
                if (visual != null)
                {
                    var color = visual.color;
                    color.a = Mathf.Lerp(1f, 0.18f, t);
                    visual.color = color;
                }
                SetColor(modelRenderers, modelColors, new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.18f, t)));
                yield return null;
            }

            GameManager.Instance?.GameOver();
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
