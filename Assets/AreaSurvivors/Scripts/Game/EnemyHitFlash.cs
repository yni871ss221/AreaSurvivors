using UnityEngine;

namespace AreaSurvivors
{
    [DefaultExecutionOrder(2000)]
    public sealed class EnemyHitFlash : MonoBehaviour
    {
        const float FlashSeconds = 0.22f;
        static readonly Color FlashColor = new Color(1f, 1f, 1f, 1f);

        PaperMeshVisual sourceVisual;
        MeshFilter sourceFilter;
        MeshRenderer sourceRenderer;
        MeshFilter overlayFilter;
        MeshRenderer overlayRenderer;
        Material overlayMaterial;
        float remainingSeconds;

        public void Play(PaperMeshVisual sourceVisual)
        {
            if (sourceVisual == null || !sourceVisual.visible) return;
            this.sourceVisual = sourceVisual;
            sourceFilter = sourceVisual.GetComponent<MeshFilter>();
            sourceRenderer = sourceVisual.GetComponent<MeshRenderer>();
            if (sourceFilter == null || sourceRenderer == null || sourceFilter.sharedMesh == null) return;

            EnsureOverlay(sourceVisual.transform);
            if (overlayFilter == null || overlayRenderer == null || overlayMaterial == null) return;

            remainingSeconds = FlashSeconds;
            overlayRenderer.enabled = true;
            SyncOverlay(1f);
        }

        void EnsureOverlay(Transform sourceTransform)
        {
            if (overlayRenderer != null) return;
            var go = new GameObject("Enemy Hit Flash");
            go.transform.SetParent(sourceTransform, false);
            overlayFilter = go.AddComponent<MeshFilter>();
            overlayRenderer = go.AddComponent<MeshRenderer>();
            var shader = Shader.Find("AreaSurvivors/SpriteAlphaFill");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            overlayMaterial = new Material(shader)
            {
                name = "Enemy Hit Flash",
                hideFlags = HideFlags.HideAndDontSave
            };
            overlayRenderer.sharedMaterial = overlayMaterial;
            overlayRenderer.enabled = false;
        }

        void LateUpdate()
        {
            if (remainingSeconds <= 0f)
            {
                if (overlayRenderer != null) overlayRenderer.enabled = false;
                return;
            }

            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            float alphaScale = Mathf.Clamp01(remainingSeconds / FlashSeconds);
            SyncOverlay(alphaScale);
        }

        void SyncOverlay(float alphaScale)
        {
            if (sourceVisual == null || sourceFilter == null || sourceRenderer == null || overlayRenderer == null || overlayMaterial == null) return;
            if (!sourceVisual.visible || sourceFilter.sharedMesh == null)
            {
                overlayRenderer.enabled = false;
                return;
            }

            overlayFilter.sharedMesh = sourceFilter.sharedMesh;
            overlayMaterial.mainTexture = sourceRenderer.sharedMaterial != null ? sourceRenderer.sharedMaterial.mainTexture : null;
            var color = FlashColor;
            color.a *= alphaScale;
            overlayMaterial.color = color;
            overlayRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            overlayRenderer.sortingOrder = sourceRenderer.sortingOrder + 80;
            overlayRenderer.enabled = alphaScale > 0.001f;
        }

        void OnDestroy()
        {
            if (overlayMaterial == null) return;
            if (Application.isPlaying) Destroy(overlayMaterial);
            else DestroyImmediate(overlayMaterial);
        }
    }
}
