using UnityEngine;

namespace AreaSurvivors
{
    public sealed class YSort : MonoBehaviour
    {
        const int WorldObjectSortingOffset = 5000;

        public int baseOrder = 1000;
        public int orderOffset;
        public float sortPivotOffsetY;
        public Renderer[] renderers;

        void Awake()
        {
            if (renderers == null || renderers.Length == 0)
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        void LateUpdate()
        {
            Apply();
        }

        public void Apply()
        {
            if (renderers == null || renderers.Length == 0) return;
            int order = WorldObjectSortingOffset + baseOrder + Mathf.RoundToInt(-(transform.position.y + sortPivotOffsetY) * 100f) + orderOffset;
            foreach (var renderer in renderers)
            {
                if (renderer != null) renderer.sortingOrder = order;
            }
        }
    }
}
