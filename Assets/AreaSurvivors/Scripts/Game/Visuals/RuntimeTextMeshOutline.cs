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
        public Material sharedOutlineMaterial;

        TextMesh textMesh;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock propertyBlock;
        static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        static readonly int FaceColorId = Shader.PropertyToID("_FaceColor");
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int OutlineTexelId = Shader.PropertyToID("_OutlineTexel");
        static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");

        public Material SharedOutlineMaterial => sharedOutlineMaterial;

        void Awake()
        {
            EnsureReferences();
        }

        void OnEnable()
        {
            EnsureReferences();
            Apply();
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

        void EnsureReferences()
        {
            if (textMesh == null) textMesh = GetComponent<TextMesh>();
            if (meshRenderer == null) meshRenderer = GetComponent<MeshRenderer>();
            if (textMesh == null || meshRenderer == null) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            if (sharedOutlineMaterial != null &&
                meshRenderer.sharedMaterial != sharedOutlineMaterial)
            {
                meshRenderer.sharedMaterial = sharedOutlineMaterial;
            }
        }

        void Apply()
        {
            EnsureReferences();
            if (meshRenderer == null || sharedOutlineMaterial == null || propertyBlock == null) return;

            var fontMaterial = textMesh != null && textMesh.font != null ? textMesh.font.material : null;
            var texture = fontMaterial != null
                ? fontMaterial.mainTexture
                : sharedOutlineMaterial.mainTexture;

            float width = texture != null ? Mathf.Max(1f, texture.width) : 1024f;
            float height = texture != null ? Mathf.Max(1f, texture.height) : 1024f;
            propertyBlock.Clear();
            if (texture != null) propertyBlock.SetTexture(MainTextureId, texture);
            propertyBlock.SetColor(FaceColorId, faceColor);
            propertyBlock.SetColor(OutlineColorId, outlineColor);
            propertyBlock.SetVector(
                OutlineTexelId,
                new Vector4(outlinePixels / width, outlinePixels / height, 0f, 0f));
            propertyBlock.SetFloat(AlphaThresholdId, 0.05f);
            meshRenderer.SetPropertyBlock(propertyBlock);
            if (textMesh != null) textMesh.color = faceColor;
        }
    }
}
