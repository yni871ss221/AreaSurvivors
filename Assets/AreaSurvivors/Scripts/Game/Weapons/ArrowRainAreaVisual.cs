using UnityEngine;

namespace AreaSurvivors
{
    [ExecuteAlways]
    public sealed class ArrowRainAreaVisual : MonoBehaviour
    {
        const int CircleSegments = 64;

        [SerializeField] MeshFilter fillMeshFilter;
        [SerializeField] MeshRenderer fillRenderer;
        [SerializeField] LineRenderer outlineRenderer;
        [SerializeField] Transform[] animatorVisuals;
        [SerializeField, Range(0.1f, 1f)] float arrowAnimationScale = 0.56f;
        [SerializeField] Color fillColor = new Color(0.24f, 0.62f, 0.96f, 0.24f);
        [SerializeField] Color outlineColor = new Color(0.58f, 0.86f, 1f, 0.78f);
        [SerializeField] int fillSortingOrder = WeaponSortingOrders.AreaEffect;
        [SerializeField] int outlineSortingOrder = WeaponSortingOrders.AreaEffect + 1;
        [SerializeField, Min(0.001f)] float outlineWidth = 0.05f;
        [SerializeField, Range(0.1f, 1f)] float areaVerticalAspect = 1f;

        Mesh generatedMesh;
        Material fillMaterial;
        Material outlineMaterial;
#if UNITY_EDITOR
        bool editorApplyQueued;
#endif

        public void Initialize(
            MeshFilter areaFillMeshFilter,
            MeshRenderer areaFillRenderer,
            LineRenderer areaOutlineRenderer)
        {
            fillMeshFilter = areaFillMeshFilter;
            fillRenderer = areaFillRenderer;
            outlineRenderer = areaOutlineRenderer;
            ApplyCircle();
        }

        public void SetAreaAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            fillColor.a = Mathf.Max(0.16f, alpha * 0.55f);
            outlineColor.a = Mathf.Max(0.68f, alpha * 0.95f);
            ApplyCircle();
            ApplyArrowScale();
        }

        public void SetAreaShape(float verticalAspect)
        {
            areaVerticalAspect = Mathf.Clamp(verticalAspect, 0.1f, 1f);
            ApplyCircle();
        }

        void Awake()
        {
            ApplyCircle();
            ApplyArrowScale();
        }

        void OnEnable()
        {
            ApplyCircle();
            ApplyArrowScale();
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            QueueEditorApply();
#else
            ApplyCircle();
            ApplyArrowScale();
#endif
        }

        void OnDestroy()
        {
            DestroyGenerated(generatedMesh);
            DestroyGenerated(fillMaterial);
            DestroyGenerated(outlineMaterial);
        }

        void ApplyArrowScale()
        {
            float parentScale = Mathf.Max(0.1f, transform.lossyScale.x);
            float localScale = Mathf.Clamp(arrowAnimationScale, 0.1f, 1f) / parentScale;
            if (animatorVisuals == null) return;
            foreach (var visual in animatorVisuals)
            {
                if (visual == null) continue;
                visual.localScale = Vector3.one * localScale;
            }
        }

        void ApplyCircle()
        {
            EnsureMaterials();
            ApplyFillMesh();
            ApplyOutline();
        }

        void EnsureMaterials()
        {
            if (fillRenderer != null && fillMaterial == null)
            {
                fillMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Arrow Rain Area Fill Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
                fillRenderer.sharedMaterial = fillMaterial;
            }
            if (fillMaterial != null) fillMaterial.mainTexture = Texture2D.whiteTexture;

            if (outlineRenderer != null && outlineMaterial == null)
            {
                outlineMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Arrow Rain Area Outline Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
                outlineRenderer.sharedMaterial = outlineMaterial;
            }
            if (outlineMaterial != null) outlineMaterial.mainTexture = Texture2D.whiteTexture;
        }

        void ApplyFillMesh()
        {
            if (fillMeshFilter == null || fillRenderer == null) return;
            DestroyGenerated(generatedMesh);
            generatedMesh = new Mesh
            {
                name = "Arrow Rain Area Circle",
                hideFlags = HideFlags.HideAndDontSave
            };

            var vertices = new Vector3[CircleSegments + 1];
            var triangles = new int[CircleSegments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * areaVerticalAspect, 0f);
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i == CircleSegments - 1 ? 1 : i + 2;
            }

            generatedMesh.vertices = vertices;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();
            fillMeshFilter.sharedMesh = generatedMesh;
            fillRenderer.sortingOrder = fillSortingOrder;
            if (fillMaterial != null) fillMaterial.color = fillColor;
        }

        void ApplyOutline()
        {
            if (outlineRenderer == null) return;
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.loop = true;
            outlineRenderer.positionCount = CircleSegments;
            outlineRenderer.widthMultiplier = Mathf.Max(0.001f, outlineWidth);
            outlineRenderer.sortingOrder = outlineSortingOrder;
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;
            if (outlineMaterial != null) outlineMaterial.color = outlineColor;

            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
                outlineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * areaVerticalAspect, 0f));
            }
        }

        static void DestroyGenerated(Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }

#if UNITY_EDITOR
        void QueueEditorApply()
        {
            if (editorApplyQueued) return;
            editorApplyQueued = true;
            UnityEditor.EditorApplication.delayCall += ApplyFromEditorDelay;
        }

        void ApplyFromEditorDelay()
        {
            editorApplyQueued = false;
            if (this == null) return;
            ApplyCircle();
            ApplyArrowScale();
        }
#endif
    }
}
