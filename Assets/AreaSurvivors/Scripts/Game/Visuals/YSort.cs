using UnityEngine;

namespace AreaSurvivors
{
    public sealed class YSort : MonoBehaviour
    {
        const int WorldObjectSortingOffset = 5000;

        public int baseOrder = 1000;
        public int orderOffset;
        public float sortPivotOffsetY;
        [Min(1)] public int updateFrameInterval = 1;
        public Renderer[] renderers;
        int nextApplyFrame;

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
            if (updateFrameInterval > 1 && Time.frameCount < nextApplyFrame) return;
            Apply();
            ScheduleNextApply(false);
        }

        public void Apply()
        {
            if (renderers == null || renderers.Length == 0) return;
            int order = WorldObjectSortingOffset + baseOrder + Mathf.RoundToInt(-(transform.position.y + sortPivotOffsetY) * 100f) + orderOffset;
            foreach (var renderer in renderers)
            {
                if (renderer == null ||
                    renderer.GetComponent<PreserveSortingOrder>() != null ||
                    IsRuntimeOutlineRenderer(renderer)) continue;
                var visual = renderer.GetComponent<PaperMeshVisual>();
                if (visual != null)
                {
                    if (visual.order != order || renderer.sortingOrder != order) visual.order = order;
                }
                else if (renderer.sortingOrder != order)
                {
                    renderer.sortingOrder = order;
                }
            }
        }

        static bool IsRuntimeOutlineRenderer(Renderer renderer)
        {
            if (renderer == null || renderer.gameObject.name != "Runtime Outline") return false;
            Transform parent = renderer.transform.parent;
            return parent != null && parent.GetComponent<RuntimeSpriteOutline>() != null;
        }

        public void SetUpdateFrameInterval(int frameInterval)
        {
            updateFrameInterval = Mathf.Max(1, frameInterval);
            ScheduleNextApply(true);
        }

        void ScheduleNextApply(bool stagger)
        {
            int interval = Mathf.Max(1, updateFrameInterval);
            int staggerFrames = stagger && interval > 1 ? Mathf.Abs(GetInstanceID()) % interval : 0;
            nextApplyFrame = Time.frameCount + interval + staggerFrames;
        }
    }
}
