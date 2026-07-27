using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreaSurvivors
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(2000)]
    public sealed class CharacterOcclusionReveal : MonoBehaviour
    {
        public Color silhouetteColor = new Color(0.3f, 0.95f, 1f, 0.72f);
        public Color outlineColor = Color.white;
        [Min(0.02f)] public float checkInterval = 0.08f;
        [Min(0.08f)] public float normalEnemyCheckInterval = 0.5f;

        static Renderer[] cachedOccluders;
        static float nextOccluderRefresh;
        static readonly Dictionary<Texture, Material> StencilMaterials = new Dictionary<Texture, Material>();
        const int NormalEnemyChecksPerFrame = 24;
        const float NormalEnemyRetrySeconds = 0.03f;
        const float AttachedNormalEnemyCheckInterval = 0.1f;
        const int ResourceValidationFrameInterval = 60;
        static int normalEnemyBudgetFrame = -1;
        static int normalEnemyChecksThisFrame;

        PaperMeshVisual source;
        MeshRenderer sourceRenderer;
        MeshFilter sourceFilter;
        RuntimeSpriteOutline sourceOutline;
        Shader stencilShader;
        Material silhouetteMaterial;
        CommandBuffer commandBuffer;
        Camera renderCamera;
        CharacterFootprint footprint;
        EnemyController enemy;
        readonly List<Renderer> activeOccluders = new List<Renderer>();
        bool commandBufferAttached;
        float timer;
        int nextResourceValidationFrame;
        Mesh lastSilhouetteSourceMesh;
        Texture lastSilhouetteTexture;
        Color lastSilhouetteColor;
        Color lastSilhouetteOutlineColor;
        float lastSilhouetteThickness;
        bool silhouetteMaterialInitialized;
        Matrix4x4 lastCommandSourceMatrix;
        bool commandSourceMatrixInitialized;
        static readonly Dictionary<SilhouetteMeshCacheKey, SilhouetteMeshData> SilhouetteMeshCache = new Dictionary<SilhouetteMeshCacheKey, SilhouetteMeshData>();

        sealed class SilhouetteMeshData
        {
            public Mesh mesh;
            public Vector4 spriteRect;
            public Vector4 outlineUv;
        }

        readonly struct SilhouetteMeshCacheKey : IEquatable<SilhouetteMeshCacheKey>
        {
            readonly int sourceMeshId;
            readonly int effectiveThickness;

            public SilhouetteMeshCacheKey(Mesh sourceMesh, float effectiveThickness)
            {
                sourceMeshId = sourceMesh != null ? sourceMesh.GetInstanceID() : 0;
                this.effectiveThickness = Mathf.RoundToInt(effectiveThickness * 10000f);
            }

            public bool Equals(SilhouetteMeshCacheKey other)
            {
                return sourceMeshId == other.sourceMeshId && effectiveThickness == other.effectiveThickness;
            }

            public override bool Equals(object obj)
            {
                return obj is SilhouetteMeshCacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (sourceMeshId * 397) ^ effectiveThickness;
                }
            }
        }

        void OnEnable()
        {
            EnsureResources();
            timer = InitialTimerOffset();
            nextResourceValidationFrame = Time.frameCount + ResourceValidationFrameInterval;
            silhouetteMaterialInitialized = false;
        }

        void LateUpdate()
        {
            if (sourceRenderer == null || commandBuffer == null || renderCamera == null ||
                Time.frameCount >= nextResourceValidationFrame)
            {
                EnsureResources();
                nextResourceValidationFrame = Time.frameCount + ResourceValidationFrameInterval;
            }
            if (sourceRenderer == null || commandBuffer == null) return;

            bool silhouetteGeometryChanged = commandBufferAttached && SyncSilhouetteMaterialIfNeeded();
            bool sourceTransformChanged = commandBufferAttached && SourceTransformChanged();
            timer -= Time.unscaledDeltaTime;
            bool rebuiltCommands = false;
            if (timer <= 0f)
            {
                if (CanRunThisFrame())
                {
                    timer = EffectiveRefreshInterval();
                    RefreshActiveOccluders();
                    RebuildCommands();
                    rebuiltCommands = true;
                }
                else
                {
                    timer = NormalEnemyRetrySeconds;
                }
            }
            if ((silhouetteGeometryChanged || sourceTransformChanged) && !rebuiltCommands) RebuildCommands();
        }

        void EnsureResources()
        {
            if (source == null)
            {
                foreach (var candidate in GetComponentsInChildren<PaperMeshVisual>(true))
                {
                    if (candidate.gameObject.name == "Occlusion Silhouette") continue;
                    source = candidate;
                    break;
                }
            }
            if (source == null) return;
            if (sourceRenderer == null) sourceRenderer = source.GetComponent<MeshRenderer>();
            if (sourceFilter == null) sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceOutline == null) sourceOutline = source.GetComponent<RuntimeSpriteOutline>();
            if (footprint == null) footprint = GetComponent<CharacterFootprint>();
            if (enemy == null) enemy = GetComponent<EnemyController>();

            var camera = Camera.main;
            if (renderCamera != camera)
            {
                DetachCommandBuffer();
                renderCamera = camera;
            }

            if (stencilShader == null) stencilShader = Shader.Find("AreaSurvivors/OcclusionStencilMask");
            if (silhouetteMaterial == null)
            {
                silhouetteMaterial = CreateMaterial("AreaSurvivors/CharacterSilhouette", "Character Occlusion Silhouette");
            }
            if (commandBuffer == null && renderCamera != null && stencilShader != null && silhouetteMaterial != null)
            {
                commandBuffer = new CommandBuffer { name = $"Character Occlusion: {name}" };
            }
        }

        bool CanRunThisFrame()
        {
            if (!IsNormalEnemy()) return true;

            int frame = Time.frameCount;
            if (normalEnemyBudgetFrame != frame)
            {
                normalEnemyBudgetFrame = frame;
                normalEnemyChecksThisFrame = 0;
            }

            if (normalEnemyChecksThisFrame >= NormalEnemyChecksPerFrame) return false;
            normalEnemyChecksThisFrame++;
            return true;
        }

        float EffectiveCheckInterval()
        {
            return IsNormalEnemy()
                ? Mathf.Max(checkInterval, normalEnemyCheckInterval)
                : checkInterval;
        }

        float EffectiveRefreshInterval()
        {
            float interval = EffectiveCheckInterval();
            return commandBufferAttached && IsNormalEnemy()
                ? Mathf.Min(interval, AttachedNormalEnemyCheckInterval)
                : interval;
        }

        float InitialTimerOffset()
        {
            float interval = EffectiveCheckInterval();
            int hash = Mathf.Abs(GetInstanceID());
            return (hash % 997) / 997f * interval;
        }

        bool IsNormalEnemy()
        {
            if (enemy == null) enemy = GetComponent<EnemyController>();
            return enemy != null && !enemy.boss && !enemy.elite;
        }

        void RefreshActiveOccluders()
        {
            activeOccluders.Clear();
            if (source == null || sourceRenderer == null || !sourceRenderer.enabled) return;

            Rect sourceScreenRect = ScreenRect(sourceRenderer.bounds);
            int sourceOrder = sourceRenderer.sortingOrder;
            foreach (var renderer in GetOccluders())
            {
                if (!IsFrontOverlappingOccluder(renderer, sourceScreenRect, sourceOrder)) continue;
                activeOccluders.Add(renderer);
            }
        }

        void RebuildCommands()
        {
            commandBuffer.Clear();
            if (source == null || sourceRenderer == null || !sourceRenderer.enabled)
            {
                SetCommandBufferAttached(false);
                return;
            }

            bool hasOccluder = false;
            for (int i = activeOccluders.Count - 1; i >= 0; i--)
            {
                var renderer = activeOccluders[i];
                if (renderer == null || !renderer.enabled) continue;
                Texture texture = renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;
                if (texture == null) continue;
                Material maskMaterial = GetStencilMaterial(texture);
                if (maskMaterial == null) continue;

                if (!hasOccluder)
                {
                    // Runs after the scene has rendered, so clearing depth/stencil cannot affect normal visuals.
                    commandBuffer.ClearRenderTarget(true, false, Color.clear, 1f);
                    hasOccluder = true;
                }
                commandBuffer.DrawRenderer(renderer, maskMaterial);
            }

            SetCommandBufferAttached(hasOccluder);
            if (!hasOccluder) return;
            SyncSilhouetteMaterialIfNeeded();
            var silhouetteData = EnsureSilhouetteMesh(sourceFilter != null ? sourceFilter.sharedMesh : null);
            if (silhouetteData != null && silhouetteData.mesh != null)
            {
                commandBuffer.DrawMesh(silhouetteData.mesh, source.transform.localToWorldMatrix, silhouetteMaterial);
            }
            else
            {
                commandBuffer.DrawRenderer(sourceRenderer, silhouetteMaterial);
            }
            lastCommandSourceMatrix = source.transform.localToWorldMatrix;
            commandSourceMatrixInitialized = true;
        }

        bool SourceTransformChanged()
        {
            if (source == null) return false;
            Matrix4x4 current = source.transform.localToWorldMatrix;
            return !commandSourceMatrixInitialized || current != lastCommandSourceMatrix;
        }

        bool SyncSilhouetteMaterialIfNeeded()
        {
            if (silhouetteMaterial == null || source == null || sourceFilter == null) return false;

            Mesh sourceMesh = sourceFilter.sharedMesh;
            Texture texture = source.sprite != null ? source.sprite.texture : null;
            float thickness = EffectiveSilhouetteThickness();
            bool geometryChanged =
                !silhouetteMaterialInitialized ||
                lastSilhouetteSourceMesh != sourceMesh ||
                !Mathf.Approximately(lastSilhouetteThickness, thickness);
            bool materialChanged =
                geometryChanged ||
                lastSilhouetteTexture != texture ||
                lastSilhouetteColor != silhouetteColor ||
                lastSilhouetteOutlineColor != outlineColor;
            if (!materialChanged) return false;

            silhouetteMaterial.mainTexture = texture;
            silhouetteMaterial.SetColor("_Color", silhouetteColor);
            silhouetteMaterial.SetColor("_OutlineColor", outlineColor);
            ApplySilhouetteProperties(EnsureSilhouetteMesh(sourceMesh));

            lastSilhouetteSourceMesh = sourceMesh;
            lastSilhouetteTexture = texture;
            lastSilhouetteColor = silhouetteColor;
            lastSilhouetteOutlineColor = outlineColor;
            lastSilhouetteThickness = thickness;
            silhouetteMaterialInitialized = true;
            return geometryChanged;
        }

        SilhouetteMeshData EnsureSilhouetteMesh(Mesh sourceMesh)
        {
            if (sourceMesh == null) return null;
            float effectiveThickness = EffectiveSilhouetteThickness();
            var key = new SilhouetteMeshCacheKey(sourceMesh, effectiveThickness);
            if (SilhouetteMeshCache.TryGetValue(key, out var cached) && cached != null) return cached;

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
            var data = new SilhouetteMeshData
            {
                spriteRect = new Vector4(uvMin.x, uvMin.y, uvMax.x, uvMax.y),
                outlineUv = new Vector4(uvPadX, uvPadY, 0f, 0f)
            };

            data.mesh = new Mesh
            {
                name = sourceMesh.name + " Occlusion Silhouette",
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
            SilhouetteMeshCache[key] = data;
            return data;
        }

        void ApplySilhouetteProperties(SilhouetteMeshData data)
        {
            if (data == null || silhouetteMaterial == null) return;
            silhouetteMaterial.SetVector("_SpriteRect", data.spriteRect);
            silhouetteMaterial.SetVector("_OutlineUv", data.outlineUv);
            silhouetteMaterial.SetFloat("_AlphaThreshold", 0.05f);
        }

        float EffectiveSilhouetteThickness()
        {
            float thickness = sourceOutline != null ? sourceOutline.thickness : 0.035f;
            if (sourceOutline == null || !sourceOutline.compensateTransformScale) return thickness;

            var scale = sourceOutline.transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z), 0.001f);
            return thickness / maxScale;
        }

        bool IsFrontOverlappingOccluder(Renderer renderer, Rect sourceScreenRect, int sourceOrder)
        {
            if (renderer == null || renderer == sourceRenderer ||
                renderer.transform.IsChildOf(transform) || !renderer.enabled) return false;

            if (!IsOccluderInFrontOfCharacter(renderer, CharacterFrontY(), sourceOrder)) return false;

            return sourceScreenRect.Overlaps(ScreenRect(renderer.bounds));
        }

        float CharacterFrontY()
        {
            if (footprint != null) return footprint.FrontY;
            var collider = GetComponent<Collider2D>();
            if (collider != null && collider.enabled) return collider.bounds.min.y;
            return transform.position.y;
        }

        public static bool IsOccluderInFrontOfCharacter(Renderer renderer, float characterY, int sourceOrder)
        {
            if (renderer == null) return false;

            var sort = renderer.GetComponentInParent<YSort>();
            if (sort != null)
            {
                return ComputeOccluderFrontY(renderer) < characterY - 0.02f;
            }

            return renderer.sortingOrder > sourceOrder;
        }

        public static float ComputeOccluderFrontY(Renderer renderer)
        {
            if (renderer == null) return float.PositiveInfinity;

            var gridVisual = renderer.GetComponentInParent<GridObjectVisual>();
            if (gridVisual != null && gridVisual.kind == GridObjectVisualKind.FootprintObject)
            {
                // Grid objects are positioned at their footprint bottom-center. Occlusion should
                // use that physical front edge, while YSort.sortPivotOffsetY remains render-order only.
                return gridVisual.transform.position.y;
            }

            var sort = renderer.GetComponentInParent<YSort>();
            if (sort != null) return sort.transform.position.y + sort.sortPivotOffsetY;
            return float.PositiveInfinity;
        }

        Rect ScreenRect(Bounds bounds)
        {
            if (renderCamera == null) return Rect.zero;

            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector2 screenMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 screenMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        var world = new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z);
                        Vector3 screen = renderCamera.WorldToScreenPoint(world);
                        screenMin = Vector2.Min(screenMin, screen);
                        screenMax = Vector2.Max(screenMax, screen);
                    }
                }
            }

            return Rect.MinMaxRect(screenMin.x, screenMin.y, screenMax.x, screenMax.y);
        }

        static Renderer[] GetOccluders()
        {
            if (cachedOccluders != null && Time.unscaledTime < nextOccluderRefresh) return cachedOccluders;
            nextOccluderRefresh = Time.unscaledTime + 0.18f;
            var result = new List<Renderer>();
            foreach (var renderer in FindObjectsOfType<Renderer>())
            {
                if (!IsOccludingBodyRenderer(renderer)) continue;
                result.Add(renderer);
            }
            cachedOccluders = result.ToArray();
            return cachedOccluders;
        }

        static bool IsOccludingBodyRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.GetComponent<OcclusionMaskSource>() == null) return false;
            if (renderer.GetComponentInParent<PlayerController>() != null ||
                renderer.GetComponentInParent<EnemyController>() != null) return false;

            return renderer.GetComponentInParent<YSort>() != null;
        }

        static Material CreateMaterial(string shaderName, string materialName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) return null;
            return new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        void OnDisable()
        {
            DetachCommandBuffer();
            silhouetteMaterialInitialized = false;
        }

        void OnDestroy()
        {
            DetachCommandBuffer();
            DestroyGenerated(silhouetteMaterial);
        }

        void DetachCommandBuffer()
        {
            SetCommandBufferAttached(false);
            commandSourceMatrixInitialized = false;
            commandBuffer?.Release();
            commandBuffer = null;
        }

        void SetCommandBufferAttached(bool attached)
        {
            if (renderCamera == null || commandBuffer == null || commandBufferAttached == attached) return;
            if (attached) renderCamera.AddCommandBuffer(CameraEvent.AfterEverything, commandBuffer);
            else renderCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, commandBuffer);
            commandBufferAttached = attached;
        }

        static void DestroyGenerated(UnityEngine.Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }

        Material GetStencilMaterial(Texture texture)
        {
            if (texture == null || stencilShader == null) return null;
            if (StencilMaterials.TryGetValue(texture, out var material) && material != null) return material;
            material = new Material(stencilShader)
            {
                name = $"Occlusion Stencil: {texture.name}",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetTexture("_OccluderTex", texture);
            StencilMaterials[texture] = material;
            return material;
        }
    }
}
