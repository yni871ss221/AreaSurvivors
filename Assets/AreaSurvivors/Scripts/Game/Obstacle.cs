using UnityEngine;

namespace AreaSurvivors
{
    public sealed class Obstacle : MonoBehaviour
    {
        public Vector2 visualSize;

        void Awake()
        {
            ApplyGeneratedVisualSize();
        }

        void Start()
        {
            ApplyGeneratedVisualSize();
        }

        void ApplyGeneratedVisualSize()
        {
            var visual = GetComponentInChildren<PaperMeshVisual>(true);
            if (visual == null || visual.sprite == null) return;
            if (visual.GetComponent<OcclusionMaskSource>() == null)
                visual.gameObject.AddComponent<OcclusionMaskSource>();

            var size = visualSize.sqrMagnitude > 0.001f ? visualSize : DefaultVisualSize(name);
            if (size.sqrMagnitude <= 0.001f) return;

            var bounds = visual.sprite.bounds.size;
            float x = Mathf.Abs(bounds.x) > 0.001f ? size.x / bounds.x : 1f;
            float y = Mathf.Abs(bounds.y) > 0.001f ? size.y / bounds.y : 1f;
            visual.transform.localScale = new Vector3(x, y, 1f);
        }

        static Vector2 DefaultVisualSize(string objectName)
        {
            if (objectName.Contains("Tree")) return new Vector2(0.7f, 0.8f);
            if (objectName.Contains("Rock")) return new Vector2(0.7f, 0.47f);
            if (objectName.Contains("Pond")) return new Vector2(0.7f, 0.31f);
            return Vector2.zero;
        }
    }
}
