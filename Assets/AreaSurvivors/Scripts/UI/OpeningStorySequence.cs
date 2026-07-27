using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class OpeningStorySequence : MonoBehaviour
    {
        public CanvasGroup[] sceneGroups;
        public Image backgroundDimmer;
        public Text captionText;
        public CanvasGroup captionGroup;
        [TextArea] public string[] captions;
        public Button startGameButton;
        public CanvasGroup startGameButtonGroup;
        public float fadeInDuration = 0.8f;
        public float holdDuration = 2.4f;
        public float fadeOutDuration = 0.6f;
        public float totalSequenceDuration = 36f;
        public float slideDistance = 36f;

        static readonly Vector2[] SlideDirections =
        {
            Vector2.left,
            new Vector2(1f, 1f).normalized,
            Vector2.down,
            new Vector2(-1f, 1f).normalized,
            Vector2.right
        };

        Action<bool> completed;
        Coroutine sequenceRoutine;
        bool completing;
        bool sequenceActive;
        float skipInputEnabledAt;

        public bool IsVisible => gameObject.activeSelf;

        void Update()
        {
            if (!sequenceActive || completing || Time.unscaledTime < skipInputEnabledAt) return;
            if (Input.anyKeyDown) CompleteStory(true);
        }

        public void Play(Action<bool> onCompleted)
        {
            completed = onCompleted;
            completing = false;
            sequenceActive = true;
            skipInputEnabledAt = Time.unscaledTime + 0.35f;
            gameObject.SetActive(true);
            ResetVisuals();
            AudioManager.PlayBgm(BgmTrack.OpeningStory);
            if (sequenceRoutine != null) StopCoroutine(sequenceRoutine);
            sequenceRoutine = StartCoroutine(PlayRoutine());
        }

        void OnDisable()
        {
            if (!sequenceActive) return;
            sequenceActive = false;
            AudioManager.StopBgm();
        }

        void ResetVisuals()
        {
            if (sceneGroups != null)
            {
                foreach (var group in sceneGroups)
                {
                    if (group == null) continue;
                    group.alpha = 0f;
                    group.interactable = false;
                    group.blocksRaycasts = false;
                    var slideEffect = group.GetComponent<OpeningStorySlideEffect>();
                    if (slideEffect != null) slideEffect.Offset = Vector2.zero;
                }
            }

            if (captionText != null)
            {
                captionText.gameObject.SetActive(true);
                captionText.text = string.Empty;
            }
            if (captionGroup != null)
            {
                captionGroup.alpha = 0f;
                captionGroup.interactable = false;
                captionGroup.blocksRaycasts = false;
            }
            if (startGameButton != null) startGameButton.gameObject.SetActive(false);
            if (startGameButtonGroup != null)
            {
                startGameButtonGroup.alpha = 0f;
                startGameButtonGroup.interactable = false;
                startGameButtonGroup.blocksRaycasts = false;
            }
        }

        IEnumerator PlayRoutine()
        {
            if (sceneGroups == null || sceneGroups.Length == 0)
            {
                CompleteStory(false);
                yield break;
            }

            int validSceneCount = 0;
            for (int i = 0; i < sceneGroups.Length; i++)
            {
                if (sceneGroups[i] != null) validSceneCount++;
            }

            if (validSceneCount == 0)
            {
                CompleteStory(false);
                yield break;
            }

            float fallbackSceneDuration = Mathf.Max(0.001f, fadeInDuration + holdDuration + fadeOutDuration);
            float sequenceDuration = totalSequenceDuration > 0f
                ? totalSequenceDuration
                : fallbackSceneDuration * validSceneCount;
            float sceneDuration = sequenceDuration / validSceneCount;
            float sequenceStartedAt = Time.unscaledTime;
            int displayedSceneIndex = 0;

            for (int i = 0; i < sceneGroups.Length; i++)
            {
                var group = sceneGroups[i];
                if (group == null) continue;
                Vector2 direction = SlideDirections[displayedSceneIndex % SlideDirections.Length];
                float sceneStartedAt = sequenceStartedAt + sceneDuration * displayedSceneIndex;
                yield return PlayScene(group, displayedSceneIndex, direction, sceneStartedAt, sceneDuration);
                displayedSceneIndex++;
            }

            sequenceRoutine = null;
            CompleteStory(false);
        }

        void CompleteStory(bool skipped)
        {
            if (completing) return;
            completing = true;
            sequenceActive = false;
            if (sequenceRoutine != null)
            {
                StopCoroutine(sequenceRoutine);
                sequenceRoutine = null;
            }

            ResetVisuals();
            AudioManager.StopBgm();
            completed?.Invoke(skipped);
        }

        IEnumerator PlayScene(CanvasGroup group, int captionIndex, Vector2 direction, float sceneStartedAt, float sceneDuration)
        {
            if (group == null) yield break;

            bool hasCaption = PrepareCaption(captionIndex);
            var slideEffect = group.GetComponent<OpeningStorySlideEffect>();
            float totalDuration = Mathf.Max(0.001f, sceneDuration);
            float fadeIn = Mathf.Min(Mathf.Max(0f, fadeInDuration), totalDuration);
            float fadeOut = Mathf.Min(Mathf.Max(0f, fadeOutDuration), totalDuration - fadeIn);
            float hold = Mathf.Max(0f, totalDuration - fadeIn - fadeOut);
            Vector2 startOffset = -direction * slideDistance;
            Vector2 endOffset = direction * slideDistance;
            group.alpha = 0f;
            if (slideEffect != null) slideEffect.Offset = startOffset;

            while (true)
            {
                float elapsed = Time.unscaledTime - sceneStartedAt;
                if (elapsed >= totalDuration) break;
                float sceneProgress = Mathf.Clamp01(elapsed / totalDuration);
                if (slideEffect != null)
                {
                    slideEffect.Offset = Vector2.Lerp(startOffset, endOffset, sceneProgress);
                }

                if (fadeIn > 0f && elapsed < fadeIn)
                {
                    SetSceneAlpha(group, hasCaption, Mathf.SmoothStep(0f, 1f, elapsed / fadeIn));
                }
                else if (fadeOut > 0f && elapsed > fadeIn + hold)
                {
                    float fadeProgress = (elapsed - fadeIn - hold) / fadeOut;
                    SetSceneAlpha(group, hasCaption, Mathf.SmoothStep(1f, 0f, fadeProgress));
                }
                else
                {
                    SetSceneAlpha(group, hasCaption, 1f);
                }

                yield return null;
            }

            group.alpha = 0f;
            if (captionGroup != null) captionGroup.alpha = 0f;
            if (slideEffect != null) slideEffect.Offset = endOffset;
        }

        bool PrepareCaption(int index)
        {
            if (captionText == null || captionGroup == null) return false;

            string source = captions != null && index >= 0 && index < captions.Length
                ? captions[index]
                : string.Empty;
            bool hasCaption = !string.IsNullOrWhiteSpace(source);
            captionText.gameObject.SetActive(hasCaption);
            captionText.text = hasCaption ? LocalizationService.LocalizeSource(source) : string.Empty;
            captionGroup.alpha = 0f;
            return hasCaption;
        }

        void SetSceneAlpha(CanvasGroup sceneGroup, bool hasCaption, float alpha)
        {
            sceneGroup.alpha = alpha;
            if (captionGroup != null) captionGroup.alpha = hasCaption ? alpha : 0f;
        }
    }
}
