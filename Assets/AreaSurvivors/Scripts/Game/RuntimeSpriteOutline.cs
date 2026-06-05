using UnityEngine;

namespace AreaSurvivors
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class RuntimeSpriteOutline : MonoBehaviour
    {
        public Color outlineColor = Color.black;
        public float thickness = 0.035f;
        public bool blink;
        public float blinkSpeed = 5f;

        static readonly Vector3[] Directions =
        {
            new Vector3(-1f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(-1f, -1f, 0f).normalized,
            new Vector3(-1f, 1f, 0f).normalized,
            new Vector3(1f, -1f, 0f).normalized,
            new Vector3(1f, 1f, 0f).normalized
        };

        MeshFilter sourceFilter;
        MeshRenderer sourceRenderer;
        MeshFilter[] outlineFilters;
        MeshRenderer[] outlineRenderers;
        Material outlineMaterial;

        void Awake()
        {
            EnsureOutline();
        }

        void OnEnable()
        {
            EnsureOutline();
            SyncOutline();
        }

        void LateUpdate()
        {
            SyncOutline();
        }

        void OnDestroy()
        {
            if (outlineMaterial == null) return;
            if (Application.isPlaying) Destroy(outlineMaterial);
            else DestroyImmediate(outlineMaterial);
        }

        void EnsureOutline()
        {
            if (sourceFilter == null) sourceFilter = GetComponent<MeshFilter>();
            if (sourceRenderer == null) sourceRenderer = GetComponent<MeshRenderer>();
            if (sourceFilter == null || sourceRenderer == null) return;

            if (outlineMaterial == null)
            {
                outlineMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Runtime Sprite Outline",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            if (outlineRenderers != null && outlineRenderers.Length == Directions.Length) return;

            outlineFilters = new MeshFilter[Directions.Length];
            outlineRenderers = new MeshRenderer[Directions.Length];
            for (int i = 0; i < Directions.Length; i++)
            {
                var childName = "Runtime Outline " + i;
                var child = transform.Find(childName);
                var go = child != null ? child.gameObject : new GameObject(childName);
                go.transform.SetParent(transform, false);

                outlineFilters[i] = go.GetComponent<MeshFilter>();
                if (outlineFilters[i] == null) outlineFilters[i] = go.AddComponent<MeshFilter>();
                outlineRenderers[i] = go.GetComponent<MeshRenderer>();
                if (outlineRenderers[i] == null) outlineRenderers[i] = go.AddComponent<MeshRenderer>();
                outlineRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                outlineRenderers[i].receiveShadows = false;
            }
        }

        void SyncOutline()
        {
            if (sourceFilter == null || sourceRenderer == null || outlineRenderers == null) return;

            var sourceMaterial = sourceRenderer.sharedMaterial;
            if (sourceMaterial == null || sourceFilter.sharedMesh == null)
            {
                SetVisible(false);
                return;
            }

            var color = outlineColor;
            color.a *= SourceAlpha(sourceMaterial);
            if (blink)
            {
                color.a *= Mathf.Lerp(0.35f, 1f, Mathf.PingPong(Time.time * Mathf.Max(0.1f, blinkSpeed), 1f));
            }

            outlineMaterial.mainTexture = sourceMaterial.mainTexture;
            outlineMaterial.color = color;

            bool visible = sourceRenderer.enabled && color.a > 0.001f && thickness > 0.001f;
            for (int i = 0; i < outlineRenderers.Length; i++)
            {
                var outlineTransform = outlineRenderers[i].transform;
                outlineTransform.localPosition = Directions[i] * thickness;
                outlineTransform.localRotation = Quaternion.identity;
                outlineTransform.localScale = Vector3.one;
                outlineFilters[i].sharedMesh = sourceFilter.sharedMesh;
                outlineRenderers[i].sharedMaterial = outlineMaterial;
                outlineRenderers[i].sortingLayerID = sourceRenderer.sortingLayerID;
                outlineRenderers[i].sortingOrder = sourceRenderer.sortingOrder - 1;
                outlineRenderers[i].enabled = visible;
            }
        }

        void SetVisible(bool visible)
        {
            if (outlineRenderers == null) return;
            foreach (var outlineRenderer in outlineRenderers)
            {
                if (outlineRenderer != null) outlineRenderer.enabled = visible;
            }
        }

        static float SourceAlpha(Material material)
        {
            return material.HasProperty("_Color") ? material.color.a : 1f;
        }
    }
}
