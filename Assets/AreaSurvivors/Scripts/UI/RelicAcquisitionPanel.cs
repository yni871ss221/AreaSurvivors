using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class RelicAcquisitionPanel : MonoBehaviour
    {
        const float ChestPunchDuration = 0.5f;
        const float ChestSquashDuration = 0.36f;
        const float ItemRevealDuration = 0.75f;
        const float FxFadeDuration = 0.35f;
        const float TextFadeDuration = 0.35f;

        public CanvasGroup rootGroup;
        public Image closedChestImage;
        public Image openChestImage;
        public Image itemImage;
        public Image backFxImage;
        public Image backFxShinyImage;
        public Text titleText;
        public Text relicNameText;
        public Image rarityBadgeImage;
        public Text rarityText;
        public Text descriptionText;
        public Text effectText;
        public Button openButton;
        public Button closeButton;

        Vector2 itemStartPosition;
        Vector2 itemTargetPosition = new Vector2(0f, 70f);
        Vector3 closedChestStartScale;
        float previousTimeScale = 1f;
        int duplicateTokenReward;
        Action closeAction;
        Coroutine animationRoutine;

        void Awake()
        {
            if (itemImage != null) itemStartPosition = itemImage.rectTransform.anchoredPosition;
            if (closedChestImage != null) closedChestStartScale = closedChestImage.rectTransform.localScale;
            BindButtons();
        }

        void Update()
        {
            var candidates = ActiveButtons();
            if (UiSelectionUtility.TickControllerSubmit(candidates)) return;
            UiSelectionUtility.ConfigureDirectionalNavigation(candidates);
            UiSelectionUtility.EnsureSelection(candidates);
        }

        public void Show(RelicDefinition definition, Action onClosed)
        {
            Show(definition, 0, onClosed);
        }

        public void Show(RelicDefinition definition, int duplicateTokenReward, Action onClosed)
        {
            closeAction = onClosed;
            this.duplicateTokenReward = Mathf.Max(0, duplicateTokenReward);
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            BindButtons();
            ResetVisuals(definition);
            gameObject.SetActive(true);
            if (rootGroup != null)
            {
                rootGroup.alpha = 1f;
                rootGroup.interactable = true;
                rootGroup.blocksRaycasts = true;
            }

            if (openButton != null)
            {
                openButton.gameObject.SetActive(true);
                ConfigureButtonFocus(openButton);
                openButton.Select();
            }
        }

        void BindButtons()
        {
            if (openButton != null)
            {
                ConfigureButtonFocus(openButton);
                openButton.onClick.RemoveListener(OnClickOpen);
                openButton.onClick.AddListener(OnClickOpen);
            }

            if (closeButton != null)
            {
                ConfigureButtonFocus(closeButton);
                closeButton.onClick.RemoveListener(OnClickClose);
                closeButton.onClick.AddListener(OnClickClose);
            }
        }

        void ResetVisuals(RelicDefinition definition)
        {
            SetText(titleText, "レリック獲得");
            SetText(relicNameText, definition != null ? definition.displayName : string.Empty);
            SetText(rarityText, definition != null ? RelicCatalog.GetRarityDisplayName(definition.rarity) : string.Empty);
            SetText(descriptionText, DuplicateMessageOrDescription(definition));
            SetText(effectText, duplicateTokenReward > 0 ? "変換トークン +" + duplicateTokenReward : definition != null ? definition.effectText : string.Empty);
            ApplyRarityVisuals(definition);

            if (itemImage != null)
            {
                itemImage.sprite = LoadIcon(definition);
                itemImage.rectTransform.anchoredPosition = itemStartPosition;
                itemImage.rectTransform.localScale = Vector3.one * RelicCatalog.IconScale(definition);
                SetAlpha(itemImage, 0f);
            }

            if (closedChestImage != null)
            {
                closedChestImage.gameObject.SetActive(true);
                closedChestImage.rectTransform.localScale = closedChestStartScale == Vector3.zero ? Vector3.one : closedChestStartScale;
                closedChestImage.rectTransform.localEulerAngles = Vector3.zero;
                SetAlpha(closedChestImage, 1f);
            }

            if (openChestImage != null)
            {
                openChestImage.gameObject.SetActive(false);
                openChestImage.rectTransform.localScale = Vector3.one;
                SetAlpha(openChestImage, 1f);
            }

            if (backFxImage != null)
            {
                backFxImage.rectTransform.anchoredPosition = itemTargetPosition;
                backFxImage.rectTransform.localEulerAngles = Vector3.zero;
                SetAlpha(backFxImage, 0f);
            }

            if (backFxShinyImage != null)
            {
                backFxShinyImage.rectTransform.anchoredPosition = itemTargetPosition;
                backFxShinyImage.rectTransform.localEulerAngles = Vector3.zero;
                SetAlpha(backFxShinyImage, 0f);
            }

            SetAlpha(relicNameText, 0f);
            SetAlpha(rarityBadgeImage, 0f);
            SetAlpha(rarityText, 0f);
            SetAlpha(descriptionText, 0f);
            SetAlpha(effectText, 0f);
            SetButtonVisible(closeButton, false, 0f);
        }

        void OnClickOpen()
        {
            if (animationRoutine != null) StopCoroutine(animationRoutine);
            animationRoutine = StartCoroutine(OpenRoutine());
        }

        IEnumerator OpenRoutine()
        {
            AudioManager.PlaySfx(SfxTrack.RelicChestOpen);
            SetButtonVisible(openButton, false, 0f);

            if (closedChestImage != null)
            {
                var rect = closedChestImage.rectTransform;
                float elapsed = 0f;
                while (elapsed < ChestPunchDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / ChestPunchDuration);
                    float pulse = Mathf.Sin(t * Mathf.PI * 5f) * (1f - t);
                    rect.localScale = closedChestStartScale * (1f + pulse * 0.34f);
                    rect.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 18f * (1f - t));
                    yield return null;
                }

                elapsed = 0f;
                var startScale = rect.localScale;
                var endScale = new Vector3(closedChestStartScale.x * 1.45f, closedChestStartScale.y * 0.52f, closedChestStartScale.z);
                while (elapsed < ChestSquashDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = EaseOutBounce(Mathf.Clamp01(elapsed / ChestSquashDuration));
                    rect.localScale = Vector3.LerpUnclamped(startScale, endScale, t);
                    rect.localEulerAngles = Vector3.zero;
                    yield return null;
                }
            }

            yield return DisplayItemRoutine();
            animationRoutine = null;
        }

        IEnumerator DisplayItemRoutine()
        {
            if (closedChestImage != null) closedChestImage.gameObject.SetActive(false);
            if (openChestImage != null) openChestImage.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < ItemRevealDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / ItemRevealDuration);
                if (openChestImage != null) SetAlpha(openChestImage, 1f - Mathf.Clamp01((t - 0.25f) / 0.75f));
                if (itemImage != null)
                {
                    SetAlpha(itemImage, t);
                    itemImage.rectTransform.anchoredPosition = Vector2.Lerp(itemStartPosition, itemTargetPosition, EaseOutCubic(t));
                }

                yield return null;
            }

            yield return FadeGraphics(FxFadeDuration, t =>
            {
                SetAlpha(backFxImage, t);
                SetAlpha(backFxShinyImage, t * 0.8f);
            });

            StartCoroutine(RotateLoop(backFxImage, 1f));
            StartCoroutine(RotateLoop(backFxShinyImage, -1f));

            yield return FadeGraphics(TextFadeDuration, t =>
            {
                SetAlpha(relicNameText, t);
                SetAlpha(rarityBadgeImage, t);
                SetAlpha(rarityText, t);
                SetAlpha(descriptionText, t);
                SetAlpha(effectText, t);
            });

            yield return FadeGraphics(TextFadeDuration, t => SetButtonVisible(closeButton, true, t));
            if (closeButton != null)
            {
                ConfigureButtonFocus(closeButton);
                UiSelectionUtility.ConfigureDirectionalNavigation(closeButton);
                UiSelectionUtility.SelectFirst(closeButton);
            }
        }

        IEnumerator RotateLoop(Image image, float direction)
        {
            if (image == null) yield break;
            while (gameObject.activeInHierarchy)
            {
                image.rectTransform.Rotate(0f, 0f, 36f * direction * Time.unscaledDeltaTime);
                yield return null;
            }
        }

        IEnumerator FadeGraphics(float duration, Action<float> apply)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                apply?.Invoke(Mathf.Clamp01(elapsed / Mathf.Max(0.001f, duration)));
                yield return null;
            }

            apply?.Invoke(1f);
        }

        void OnClickClose()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            closeAction?.Invoke();
            Destroy(gameObject);
        }

        void ApplyRarityVisuals(RelicDefinition definition)
        {
            if (definition == null) return;
            Color rarityColor = RelicRarityVisuals.GetColor(definition.rarity);
            if (relicNameText != null) relicNameText.color = rarityColor;
            if (rarityBadgeImage != null) rarityBadgeImage.color = rarityColor;
            if (rarityText != null) rarityText.color = RelicRarityVisuals.GetBadgeTextColor(definition.rarity);
        }

        static void SetButtonVisible(Button button, bool active, float alpha)
        {
            if (button == null) return;
            button.gameObject.SetActive(active);
            var graphics = button.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                SetAlpha(graphics[i], alpha);
            }
        }

        Selectable[] ActiveButtons()
        {
            var candidates = new List<Selectable>();
            if (UiSelectionUtility.IsSelectable(openButton)) candidates.Add(openButton);
            if (UiSelectionUtility.IsSelectable(closeButton)) candidates.Add(closeButton);
            return candidates.ToArray();
        }

        static void ConfigureButtonFocus(Button button)
        {
            if (button == null) return;
            button.transition = Selectable.Transition.None;
            var highlight = button.GetComponent<UiSelectionHighlight>();
            if (highlight == null) highlight = button.gameObject.AddComponent<UiSelectionHighlight>();
            highlight.padding = 6f;
            highlight.thickness = 4f;
            highlight.enabled = true;
            if (button.GetComponent<SelectOnPointerEnter>() == null) button.gameObject.AddComponent<SelectOnPointerEnter>();
        }

        static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        string DuplicateMessageOrDescription(RelicDefinition definition)
        {
            if (duplicateTokenReward > 0)
            {
                return "既に獲得済みのレリックのためトークン" + duplicateTokenReward + "に変換します";
            }

            return definition != null ? definition.description : string.Empty;
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }

        static Sprite LoadIcon(RelicDefinition definition)
        {
            var sprite = definition != null ? GeneratedSpriteLoader.Load(definition.iconPath) : null;
            return sprite != null ? sprite : GeneratedSpriteLoader.Load("TreasureChest");
        }

        static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        static float EaseOutBounce(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 1f / 2.75f) return 7.5625f * t * t;
            if (t < 2f / 2.75f)
            {
                t -= 1.5f / 2.75f;
                return 7.5625f * t * t + 0.75f;
            }

            if (t < 2.5f / 2.75f)
            {
                t -= 2.25f / 2.75f;
                return 7.5625f * t * t + 0.9375f;
            }

            t -= 2.625f / 2.75f;
            return 7.5625f * t * t + 0.984375f;
        }
    }
}
