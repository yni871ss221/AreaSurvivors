using UnityEngine;

namespace AreaSurvivors
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class RuntimeSpriteOutline : MonoBehaviour
    {
        public Color outlineColor = Color.black;
        public float thickness = 0.035f;
        public bool blink;
        public float blinkSpeed = 5f;

        MeshFilter sourceFilter;
        MeshRenderer sourceRenderer;
        MeshFilter outlineFilter;
        MeshRenderer outlineRenderer;
        Material outlineMaterial;
        Mesh outlineMesh;
        Mesh lastSourceMesh;
        float lastThickness = -1f;

        void Awake()
        {
            EnsureOutline();
        }

        void OnEnable()
        {
            EnsureOutline();
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
            if (outlineMesh == null) return;
            if (Application.isPlaying) Destroy(outlineMesh);
            else DestroyImmediate(outlineMesh);
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
            var go = child != null ? child.gameObject : new GameObject("Runtime Outline");
            go.transform.SetParent(transform, false);
            outlineFilter = go.GetComponent<MeshFilter>();
            if (outlineFilter == null) outlineFilter = go.AddComponent<MeshFilter>();
            outlineRenderer = go.GetComponent<MeshRenderer>();
            if (outlineRenderer == null) outlineRenderer = go.AddComponent<MeshRenderer>();
            outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            RemoveLegacyOutlineCopies();
        }

        void SyncOutline()
        {
            if (sourceFilter == null || sourceRenderer == null || outlineRenderer == null) return;

            var sourceMaterial = sourceRenderer.sharedMaterial;
            if (sourceMaterial == null || sourceFilter.sharedMesh == null)
            {
                SetVisible(false);
                return;
            }

            var color = outlineColor;
            color.a *= SourceAlpha(sourceMaterial);
            if (blink)
            {
                color.a *= Mathf.Lerp(0.35f, 1f, Mathf.PingPong(Time.time * Mathf.Max(0.1f, blinkSpeed), 1f));
            }

            EnsureOutlineMesh(sourceFilter.sharedMesh);
            outlineMaterial.mainTexture = sourceMaterial.mainTexture;
            outlineMaterial.color = color;
            ApplySpriteRectProperties();

            bool visible = sourceRenderer.enabled && color.a > 0.001f && thickness > 0.001f;
            var outlineTransform = outlineRenderer.transform;
            outlineTransform.localPosition = Vector3.zero;
            outlineTransform.localRotation = Quaternion.identity;
            outlineTransform.localScale = Vector3.one;
            outlineRenderer.sharedMaterial = outlineMaterial;
            outlineRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
            outlineRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
            outlineRenderer.enabled = visible;
        }

        void SetVisible(bool visible)
        {
            if (outlineRenderer != null) outlineRenderer.enabled = visible;
        }

        void EnsureOutlineMesh(Mesh sourceMesh)
        {
            if (sourceMesh == null) return;
            if (outlineMesh != null && lastSourceMesh == sourceMesh && Mathf.Approximately(lastThickness, thickness)) return;

            var vertices = sourceMesh.vertices;
            var uvs = sourceMesh.uv;
            if (vertices == null || vertices.Length < 4 || uvs == null || uvs.Length < 4)
            {
                outlineFilter.sharedMesh = sourceMesh;
                return;
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
            float uvPadX = (uvMax.x - uvMin.x) * thickness / width;
            float uvPadY = (uvMax.y - uvMin.y) * thickness / height;

            if (outlineMesh == null)
            {
                outlineMesh = new Mesh
                {
                    name = sourceMesh.name + " Outline",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            else
            {
                outlineMesh.Clear();
            }

            outlineMesh.vertices = new[]
            {
                new Vector3(min.x - thickness, min.y - thickness, 0f),
                new Vector3(max.x + thickness, min.y - thickness, 0f),
                new Vector3(min.x - thickness, max.y + thickness, 0f),
                new Vector3(max.x + thickness, max.y + thickness, 0f)
            };
            outlineMesh.uv = new[]
            {
                new Vector2(uvMin.x - uvPadX, uvMin.y - uvPadY),
                new Vector2(uvMax.x + uvPadX, uvMin.y - uvPadY),
                new Vector2(uvMin.x - uvPadX, uvMax.y + uvPadY),
                new Vector2(uvMax.x + uvPadX, uvMax.y + uvPadY)
            };
            outlineMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            outlineMesh.RecalculateBounds();
            outlineFilter.sharedMesh = outlineMesh;
            lastSourceMesh = sourceMesh;
            lastThickness = thickness;
        }

        void ApplySpriteRectProperties()
        {
            if (outlineMesh == null || outlineMesh.uv == null || outlineMesh.uv.Length < 4) return;
            var sourceUvs = sourceFilter.sharedMesh.uv;
            if (sourceUvs == null || sourceUvs.Length < 4) return;

            var uvMin = sourceUvs[0];
            var uvMax = sourceUvs[0];
            for (int i = 1; i < sourceUvs.Length; i++)
            {
                uvMin = Vector2.Min(uvMin, sourceUvs[i]);
                uvMax = Vector2.Max(uvMax, sourceUvs[i]);
            }

            var vertices = sourceFilter.sharedMesh.vertices;
            var min = vertices[0];
            var max = vertices[0];
            for (int i = 1; i < vertices.Length; i++)
            {
                min = Vector3.Min(min, vertices[i]);
                max = Vector3.Max(max, vertices[i]);
            }

            float width = Mathf.Max(0.001f, max.x - min.x);
            float height = Mathf.Max(0.001f, max.y - min.y);
            float uvPadX = (uvMax.x - uvMin.x) * thickness / width;
            float uvPadY = (uvMax.y - uvMin.y) * thickness / height;
            outlineMaterial.SetVector("_SpriteRect", new Vector4(uvMin.x, uvMin.y, uvMax.x, uvMax.y));
            outlineMaterial.SetVector("_OutlineUv", new Vector4(uvPadX, uvPadY, 0f, 0f));
            outlineMaterial.SetFloat("_AlphaThreshold", 0.05f);
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
    }
}
