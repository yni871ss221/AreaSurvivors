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
        float baseFramesPerSecond;

        void Awake()
        {
            baseFramesPerSecond = Mathf.Max(1f, framesPerSecond);
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

        public bool SetFramesFromResources(string spriteKey)
        {
            if (string.IsNullOrWhiteSpace(spriteKey)) return false;
            var down = LoadFrames(spriteKey, "Down");
            var left = LoadFrames(spriteKey, "Left");
            var right = LoadFrames(spriteKey, "Right");
            var up = LoadFrames(spriteKey, "Up");
            if (!HasCompleteFrames(down) || !HasCompleteFrames(left) || !HasCompleteFrames(right) || !HasCompleteFrames(up))
                return false;

            SetFrames(down, left, right, up);
            return true;
        }

        public void SetPlaybackSpeedMultiplier(float multiplier)
        {
            if (baseFramesPerSecond <= 0f) baseFramesPerSecond = Mathf.Max(1f, framesPerSecond);
            framesPerSecond = baseFramesPerSecond * Mathf.Max(0.1f, multiplier);
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

        static bool HasCompleteFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) return false;
            foreach (var frame in frames)
            {
                if (frame == null) return false;
            }
            return true;
        }

        static Sprite[] LoadFrames(string spriteKey, string direction)
        {
            var frames = new Sprite[3];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = Resources.Load<Sprite>($"Generated/Walk/{spriteKey}/{direction}_{i}");
            }
            return frames;
        }
    }
}
