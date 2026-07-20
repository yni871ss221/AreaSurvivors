using UnityEngine;

namespace AreaSurvivors
{
    [ExecuteAlways]
    [RequireComponent(typeof(PolygonCollider2D))]
    public sealed class ExcaliburSectorVisual : MonoBehaviour
    {
        [SerializeField] MeshFilter sectorMeshFilter;
        [SerializeField] MeshRenderer sectorRenderer;
        [SerializeField] PolygonCollider2D sectorCollider;
        [SerializeField] Material effectMaterial;
        [SerializeField, Range(8, 64)] int angleSegments = 32;
        [SerializeField] int sortingOrder = WeaponSortingOrders.Projectile;
        [SerializeField, Min(0.01f)] float previewLength = 0.2f;
        [SerializeField, Range(1f, 170f)] float previewArcDegrees = 30f;
        [SerializeField, Min(0.05f)] float previewBandWidth = 1.5f;
        [SerializeField, Range(0f, 1f)] float previewRevealFraction = 1f;

        Mesh generatedMesh;
        bool shapeDirty;

        public MeshFilter SectorMeshFilter => sectorMeshFilter;
        public MeshRenderer SectorRenderer => sectorRenderer;
        public PolygonCollider2D SectorCollider => sectorCollider;
        public Material EffectMaterial => effectMaterial;

        public void Initialize(
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            PolygonCollider2D polygonCollider,
            Material material)
        {
            sectorMeshFilter = meshFilter;
            sectorRenderer = meshRenderer;
            sectorCollider = polygonCollider;
            effectMaterial = material;
            ApplyShape();
        }

        public void Configure(float length, float arcDegrees)
        {
            Configure(length, arcDegrees, previewBandWidth);
        }

        public void Configure(float length, float arcDegrees, float bandWidth)
        {
            Configure(length, arcDegrees, bandWidth, 1f);
        }

        public void Configure(float length, float arcDegrees, float bandWidth, float revealFraction)
        {
            previewLength = Mathf.Max(0.01f, length);
            previewArcDegrees = Mathf.Clamp(arcDegrees, 1f, 170f);
            previewBandWidth = Mathf.Max(0.05f, bandWidth);
            previewRevealFraction = Mathf.Clamp01(revealFraction);
            ApplyShape();
        }

        void Awake()
        {
            shapeDirty = true;
        }

        void OnEnable()
        {
            shapeDirty = true;
        }

        void OnValidate()
        {
            angleSegments = Mathf.Clamp(angleSegments, 8, 64);
            previewLength = Mathf.Max(0.01f, previewLength);
            previewArcDegrees = Mathf.Clamp(previewArcDegrees, 1f, 170f);
            previewBandWidth = Mathf.Max(0.05f, previewBandWidth);
            previewRevealFraction = Mathf.Clamp01(previewRevealFraction);
            shapeDirty = true;
        }

        void Update()
        {
            if (shapeDirty) ApplyShape();
        }

        void OnDestroy()
        {
            DestroyGeneratedMesh();
        }

        void ApplyShape()
        {
            shapeDirty = false;
            if (sectorMeshFilter == null || sectorRenderer == null || sectorCollider == null) return;

            EnsureMesh();
            float fullInnerRadius = CalculateInnerRadius(previewLength, previewBandWidth);
            float visibleInnerRadius = CalculateVisibleInnerRadius(fullInnerRadius, previewLength, previewRevealFraction);
            BuildSectorMesh(visibleInnerRadius, previewLength, previewArcDegrees, previewRevealFraction);
            BuildSectorCollider(visibleInnerRadius, previewLength, previewArcDegrees);

            sectorRenderer.sharedMaterial = effectMaterial;
            sectorRenderer.sortingOrder = sortingOrder;
            sectorCollider.isTrigger = true;
        }

        void EnsureMesh()
        {
            if (generatedMesh != null) return;
            generatedMesh = new Mesh
            {
                name = "Excalibur Sector Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            sectorMeshFilter.sharedMesh = generatedMesh;
        }

        void BuildSectorMesh(float innerRadius, float outerRadius, float arcDegrees, float revealFraction)
        {
            int angularCount = angleSegments + 1;
            int vertexCount = angularCount * 2;
            var vertices = new Vector3[vertexCount];
            var uv = new Vector2[vertexCount];
            var triangles = new int[angleSegments * 6];

            float halfArc = arcDegrees * 0.5f;
            for (int angleIndex = 0; angleIndex <= angleSegments; angleIndex++)
            {
                float angle01 = angleIndex / (float)angleSegments;
                float radians = Mathf.Lerp(-halfArc, halfArc, angle01) * Mathf.Deg2Rad;
                Vector2 radial = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                vertices[angleIndex] = radial * innerRadius;
                vertices[angularCount + angleIndex] = radial * outerRadius;
                // The annular-sector Mesh exclusively defines the visual/collider/paint silhouette.
                // The full Material surface is mapped onto that Mesh, so texture alpha and brushwork
                // never determine the combat shape.
                // Reveal from the far (outer) edge back toward the player without
                // scaling the texture: V=1 stays on the outer blue edge throughout.
                uv[angleIndex] = new Vector2(angle01, 1f - Mathf.Clamp01(revealFraction));
                uv[angularCount + angleIndex] = new Vector2(angle01, 1f);
            }

            int triangleCursor = 0;
            for (int angleIndex = 0; angleIndex < angleSegments; angleIndex++)
            {
                int innerLeft = angleIndex;
                int innerRight = innerLeft + 1;
                int outerLeft = angularCount + angleIndex;
                int outerRight = outerLeft + 1;
                triangles[triangleCursor++] = innerLeft;
                triangles[triangleCursor++] = outerLeft;
                triangles[triangleCursor++] = outerRight;
                triangles[triangleCursor++] = innerLeft;
                triangles[triangleCursor++] = outerRight;
                triangles[triangleCursor++] = innerRight;
            }

            generatedMesh.Clear();
            generatedMesh.vertices = vertices;
            generatedMesh.uv = uv;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();
        }

        public static float CalculateInnerRadius(float outerRadius, float bandWidth)
        {
            return Mathf.Max(0f, Mathf.Max(0.01f, outerRadius) - Mathf.Max(0.05f, bandWidth));
        }

        public static float CalculateVisibleInnerRadius(float fullInnerRadius, float outerRadius, float revealFraction)
        {
            float clampedOuterRadius = Mathf.Max(0f, outerRadius);
            float clampedFullInnerRadius = Mathf.Clamp(fullInnerRadius, 0f, clampedOuterRadius);
            float minimumVisibleWidth = Mathf.Min(0.01f, clampedOuterRadius - clampedFullInnerRadius);
            return Mathf.Lerp(
                clampedOuterRadius - minimumVisibleWidth,
                clampedFullInnerRadius,
                Mathf.Clamp01(revealFraction));
        }

        void BuildSectorCollider(float innerRadius, float outerRadius, float arcDegrees)
        {
            float halfArc = arcDegrees * 0.5f;
            if (innerRadius <= 0.001f)
            {
                var fanPoints = new Vector2[angleSegments + 2];
                fanPoints[0] = Vector2.zero;
                for (int angleIndex = 0; angleIndex <= angleSegments; angleIndex++)
                {
                    float angle01 = angleIndex / (float)angleSegments;
                    float radians = Mathf.Lerp(-halfArc, halfArc, angle01) * Mathf.Deg2Rad;
                    fanPoints[angleIndex + 1] = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * outerRadius;
                }
                sectorCollider.pathCount = 1;
                sectorCollider.SetPath(0, fanPoints);
                return;
            }

            var points = new Vector2[(angleSegments + 1) * 2];
            for (int angleIndex = 0; angleIndex <= angleSegments; angleIndex++)
            {
                float angle01 = angleIndex / (float)angleSegments;
                float radians = Mathf.Lerp(-halfArc, halfArc, angle01) * Mathf.Deg2Rad;
                points[angleIndex] = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * outerRadius;
            }
            for (int angleIndex = 0; angleIndex <= angleSegments; angleIndex++)
            {
                float angle01 = angleIndex / (float)angleSegments;
                float radians = Mathf.Lerp(halfArc, -halfArc, angle01) * Mathf.Deg2Rad;
                points[angleSegments + 1 + angleIndex] = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * innerRadius;
            }
            sectorCollider.pathCount = 1;
            sectorCollider.SetPath(0, points);
        }

        void DestroyGeneratedMesh()
        {
            if (generatedMesh == null) return;
            if (Application.isPlaying) Destroy(generatedMesh);
            else DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }
    }
}
