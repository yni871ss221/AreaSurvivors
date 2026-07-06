using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class GameOverIntroAnimator : MonoBehaviour
    {
        [System.Serializable]
        public sealed class AnimatedItem
        {
            public RectTransform rect;
            public CanvasGroup group;
            public CanvasGroup[] extraGroups;
            public RectTransform[] extraRects;
            [System.NonSerialized] public Vector2[] extraTargetPositions;
        }

        public AnimatedItem title;
        public CanvasGroup subtitleGroup;
        public AnimatedItem[] resultItems;
        public AnimatedItem stageUnlockPopupItem;
        public Button stageUnlockOkButton;
        public RectTransform missionCompleteTextRect;
        public AnimatedItem lobbyButtonItem;
        public Button lobbyButton;
        public Vector2 titleStartOffset = new Vector2(0f, -48f);
        public Vector2 itemStartOffset = new Vector2(0f, -22f);
        public float initialDelay = 0.16f;
        public float titleDuration = 1.1f;
        public float subtitleDelay = 0.08f;
        public float itemDelay = 0.06f;
        public float itemDuration = 0.42f;
        public float buttonDelay = 0.08f;
        public float buttonDuration = 0.72f;
        public float stageUnlockPopupDuration = 0.45f;
        public float missionCompleteCheerDelay = 3f;
        public int panelRevealSfxCount = 7;

        Vector2 titleTargetPosition;
        Vector2[] itemTargetPositions;
        Vector2 buttonTargetPosition;
        Coroutine playingRoutine;
        bool currentGameClear;
        bool currentStageUnlockPopup;
        bool currentMissionCompletePopup;
        bool stageUnlockOkPressed;

        void Awake()
        {
            CaptureScenePositions();
            SetLobbyButtonInteractable(false);
            ApplyInitialState();
        }

        void OnDisable()
        {
            SetLobbyButtonInteractable(true);
            if (stageUnlockOkButton != null) stageUnlockOkButton.onClick.RemoveListener(OnStageUnlockOkClicked);
        }

        public void Play(bool gameClear, bool showStageUnlockPopup = false, bool missionCompletePopup = false)
        {
            if (playingRoutine != null) StopCoroutine(playingRoutine);
            currentGameClear = gameClear;
            currentStageUnlockPopup = showStageUnlockPopup;
            currentMissionCompletePopup = missionCompletePopup;
            playingRoutine = StartCoroutine(PlayRoutine());
        }

        IEnumerator PlayRoutine()
        {
            SetLobbyButtonInteractable(false);
            ApplyInitialState();
            AudioManager.PlaySfx(currentGameClear ? SfxTrack.GameClear : SfxTrack.GameOver);

            if (initialDelay > 0f) yield return Wait(initialDelay);

            yield return Animate(title, titleTargetPosition + titleStartOffset, titleTargetPosition, titleDuration);

            if (subtitleDelay > 0f) yield return Wait(subtitleDelay);
            ShowImmediately(subtitleGroup);

            if (resultItems != null)
            {
                for (int i = 0; i < resultItems.Length; i++)
                {
                    var item = resultItems[i];
                    if (!IsValid(item)) continue;
                    if (!item.rect.gameObject.activeInHierarchy) continue;

                    if (i < panelRevealSfxCount) AudioManager.PlaySfx(SfxTrack.ResultPanelReveal);
                    yield return Animate(item, itemTargetPositions[i] + itemStartOffset, itemTargetPositions[i], itemDuration);
                    if (itemDelay > 0f) yield return Wait(itemDelay);
                }
            }

            if (currentStageUnlockPopup)
            {
                yield return ShowStageUnlockPopup();
            }

            if (buttonDelay > 0f) yield return Wait(buttonDelay);
            yield return Animate(lobbyButtonItem, buttonTargetPosition + itemStartOffset, buttonTargetPosition, buttonDuration);
            SetLobbyButtonInteractable(true);
            playingRoutine = null;
        }

        IEnumerator ShowStageUnlockPopup()
        {
            if (!IsValid(stageUnlockPopupItem)) yield break;

            stageUnlockOkPressed = false;
            SetStageUnlockButtonInteractable(false);
            if (stageUnlockOkButton != null)
            {
                stageUnlockOkButton.onClick.RemoveListener(OnStageUnlockOkClicked);
                stageUnlockOkButton.onClick.AddListener(OnStageUnlockOkClicked);
            }

            Coroutine bounceRoutine = null;
            if (currentMissionCompletePopup)
            {
                AudioManager.PlaySfx(SfxTrack.MissionCompleteFanfare);
                StartCoroutine(PlayMissionCompleteCheerDelayed());
            }
            else
            {
                AudioManager.PlaySfx(SfxTrack.StageUnlockPopup);
            }

            yield return Animate(stageUnlockPopupItem, Vector2.zero, Vector2.zero, stageUnlockPopupDuration);
            if (currentMissionCompletePopup) bounceRoutine = StartCoroutine(BounceMissionCompleteText());
            SetStageUnlockButtonInteractable(true);

            while (!stageUnlockOkPressed)
            {
                yield return null;
            }

            if (bounceRoutine != null) StopCoroutine(bounceRoutine);
            if (missionCompleteTextRect != null) missionCompleteTextRect.localScale = Vector3.one;
            SetStageUnlockButtonInteractable(false);
            HideImmediately(stageUnlockPopupItem);
        }

        IEnumerator PlayMissionCompleteCheerDelayed()
        {
            yield return Wait(missionCompleteCheerDelay);
            AudioManager.PlaySfx(SfxTrack.MissionCompleteCheer);
        }

        IEnumerator BounceMissionCompleteText()
        {
            if (missionCompleteTextRect == null) yield break;

            const float cycleSeconds = 0.58f;
            while (!stageUnlockOkPressed)
            {
                float phase = Mathf.PingPong(Time.unscaledTime / cycleSeconds, 1f);
                float scale = Mathf.Lerp(1f, 1.16f, EaseOutCubic(phase));
                missionCompleteTextRect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            missionCompleteTextRect.localScale = Vector3.one;
        }

        IEnumerator Animate(AnimatedItem item, Vector2 startPosition, Vector2 targetPosition, float duration)
        {
            if (!IsValid(item)) yield break;

            duration = Mathf.Max(0.001f, duration);
            float elapsed = 0f;
            item.group.alpha = 0f;
            item.group.blocksRaycasts = false;
            item.group.interactable = false;
            SetExtraGroups(item, 0f);
            item.rect.anchoredPosition = startPosition;
            SetExtraRectOffset(item, itemStartOffset);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = EaseOutCubic(t);
                item.group.alpha = eased;
                SetExtraGroups(item, eased);
                item.rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, eased);
                SetExtraRectOffset(item, Vector2.LerpUnclamped(itemStartOffset, Vector2.zero, eased));
                yield return null;
            }

            item.group.alpha = 1f;
            item.group.blocksRaycasts = true;
            item.group.interactable = true;
            SetExtraGroups(item, 1f);
            item.rect.anchoredPosition = targetPosition;
            SetExtraRectOffset(item, Vector2.zero);
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
            if (title != null && title.rect != null) titleTargetPosition = title.rect.anchoredPosition;
            CaptureExtraScenePositions(title);

            if (resultItems == null) resultItems = new AnimatedItem[0];
            itemTargetPositions = new Vector2[resultItems.Length];
            for (int i = 0; i < resultItems.Length; i++)
            {
                if (resultItems[i] != null && resultItems[i].rect != null)
                {
                    itemTargetPositions[i] = resultItems[i].rect.anchoredPosition;
                }

                CaptureExtraScenePositions(resultItems[i]);
            }

            if (lobbyButtonItem != null && lobbyButtonItem.rect != null)
            {
                buttonTargetPosition = lobbyButtonItem.rect.anchoredPosition;
            }
            CaptureExtraScenePositions(lobbyButtonItem);
            CaptureExtraScenePositions(stageUnlockPopupItem);
        }

        void ApplyInitialState()
        {
            ApplyItemInitialState(title, titleTargetPosition + titleStartOffset);
            SetGroupAlpha(subtitleGroup, 0f);

            if (resultItems != null)
            {
                for (int i = 0; i < resultItems.Length; i++)
                {
                    ApplyItemInitialState(resultItems[i], itemTargetPositions[i] + itemStartOffset);
                }
            }

            ApplyItemInitialState(lobbyButtonItem, buttonTargetPosition + itemStartOffset);
            ApplyStageUnlockInitialState();
        }

        static void ApplyItemInitialState(AnimatedItem item, Vector2 startPosition)
        {
            if (!IsValid(item)) return;

            item.group.alpha = 0f;
            item.group.blocksRaycasts = false;
            item.group.interactable = false;
            SetExtraGroups(item, 0f);
            var extraOffset = startPosition - item.rect.anchoredPosition;
            item.rect.anchoredPosition = startPosition;
            SetExtraRectOffset(item, extraOffset);
        }

        static void ShowImmediately(CanvasGroup group)
        {
            if (group == null) return;

            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        static void HideImmediately(AnimatedItem item)
        {
            if (!IsValid(item)) return;
            item.group.alpha = 0f;
            item.group.blocksRaycasts = false;
            item.group.interactable = false;
            SetExtraGroups(item, 0f);
            item.rect.gameObject.SetActive(false);
        }

        static void SetGroupAlpha(CanvasGroup group, float alpha)
        {
            if (group == null) return;

            group.alpha = alpha;
            group.blocksRaycasts = alpha >= 1f;
            group.interactable = alpha >= 1f;
        }

        static void SetExtraGroups(AnimatedItem item, float alpha)
        {
            if (item == null || item.extraGroups == null) return;

            foreach (var group in item.extraGroups)
            {
                SetGroupAlpha(group, alpha);
            }
        }

        static void CaptureExtraScenePositions(AnimatedItem item)
        {
            if (item == null || item.extraRects == null)
            {
                if (item != null) item.extraTargetPositions = new Vector2[0];
                return;
            }

            item.extraTargetPositions = new Vector2[item.extraRects.Length];
            for (int i = 0; i < item.extraRects.Length; i++)
            {
                if (item.extraRects[i] != null)
                {
                    item.extraTargetPositions[i] = item.extraRects[i].anchoredPosition;
                }
            }
        }

        static void SetExtraRectOffset(AnimatedItem item, Vector2 offset)
        {
            if (item == null || item.extraRects == null) return;

            for (int i = 0; i < item.extraRects.Length; i++)
            {
                var rect = item.extraRects[i];
                if (rect == null) continue;

                var target = item.extraTargetPositions != null && i < item.extraTargetPositions.Length
                    ? item.extraTargetPositions[i]
                    : rect.anchoredPosition;
                rect.anchoredPosition = target + offset;
            }
        }

        void SetLobbyButtonInteractable(bool interactable)
        {
            if (lobbyButton != null) lobbyButton.interactable = interactable;
        }

        void SetStageUnlockButtonInteractable(bool interactable)
        {
            if (stageUnlockOkButton != null) stageUnlockOkButton.interactable = interactable;
        }

        void OnStageUnlockOkClicked()
        {
            stageUnlockOkPressed = true;
        }

        void ApplyStageUnlockInitialState()
        {
            if (!IsValid(stageUnlockPopupItem)) return;
            if (currentStageUnlockPopup)
            {
                stageUnlockPopupItem.rect.gameObject.SetActive(true);
                ApplyItemInitialState(stageUnlockPopupItem, Vector2.zero);
                SetStageUnlockButtonInteractable(false);
                return;
            }

            HideImmediately(stageUnlockPopupItem);
        }

        static bool IsValid(AnimatedItem item)
        {
            return item != null && item.rect != null && item.group != null;
        }

        static float EaseOutCubic(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }
    }
}
