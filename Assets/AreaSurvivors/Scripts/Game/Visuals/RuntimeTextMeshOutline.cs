using UnityEngine;

namespace AreaSurvivors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class RuntimeTextMeshOutline : MonoBehaviour
    {
        public Color faceColor = Color.white;
        public Color outlineColor = Color.black;
        public float outlinePixels = 2f;

        TextMesh textMesh;
        MeshRenderer meshRenderer;
        Material material;

        void Awake()
        {
            EnsureMaterial();
        }

        void OnEnable()
        {
            EnsureMaterial();
            Apply();
        }

        void LateUpdate()
        {
            Apply();
        }

        void OnDestroy()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        public void SetColors(Color face, Color outline)
        {
            faceColor = face;
            outlineColor = outline;
            Apply();
        }

        public void SetAlpha(float alpha)
        {
            faceColor.a = alpha;
            outlineColor.a = alpha;
            Apply();
        }

        void EnsureMaterial()
        {
            if (textMesh == null) textMesh = GetComponent<TextMesh>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (textMesh == null || meshRenderer == null) return;
            if (material != null) return;

            var shader = Shader.Find("AreaSurvivors/TextMeshAlphaOutline");
            if (shader == null) shader = Shader.Find("GUI/Text Shader");
            material = new Material(shader)
            {
                name = "Runtime TextMesh Outline",
                hideFlags = HideFlags.HideAndDontSave
            };

            var fontMaterial = textMesh.font != null ? textMesh.font.material : meshRenderer.sharedMaterial;
            if (fontMaterial != null) material.mainTexture = fontMaterial.mainTexture;
            meshRenderer.sharedMaterial = material;
        }

        void Apply()
        {
            EnsureMaterial();
            if (material == null) return;

            var fontMaterial = textMesh != null && textMesh.font != null ? textMesh.font.material : null;
            var texture = fontMaterial != null ? fontMaterial.mainTexture : material.mainTexture;
            if (texture != null) material.mainTexture = texture;

            float width = texture != null ? Mathf.Max(1f, texture.width) : 1024f;
            float height = texture != null ? Mathf.Max(1f, texture.height) : 1024f;
            material.SetColor("_FaceColor", faceColor);
            material.SetColor("_OutlineColor", outlineColor);
            material.SetVector("_OutlineTexel", new Vector4(outlinePixels / width, outlinePixels / height, 0f, 0f));
            material.SetFloat("_AlphaThreshold", 0.05f);
            if (textMesh != null) textMesh.color = faceColor;
        }
    }
}
