using System;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class RuntimeSpriteOutline : MonoBehaviour
    {
        public Color outlineColor = Color.black;
        public float thickness = 0.035f;
        public bool compensateTransformScale;
        public bool requireExistingOutlineObject;
        public bool blink;
        public float blinkSpeed = 5f;

        MeshFilter sourceFilter;
        MeshRenderer sourceRenderer;
        MeshFilter outlineFilter;
        MeshRenderer outlineRenderer;
        Material outlineMaterial;
        Mesh lastSourceMesh;
        Material lastSourceMaterial;
        Texture lastTexture;
        Color lastColor;
        float lastEffectiveThickness;
        bool lastSourceEnabled;
        int lastSortingLayerId;
        int lastSortingOrder;
        bool syncInitialized;
        static readonly Dictionary<OutlineMeshCacheKey, OutlineMeshData> OutlineMeshCache = new Dictionary<OutlineMeshCacheKey, OutlineMeshData>();

        public MeshRenderer OutlineRenderer => outlineRenderer;

        sealed class OutlineMeshData
        {
            public Mesh mesh;
            public Vector4 spriteRect;
            public Vector4 outlineUv;
        }

        readonly struct OutlineMeshCacheKey : IEquatable<OutlineMeshCacheKey>
        {
            readonly int sourceMeshId;
            readonly int effectiveThickness;

            public OutlineMeshCacheKey(Mesh sourceMesh, float effectiveThickness)
            {
                sourceMeshId = sourceMesh != null ? sourceMesh.GetInstanceID() : 0;
                this.effectiveThickness = Mathf.RoundToInt(effectiveThickness * 10000f);
            }

            public bool Equals(OutlineMeshCacheKey other)
            {
                return sourceMeshId == other.sourceMeshId && effectiveThickness == other.effectiveThickness;
            }

            public override bool Equals(object obj)
            {
                return obj is OutlineMeshCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (sourceMeshId * 397) ^ effectiveThickness;
                }
            }
        }

        void Awake()
        {
            EnsureOutline();
        }

        void OnEnable()
        {
            EnsureOutline();
            syncInitialized = false;
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
                var shader = Shader.Find("AreaSurvivors/SpriteAlphaOutline");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                outlineMaterial = new Material(shader)
                {
                    name = "Runtime Sprite Outline",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            var child = transform.Find("Runtime Outline");
            if (child == null)
            {
                if (requireExistingOutlineObject)
                {
                    Debug.LogError($"{nameof(RuntimeSpriteOutline)} on {name} requires a prefab-authored Runtime Outline child.");
                    return;
                }

                child = new GameObject("Runtime Outline").transform;
                child.SetParent(transform, false);
            }

            var go = child.gameObject;
            outlineFilter = go.GetComponent<MeshFilter>();
            if (outlineFilter == null)
            {
                if (requireExistingOutlineObject)
                {
                    Debug.LogError($"{nameof(RuntimeSpriteOutline)} on {name} requires a prefab-authored MeshFilter on Runtime Outline.");
                    return;
                }

                outlineFilter = go.AddComponent<MeshFilter>();
            }

            outlineRenderer = go.GetComponent<MeshRenderer>();
            if (outlineRenderer == null)
            {
                if (requireExistingOutlineObject)
                {
                    Debug.LogError($"{nameof(RuntimeSpriteOutline)} on {name} requires a prefab-authored MeshRenderer on Runtime Outline.");
                    return;
                }

                outlineRenderer = go.AddComponent<MeshRenderer>();
            }

            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            var outlineTransform = outlineRenderer.transform;
            outlineTransform.localPosition = Vector3.zero;
            outlineTransform.localRotation = Quaternion.identity;
            outlineTransform.localScale = Vector3.one;
            outlineRenderer.sharedMaterial = outlineMaterial;
            RemoveLegacyOutlineCopies();
        }

        void SyncOutline()
        {
            if (sourceFilter == null || sourceRenderer == null || outlineRenderer == null)
            {
                EnsureOutline();
                if (sourceFilter == null || sourceRenderer == null || outlineRenderer == null) return;
            }

            var sourceMaterial = sourceRenderer.sharedMaterial;
            var sourceMesh = sourceFilter.sharedMesh;
            if (sourceMaterial == null || sourceMesh == null)
            {
                SetVisible(false);
                syncInitialized = false;
                return;
            }

            var color = outlineColor;
            color.a *= SourceAlpha(sourceMaterial);
            if (blink)
            {
                color.a *= Mathf.Lerp(0.35f, 1f, Mathf.PingPong(Time.time * Mathf.Max(0.1f, blinkSpeed), 1f));
            }

            float effectiveThickness = EffectiveThickness;
            Texture texture = sourceMaterial.mainTexture;
            bool visible = sourceRenderer.enabled && color.a > 0.001f && effectiveThickness > 0.001f;
            int desiredSortingOrder = sourceRenderer.sortingOrder - 1;
            bool changed =
                !syncInitialized ||
                blink ||
                lastSourceMesh != sourceMesh ||
                lastSourceMaterial != sourceMaterial ||
                lastTexture != texture ||
                lastColor != color ||
                !Mathf.Approximately(lastEffectiveThickness, effectiveThickness) ||
                lastSourceEnabled != sourceRenderer.enabled ||
                lastSortingLayerId != sourceRenderer.sortingLayerID ||
                lastSortingOrder != sourceRenderer.sortingOrder ||
                outlineRenderer.sharedMaterial != outlineMaterial ||
                outlineRenderer.sortingLayerID != sourceRenderer.sortingLayerID ||
                outlineRenderer.sortingOrder != desiredSortingOrder ||
                outlineRenderer.enabled != visible;
            if (!changed) return;

            var outlineData = EnsureOutlineMesh(sourceMesh, effectiveThickness);
            outlineMaterial.mainTexture = texture;
            outlineMaterial.color = color;
            if (outlineData != null)
            {
                outlineMaterial.SetVector("_SpriteRect", outlineData.spriteRect);
                outlineMaterial.SetVector("_OutlineUv", outlineData.outlineUv);
            }
            outlineMaterial.SetFloat("_AlphaThreshold", 0.05f);

            if (outlineRenderer.sharedMaterial != outlineMaterial)
            {
                outlineRenderer.sharedMaterial = outlineMaterial;
            }
            outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            outlineRenderer.sortingOrder = desiredSortingOrder;
            outlineRenderer.enabled = visible;

            lastSourceMesh = sourceMesh;
            lastSourceMaterial = sourceMaterial;
            lastTexture = texture;
            lastColor = color;
            lastEffectiveThickness = effectiveThickness;
            lastSourceEnabled = sourceRenderer.enabled;
            lastSortingLayerId = sourceRenderer.sortingLayerID;
            lastSortingOrder = sourceRenderer.sortingOrder;
            syncInitialized = true;
        }

        void SetVisible(bool visible)
        {
            if (outlineRenderer != null) outlineRenderer.enabled = visible;
        }

        OutlineMeshData EnsureOutlineMesh(Mesh sourceMesh, float effectiveThickness)
        {
            if (sourceMesh == null) return null;
            var key = new OutlineMeshCacheKey(sourceMesh, effectiveThickness);
            if (OutlineMeshCache.TryGetValue(key, out var cached) && cached != null)
            {
                if (outlineFilter.sharedMesh != cached.mesh) outlineFilter.sharedMesh = cached.mesh;
                return cached;
            }

            var vertices = sourceMesh.vertices;
            var uvs = sourceMesh.uv;
            if (vertices == null || vertices.Length < 4 || uvs == null || uvs.Length < 4)
            {
                outlineFilter.sharedMesh = sourceMesh;
                return null;
            }

            var min = vertices[0];
            var max = vertices[0];
            var uvMin = uvs[0];
            var uvMax = uvs[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
                uvMin = Vector2.Min(uvMin, uvs[i]);
                uvMax = Vector2.Max(uvMax, uvs[i]);
            }

            float width = Mathf.Max(0.001f, max.x - min.x);
            float height = Mathf.Max(0.001f, max.y - min.y);
            float uvPadX = (uvMax.x - uvMin.x) * effectiveThickness / width;
            float uvPadY = (uvMax.y - uvMin.y) * effectiveThickness / height;
            var data = new OutlineMeshData
            {
                spriteRect = new Vector4(uvMin.x, uvMin.y, uvMax.x, uvMax.y),
                outlineUv = new Vector4(uvPadX, uvPadY, 0f, 0f)
            };

            data.mesh = new Mesh
            {
                name = sourceMesh.name + " Outline",
                hideFlags = HideFlags.HideAndDontSave
            };
            data.mesh.vertices = new[]
            {
                new Vector3(min.x - effectiveThickness, min.y - effectiveThickness, 0f),
                new Vector3(max.x + effectiveThickness, min.y - effectiveThickness, 0f),
                new Vector3(min.x - effectiveThickness, max.y + effectiveThickness, 0f),
                new Vector3(max.x + effectiveThickness, max.y + effectiveThickness, 0f)
            };
            data.mesh.uv = new[]
            {
                new Vector2(uvMin.x - uvPadX, uvMin.y - uvPadY),
                new Vector2(uvMax.x + uvPadX, uvMin.y - uvPadY),
                new Vector2(uvMin.x - uvPadX, uvMax.y + uvPadY),
                new Vector2(uvMax.x + uvPadX, uvMax.y + uvPadY)
            };
            data.mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            data.mesh.RecalculateBounds();
            OutlineMeshCache[key] = data;
            outlineFilter.sharedMesh = data.mesh;
            return data;
        }

        void RemoveLegacyOutlineCopies()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child == null || !child.name.StartsWith("Runtime Outline ")) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

        static float SourceAlpha(Material material)
        {
            return material.HasProperty("_Color") ? material.color.a : 1f;
        }

        float EffectiveThickness
        {
            get
            {
                if (!compensateTransformScale) return thickness;
                var scale = transform.lossyScale;
                float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z), 0.001f);
                return thickness / maxScale;
            }
        }
    }
}
