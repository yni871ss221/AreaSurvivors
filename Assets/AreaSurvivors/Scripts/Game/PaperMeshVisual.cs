using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AreaSurvivors
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    [ExecuteAlways]
    public sealed class PaperMeshVisual : MonoBehaviour
    {
        [SerializeField] Sprite sourceSprite;
        [SerializeField] Color tint = Color.white;
        [SerializeField] int sortingOrder;
        [SerializeField] bool anchorBottomCenter;
        [SerializeField, Range(0f, 1f)] float verticalFill = 1f;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Material material;
#if UNITY_EDITOR
        bool editorApplyQueued;
#endif

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

        public bool useBottomCenterAnchor
        {
            get => anchorBottomCenter;
            set
            {
                if (anchorBottomCenter == value) return;
                anchorBottomCenter = value;
                ApplySprite();
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

        public float VerticalFill => verticalFill;

        public void SetVerticalFill(float fill)
        {
            fill = Mathf.Clamp01(fill);
            if (Mathf.Approximately(verticalFill, fill)) return;
            verticalFill = fill;
            ApplySprite();
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
            EnsureRenderer();
        }

        void OnEnable()
        {
            ApplySprite();
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            QueueEditorApplySprite();
#else
            ApplySprite();
#endif
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
            if (anchorBottomCenter)
            {
                float halfWidth = bounds.size.x * 0.5f;
                min = new Vector3(-halfWidth, 0f, 0f);
                max = new Vector3(halfWidth, bounds.size.y, 0f);
            }
            var texture = sourceSprite.texture;
            var rect = sourceSprite.textureRect;
            float x0 = rect.xMin / texture.width;
            float x1 = rect.xMax / texture.width;
            float y0 = rect.yMin / texture.height;
            float y1 = rect.yMax / texture.height;
            float fill = Mathf.Clamp01(verticalFill);
            float filledMaxY = Mathf.Lerp(min.y, max.y, fill);
            float filledY1 = Mathf.Lerp(y0, y1, fill);

            var mesh = new Mesh
            {
                name = sourceSprite.name + " Quad",
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.vertices = new[]
            {
                new Vector3(min.x, min.y, 0f),
                new Vector3(max.x, min.y, 0f),
                new Vector3(min.x, filledMaxY, 0f),
                new Vector3(max.x, filledMaxY, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(x0, y0),
                new Vector2(x1, y0),
                new Vector2(x0, filledY1),
                new Vector2(x1, filledY1)
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

#if UNITY_EDITOR
        void QueueEditorApplySprite()
        {
            if (editorApplyQueued) return;
            editorApplyQueued = true;
            EditorApplication.delayCall += ApplySpriteFromEditorDelay;
        }

        void ApplySpriteFromEditorDelay()
        {
            editorApplyQueued = false;
            if (this == null) return;
            ApplySprite();
        }
#endif
    }
}
