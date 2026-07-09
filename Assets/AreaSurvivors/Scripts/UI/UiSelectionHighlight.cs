using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class UiSelectionHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public bool forceSelected;
        public float padding = 5f;
        public float thickness = 4f;
        public Color color = new Color(1f, 0.86f, 0.18f, 1f);
        public Color focusColor = Color.white;
        public Color shadowColor = new Color(0f, 0f, 0f, 0.78f);
        public Color selectedBackgroundColor = new Color(0.114f, 0.529f, 0.298f, 0.98f);
        public Color hoverBackgroundColor = new Color(0.106f, 0.353f, 0.216f, 0.98f);
        public bool showBackgroundOnFocus = true;
        public bool bringToFrontOnHighlight = true;

        RectTransform rect;
        Image background;
        Image stateFill;
        Color normalBackgroundColor;
        bool hasBackgroundColor;
        bool pointerOver;
        Image[] brightEdges;
        Image[] darkEdges;
        bool wasFocused;
        static UiSelectionHighlight activeHighlight;
        static bool activeHighlightIsPointer;

        void Awake()
        {
            rect = GetComponent<RectTransform>();
            CaptureBackground();
            EnsureEdges();
        }

        void LateUpdate()
        {
            if (rect == null) rect = GetComponent<RectTransform>();
            CaptureBackground();
            EnsureEdges();
            EnsureStateFill();
            bool focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            bool pointerCanDriveFocus = UiSelectionUtility.PointerCanDriveFocus();
            bool mouseOver = pointerCanDriveFocus && IsMouseOverRect();
            if (!pointerCanDriveFocus && pointerOver) ClearPointerHighlight();
            if (mouseOver && !pointerOver && activeHighlight == null) ActivatePointerHighlight();
            if (!mouseOver && pointerOver) ClearPointerHighlight();
            if (!mouseOver && activeHighlight == this && activeHighlightIsPointer) ClearPointerHighlight();
            if (focused && !wasFocused && !mouseOver && !pointerOver) ActivateFocusHighlight();
            wasFocused = focused;

            bool highlighted = !forceSelected &&
                activeHighlight == this &&
                (activeHighlightIsPointer ? mouseOver : focused);
            SetEdgesActive(forceSelected || highlighted);
            ApplyBackground(forceSelected, highlighted);
            if (!forceSelected && !highlighted) return;

            BringToFront();
            var edgeColor = forceSelected ? color : focusColor;
            var bright = new Color(edgeColor.r, edgeColor.g, edgeColor.b, forceSelected ? 0.88f : 1f);
            for (int i = 0; i < brightEdges.Length; i++)
            {
                if (brightEdges[i] != null) brightEdges[i].color = bright;
            }

            var dark = forceSelected
                ? new Color(shadowColor.r, shadowColor.g, shadowColor.b, shadowColor.a)
                : new Color(shadowColor.r, shadowColor.g, shadowColor.b, 0f);
            for (int i = 0; i < darkEdges.Length; i++)
            {
                if (darkEdges[i] != null) darkEdges[i].color = dark;
            }
        }

        public void SetForceSelected(bool selected)
        {
            forceSelected = selected;
        }

        public void SetNormalBackgroundColor(Color color)
        {
            if (background == null) background = GetComponent<Image>();
            normalBackgroundColor = color;
            hasBackgroundColor = true;
            if (background != null) background.color = color;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!UiSelectionUtility.PointerCanDriveFocus()) return;
            ActivatePointerHighlight();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ClearPointerHighlight();
        }

        void ActivatePointerHighlight()
        {
            pointerOver = true;
            activeHighlight = this;
            activeHighlightIsPointer = true;
        }

        void ActivateFocusHighlight()
        {
            activeHighlight = this;
            activeHighlightIsPointer = false;
        }

        void ClearPointerHighlight()
        {
            pointerOver = false;
            if (activeHighlight == this && activeHighlightIsPointer) activeHighlight = null;
        }

        void CaptureBackground()
        {
            if (background != null) return;
            background = GetComponent<Image>();
            if (background == null) return;
            normalBackgroundColor = background.color;
            hasBackgroundColor = true;
        }

        void ApplyBackground(bool selected, bool highlighted)
        {
            if (background == null || !hasBackgroundColor) return;
            background.color = normalBackgroundColor;
            if (stateFill == null) return;
            bool showFill = selected || highlighted && showBackgroundOnFocus;
            stateFill.gameObject.SetActive(showFill);
            stateFill.color = selected ? selectedBackgroundColor : hoverBackgroundColor;
        }

        void BringToFront()
        {
            if (!bringToFrontOnHighlight || transform.parent == null) return;
            transform.SetAsLastSibling();
            SetEdgesAsLastSiblings();
        }

        void EnsureStateFill()
        {
            if (stateFill != null) return;
            var existing = transform.Find("State Fill");
            stateFill = existing != null ? existing.GetComponent<Image>() : null;
            if (stateFill == null)
            {
                stateFill = new GameObject("State Fill").AddComponent<Image>();
                stateFill.transform.SetParent(transform, false);
            }

            stateFill.raycastTarget = false;
            stateFill.gameObject.SetActive(false);
            var fillRect = stateFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            fillRect.SetAsFirstSibling();
            SetEdgesAsLastSiblings();
        }

        bool IsMouseOverRect()
        {
            if (rect == null || !gameObject.activeInHierarchy) return false;
            var canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) eventCamera = canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, eventCamera);
        }

        void EnsureEdges()
        {
            if (rect == null || brightEdges != null && brightEdges.Length == 4) return;
            brightEdges = CreateEdges("Selected Edge", color, 0f, thickness);
            darkEdges = CreateEdges("Selected Shadow", shadowColor, 2f, thickness + 3f);
            SetEdgesActive(false);
        }

        void SetEdgesAsLastSiblings()
        {
            SetEdgesAsLastSiblings(darkEdges);
            SetEdgesAsLastSiblings(brightEdges);
        }

        static void SetEdgesAsLastSiblings(Image[] edges)
        {
            if (edges == null) return;
            foreach (var edge in edges)
            {
                if (edge != null) edge.transform.SetAsLastSibling();
            }
        }

        Image[] CreateEdges(string prefix, Color edgeColor, float offset, float edgeThickness)
        {
            var edges = new Image[4];
            for (int i = 0; i < edges.Length; i++)
            {
                var go = new GameObject(prefix + " " + i);
                go.transform.SetParent(transform, false);
                var image = go.AddComponent<Image>();
                image.color = edgeColor;
                image.raycastTarget = false;
                edges[i] = image;
            }

            float width = rect.sizeDelta.x + padding * 2f + offset * 2f;
            float height = rect.sizeDelta.y + padding * 2f + offset * 2f;
            SetEdge(edges[0].rectTransform, new Vector2(0f, height * 0.5f), new Vector2(width, edgeThickness));
            SetEdge(edges[1].rectTransform, new Vector2(0f, -height * 0.5f), new Vector2(width, edgeThickness));
            SetEdge(edges[2].rectTransform, new Vector2(-width * 0.5f, 0f), new Vector2(edgeThickness, height));
            SetEdge(edges[3].rectTransform, new Vector2(width * 0.5f, 0f), new Vector2(edgeThickness, height));
            return edges;
        }

        static void SetEdge(RectTransform edge, Vector2 position, Vector2 size)
        {
            edge.anchorMin = new Vector2(0.5f, 0.5f);
            edge.anchorMax = new Vector2(0.5f, 0.5f);
            edge.pivot = new Vector2(0.5f, 0.5f);
            edge.anchoredPosition = position;
            edge.sizeDelta = size;
        }

        void SetEdgesActive(bool active)
        {
            SetEdgesActive(brightEdges, active);
            SetEdgesActive(darkEdges, active);
        }

        static void SetEdgesActive(Image[] edges, bool active)
        {
            if (edges == null) return;
            foreach (var edge in edges)
            {
                if (edge != null) edge.gameObject.SetActive(active);
            }
        }
    }
}
