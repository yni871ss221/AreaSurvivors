using System;
using System.Collections.Generic;
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
        RuntimeSpriteOutline sourceOutline;
        MeshFilter overlayFilter;
        MeshRenderer overlayRenderer;
        Material overlayMaterial;
        float remainingSeconds;
        static readonly Dictionary<FlashMeshCacheKey, FlashMeshData> FlashMeshCache = new Dictionary<FlashMeshCacheKey, FlashMeshData>();

        sealed class FlashMeshData
        {
            public Mesh mesh;
            public Vector4 spriteRect;
            public Vector4 outlineUv;
        }

        readonly struct FlashMeshCacheKey : IEquatable<FlashMeshCacheKey>
        {
            readonly int sourceMeshId;
            readonly int effectiveThickness;

            public FlashMeshCacheKey(Mesh sourceMesh, float effectiveThickness)
            {
                sourceMeshId = sourceMesh != null ? sourceMesh.GetInstanceID() : 0;
                this.effectiveThickness = Mathf.RoundToInt(effectiveThickness * 10000f);
            }

            public bool Equals(FlashMeshCacheKey other)
            {
                return sourceMeshId == other.sourceMeshId && effectiveThickness == other.effectiveThickness;
            }

            public override bool Equals(object obj)
            {
                return obj is FlashMeshCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (sourceMeshId * 397) ^ effectiveThickness;
                }
            }
        }

        public void Play(PaperMeshVisual sourceVisual)
        {
            if (sourceVisual == null || !sourceVisual.visible) return;
            this.sourceVisual = sourceVisual;
            sourceFilter = sourceVisual.GetComponent<MeshFilter>();
            sourceRenderer = sourceVisual.GetComponent<MeshRenderer>();
            sourceOutline = sourceVisual.GetComponent<RuntimeSpriteOutline>();
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
            var shader = Shader.Find("AreaSurvivors/SpriteAlphaOutline");
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

            var flashData = EnsureFlashMesh(sourceFilter.sharedMesh);
            overlayFilter.sharedMesh = flashData != null && flashData.mesh != null ? flashData.mesh : sourceFilter.sharedMesh;
            overlayMaterial.mainTexture = sourceRenderer.sharedMaterial != null ? sourceRenderer.sharedMaterial.mainTexture : null;
            var color = FlashColor;
            color.a *= alphaScale;
            overlayMaterial.color = color;
            ApplySpriteRectProperties(flashData);
            overlayRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            overlayRenderer.sortingOrder = sourceRenderer.sortingOrder + 80;
            overlayRenderer.enabled = alphaScale > 0.001f;
        }

        FlashMeshData EnsureFlashMesh(Mesh sourceMesh)
        {
            if (sourceMesh == null) return null;
            float effectiveThickness = EffectiveFlashThickness();
            var key = new FlashMeshCacheKey(sourceMesh, effectiveThickness);
            if (FlashMeshCache.TryGetValue(key, out var cached) && cached != null) return cached;

            var vertices = sourceMesh.vertices;
            var uvs = sourceMesh.uv;
            if (vertices == null || vertices.Length < 4 || uvs == null || uvs.Length < 4) return null;

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
            var data = new FlashMeshData
            {
                spriteRect = new Vector4(uvMin.x, uvMin.y, uvMax.x, uvMax.y),
                outlineUv = new Vector4(uvPadX, uvPadY, 0f, 0f)
            };

            data.mesh = new Mesh
            {
                name = sourceMesh.name + " Hit Flash",
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
            FlashMeshCache[key] = data;
            return data;
        }

        void ApplySpriteRectProperties(FlashMeshData data)
        {
            if (data == null || overlayMaterial == null) return;
            overlayMaterial.SetVector("_SpriteRect", data.spriteRect);
            overlayMaterial.SetVector("_OutlineUv", data.outlineUv);
            overlayMaterial.SetFloat("_AlphaThreshold", 0.05f);
        }

        float EffectiveFlashThickness()
        {
            float thickness = sourceOutline != null ? sourceOutline.thickness : 0.035f;
            if (sourceOutline == null || !sourceOutline.compensateTransformScale) return thickness;

            var scale = sourceOutline.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z), 0.001f);
            return thickness / maxScale;
        }

        void OnDestroy()
        {
            if (overlayMaterial == null) return;
            if (Application.isPlaying) Destroy(overlayMaterial);
            else DestroyImmediate(overlayMaterial);
        }
    }
}
