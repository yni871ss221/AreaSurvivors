using UnityEngine;
using UnityEngine.EventSystems;

namespace AreaSurvivors
{
    public sealed class OptionsPanelScrollController : MonoBehaviour, IScrollHandler, IDragHandler
    {
        [SerializeField] RectTransform content;
        [SerializeField] RectTransform viewport;
        [SerializeField] float scrollSensitivity = 64f;
        [SerializeField] float dragSensitivity = 1f;
        [SerializeField] float bottomPadding = 32f;
        [SerializeField] bool resetOnEnable = true;

        Vector2 initialAnchoredPosition;
        bool initialized;

        void Awake()
        {
            Initialize();
        }

        public void Configure(RectTransform content, RectTransform viewport)
        {
            this.content = content;
            this.viewport = viewport;
            initialized = false;
            Initialize();
        }

        void OnEnable()
        {
            Initialize();
            if (resetOnEnable && content != null)
            {
                content.anchoredPosition = initialAnchoredPosition;
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (eventData == null) return;
            Move(-eventData.scrollDelta.y * scrollSensitivity);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null) return;
            Move(eventData.delta.y * dragSensitivity);
        }

        public void EnsureVisible(RectTransform target, float padding)
        {
            Initialize();
            if (content == null || viewport == null || target == null) return;
            if (!target.IsChildOf(content)) return;

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            var viewportRect = viewport.rect;
            float deltaY = 0f;

            if (bounds.max.y > viewportRect.yMax - padding)
            {
                deltaY = viewportRect.yMax - padding - bounds.max.y;
            }
            else if (bounds.min.y < viewportRect.yMin + padding)
            {
                deltaY = viewportRect.yMin + padding - bounds.min.y;
            }

            Move(deltaY);
        }

        void Initialize()
        {
            if (initialized) return;
            if (content == null) content = transform as RectTransform;
            if (viewport == null && content != null)
            {
                var canvas = content.GetComponentInParent<Canvas>();
                viewport = canvas != null ? canvas.transform as RectTransform : content.parent as RectTransform;
            }

            if (content != null)
            {
                initialAnchoredPosition = content.anchoredPosition;
            }

            initialized = true;
        }

        void Move(float deltaY)
        {
            Initialize();
            if (content == null || Mathf.Approximately(deltaY, 0f)) return;

            float maxOffset = CalculateMaxOffset();
            var position = content.anchoredPosition;
            position.y = Mathf.Clamp(position.y + deltaY, initialAnchoredPosition.y, initialAnchoredPosition.y + maxOffset);
            content.anchoredPosition = position;
        }

        float CalculateMaxOffset()
        {
            if (content == null || viewport == null) return 0f;

            float contentHeight = content.rect.height;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, content);
            if (bounds.size.y > 0f) contentHeight = Mathf.Max(contentHeight, bounds.size.y);

            float viewportHeight = viewport.rect.height;
            if (viewportHeight <= 0f) viewportHeight = Screen.height;
            return Mathf.Max(0f, contentHeight - viewportHeight + bottomPadding);
        }
    }
}
