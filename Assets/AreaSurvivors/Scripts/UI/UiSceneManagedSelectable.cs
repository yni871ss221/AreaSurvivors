using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    // Uses only Scene/Prefab-provided graphics for the shared bright focus presentation.
    public sealed class UiSceneManagedSelectable : MonoBehaviour
    {
        public Color focusBackgroundColor = new Color(0.114f, 0.529f, 0.298f, 0.98f);
        public Color focusOutlineColor = Color.white;
        public float focusOutlineThickness = 5f;

        Selectable selectable;
        Image background;
        Outline outline;
        Color normalBackgroundColor;
        Color normalOutlineColor;
        Vector2 normalOutlineDistance;
        bool captured;
        bool highlighted;

        void OnEnable()
        {
            CaptureSceneStyle();
            ApplyHighlight(false);
        }

        void LateUpdate()
        {
            if (!captured) CaptureSceneStyle();
            if (selectable == null || background == null || outline == null) return;

            bool pointerMode = UiSelectionUtility.PointerCanDriveFocus();
            bool selected = EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject;
            bool shouldHighlight = selectable.IsInteractable() &&
                (pointerMode ? IsPointerOver() : selected);
            if (shouldHighlight != highlighted) ApplyHighlight(shouldHighlight);
        }

        void OnDisable()
        {
            if (captured) ApplyHighlight(false);
        }

        void CaptureSceneStyle()
        {
            selectable = GetComponent<Selectable>();
            background = GetComponent<Image>();
            outline = GetComponent<Outline>();
            if (background == null || outline == null) return;

            normalBackgroundColor = background.color;
            normalOutlineColor = outline.effectColor;
            normalOutlineDistance = outline.effectDistance;
            captured = true;
        }

        bool IsPointerOver()
        {
            var rect = transform as RectTransform;
            if (rect == null) return false;
            var canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, eventCamera);
        }

        void ApplyHighlight(bool active)
        {
            highlighted = active;
            if (background != null)
            {
                background.color = active ? focusBackgroundColor : normalBackgroundColor;
            }

            if (outline != null)
            {
                outline.effectColor = active ? focusOutlineColor : normalOutlineColor;
                float thickness = Mathf.Max(1f, focusOutlineThickness);
                outline.effectDistance = active
                    ? new Vector2(thickness, -thickness)
                    : normalOutlineDistance;
            }
        }
    }
}
