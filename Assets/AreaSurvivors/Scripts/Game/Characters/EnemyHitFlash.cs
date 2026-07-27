using System;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    [DefaultExecutionOrder(2000)]
    public sealed class EnemyHitFlash : MonoBehaviour
    {
        public const float FlashSeconds = 0.22f;
        static readonly Color FlashColor = new Color(1f, 1f, 1f, 1f);

        PaperMeshVisual sourceVisual;
        MeshFilter sourceFilter;
        MeshRenderer sourceRenderer;
        RuntimeSpriteOutline sourceOutline;
        [SerializeField] MeshFilter overlayFilter;
        [SerializeField] MeshRenderer overlayRenderer;
        [SerializeField] Material sharedOverlayMaterial;
        MaterialPropertyBlock propertyBlock;
        float remainingSeconds;
        int lastPlayFrame = -1;
        bool countedActive;
        static int activeFlashCount;
        static readonly Dictionary<FlashMeshCacheKey, FlashMeshData> FlashMeshCache = new Dictionary<FlashMeshCacheKey, FlashMeshData>();
        static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int SpriteRectId = Shader.PropertyToID("_SpriteRect");
        static readonly int OutlineUvId = Shader.PropertyToID("_OutlineUv");
        static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");

        public static int ActiveFlashCount => activeFlashCount;
        public bool HasPrefabReferences =>
            overlayFilter != null &&
            overlayRenderer != null &&
            sharedOverlayMaterial != null;
        public Material SharedOverlayMaterial => sharedOverlayMaterial;

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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeState()
        {
            activeFlashCount = 0;
            FlashMeshCache.Clear();
        }

        void Awake()
        {
            ResolveReferences(null);
            if (overlayRenderer != null) overlayRenderer.enabled = false;
            enabled = false;
        }

        public void ConfigurePrefabReferences(
            PaperMeshVisual visual,
            MeshFilter flashFilter,
            MeshRenderer flashRenderer,
            Material flashMaterial)
        {
            sourceVisual = visual;
            overlayFilter = flashFilter;
            overlayRenderer = flashRenderer;
            sharedOverlayMaterial = flashMaterial;
            ResolveReferences(visual);
            if (overlayRenderer != null) overlayRenderer.enabled = false;
        }

        public void Play(PaperMeshVisual sourceVisual)
        {
            CombatPerformanceDiagnostics.RecordHitFlashPlayRequest();
            if (CombatPerformanceDiagnostics.SuppressHitFlash) return;
            if (sourceVisual == null || !sourceVisual.visible) return;
            ResolveReferences(sourceVisual);
            if (sourceFilter == null || sourceRenderer == null || sourceFilter.sharedMesh == null) return;
            if (overlayFilter == null || overlayRenderer == null || sharedOverlayMaterial == null) return;

            remainingSeconds = FlashSeconds;
            if (!countedActive)
            {
                countedActive = true;
                activeFlashCount++;
            }
            enabled = true;
            overlayRenderer.enabled = true;
            if (lastPlayFrame == Time.frameCount)
            {
                CombatPerformanceDiagnostics.RecordHitFlashCoalescedRequest();
                return;
            }
            lastPlayFrame = Time.frameCount;
            SyncOverlay(1f);
        }

        void ResolveReferences(PaperMeshVisual requestedVisual)
        {
            if (requestedVisual != null && sourceVisual != requestedVisual)
            {
                sourceVisual = requestedVisual;
                sourceFilter = null;
                sourceRenderer = null;
                sourceOutline = null;
            }
            if (sourceVisual == null) sourceVisual = GetComponentInChildren<PaperMeshVisual>();
            if (sourceVisual != null)
            {
                if (sourceFilter == null) sourceFilter = sourceVisual.GetComponent<MeshFilter>();
                if (sourceRenderer == null) sourceRenderer = sourceVisual.GetComponent<MeshRenderer>();
                if (sourceOutline == null) sourceOutline = sourceVisual.GetComponent<RuntimeSpriteOutline>();
                if (overlayRenderer == null)
                {
                    var child = sourceVisual.transform.Find("Enemy Hit Flash");
                    if (child != null)
                    {
                        overlayFilter = child.GetComponent<MeshFilter>();
                        overlayRenderer = child.GetComponent<MeshRenderer>();
                    }
                }
            }
            if (sharedOverlayMaterial == null && overlayRenderer != null)
                sharedOverlayMaterial = overlayRenderer.sharedMaterial;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            if (overlayRenderer != null &&
                sharedOverlayMaterial != null &&
                overlayRenderer.sharedMaterial != sharedOverlayMaterial)
            {
                overlayRenderer.sharedMaterial = sharedOverlayMaterial;
            }
        }

        void LateUpdate()
        {
            if (remainingSeconds <= 0f)
            {
                enabled = false;
                return;
            }

            remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
            float alphaScale = Mathf.Clamp01(remainingSeconds / FlashSeconds);
            SyncOverlay(alphaScale);
            if (remainingSeconds <= 0f) enabled = false;
        }

        void OnDisable()
        {
            if (overlayRenderer != null) overlayRenderer.enabled = false;
            if (!countedActive) return;
            countedActive = false;
            activeFlashCount = Mathf.Max(0, activeFlashCount - 1);
        }

        void SyncOverlay(float alphaScale)
        {
            if (sourceVisual == null ||
                sourceFilter == null ||
                sourceRenderer == null ||
                overlayRenderer == null ||
                sharedOverlayMaterial == null ||
                propertyBlock == null)
            {
                return;
            }
            if (!sourceVisual.visible || sourceFilter.sharedMesh == null)
            {
                overlayRenderer.enabled = false;
                return;
            }

            var flashData = EnsureFlashMesh(sourceFilter.sharedMesh);
            overlayFilter.sharedMesh = flashData != null && flashData.mesh != null ? flashData.mesh : sourceFilter.sharedMesh;
            var color = FlashColor;
            color.a *= alphaScale;
            propertyBlock.Clear();
            var texture = sourceRenderer.sharedMaterial != null
                ? sourceRenderer.sharedMaterial.mainTexture
                : null;
            if (texture != null) propertyBlock.SetTexture(MainTextureId, texture);
            propertyBlock.SetColor(ColorId, color);
            ApplySpriteRectProperties(flashData, propertyBlock);
            overlayRenderer.SetPropertyBlock(propertyBlock);
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

        static void ApplySpriteRectProperties(
            FlashMeshData data,
            MaterialPropertyBlock properties)
        {
            if (data == null || properties == null) return;
            properties.SetVector(SpriteRectId, data.spriteRect);
            properties.SetVector(OutlineUvId, data.outlineUv);
            properties.SetFloat(AlphaThresholdId, 0.05f);
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
            if (!countedActive) return;
            countedActive = false;
            activeFlashCount = Mathf.Max(0, activeFlashCount - 1);
        }
    }
}
