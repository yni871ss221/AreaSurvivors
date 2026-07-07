using System;
using System.Collections.Generic;
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
        [SerializeField] Sprite shapeSpriteOverride;
        [SerializeField] bool useEllipseShape;
        [SerializeField, Range(16, 128)] int ellipseSegments = 64;
        [SerializeField, Range(0f, 0.2f)] float ellipseTextureCrop;
        [SerializeField] bool useSourceTexture = true;
        [SerializeField] Color tint = Color.white;
        [SerializeField] int sortingOrder;
        [SerializeField] bool anchorBottomCenter;
        [SerializeField, Range(0f, 1f)] float verticalFill = 1f;

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        Material material;
        static readonly Dictionary<MeshCacheKey, Mesh> MeshCache = new Dictionary<MeshCacheKey, Mesh>();
#if UNITY_EDITOR
        bool editorApplyQueued;
#endif

        readonly struct MeshCacheKey : IEquatable<MeshCacheKey>
        {
            readonly int spriteId;
            readonly int shapeSpriteId;
            readonly bool anchorBottomCenter;
            readonly int verticalFill;
            readonly bool ellipseShape;
            readonly int ellipseSegments;
            readonly int ellipseTextureCrop;

            public MeshCacheKey(
                Sprite sprite,
                Sprite shapeSprite,
                bool anchorBottomCenter,
                float verticalFill,
                bool ellipseShape,
                int ellipseSegments,
                float ellipseTextureCrop)
            {
                spriteId = sprite != null ? sprite.GetInstanceID() : 0;
                shapeSpriteId = shapeSprite != null ? shapeSprite.GetInstanceID() : 0;
                this.anchorBottomCenter = anchorBottomCenter;
                this.verticalFill = Quantize(verticalFill);
                this.ellipseShape = ellipseShape;
                this.ellipseSegments = ellipseSegments;
                this.ellipseTextureCrop = Quantize(ellipseTextureCrop);
            }

            public bool Equals(MeshCacheKey other)
            {
                return spriteId == other.spriteId &&
                    shapeSpriteId == other.shapeSpriteId &&
                    anchorBottomCenter == other.anchorBottomCenter &&
                    verticalFill == other.verticalFill &&
                    ellipseShape == other.ellipseShape &&
                    ellipseSegments == other.ellipseSegments &&
                    ellipseTextureCrop == other.ellipseTextureCrop;
            }

            public override bool Equals(object obj)
            {
                return obj is MeshCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = spriteId;
                    hash = (hash * 397) ^ shapeSpriteId;
                    hash = (hash * 397) ^ anchorBottomCenter.GetHashCode();
                    hash = (hash * 397) ^ verticalFill;
                    hash = (hash * 397) ^ ellipseShape.GetHashCode();
                    hash = (hash * 397) ^ ellipseSegments;
                    hash = (hash * 397) ^ ellipseTextureCrop;
                    return hash;
                }
            }

            static int Quantize(float value)
            {
                return Mathf.RoundToInt(value * 10000f);
            }
        }

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

        public bool useTexture
        {
            get => useSourceTexture;
            set
            {
                useSourceTexture = value;
                ApplySprite();
            }
        }

        public float VerticalFill => verticalFill;
        public bool UsesEllipseShape => useEllipseShape && shapeSpriteOverride != null;
        public float EllipseShapeAspectY
        {
            get
            {
                if (!UsesEllipseShape) return 1f;
                var size = shapeSpriteOverride.bounds.size;
                return size.x > 0.001f ? Mathf.Max(0.05f, size.y / size.x) : 1f;
            }
        }
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

        public void ConfigureEllipseShape(Sprite shapeSprite, float textureCrop = 0f, int segments = 64)
        {
            shapeSpriteOverride = shapeSprite;
            useEllipseShape = shapeSpriteOverride != null;
            ellipseTextureCrop = Mathf.Clamp(textureCrop, 0f, 0.2f);
            ellipseSegments = Mathf.Clamp(segments, 16, 128);
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
            DestroyGenerated(material);
        }

        void ApplySprite()
        {
            EnsureRenderer();
            if (sourceSprite == null) return;
            if (UsesEllipseShape)
            {
                meshFilter.sharedMesh = GetOrCreateEllipseMesh();
                ApplyMaterial();
                return;
            }

            meshFilter.sharedMesh = GetOrCreateSpriteMesh();
            ApplyMaterial();
        }

        Mesh GetOrCreateSpriteMesh()
        {
            var key = new MeshCacheKey(sourceSprite, null, anchorBottomCenter, verticalFill, false, 0, 0f);
            if (MeshCache.TryGetValue(key, out var cached) && cached != null) return cached;

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

            MeshCache[key] = mesh;
            return mesh;
        }

        Mesh GetOrCreateEllipseMesh()
        {
            int segments = Mathf.Clamp(ellipseSegments, 16, 128);
            var key = new MeshCacheKey(sourceSprite, shapeSpriteOverride, false, 1f, true, segments, ellipseTextureCrop);
            if (MeshCache.TryGetValue(key, out var cached) && cached != null) return cached;

            float radiusX = 1f;
            float radiusY = EllipseShapeAspectY;
            var vertices = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uv[0] = TextureUv(0.5f, 0.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[i + 1] = new Vector3(cos * radiusX, sin * radiusY, 0f);
                uv[i + 1] = TextureUv(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);

                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i == segments - 1 ? 1 : i + 2;
            }

            var mesh = new Mesh
            {
                name = shapeSpriteOverride.name + " Textured Ellipse",
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            MeshCache[key] = mesh;
            return mesh;
        }

        Vector2 TextureUv(float normalizedX, float normalizedY)
        {
            var texture = sourceSprite.texture;
            var rect = sourceSprite.textureRect;
            float x0 = rect.xMin / texture.width;
            float x1 = rect.xMax / texture.width;
            float y0 = rect.yMin / texture.height;
            float y1 = rect.yMax / texture.height;
            if (UsesEllipseShape && ellipseTextureCrop > 0f)
            {
                normalizedX = Mathf.Lerp(ellipseTextureCrop, 1f - ellipseTextureCrop, normalizedX);
                normalizedY = Mathf.Lerp(ellipseTextureCrop, 1f - ellipseTextureCrop, normalizedY);
            }
            return new Vector2(Mathf.Lerp(x0, x1, normalizedX), Mathf.Lerp(y0, y1, normalizedY));
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

            material.mainTexture = useSourceTexture && sourceSprite != null ? sourceSprite.texture : Texture2D.whiteTexture;
            material.color = tint;
        }

        static void DestroyGenerated(UnityEngine.Object generated)
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
