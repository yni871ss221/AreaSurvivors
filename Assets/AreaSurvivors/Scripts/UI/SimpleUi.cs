using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public static class SimpleUi
    {
        static readonly Color PanelEdge = new Color(0.47f, 0.56f, 0.38f, 0.88f);
        static readonly Color PanelGlow = new Color(0.95f, 0.86f, 0.52f, 0.12f);
        static readonly Color TextShadow = new Color(0.02f, 0.025f, 0.018f, 0.82f);
        static readonly Color ButtonBase = new Color(0.12f, 0.20f, 0.16f, 0.94f);
        static readonly Color ButtonEdge = new Color(0.56f, 0.65f, 0.42f, 0.96f);
        static readonly Color ButtonSelected = new Color(0.114f, 0.529f, 0.298f, 0.98f);

        public static Canvas Root(string name, Color background, string backgroundResource = null)
        {
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.tag = "MainCamera";

            var canvas = new GameObject(name).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            if (!string.IsNullOrEmpty(backgroundResource))
            {
                var sprite = Resources.Load<Sprite>(backgroundResource);
                if (sprite != null)
                {
                    var image = new GameObject("Background").AddComponent<Image>();
                    image.transform.SetParent(canvas.transform, false);
                    image.sprite = sprite;
                    image.color = Color.white;
                    Stretch(image.rectTransform);
                }
            }

            var shade = new GameObject("Vignette").AddComponent<Image>();
            shade.transform.SetParent(canvas.transform, false);
            shade.color = new Color(0f, 0f, 0f, 0.24f);
            Stretch(shade.rectTransform);

            var topShade = new GameObject("Top Shade").AddComponent<Image>();
            topShade.transform.SetParent(canvas.transform, false);
            topShade.color = new Color(0f, 0f, 0f, 0.18f);
            topShade.rectTransform.anchorMin = new Vector2(0f, 0.72f);
            topShade.rectTransform.anchorMax = Vector2.one;
            topShade.rectTransform.offsetMin = Vector2.zero;
            topShade.rectTransform.offsetMax = Vector2.zero;
            return canvas;
        }

        public static Image Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.rectTransform.anchoredPosition = pos;
            image.rectTransform.sizeDelta = size;
            AddFrame(image.transform, size, PanelEdge, 2f);
            AddInset(image.transform, size, PanelGlow);
            return image;
        }

        public static Text Label(Transform parent, string text, int fontSize, Vector2 pos, Vector2 sizeDelta, string name = "Label", Color? color = null, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = color ?? Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.rectTransform.anchoredPosition = pos;
            label.rectTransform.sizeDelta = sizeDelta;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = TextShadow;
            outline.effectDistance = new Vector2(1f, -1f);
            return label;
        }

        public static Button Button(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction action, Vector2? size = null, string iconResource = null)
        {
            var go = new GameObject(string.IsNullOrEmpty(text) ? "Button" : text);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = ButtonBase;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            if (action != null) button.onClick.AddListener(action);
            button.colors = new ColorBlock
            {
                normalColor = image.color,
                highlightedColor = ButtonSelected,
                pressedColor = new Color(0.08f, 0.14f, 0.11f, 0.98f),
                selectedColor = ButtonSelected,
                disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.72f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            var rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size ?? new Vector2(290, 58);
            AddFrame(go.transform, rect.sizeDelta, ButtonEdge, 2f);
            AddInset(go.transform, rect.sizeDelta, new Color(1f, 0.92f, 0.58f, 0.08f));
            var highlight = go.AddComponent<UiSelectionHighlight>();
            highlight.padding = 6f;
            highlight.thickness = 4f;
            go.AddComponent<SelectOnPointerEnter>();

            if (!string.IsNullOrEmpty(iconResource))
            {
                AddIcon(go.transform, iconResource, new Vector2(-rect.sizeDelta.x * 0.34f, 0f), new Vector2(38, 38));
            }

            Label(go.transform, text, 22, string.IsNullOrEmpty(iconResource) ? Vector2.zero : new Vector2(18, 0), new Vector2(rect.sizeDelta.x - 58, rect.sizeDelta.y));
            return button;
        }

        public static Slider Slider(Transform parent, Vector2 pos)
        {
            var root = new GameObject("Slider");
            root.transform.SetParent(parent, false);
            var slider = root.AddComponent<Slider>();
            var rect = slider.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(390, 30);
            slider.transition = Selectable.Transition.None;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;

            var bg = BarImage(root.transform, "Background", new Color(0.05f, 0.07f, 0.08f, 0.95f), Vector2.zero, Vector2.one);
            AddFrame(root.transform, rect.sizeDelta, ButtonEdge, 2f);
            var fillArea = new GameObject("Fill Area").AddComponent<RectTransform>();
            fillArea.SetParent(root.transform, false);
            Stretch(fillArea);
            var fill = BarImage(fillArea, "Fill", new Color(0.39f, 0.78f, 0.47f, 0.95f), Vector2.zero, Vector2.one);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = bg;
            return slider;
        }

        public static void CharacterSelector(Transform parent)
        {
            CharacterCard(parent, "\u30ca\u30a4\u30c8", "\u524d\u65b9\u3092\u5207\u308a\u6255\u3046", "Generated/Knight", CharacterType.Knight, new Vector2(-250, -10));
            CharacterCard(parent, "\u30a2\u30fc\u30c1\u30e3\u30fc", "\u9060\u304f\u306e\u6575\u3092\u72d9\u3046", "Generated/Archer", CharacterType.Archer, new Vector2(0, -10));
            CharacterCard(parent, "\u30e1\u30a4\u30b8", "\u706b\u7403\u3067\u7fa4\u308c\u3092\u6255\u3046", "Generated/Mage", CharacterType.Mage, new Vector2(250, -10));
        }

        public static Sprite Sprite(string resource)
        {
            return Resources.Load<Sprite>(resource);
        }

        static void CharacterCard(Transform parent, string title, string description, string spriteResource, CharacterType type, Vector2 pos)
        {
            var button = Button(parent, "", pos, () =>
            {
                RunState.SelectedCharacter = type;
                ProgressionStore.Data.selectedCharacter = type;
                ProgressionStore.Save();
            }, new Vector2(210, 190));
            var selected = button.gameObject.AddComponent<CharacterSelectionHighlight>();
            selected.type = type;

            AddIcon(button.transform, spriteResource, new Vector2(0, 38), new Vector2(94, 94));
            Label(button.transform, title, 24, new Vector2(0, -42), new Vector2(180, 32));
            Label(button.transform, description, 15, new Vector2(0, -78), new Vector2(180, 36), "Description", new Color(0.82f, 0.92f, 0.84f));
        }

        static void AddIcon(Transform parent, string resource, Vector2 pos, Vector2 size)
        {
            var sprite = Resources.Load<Sprite>(resource);
            if (sprite == null) return;
            var image = new GameObject("Icon").AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.rectTransform.anchoredPosition = pos;
            image.rectTransform.sizeDelta = size;
        }

        static Image BarImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.rectTransform.anchorMin = anchorMin;
            image.rectTransform.anchorMax = anchorMax;
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            return image;
        }

        static void AddFrame(Transform parent, Vector2 size, Color color, float thickness)
        {
            UiBoxOutline.Apply(parent, color, thickness);
        }

        static void AddInset(Transform parent, Vector2 size, Color color)
        {
            var inset = Border(parent, "Inset Glow", color, Vector2.zero, new Vector2(Mathf.Max(0f, size.x - 8f), Mathf.Max(0f, size.y - 8f)));
            inset.raycastTarget = false;
        }

        static Image Border(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.anchoredPosition = pos;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    public sealed class UiSelectionHighlight : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public bool forceSelected;
        public float padding = 5f;
        public float thickness = 4f;
        public Color color = new Color(1f, 0.86f, 0.18f, 1f);
        public Color shadowColor = new Color(0f, 0f, 0f, 0.78f);
        public Color selectedBackgroundColor = new Color(0.114f, 0.529f, 0.298f, 0.98f);
        public Color hoverBackgroundColor = new Color(0.106f, 0.353f, 0.216f, 0.98f);

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
            bool mouseOver = IsMouseOverRect();
            if (mouseOver && !pointerOver && activeHighlight == null) ActivatePointerHighlight();
            if (!mouseOver && pointerOver) ClearPointerHighlight();
            if (!mouseOver && activeHighlight == this && activeHighlightIsPointer) ClearPointerHighlight();
            if (focused && !wasFocused && !mouseOver && !pointerOver) ActivateFocusHighlight();
            wasFocused = focused;

            bool highlighted = !forceSelected &&
                activeHighlight == this &&
                (activeHighlightIsPointer ? mouseOver : focused);
            SetEdgesActive(forceSelected);
            ApplyBackground(forceSelected, highlighted);
            if (!forceSelected) return;

            var bright = new Color(color.r, color.g, color.b, 0.88f);
            for (int i = 0; i < brightEdges.Length; i++)
            {
                if (brightEdges[i] != null) brightEdges[i].color = bright;
            }
        }

        public void SetForceSelected(bool selected)
        {
            forceSelected = selected;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
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
            stateFill.gameObject.SetActive(selected || highlighted);
            stateFill.color = selected ? selectedBackgroundColor : hoverBackgroundColor;
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

    public sealed class CharacterSelectionHighlight : MonoBehaviour
    {
        public CharacterType type;
        UiSelectionHighlight highlight;

        void Awake()
        {
            highlight = GetComponent<UiSelectionHighlight>();
        }

        void Update()
        {
            if (highlight == null) highlight = GetComponent<UiSelectionHighlight>();
            if (highlight != null) highlight.forceSelected = RunState.SelectedCharacter == type;
        }
    }

    public sealed class SelectOnPointerEnter : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}
