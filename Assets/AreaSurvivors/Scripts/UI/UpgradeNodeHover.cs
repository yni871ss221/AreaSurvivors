using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class UpgradeNodeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        const float DefaultSpacing = 18f;
        const float CanvasPadding = 16f;
        const float CursorPadding = 30f;
        const float FocusPointerOffset = 42f;

        static readonly Vector3[] WorldCorners = new Vector3[4];

        public Text title;
        public Text description;
        public RectTransform tooltipRoot;
        public RectTransform canvasRoot;
        public RectTransform targetRect;
        public Canvas canvas;
        public string titleText;
        public string descriptionText;

        public static UpgradeNodeHover PointerHover { get; private set; }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!UiSelectionUtility.PointerCanDriveFocus()) return;
            UiSelectionUtility.NotifyKeyboardMouseInput();
            PointerHover = this;
            Show(eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (!UiSelectionUtility.PointerCanDriveFocus()) return;
            UiSelectionUtility.NotifyKeyboardMouseInput();
            PointerHover = this;
            Show(eventData);
            PositionTooltip(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (PointerHover == this) PointerHover = null;
            Hide();
        }

        void OnDisable()
        {
            if (PointerHover == this) PointerHover = null;
        }

        public void ShowForFocus()
        {
            Show(null, FocusPointerInCanvas());
        }

        public void Hide()
        {
            if (tooltipRoot != null) tooltipRoot.gameObject.SetActive(false);
        }

        void Show(PointerEventData eventData, Vector2? fallbackPointer = null)
        {
            if (title != null) title.text = titleText;
            if (description != null) description.text = descriptionText;
            if (tooltipRoot != null)
            {
                tooltipRoot.gameObject.SetActive(true);
                tooltipRoot.SetAsLastSibling();
                PositionTooltip(eventData, fallbackPointer);
            }
        }

        void PositionTooltip(PointerEventData eventData, Vector2? fallbackPointer = null)
        {
            if (tooltipRoot == null || canvasRoot == null || targetRect == null) return;

            var uiCamera = UiCamera(eventData);
            Rect canvasRect = canvasRoot.rect;
            Rect target = RectInCanvas(targetRect, uiCamera);
            Vector2 pointer = PointerInCanvas(eventData, uiCamera, fallbackPointer);
            Vector2 size = TooltipSize();
            Vector2 center = ChooseSidePosition(canvasRect, target, size, pointer);
            center = AvoidPointer(canvasRect, center, size, pointer);
            center = AvoidTarget(canvasRect, target, center, size, pointer);
            tooltipRoot.anchoredPosition = ClampCenter(canvasRect, center, size);
        }

        Camera UiCamera(PointerEventData eventData)
        {
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                return canvas.worldCamera != null ? canvas.worldCamera : eventData != null ? eventData.pressEventCamera : null;
            }

            return null;
        }

        Rect RectInCanvas(RectTransform rect, Camera uiCamera)
        {
            rect.GetWorldCorners(WorldCorners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < WorldCorners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, WorldCorners[i]);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screen, uiCamera, out var local);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        Vector2 PointerInCanvas(PointerEventData eventData, Camera uiCamera, Vector2? fallbackPointer)
        {
            if (fallbackPointer.HasValue) return fallbackPointer.Value;
            if (eventData == null) return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, eventData.position, uiCamera, out var local);
            return local;
        }

        Vector2 FocusPointerInCanvas()
        {
            if (canvasRoot == null || targetRect == null) return Vector2.zero;
            Rect target = RectInCanvas(targetRect, UiCamera(null));
            return new Vector2(target.xMin - FocusPointerOffset, target.center.y);
        }

        Vector2 TooltipSize()
        {
            Vector2 size = tooltipRoot.rect.size;
            if (size.x <= 0f || size.y <= 0f) size = tooltipRoot.sizeDelta;
            return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        }

        Vector2 ChooseSidePosition(Rect canvasRect, Rect target, Vector2 size, Vector2 pointer)
        {
            float rightSpace = canvasRect.xMax - target.xMax - DefaultSpacing;
            float leftSpace = target.xMin - canvasRect.xMin - DefaultSpacing;
            bool preferRight = pointer.x <= target.center.x;
            bool useRight = preferRight
                ? rightSpace >= size.x || rightSpace >= leftSpace
                : !(leftSpace >= size.x || leftSpace >= rightSpace);

            float x = useRight
                ? target.xMax + DefaultSpacing + size.x * 0.5f
                : target.xMin - DefaultSpacing - size.x * 0.5f;
            return new Vector2(x, target.center.y);
        }

        Vector2 AvoidPointer(Rect canvasRect, Vector2 center, Vector2 size, Vector2 pointer)
        {
            Rect tooltip = RectFromCenter(center, size);
            Rect cursor = Rect.MinMaxRect(
                pointer.x - CursorPadding,
                pointer.y - CursorPadding,
                pointer.x + CursorPadding,
                pointer.y + CursorPadding);
            if (!tooltip.Overlaps(cursor)) return center;

            float above = pointer.y + CursorPadding + size.y * 0.5f;
            float below = pointer.y - CursorPadding - size.y * 0.5f;
            center.y = Mathf.Abs(above - center.y) < Mathf.Abs(below - center.y) ? below : above;
            return ClampCenter(canvasRect, center, size);
        }

        Vector2 AvoidTarget(Rect canvasRect, Rect target, Vector2 center, Vector2 size, Vector2 pointer)
        {
            if (!RectFromCenter(center, size).Overlaps(target)) return ClampCenter(canvasRect, center, size);

            float aboveSpace = canvasRect.yMax - target.yMax - DefaultSpacing;
            float belowSpace = target.yMin - canvasRect.yMin - DefaultSpacing;
            bool useAbove = pointer.y <= target.center.y
                ? aboveSpace >= size.y || aboveSpace >= belowSpace
                : !(belowSpace >= size.y || belowSpace >= aboveSpace);

            center.x = target.center.x;
            center.y = useAbove
                ? target.yMax + DefaultSpacing + size.y * 0.5f
                : target.yMin - DefaultSpacing - size.y * 0.5f;
            return ClampCenter(canvasRect, center, size);
        }

        Vector2 ClampCenter(Rect canvasRect, Vector2 center, Vector2 size)
        {
            float halfWidth = size.x * 0.5f;
            float halfHeight = size.y * 0.5f;
            center.x = Mathf.Clamp(center.x, canvasRect.xMin + halfWidth + CanvasPadding, canvasRect.xMax - halfWidth - CanvasPadding);
            center.y = Mathf.Clamp(center.y, canvasRect.yMin + halfHeight + CanvasPadding, canvasRect.yMax - halfHeight - CanvasPadding);
            return center;
        }

        static Rect RectFromCenter(Vector2 center, Vector2 size)
        {
            return new Rect(center - size * 0.5f, size);
        }
    }
}
