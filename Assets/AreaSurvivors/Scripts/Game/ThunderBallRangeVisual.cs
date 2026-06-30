using UnityEngine;

namespace AreaSurvivors
{
    public sealed class ThunderBallRangeVisual : MonoBehaviour
    {
        const int EllipseSegments = 64;
        const float DefaultPerspectiveYScale = 0.65f;

        [SerializeField] MeshFilter fillMeshFilter;
        [SerializeField] MeshRenderer fillRenderer;
        [SerializeField] LineRenderer outlineRenderer;
        [SerializeField, Range(0.2f, 1f)] float perspectiveYScale = DefaultPerspectiveYScale;
        [SerializeField] Color fillColor = new Color(0.55f, 0.25f, 1f, 0.16f);
        [SerializeField] Color outlineColor = new Color(0.86f, 0.62f, 1f, 0.62f);

        Mesh generatedMesh;
        Material fillMaterial;
        Material outlineMaterial;
        float range = 1f;

        public void Initialize(MeshFilter areaFillMeshFilter, MeshRenderer areaFillRenderer, LineRenderer areaOutlineRenderer)
        {
            fillMeshFilter = areaFillMeshFilter;
            fillRenderer = areaFillRenderer;
            outlineRenderer = areaOutlineRenderer;
            ApplyEllipse();
        }

        public void Configure(float attackRange)
        {
            range = Mathf.Max(0.05f, attackRange);
            ApplyScale();
            ApplyEllipse();
        }

        void Awake()
        {
            ApplyScale();
            ApplyEllipse();
        }

        void LateUpdate()
        {
            transform.localPosition = Vector3.zero;
            transform.rotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
            ApplyScale();
        }

        void OnValidate()
        {
            ApplyScale();
            ApplyEllipse();
        }

        void OnDestroy()
        {
            DestroyGenerated(generatedMesh);
            DestroyGenerated(fillMaterial);
            DestroyGenerated(outlineMaterial);
        }

        void ApplyScale()
        {
            var parent = transform.parent;
            Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
            float parentX = Mathf.Max(0.01f, Mathf.Abs(parentScale.x));
            float parentY = Mathf.Max(0.01f, Mathf.Abs(parentScale.y));
            float yScale = Mathf.Clamp(perspectiveYScale, 0.2f, 1f);
            transform.localScale = new Vector3(range / parentX, range * yScale / parentY, 1f);
        }

        void ApplyEllipse()
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
                    name = "Thunder Ball Range Fill Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
                fillRenderer.sharedMaterial = fillMaterial;
            }
            if (fillMaterial != null)
            {
                fillMaterial.mainTexture = Texture2D.whiteTexture;
                fillMaterial.color = fillColor;
            }

            if (outlineRenderer != null && outlineMaterial == null)
            {
                outlineMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Thunder Ball Range Outline Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
                outlineRenderer.sharedMaterial = outlineMaterial;
            }
            if (outlineMaterial != null)
            {
                outlineMaterial.mainTexture = Texture2D.whiteTexture;
                outlineMaterial.color = outlineColor;
            }
        }

        void ApplyFillMesh()
        {
            if (fillMeshFilter == null || fillRenderer == null) return;
            DestroyGenerated(generatedMesh);
            generatedMesh = new Mesh
            {
                name = "Thunder Ball Range Ellipse",
                hideFlags = HideFlags.HideAndDontSave
            };

            var vertices = new Vector3[EllipseSegments + 1];
            var triangles = new int[EllipseSegments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < EllipseSegments; i++)
            {
                float angle = (i / (float)EllipseSegments) * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i == EllipseSegments - 1 ? 1 : i + 2;
            }

            generatedMesh.vertices = vertices;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();
            fillMeshFilter.sharedMesh = generatedMesh;
            fillRenderer.sortingOrder = WeaponSortingOrders.AreaEffect;
        }

        void ApplyOutline()
        {
            if (outlineRenderer == null) return;
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.loop = true;
            outlineRenderer.positionCount = EllipseSegments;
            outlineRenderer.widthMultiplier = 0.04f;
            outlineRenderer.sortingOrder = WeaponSortingOrders.AreaEffect + 1;
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;

            for (int i = 0; i < EllipseSegments; i++)
            {
                float angle = (i / (float)EllipseSegments) * Mathf.PI * 2f;
                outlineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }
        }

        static void DestroyGenerated(Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }
    }
}
