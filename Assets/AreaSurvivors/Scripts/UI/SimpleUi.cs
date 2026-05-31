using System;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public static class SimpleUi
    {
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
            shade.color = new Color(0f, 0f, 0f, 0.32f);
            Stretch(shade.rectTransform);
            return canvas;
        }

        public static Image Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.rectTransform.anchoredPosition = pos;
            image.rectTransform.sizeDelta = size;
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
            return label;
        }

        public static Button Button(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction action, Vector2? size = null, string iconResource = null)
        {
            var go = new GameObject(string.IsNullOrEmpty(text) ? "Button" : text);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.12f, 0.20f, 0.16f, 0.94f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(action);
            button.colors = new ColorBlock
            {
                normalColor = image.color,
                highlightedColor = new Color(0.23f, 0.42f, 0.31f, 0.98f),
                pressedColor = new Color(0.08f, 0.14f, 0.11f, 0.98f),
                selectedColor = new Color(0.18f, 0.32f, 0.24f, 0.98f),
                disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.72f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            var rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size ?? new Vector2(290, 58);

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
            CharacterCard(parent, "ナイト", "剣で前方を薙ぎ払う", "Generated/Knight", CharacterType.Knight, new Vector2(-250, -10));
            CharacterCard(parent, "アーチャー", "近い敵を矢で狙う", "Generated/Archer", CharacterType.Archer, new Vector2(0, -10));
            CharacterCard(parent, "メイジ", "爆発する火球を放つ", "Generated/Mage", CharacterType.Mage, new Vector2(250, -10));
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

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
