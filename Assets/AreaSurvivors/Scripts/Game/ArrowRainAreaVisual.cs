using UnityEngine;

namespace AreaSurvivors
{
    [ExecuteAlways]
    public sealed class ArrowRainAreaVisual : MonoBehaviour
    {
        const int CircleSegments = 64;

        [SerializeField] MeshFilter fillMeshFilter;
        [SerializeField] MeshRenderer fillRenderer;
        [SerializeField] LineRenderer outlineRenderer;
        [SerializeField] PaperMeshVisual arrowVisual;
        [SerializeField] PaperMeshVisual[] arrowVisuals;
        [SerializeField] Sprite[] frames;
        [SerializeField] float framesPerSecond = 8f;
        [SerializeField, Range(0.1f, 1f)] float arrowAnimationScale = 0.56f;
        [SerializeField, Range(0f, 4f)] float arrowFallTravel = 1.8f;
        [SerializeField, Range(0.1f, 4f)] float arrowFallCyclesPerSecond = 2f;
        [SerializeField, Range(0f, 1f)] float arrowFallDesync = 0.85f;
        [SerializeField, Range(0f, 1f)] float arrowHeightJitter = 0.45f;
        [SerializeField] Color fillColor = new Color(0.24f, 0.62f, 0.96f, 0.24f);
        [SerializeField] Color outlineColor = new Color(0.58f, 0.86f, 1f, 0.78f);

        Mesh generatedMesh;
        Material fillMaterial;
        Material outlineMaterial;
        float timer;
        int frameIndex;
        Vector3[] arrowBasePositions;
        float[] arrowPhaseOffsets;
        float[] arrowTravelMultipliers;

        public void Initialize(
            MeshFilter areaFillMeshFilter,
            MeshRenderer areaFillRenderer,
            LineRenderer areaOutlineRenderer,
            PaperMeshVisual[] rainVisuals,
            Sprite[] animationFrames)
        {
            fillMeshFilter = areaFillMeshFilter;
            fillRenderer = areaFillRenderer;
            outlineRenderer = areaOutlineRenderer;
            arrowVisuals = rainVisuals;
            arrowVisual = rainVisuals != null && rainVisuals.Length > 0 ? rainVisuals[0] : null;
            frames = animationFrames;
            CaptureArrowBasePositions(true);
            ApplyCircle();
            ApplyFrame(0);
        }

        public void SetAreaAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);
            fillColor.a = Mathf.Max(0.16f, alpha * 0.55f);
            outlineColor.a = Mathf.Max(0.68f, alpha * 0.95f);
            ApplyCircle();
            ApplyArrowScale();
        }

        void Awake()
        {
            ApplyCircle();
            ApplyFrame(0);
            ApplyArrowScale();
        }

        void OnEnable()
        {
            timer = 0f;
            frameIndex = 0;
            CaptureArrowBasePositions(true);
            ResetArrowPositions();
            ApplyCircle();
            ApplyFrame(0);
            ApplyArrowScale();
        }

        void OnDisable()
        {
            ResetArrowPositions();
        }

        void Update()
        {
            if (!Application.isPlaying || frames == null || frames.Length == 0 || !HasArrowVisual()) return;
            timer += Time.deltaTime;
            int nextFrame = Mathf.FloorToInt(timer * Mathf.Max(1f, framesPerSecond)) % frames.Length;
            if (nextFrame != frameIndex)
            {
                frameIndex = nextFrame;
                ApplyFrame(frameIndex);
            }
            if (arrowFallTravel > 0f) ApplyFallAnimation();
        }

        void OnValidate()
        {
            ApplyCircle();
            ApplyFrame(frameIndex);
            ApplyArrowScale();
        }

        void OnDestroy()
        {
            DestroyGenerated(generatedMesh);
            DestroyGenerated(fillMaterial);
            DestroyGenerated(outlineMaterial);
        }

        void ApplyFrame(int index)
        {
            if (frames == null || frames.Length == 0 || !HasArrowVisual()) return;
            index = Mathf.Clamp(index, 0, frames.Length - 1);
            if (arrowVisuals != null && arrowVisuals.Length > 0)
            {
                for (int i = 0; i < arrowVisuals.Length; i++)
                {
                    if (arrowVisuals[i] == null) continue;
                    int visualFrame = (index + i) % frames.Length;
                    arrowVisuals[i].Configure(frames[visualFrame], Color.white, WeaponSortingOrders.Projectile);
                }
            }
            else
            {
                arrowVisual.Configure(frames[index], Color.white, WeaponSortingOrders.Projectile);
            }
            ApplyArrowScale();
        }

        void ApplyArrowScale()
        {
            float parentScale = Mathf.Max(0.1f, transform.lossyScale.x);
            float localScale = Mathf.Clamp(arrowAnimationScale, 0.1f, 1f) / parentScale;
            if (arrowVisuals != null && arrowVisuals.Length > 0)
            {
                foreach (var visual in arrowVisuals)
                {
                    if (visual == null) continue;
                    visual.transform.localScale = Vector3.one * localScale;
                }
                return;
            }

            if (arrowVisual != null) arrowVisual.transform.localScale = Vector3.one * localScale;
        }

        void ApplyFallAnimation()
        {
            CaptureArrowBasePositions(false);
            int visualCount = ActiveArrowVisualCount();
            if (visualCount == 0 || arrowBasePositions == null || arrowBasePositions.Length != visualCount) return;
            EnsureArrowFallVariation(visualCount);

            float travel = Mathf.Max(0f, arrowFallTravel);
            for (int i = 0; i < visualCount; i++)
            {
                var visual = GetActiveArrowVisual(i);
                if (visual == null) continue;

                float orderedOffset = i / (float)visualCount;
                float randomOffset = arrowPhaseOffsets != null && i < arrowPhaseOffsets.Length ? arrowPhaseOffsets[i] : orderedOffset;
                float phaseOffset = Mathf.Lerp(orderedOffset, randomOffset, arrowFallDesync);
                float phase = Mathf.Repeat(timer * Mathf.Max(0.1f, arrowFallCyclesPerSecond) + phaseOffset, 1f);
                float travelMultiplier = arrowTravelMultipliers != null && i < arrowTravelMultipliers.Length ? arrowTravelMultipliers[i] : 1f;
                float yOffset = Mathf.Lerp(travel * travelMultiplier, 0f, Mathf.Clamp01(phase));
                visual.transform.localPosition = arrowBasePositions[i] + new Vector3(0f, yOffset, 0f);
            }
        }

        void EnsureArrowFallVariation(int visualCount)
        {
            if (visualCount <= 0) return;
            if (arrowPhaseOffsets != null && arrowPhaseOffsets.Length == visualCount &&
                arrowTravelMultipliers != null && arrowTravelMultipliers.Length == visualCount)
            {
                return;
            }

            arrowPhaseOffsets = new float[visualCount];
            arrowTravelMultipliers = new float[visualCount];
            float jitter = Mathf.Clamp01(arrowHeightJitter);
            for (int i = 0; i < visualCount; i++)
            {
                arrowPhaseOffsets[i] = Hash01(i, 17);
                arrowTravelMultipliers[i] = Mathf.Lerp(1f, Mathf.Lerp(0.68f, 1.24f, Hash01(i, 43)), jitter);
            }
        }

        void CaptureArrowBasePositions(bool force)
        {
            int visualCount = ActiveArrowVisualCount();
            if (visualCount == 0) return;
            if (!force && arrowBasePositions != null && arrowBasePositions.Length == visualCount) return;

            arrowBasePositions = new Vector3[visualCount];
            arrowPhaseOffsets = null;
            arrowTravelMultipliers = null;
            for (int i = 0; i < visualCount; i++)
            {
                var visual = GetActiveArrowVisual(i);
                arrowBasePositions[i] = visual != null ? visual.transform.localPosition : Vector3.zero;
            }
        }

        void ResetArrowPositions()
        {
            int visualCount = ActiveArrowVisualCount();
            if (visualCount == 0 || arrowBasePositions == null || arrowBasePositions.Length != visualCount) return;
            for (int i = 0; i < visualCount; i++)
            {
                var visual = GetActiveArrowVisual(i);
                if (visual != null) visual.transform.localPosition = arrowBasePositions[i];
            }
        }

        int ActiveArrowVisualCount()
        {
            if (arrowVisuals != null && arrowVisuals.Length > 0) return arrowVisuals.Length;
            return arrowVisual != null ? 1 : 0;
        }

        PaperMeshVisual GetActiveArrowVisual(int index)
        {
            if (arrowVisuals != null && arrowVisuals.Length > 0)
            {
                return index >= 0 && index < arrowVisuals.Length ? arrowVisuals[index] : null;
            }
            return index == 0 ? arrowVisual : null;
        }

        bool HasArrowVisual()
        {
            if (arrowVisual != null) return true;
            if (arrowVisuals == null) return false;
            foreach (var visual in arrowVisuals)
            {
                if (visual != null) return true;
            }
            return false;
        }

        void ApplyCircle()
        {
            EnsureMaterials();
            ApplyFillMesh();
            ApplyOutline();
        }

        void EnsureMaterials()
        {
            if (fillRenderer != null && fillMaterial == null)
            {
                fillMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Arrow Rain Area Fill Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
                fillRenderer.sharedMaterial = fillMaterial;
            }
            if (fillMaterial != null) fillMaterial.mainTexture = Texture2D.whiteTexture;

            if (outlineRenderer != null && outlineMaterial == null)
            {
                outlineMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "Arrow Rain Area Outline Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
                outlineRenderer.sharedMaterial = outlineMaterial;
            }
            if (outlineMaterial != null) outlineMaterial.mainTexture = Texture2D.whiteTexture;
        }

        void ApplyFillMesh()
        {
            if (fillMeshFilter == null || fillRenderer == null) return;
            DestroyGenerated(generatedMesh);
            generatedMesh = new Mesh
            {
                name = "Arrow Rain Area Circle",
                hideFlags = HideFlags.HideAndDontSave
            };

            var vertices = new Vector3[CircleSegments + 1];
            var triangles = new int[CircleSegments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i == CircleSegments - 1 ? 1 : i + 2;
            }

            generatedMesh.vertices = vertices;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();
            fillMeshFilter.sharedMesh = generatedMesh;
            fillRenderer.sortingOrder = WeaponSortingOrders.AreaEffect;
            if (fillMaterial != null) fillMaterial.color = fillColor;
        }

        void ApplyOutline()
        {
            if (outlineRenderer == null) return;
            outlineRenderer.useWorldSpace = false;
            outlineRenderer.loop = true;
            outlineRenderer.positionCount = CircleSegments;
            outlineRenderer.widthMultiplier = 0.05f;
            outlineRenderer.sortingOrder = WeaponSortingOrders.AreaEffect + 1;
            outlineRenderer.startColor = outlineColor;
            outlineRenderer.endColor = outlineColor;
            if (outlineMaterial != null) outlineMaterial.color = outlineColor;

            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
                outlineRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }
        }

        static void DestroyGenerated(Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }

        static float Hash01(int value, int salt)
        {
            uint x = unchecked((uint)value * 747796405u + (uint)salt * 2891336453u);
            x = ((x >> 16) ^ x) * 2246822519u;
            x = ((x >> 13) ^ x) * 3266489917u;
            x = (x >> 16) ^ x;
            return (x & 0x00FFFFFF) / 16777215f;
        }
    }
}
