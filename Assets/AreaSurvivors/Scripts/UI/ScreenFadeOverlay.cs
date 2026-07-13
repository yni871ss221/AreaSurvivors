using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class ScreenFadeOverlay : MonoBehaviour
    {
        public CanvasGroup canvasGroup;
        [Min(0.01f)] public float fadeDuration = 0.65f;
        public bool startBlack;

        void Awake()
        {
            if (!ResolveReferences()) return;
            SetAlpha(startBlack ? 1f : 0f);
            canvasGroup.blocksRaycasts = startBlack;
            canvasGroup.interactable = false;
        }

        public IEnumerator FadeToBlack()
        {
            yield return FadeTo(1f);
        }

        public IEnumerator FadeFromBlack()
        {
            yield return FadeTo(0f);
        }

        IEnumerator FadeTo(float targetAlpha)
        {
            if (!ResolveReferences()) yield break;

            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = false;
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            SetAlpha(targetAlpha);
            canvasGroup.blocksRaycasts = targetAlpha > 0.001f;
        }

        bool ResolveReferences()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null) return true;

            Debug.LogError($"{nameof(ScreenFadeOverlay)} requires a Scene-authored CanvasGroup.", this);
            enabled = false;
            return false;
        }

        void SetAlpha(float alpha)
        {
            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }
    }
}
