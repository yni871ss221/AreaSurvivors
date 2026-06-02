using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PaperMeshVisual : MonoBehaviour
    {
        [SerializeField] Sprite sourceSprite;
        [SerializeField] Color tint = Color.white;
        [SerializeField] int sortingOrder;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Material material;

        public Sprite sprite
        {
            get => sourceSprite;
            set
            {
                if (sourceSprite == value && meshFilter != null && meshFilter.sharedMesh != null) return;
                sourceSprite = value;
                ApplySprite();
            }
        }

        public Color color
        {
            get => tint;
            set
            {
                tint = value;
                ApplyMaterial();
            }
        }

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

        public Renderer Renderer
        {
            get
            {
                EnsureRenderer();
                return meshRenderer;
            }
        }

        public bool visible
        {
            get
            {
                EnsureRenderer();
                return meshRenderer.enabled;
            }
            set
            {
                EnsureRenderer();
                meshRenderer.enabled = value;
            }
        }

        public void Configure(Sprite newSprite, Color newTint, int newSortingOrder)
        {
            sourceSprite = newSprite;
            tint = newTint;
            sortingOrder = newSortingOrder;
            ApplySprite();
        }

        void Awake()
        {
            ApplySprite();
        }

        void OnDestroy()
        {
            if (meshFilter != null) DestroyGenerated(meshFilter.sharedMesh);
            DestroyGenerated(material);
        }

        void ApplySprite()
        {
            EnsureRenderer();
            if (sourceSprite == null) return;

            var bounds = sourceSprite.bounds;
            var min = bounds.min;
            var max = bounds.max;
            var texture = sourceSprite.texture;
            var rect = sourceSprite.textureRect;
            float x0 = rect.xMin / texture.width;
            float x1 = rect.xMax / texture.width;
            float y0 = rect.yMin / texture.height;
            float y1 = rect.yMax / texture.height;

            var mesh = new Mesh
            {
                name = sourceSprite.name + " Quad",
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.vertices = new[]
            {
                new Vector3(min.x, min.y, 0f),
                new Vector3(max.x, min.y, 0f),
                new Vector3(min.x, max.y, 0f),
                new Vector3(max.x, max.y, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(x0, y0),
                new Vector2(x1, y0),
                new Vector2(x0, y1),
                new Vector2(x1, y1)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();

            DestroyGenerated(meshFilter.sharedMesh);
            meshFilter.sharedMesh = mesh;
            ApplyMaterial();
        }

        void EnsureRenderer()
        {
            if (meshFilter == null) meshFilter = GetComponent<MeshFilter>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sortingOrder = sortingOrder;
        }

        void ApplyMaterial()
        {
            EnsureRenderer();
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Paper Texture Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
                meshRenderer.sharedMaterial = material;
            }

            material.mainTexture = sourceSprite != null ? sourceSprite.texture : null;
            material.color = tint;
        }

        static void DestroyGenerated(Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }
    }
}
