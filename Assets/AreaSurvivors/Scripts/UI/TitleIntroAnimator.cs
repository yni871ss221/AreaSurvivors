using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class TitleIntroAnimator : MonoBehaviour
    {
        public RectTransform panel;
        public CanvasGroup panelGroup;
        public RectTransform[] buttons;
        public CanvasGroup[] buttonGroups;
        public Button[] interactiveButtons;
        public Vector2 panelStartOffset = new Vector2(0f, -56f);
        public Vector2 buttonStartOffset = new Vector2(0f, -28f);
        public float initialDelay = 0.3f;
        public float panelDuration = 1.35f;
        public float buttonDelay = 0.24f;
        public float buttonDuration = 0.8f;
        public float buttonStagger = 0.22f;

        Vector2 panelTargetPosition;
        Vector2[] buttonTargetPositions;

        void Awake()
        {
            CaptureScenePositions();
            SetButtonsInteractable(false);
            ApplyPanelState(0f);
            ApplyButtonStates(0f);
        }

        void Start()
        {
            StartCoroutine(PlayIntro());
        }

        void OnDisable()
        {
            SetButtonsInteractable(true);
        }

        IEnumerator PlayIntro()
        {
            if (initialDelay > 0f) yield return Wait(initialDelay);

            yield return Animate(panelGroup, panel, panelTargetPosition + panelStartOffset, panelTargetPosition, panelDuration);

            if (buttonDelay > 0f) yield return Wait(buttonDelay);

            float lastButtonEnd = 0f;
            for (int i = 0; i < buttons.Length; i++)
            {
                var rect = buttons[i];
                var group = GetButtonGroup(i);
                if (rect == null || group == null) continue;

                StartCoroutine(Animate(group, rect, buttonTargetPositions[i] + buttonStartOffset, buttonTargetPositions[i], buttonDuration));
                lastButtonEnd = Time.unscaledTime + buttonDuration;
                if (buttonStagger > 0f) yield return Wait(buttonStagger);
            }

            while (Time.unscaledTime < lastButtonEnd)
            {
                yield return null;
            }

            SetButtonsInteractable(true);
        }

        IEnumerator Animate(CanvasGroup group, RectTransform rect, Vector2 startPosition, Vector2 targetPosition, float duration)
        {
            if (group == null || rect == null) yield break;

            float elapsed = 0f;
            duration = Mathf.Max(0.001f, duration);
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            rect.anchoredPosition = startPosition;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutCubic(t);
                group.alpha = eased;
                rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
                yield return null;
            }

            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            rect.anchoredPosition = targetPosition;
        }

        IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
        }

        void CaptureScenePositions()
        {
            if (panel != null) panelTargetPosition = panel.anchoredPosition;

            if (buttons == null)
            {
                buttons = new RectTransform[0];
            }

            buttonTargetPositions = new Vector2[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null) buttonTargetPositions[i] = buttons[i].anchoredPosition;
            }
        }

        void ApplyPanelState(float alpha)
        {
            if (panelGroup != null)
            {
                panelGroup.alpha = alpha;
                panelGroup.blocksRaycasts = alpha >= 1f;
                panelGroup.interactable = alpha >= 1f;
            }

            if (panel != null) panel.anchoredPosition = panelTargetPosition + panelStartOffset;
        }

        void ApplyButtonStates(float alpha)
        {
            if (buttons == null) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                var group = GetButtonGroup(i);
                if (group != null)
                {
                    group.alpha = alpha;
                    group.blocksRaycasts = false;
                    group.interactable = false;
                }

                if (buttons[i] != null) buttons[i].anchoredPosition = buttonTargetPositions[i] + buttonStartOffset;
            }
        }

        void SetButtonsInteractable(bool interactable)
        {
            if (interactiveButtons == null) return;

            foreach (var button in interactiveButtons)
            {
                if (button != null) button.interactable = interactable;
            }
        }

        CanvasGroup GetButtonGroup(int index)
        {
            return buttonGroups != null && index >= 0 && index < buttonGroups.Length ? buttonGroups[index] : null;
        }

        static float EaseOutCubic(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }
    }
}
