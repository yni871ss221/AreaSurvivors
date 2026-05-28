using System;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public static class SimpleUi
    {
        public static Canvas Root(string name, Color background)
        {
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.tag = "MainCamera";

            var canvas = new GameObject(name).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.gameObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            return canvas;
        }

        public static Text Label(Transform parent, string text, int size, Vector2 pos, Vector2 sizeDelta, string name = "Label")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            var rect = label.rectTransform;
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;
            return label;
        }

        public static Button Button(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction action, Vector2? size = null)
        {
            var go = new GameObject(text);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.17f, 0.25f, 0.20f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            if (action != null) button.onClick.AddListener(action);
            button.colors = new ColorBlock
            {
                normalColor = image.color,
                highlightedColor = new Color(0.25f, 0.42f, 0.30f),
                pressedColor = new Color(0.12f, 0.20f, 0.15f),
                selectedColor = image.color,
                disabledColor = Color.gray,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            var rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size ?? new Vector2(280, 56);
            Label(go.transform, text, 22, Vector2.zero, rect.sizeDelta);
            return button;
        }

        public static Slider Slider(Transform parent, Vector2 pos)
        {
            var root = new GameObject("Slider");
            root.transform.SetParent(parent, false);
            var slider = root.AddComponent<Slider>();
            var rect = slider.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(360, 28);
            var bg = BarImage(root.transform, "Background", new Color(0.12f, 0.12f, 0.14f), new Vector2(360, 18));
            var fillArea = new GameObject("Fill Area").AddComponent<RectTransform>();
            fillArea.SetParent(root.transform, false);
            fillArea.sizeDelta = new Vector2(340, 18);
            var fill = BarImage(fillArea, "Fill", new Color(0.38f, 0.74f, 0.44f), new Vector2(340, 18));
            slider.fillRect = fill.rectTransform;
            slider.targetGraphic = bg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        public static void CharacterSelector(Transform parent)
        {
            CharacterButton(parent, "ナイト", CharacterType.Knight, new Vector2(-210, 30));
            CharacterButton(parent, "アーチャー", CharacterType.Archer, new Vector2(0, 30));
            CharacterButton(parent, "メイジ", CharacterType.Mage, new Vector2(210, 30));
        }

        static void CharacterButton(Transform parent, string text, CharacterType type, Vector2 pos)
        {
            Button(parent, text, pos, () =>
            {
                RunState.SelectedCharacter = type;
                ProgressionStore.Data.selectedCharacter = type;
                ProgressionStore.Save();
            }, new Vector2(180, 52));
        }

        static Image BarImage(Transform parent, string name, Color color, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.rectTransform.sizeDelta = size;
            return image;
        }
    }
}
