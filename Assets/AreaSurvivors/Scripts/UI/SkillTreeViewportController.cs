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

        public void ResetView()
        {
            zoom = initialZoom;
            if (content == null) return;
            content.localScale = Vector3.one * initialZoom;
            content.anchoredPosition = initialPosition;
        }
    }
}
