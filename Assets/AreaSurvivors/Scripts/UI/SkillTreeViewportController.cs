using UnityEngine;
using UnityEngine.EventSystems;

namespace AreaSurvivors
{
    public sealed class SkillTreeViewportController : MonoBehaviour, IScrollHandler, IDragHandler
    {
        public RectTransform content;
        public float minZoom = 0.3f;
        public float maxZoom = 1.45f;
        public float zoomStep = 0.08f;
        public float focusPadding = 36f;
        float zoom = 1f;
        float initialZoom = 1f;
        Vector2 initialPosition;

        void Awake()
        {
            if (content == null) content = transform.Find("Skill Tree") as RectTransform;
            if (content != null)
            {
                zoom = content.localScale.x;
                initialZoom = zoom;
                initialPosition = content.anchoredPosition;
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (content == null) return;
            float direction = Mathf.Sign(eventData.scrollDelta.y);
            if (Mathf.Approximately(direction, 0f)) return;
            zoom = Mathf.Clamp(zoom + direction * zoomStep, minZoom, maxZoom);
            content.localScale = Vector3.one * zoom;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (content == null) return;
            content.anchoredPosition += eventData.delta;
        }

        public void EnsureVisible(RectTransform target, float padding)
        {
            if (content == null || target == null) return;
            if (!target.IsChildOf(content)) return;

            var viewport = transform as RectTransform;
            if (viewport == null) return;

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            var viewportRect = viewport.rect;
            float resolvedPadding = padding > 0f ? padding : focusPadding;
            Vector2 delta = Vector2.zero;

            if (bounds.max.x > viewportRect.xMax - resolvedPadding)
            {
                delta.x = viewportRect.xMax - resolvedPadding - bounds.max.x;
            }
            else if (bounds.min.x < viewportRect.xMin + resolvedPadding)
            {
                delta.x = viewportRect.xMin + resolvedPadding - bounds.min.x;
            }

            if (bounds.max.y > viewportRect.yMax - resolvedPadding)
            {
                delta.y = viewportRect.yMax - resolvedPadding - bounds.max.y;
            }
            else if (bounds.min.y < viewportRect.yMin + resolvedPadding)
            {
                delta.y = viewportRect.yMin + resolvedPadding - bounds.min.y;
            }

            if (Mathf.Approximately(delta.x, 0f) && Mathf.Approximately(delta.y, 0f)) return;
            content.anchoredPosition += delta;
        }

        public void ResetView()
        {
            zoom = initialZoom;
            if (content == null) return;
            content.localScale = Vector3.one * initialZoom;
            content.anchoredPosition = initialPosition;
        }
    }
}
