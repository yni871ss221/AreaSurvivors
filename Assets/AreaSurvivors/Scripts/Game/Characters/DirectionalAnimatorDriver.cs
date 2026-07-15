using UnityEngine;

namespace AreaSurvivors
{
    [DisallowMultipleComponent]
    public sealed class DirectionalAnimatorDriver : MonoBehaviour
    {
        const string IdleDownState = "IdleDown";
        const string IdleLeftState = "IdleLeft";
        const string IdleRightState = "IdleRight";
        const string IdleUpState = "IdleUp";
        const string WalkDownState = "WalkDown";
        const string WalkLeftState = "WalkLeft";
        const string WalkRightState = "WalkRight";
        const string WalkUpState = "WalkUp";

        [SerializeField] Animator animator;

        Vector2 currentDirection = Vector2.down;
        int currentStateHash;

        public Animator Animator => animator;

        public void Configure(Animator targetAnimator, RuntimeAnimatorController controller)
        {
            animator = targetAnimator;
            if (animator == null) return;

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.enabled = controller != null;
            currentStateHash = 0;
            PlayState(false);
        }

        public void SetController(RuntimeAnimatorController controller)
        {
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null) return;

            animator.runtimeAnimatorController = controller;
            animator.enabled = controller != null;
            currentStateHash = 0;
            PlayState(false);
        }

        public void Tick(Vector2 direction, bool moving)
        {
            if (direction.sqrMagnitude > 0.01f) currentDirection = direction.normalized;
            PlayState(moving);
        }

        public void SetPlaybackEnabled(bool value)
        {
            if (animator == null) return;

            bool wasEnabled = animator.enabled;
            bool shouldEnable = value && animator.runtimeAnimatorController != null;
            animator.enabled = shouldEnable;
            if (shouldEnable && !wasEnabled)
            {
                currentStateHash = 0;
                PlayState(false);
            }
        }

        void PlayState(bool moving)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;

            string stateName;
            if (Mathf.Abs(currentDirection.x) > Mathf.Abs(currentDirection.y))
            {
                stateName = currentDirection.x < 0f
                    ? (moving ? WalkLeftState : IdleLeftState)
                    : (moving ? WalkRightState : IdleRightState);
            }
            else
            {
                stateName = currentDirection.y > 0f
                    ? (moving ? WalkUpState : IdleUpState)
                    : (moving ? WalkDownState : IdleDownState);
            }

            int nextStateHash = Animator.StringToHash(stateName);
            if (nextStateHash == currentStateHash) return;
            currentStateHash = nextStateHash;
            animator.Play(nextStateHash, 0, 0f);
        }
    }
}
