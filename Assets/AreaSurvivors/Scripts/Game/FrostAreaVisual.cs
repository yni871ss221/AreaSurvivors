using UnityEngine;

namespace AreaSurvivors
{
    [ExecuteAlways]
    public sealed class FrostAreaVisual : MonoBehaviour
    {
        [SerializeField] PaperMeshVisual visual;
        [SerializeField] Sprite[] frames;
        [SerializeField] float framesPerSecond = 4f;
        [SerializeField, Range(0f, 1f)] float alpha = 0.72f;

        float timer;
        int frameIndex;

        public void Initialize(PaperMeshVisual targetVisual, Sprite[] animationFrames)
        {
            visual = targetVisual;
            frames = animationFrames;
            ApplyFrame(0);
        }

        public void SetAreaAlpha(float nextAlpha)
        {
            alpha = Mathf.Clamp01(nextAlpha);
            ApplyFrame(frameIndex);
        }

        void Awake()
        {
            ApplyFrame(0);
        }

        void OnEnable()
        {
            timer = 0f;
            frameIndex = 0;
            ApplyFrame(0);
        }

        void Update()
        {
            if (frames == null || frames.Length == 0 || visual == null) return;
            timer += Time.deltaTime * Mathf.Max(1f, framesPerSecond);
            int nextFrame = Mathf.FloorToInt(timer) % frames.Length;
            if (nextFrame == frameIndex) return;
            frameIndex = nextFrame;
            ApplyFrame(frameIndex);
        }

        void OnValidate()
        {
            ApplyFrame(frameIndex);
        }

        void ApplyFrame(int index)
        {
            if (frames == null || frames.Length == 0 || visual == null) return;
            index = Mathf.Clamp(index, 0, frames.Length - 1);
            visual.Configure(frames[index], new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)), WeaponSortingOrders.AreaEffect);
        }
    }
}
