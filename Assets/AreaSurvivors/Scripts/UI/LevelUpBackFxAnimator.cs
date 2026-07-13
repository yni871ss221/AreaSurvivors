using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class LevelUpBackFxAnimator : MonoBehaviour
    {
        const float FadeDuration = 0.35f;
        const float RotationSpeed = 36f;

        public GameObject effectRoot;
        public Image backFxImage;
        public Image backFxShinyImage;

        Coroutine fadeRoutine;

        void OnEnable()
        {
            if (effectRoot != null) effectRoot.SetActive(true);

            ResetImage(backFxImage);
            ResetImage(backFxShinyImage);
            fadeRoutine = StartCoroutine(FadeIn());
        }

        void OnDisable()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            SetAlpha(backFxImage, 0f);
            SetAlpha(backFxShinyImage, 0f);
            if (effectRoot != null) effectRoot.SetActive(false);
        }

        void Update()
        {
            float angle = RotationSpeed * Time.unscaledDeltaTime;
            Rotate(backFxImage, angle);
            Rotate(backFxShinyImage, -angle);
        }

        IEnumerator FadeIn()
        {
            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / FadeDuration);
                float alpha = Mathf.SmoothStep(0f, 1f, progress);
                SetAlpha(backFxImage, alpha);
                SetAlpha(backFxShinyImage, alpha);
                yield return null;
            }

            SetAlpha(backFxImage, 1f);
            SetAlpha(backFxShinyImage, 1f);
            fadeRoutine = null;
        }

        static void ResetImage(Image image)
        {
            if (image == null) return;
            image.rectTransform.localEulerAngles = Vector3.zero;
            SetAlpha(image, 0f);
        }

        static void Rotate(Image image, float angle)
        {
            if (image == null || !image.gameObject.activeInHierarchy) return;
            image.rectTransform.Rotate(0f, 0f, angle);
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
