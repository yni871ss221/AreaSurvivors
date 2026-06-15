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

        static Renderer[] cachedOccluders;
        static float nextOccluderRefresh;
        static readonly Dictionary<Texture, Material> StencilMaterials = new Dictionary<Texture, Material>();

        PaperMeshVisual source;
        MeshRenderer sourceRenderer;
        Shader stencilShader;
        Material silhouetteMaterial;
        CommandBuffer commandBuffer;
        Camera renderCamera;
        bool commandBufferAttached;
        float timer;

        void OnEnable()
        {
            EnsureResources();
        }

        void LateUpdate()
        {
            EnsureResources();
            if (sourceRenderer == null || commandBuffer == null) return;

            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = checkInterval;
            RebuildCommands();
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

        void RebuildCommands()
        {
            commandBuffer.Clear();
            if (source == null || sourceRenderer == null || !sourceRenderer.enabled) return;

            Rect sourceScreenRect = ScreenRect(sourceRenderer.bounds);
            int sourceOrder = sourceRenderer.sortingOrder;
            bool hasOccluder = false;
            foreach (var renderer in GetOccluders())
            {
                if (!IsFrontOverlappingOccluder(renderer, sourceScreenRect, sourceOrder)) continue;
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
            silhouetteMaterial.mainTexture = source.sprite != null ? source.sprite.texture : null;
            silhouetteMaterial.SetColor("_Color", silhouetteColor);
            silhouetteMaterial.SetColor("_OutlineColor", outlineColor);
            commandBuffer.DrawRenderer(sourceRenderer, silhouetteMaterial);
        }

        bool IsFrontOverlappingOccluder(Renderer renderer, Rect sourceScreenRect, int sourceOrder)
        {
            if (renderer == null || renderer == sourceRenderer ||
                renderer.transform.IsChildOf(transform) || !renderer.enabled) return false;

            var sort = renderer.GetComponentInParent<YSort>();
            if (sort != null)
            {
                bool inFrontByPivot = sort.transform.position.y + sort.sortPivotOffsetY < transform.position.y - 0.02f;
                if (!inFrontByPivot) return false;
            }
            else if (renderer.sortingOrder <= sourceOrder)
            {
                return false;
            }

            return sourceScreenRect.Overlaps(ScreenRect(renderer.bounds));
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
        }

        void OnDestroy()
        {
            DetachCommandBuffer();
            DestroyGenerated(silhouetteMaterial);
        }

        void DetachCommandBuffer()
        {
            SetCommandBufferAttached(false);
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

        static void DestroyGenerated(Object generated)
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
