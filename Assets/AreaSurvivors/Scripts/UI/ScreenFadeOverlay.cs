using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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

        public IEnumerator FlashWhite(float peakAlpha, float fadeInSeconds, float holdSeconds, float fadeOutSeconds)
        {
            if (!ResolveReferences()) yield break;
            var overlayGraphic = GetComponent<Graphic>();
            var overlayCanvas = GetComponentInParent<Canvas>(true);
            if (overlayGraphic == null)
            {
                Debug.LogError($"{nameof(ScreenFadeOverlay)} requires a Scene-authored Graphic for a white flash.", this);
                yield break;
            }
            if (overlayCanvas == null)
            {
                Debug.LogError($"{nameof(ScreenFadeOverlay)} requires a Scene-authored parent Canvas for a white flash.", this);
                yield break;
            }

            Color originalColor = overlayGraphic.color;
            float originalAlpha = canvasGroup.alpha;
            bool originalBlocksRaycasts = canvasGroup.blocksRaycasts;
            bool canvasWasActive = overlayCanvas.gameObject.activeSelf;
            overlayGraphic.color = Color.white;
            if (!canvasWasActive) overlayCanvas.gameObject.SetActive(true);
            SetAlpha(canvasWasActive ? originalAlpha : 0f);
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            yield return FadeAlpha(originalAlpha, Mathf.Clamp01(peakAlpha), fadeInSeconds);
            yield return WaitUnscaled(holdSeconds);
            yield return FadeAlpha(canvasGroup.alpha, 0f, fadeOutSeconds);

            overlayGraphic.color = originalColor;
            SetAlpha(originalAlpha);
            canvasGroup.blocksRaycasts = originalBlocksRaycasts;
            if (!canvasWasActive) overlayCanvas.gameObject.SetActive(false);
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

        IEnumerator FadeAlpha(float startAlpha, float targetAlpha, float duration)
        {
            float safeDuration = Mathf.Max(0.01f, duration);
            float elapsed = 0f;
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / safeDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }

            SetAlpha(targetAlpha);
        }

        static IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0f, duration);
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
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
