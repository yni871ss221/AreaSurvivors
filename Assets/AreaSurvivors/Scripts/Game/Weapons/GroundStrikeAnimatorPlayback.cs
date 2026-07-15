using UnityEngine;

namespace AreaSurvivors
{
    public sealed class GroundStrikeAnimatorPlayback : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] AnimationClip animationClip;
        [Min(0f)] [SerializeField] float impactDelaySeconds = 0.32f;

        public Animator Animator => animator;
        public AnimationClip AnimationClip => animationClip;
        public float AnimationDurationSeconds => animationClip != null ? animationClip.length : 0f;
        public float ImpactDelaySeconds => impactDelaySeconds;

        public void Configure(Animator targetAnimator, AnimationClip clip, float impactDelay)
        {
            animator = targetAnimator;
            animationClip = clip;
            impactDelaySeconds = Mathf.Max(0f, impactDelay);
        }

        public void Restart()
        {
            if (animator == null || animationClip == null) return;
            animator.Play(animationClip.name, 0, 0f);
            animator.Update(0f);
        }
    }
}
