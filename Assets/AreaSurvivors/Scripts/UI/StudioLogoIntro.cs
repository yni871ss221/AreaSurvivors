using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [DefaultExecutionOrder(-10000)]
    public sealed class StudioLogoIntro : MonoBehaviour
    {
        public static bool IsPlaying { get; private set; }
        public static bool HasPlayedThisSession { get; private set; }

        public GameObject logoOverlay;
        public CanvasGroup logoGroup;
        public RectTransform logoRect;
        public TitleIntroAnimator titleIntroAnimator;
        public float fadeInDuration = 0.28f;
        public float bounceDuration = 0.72f;
        public float holdDuration = 0.95f;
        public float fadeOutDuration = 0.42f;
        public Vector3 startScale = new Vector3(0.62f, 0.62f, 1f);
        public Vector3 overshootScale = new Vector3(1.12f, 1.12f, 1f);
        public Vector3 finalScale = Vector3.one;

        Graphic logoGraphic;

        void Awake()
        {
            logoGraphic = logoRect != null ? logoRect.GetComponent<Graphic>() : null;

            if (HasPlayedThisSession)
            {
                IsPlaying = false;
                if (logoOverlay != null) logoOverlay.SetActive(false);
                if (logoGroup != null)
                {
                    logoGroup.alpha = 0f;
                    logoGroup.interactable = false;
                    logoGroup.blocksRaycasts = false;
                }

                SetLogoAlpha(0f);
                return;
            }

            HasPlayedThisSession = true;
            IsPlaying = true;
            if (titleIntroAnimator != null)
            {
                titleIntroAnimator.enabled = false;
                titleIntroAnimator.HideInstant();
            }

            if (logoOverlay != null) logoOverlay.SetActive(true);
            if (logoGroup != null)
            {
                logoGroup.alpha = 1f;
                logoGroup.interactable = false;
                logoGroup.blocksRaycasts = true;
            }

            if (logoRect != null)
            {
                logoRect.localScale = startScale;
            }

            SetLogoAlpha(0f);
        }

        void Start()
        {
            if (!IsPlaying) return;
            StartCoroutine(Play());
        }

        void OnDisable()
        {
            IsPlaying = false;
        }

        IEnumerator Play()
        {
            if (logoOverlay == null || logoRect == null)
            {
                BeginTitleIntro();
                yield break;
            }

            yield return FadeLogo(0f, 1f, fadeInDuration);
            yield return Scale(startScale, overshootScale, bounceDuration * 0.55f, EaseOutBack);
            AudioManager.PlaySfx(SfxTrack.StudioLogoBounce);
            yield return Scale(overshootScale, finalScale, bounceDuration * 0.45f, EaseOutBounce);
            if (holdDuration > 0f) yield return Wait(holdDuration);
            yield return FadeLogo(1f, 0f, fadeOutDuration);

            if (logoOverlay != null) logoOverlay.SetActive(false);
            BeginTitleIntro();
        }

        void BeginTitleIntro()
        {
            IsPlaying = false;
            AudioManager.PlayBgm(BgmTrack.TitleOptions);
            if (titleIntroAnimator != null) titleIntroAnimator.enabled = true;
        }

        IEnumerator FadeLogo(float from, float to, float duration)
        {
            duration = Mathf.Max(0.001f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetLogoAlpha(Mathf.Lerp(from, to, EaseOutCubic(t)));
                yield return null;
            }

            SetLogoAlpha(to);
        }

        void SetLogoAlpha(float alpha)
        {
            if (logoGraphic != null)
            {
                var color = logoGraphic.color;
                color.a = alpha;
                logoGraphic.color = color;
                return;
            }

            if (logoGroup != null) logoGroup.alpha = alpha;
        }

        IEnumerator Scale(Vector3 from, Vector3 to, float duration, System.Func<float, float> ease)
        {
            duration = Mathf.Max(0.001f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                logoRect.localScale = Vector3.LerpUnclamped(from, to, ease(t));
                yield return null;
            }

            logoRect.localScale = to;
        }

        IEnumerator Wait(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                yield return null;
            }
        }

        static float EaseOutCubic(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        static float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }
            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }

            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
