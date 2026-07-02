using UnityEngine;

namespace AreaSurvivors
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class EllipseOutlineMeshVisual : MonoBehaviour
    {
        [SerializeField, Range(16, 160)] int segments = 96;
        [SerializeField, Min(0.001f)] float outlineWidth = 0.02f;
        [SerializeField] Color color = new Color(0.48f, 0.82f, 0.66f, 0.45f);
        [SerializeField] int sortingOrder = 101;
        [SerializeField] bool visible;
        [SerializeField] Vector2 radius = Vector2.one;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Material material;
        Mesh mesh;
#if UNITY_EDITOR
        bool editorApplyQueued;
#endif

        public int order
        {
            get => sortingOrder;
            set
            {
                sortingOrder = value;
                EnsureRenderer();
                meshRenderer.sortingOrder = value;
            }
        }

        public void Configure(Vector2 worldRadius, bool show)
        {
            radius = new Vector2(Mathf.Max(0.05f, worldRadius.x), Mathf.Max(0.05f, worldRadius.y));
            visible = show;
            Apply();
        }

        public void SetVisible(bool show)
        {
            visible = show;
            ApplyRendererState();
        }

        void Awake()
        {
            Apply();
        }

        void OnEnable()
        {
            Apply();
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            QueueEditorApply();
#else
            Apply();
#endif
        }

        void OnDestroy()
        {
            DestroyGenerated(mesh);
            DestroyGenerated(material);
        }

        void Apply()
        {
            EnsureRenderer();
            ApplyMaterial();
            ApplyMesh();
            ApplyRendererState();
        }

        void EnsureRenderer()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = sortingOrder;
        }

        void ApplyMaterial()
        {
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Ellipse Outline Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            material.mainTexture = Texture2D.whiteTexture;
            material.color = color;
            meshRenderer.sharedMaterial = material;
        }

        void ApplyMesh()
        {
            int count = Mathf.Clamp(segments, 16, 160);
            float width = Mathf.Max(0.001f, outlineWidth);
            float outerX = Mathf.Max(0.05f, radius.x);
            float outerY = Mathf.Max(0.05f, radius.y);
            float innerX = Mathf.Max(0.001f, outerX - width);
            float innerY = Mathf.Max(0.001f, outerY - width);

            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = "Ellipse Outline Mesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            else
            {
                mesh.Clear();
            }

            var vertices = new Vector3[count * 2];
            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                int vertexIndex = i * 2;
                vertices[vertexIndex] = new Vector3(cos * outerX, sin * outerY, 0f);
                vertices[vertexIndex + 1] = new Vector3(cos * innerX, sin * innerY, 0f);

                int nextVertexIndex = ((i + 1) % count) * 2;
                int triangleIndex = i * 6;
                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = nextVertexIndex;
                triangles[triangleIndex + 2] = vertexIndex + 1;
                triangles[triangleIndex + 3] = vertexIndex + 1;
                triangles[triangleIndex + 4] = nextVertexIndex;
                triangles[triangleIndex + 5] = nextVertexIndex + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            meshFilter.sharedMesh = mesh;
        }

        void ApplyRendererState()
        {
            EnsureRenderer();
            meshRenderer.enabled = visible;
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
            Apply();
        }
#endif
    }
}
