using UnityEngine;

namespace AreaSurvivors
{
    [DisallowMultipleComponent]
    public sealed class PaperMeshSpriteAnimator : MonoBehaviour
    {
        [SerializeField] PaperMeshVisual visual;
        [SerializeField] Sprite[] frames;
        [SerializeField, Min(0.1f)] float framesPerSecond = 3f;

        int frameIndex;

        public void Initialize(PaperMeshVisual targetVisual, Sprite[] animationFrames, float fps = 3f)
        {
            visual = targetVisual;
            frames = animationFrames;
            framesPerSecond = Mathf.Max(0.1f, fps);
            ApplyFrame(0);
        }

        void Awake()
        {
            if (visual == null)
            {
                visual = GetComponent<PaperMeshVisual>();
            }
            ApplyFrame(frameIndex);
        }

        void OnEnable()
        {
            frameIndex = 0;
            ApplyFrame(frameIndex);
        }

        void Update()
        {
            if (visual == null || frames == null || frames.Length == 0) return;
            int nextFrame = Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length;
            if (nextFrame == frameIndex) return;

            frameIndex = nextFrame;
            ApplyFrame(frameIndex);
        }

        void ApplyFrame(int index)
        {
            if (visual == null || frames == null || frames.Length == 0) return;
            var frame = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
            if (frame != null)
            {
                visual.sprite = frame;
            }
        }
    }
}
