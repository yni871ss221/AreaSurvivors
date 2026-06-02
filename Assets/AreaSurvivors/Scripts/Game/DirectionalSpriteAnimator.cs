using UnityEngine;

namespace AreaSurvivors
{
    public sealed class DirectionalSpriteAnimator : MonoBehaviour
    {
        public Sprite[] downFrames;
        public Sprite[] leftFrames;
        public Sprite[] rightFrames;
        public Sprite[] upFrames;
        public float framesPerSecond = 8f;

        PaperMeshVisual visual;
        Sprite[] currentFrames;
        Vector2 currentDirection = Vector2.down;
        float timer;
        int frameIndex = 1;

        void Awake()
        {
            visual = GetComponentInChildren<PaperMeshVisual>();
            currentFrames = downFrames;
            ApplyFrame(false);
        }

        public void SetFrames(Sprite[] down, Sprite[] left, Sprite[] right, Sprite[] up)
        {
            downFrames = down;
            leftFrames = left;
            rightFrames = right;
            upFrames = up;
            currentFrames = FramesForDirection(currentDirection);
            ApplyFrame(false);
        }

        public void Tick(Vector2 direction, bool moving)
        {
            if (direction.sqrMagnitude > 0.01f)
            {
                currentDirection = direction.normalized;
            }

            var nextFrames = FramesForDirection(currentDirection);
            if (nextFrames != currentFrames)
            {
                currentFrames = nextFrames;
                frameIndex = 1;
                timer = 0f;
            }

            if (!moving)
            {
                frameIndex = currentFrames != null && currentFrames.Length > 1 ? 1 : 0;
                timer = 0f;
                ApplyFrame(false);
                return;
            }

            timer += Time.deltaTime;
            var frameTime = 1f / Mathf.Max(1f, framesPerSecond);
            while (timer >= frameTime)
            {
                timer -= frameTime;
                frameIndex++;
            }

            ApplyFrame(true);
        }

        Sprite[] FramesForDirection(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return direction.x < 0f && HasFrames(leftFrames) ? leftFrames : rightFrames;
            }

            return direction.y > 0f && HasFrames(upFrames) ? upFrames : downFrames;
        }

        void ApplyFrame(bool animate)
        {
            if (visual == null) visual = GetComponentInChildren<PaperMeshVisual>();
            if (!HasFrames(currentFrames)) return;

            var index = animate ? frameIndex % currentFrames.Length : Mathf.Clamp(frameIndex, 0, currentFrames.Length - 1);
            var sprite = currentFrames[index];
            if (sprite != null && visual != null) visual.sprite = sprite;
        }

        static bool HasFrames(Sprite[] frames)
        {
            return frames != null && frames.Length > 0 && frames[0] != null;
        }
    }
}
