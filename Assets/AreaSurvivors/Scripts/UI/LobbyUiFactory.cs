using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public static class LobbyUiFactory
    {
        static readonly Color PanelColor = new Color(0.03f, 0.06f, 0.05f, 0.62f);
        static readonly Color StagePanelColor = new Color(0.035f, 0.06f, 0.05f, 0.78f);
        static readonly Color ButtonColor = new Color(0.12f, 0.20f, 0.16f, 0.94f);
        static readonly Color EdgeColor = new Color(0.58f, 0.68f, 0.40f, 0.9f);
        static readonly Color AccentText = new Color(0.96f, 0.90f, 0.68f);
        static readonly Color BodyText = new Color(0.86f, 0.93f, 0.88f);
        static readonly Color ClearText = new Color(0.55f, 1f, 0.48f, 1f);

        public static Canvas Create()
        {
            EnsureCamera();
            EnsureEventSystem();

            var canvas = new GameObject("Lobby UI").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);

            CreateBackground(canvas.transform);
            Panel(canvas.transform, "Header Panel", new Vector2(0, 280), new Vector2(780, 78), new Color(0.03f, 0.06f, 0.05f, 0.68f));
            Label(canvas.transform, "Title", "\u30ed\u30d3\u30fc", 38, new Vector2(0, 298), new Vector2(520, 44), Color.white);
            Label(canvas.transform, "TokenInfo", "\u30c8\u30fc\u30af\u30f3 0   \u7d2f\u8a08\u6483\u7834 0", 21, new Vector2(0, 260), new Vector2(620, 32), new Color(0.86f, 0.94f, 0.80f));

            BuildStageProgress(canvas.transform);
            BuildKnightLoadout(canvas.transform);
            BuildFooterButtons(canvas.transform);
            return canvas;
        }

        static void CreateBackground(Transform parent)
        {
            var sprite = GeneratedSpriteLoader.Load("UI/LobbyBackground");
            if (sprite != null)
            {
                var background = Image(parent, "Background", Color.white, Vector2.zero, Vector2.zero);
                background.sprite = sprite;
                Stretch(background.rectTransform);
            }

            var shade = Image(parent, "Vignette", new Color(0f, 0f, 0f, 0.24f), Vector2.zero, Vector2.zero);
            Stretch(shade.rectTransform);
            var topShade = Image(parent, "Top Shade", new Color(0f, 0f, 0f, 0.18f), Vector2.zero, Vector2.zero);
            topShade.rectTransform.anchorMin = new Vector2(0f, 0.72f);
            topShade.rectTransform.anchorMax = Vector2.one;
            topShade.rectTransform.offsetMin = Vector2.zero;
            topShade.rectTransform.offsetMax = Vector2.zero;
        }

        static void BuildStageProgress(Transform parent)
        {
            Panel(parent, "Stage Progress Panel", new Vector2(0, 112), new Vector2(900, 176), PanelColor);
            Label(parent, "StageProgressTitle", "\u9032\u884c\u72b6\u6cc1", 23, new Vector2(0, 184), new Vector2(420, 34), AccentText);
            for (int stage = 1; stage <= 4; stage++)
            {
                float x = -318f + (stage - 1) * 212f;
                BuildStageCard(parent, stage, new Vector2(x, 104f));
            }
        }

        static void BuildStageCard(Transform parent, int stage, Vector2 position)
        {
            var panel = Panel(parent, "Stage " + stage + " Panel", position, new Vector2(184, 126), StagePanelColor);
            var button = panel.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            button.transition = Selectable.Transition.None;
            panel.gameObject.AddComponent<SelectOnPointerEnter>();
            Label(panel.transform, "Stage Label", "STAGE " + stage, 18, new Vector2(0, 46), new Vector2(150, 24), AccentText);
            var boss = Image(panel.transform, "Boss Image", Color.white, new Vector2(0, 4), new Vector2(74, 70));
            boss.preserveAspect = true;
            Label(panel.transform, "Unknown Boss", "?", 42, new Vector2(0, 2), new Vector2(74, 70), new Color(0f, 0f, 0f, 0.75f));
            Label(panel.transform, "Boss Name", "???", 15, new Vector2(0, -38), new Vector2(160, 22), BodyText);
            Label(panel.transform, "Clear", "CLEAR", 18, new Vector2(0, -60), new Vector2(90, 24), ClearText);
            BuildFastToggle(panel.transform);
        }

        static void BuildFastToggle(Transform parent)
        {
            var root = new GameObject("Fast Mode Toggle").AddComponent<Toggle>();
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(48, -58);
            rect.sizeDelta = new Vector2(72, 24);

            var box = Image(root.transform, "Box", new Color(0.09f, 0.16f, 0.12f, 0.92f), new Vector2(-22, 0), new Vector2(18, 18));
            UiBoxOutline.Apply(box.transform, EdgeColor, 2f);
            var check = Image(box.transform, "Check", ClearText, Vector2.zero, new Vector2(10, 10));
            root.graphic = check;
            root.targetGraphic = box;
            Label(root.transform, "Fast Label", "\u500d\u901f", 13, new Vector2(12, 0), new Vector2(46, 20), BodyText);
        }

        static void BuildKnightLoadout(Transform parent)
        {
            Panel(parent, "Character Panel", new Vector2(0, -66), new Vector2(760, 238), PanelColor);
            Label(parent, "CharacterTitle", "\u51fa\u6483\u30ad\u30e3\u30e9\u30af\u30bf\u30fc", 23, new Vector2(0, 28), new Vector2(420, 34), AccentText);
            CharacterCard(parent, "Character Knight", "\u30ca\u30a4\u30c8", "\u524d\u65b9\u3092\u5207\u308a\u6255\u3046", "Generated/Knight", CharacterType.Knight, new Vector2(-250, -10));
        }

        static void CharacterCard(Transform parent, string objectName, string title, string description, string spriteResource, CharacterType type, Vector2 pos)
        {
            var button = Button(parent, objectName, "", pos, new Vector2(210, 190), null);
            var selected = button.gameObject.AddComponent<CharacterSelectionHighlight>();
            selected.type = type;
            Icon(button.transform, "Icon", spriteResource, new Vector2(0, 38), new Vector2(94, 94));
            Label(button.transform, "Title", title, 24, new Vector2(0, -42), new Vector2(180, 32), Color.white);
            Label(button.transform, "Description", description, 15, new Vector2(0, -78), new Vector2(180, 36), new Color(0.82f, 0.92f, 0.84f));
        }

        static void BuildFooterButtons(Transform parent)
        {
            Button(parent, "Start Game Button", "\u30b2\u30fc\u30e0\u30b9\u30bf\u30fc\u30c8", new Vector2(-330, -300), new Vector2(260, 58), "Generated/Arrow");
            Button(parent, "Build Button", "\u5efa\u9020", new Vector2(-60, -300), new Vector2(220, 58), "Generated/Hammer");
            Button(parent, "Upgrade Button", "\u5f37\u5316", new Vector2(190, -300), new Vector2(220, 58), "Generated/Orb");
            Button(parent, "Title Button", "\u30bf\u30a4\u30c8\u30eb\u3078", new Vector2(430, -300), new Vector2(210, 52), "Generated/Slash_0");
        }

        static Button Button(Transform parent, string objectName, string text, Vector2 pos, Vector2 size, string iconResource)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = ButtonColor;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            UiBoxOutline.Apply(go.transform, EdgeColor, 2f);
            var highlight = go.AddComponent<UiSelectionHighlight>();
            highlight.padding = 6f;
            highlight.thickness = 4f;
            go.AddComponent<SelectOnPointerEnter>();
            if (!string.IsNullOrEmpty(iconResource))
            {
                Icon(go.transform, "Icon", iconResource, new Vector2(-size.x * 0.34f, 0f), new Vector2(38, 38));
            }

            if (!string.IsNullOrEmpty(text))
            {
                Label(go.transform, "Label", text, 22, string.IsNullOrEmpty(iconResource) ? Vector2.zero : new Vector2(18, 0), new Vector2(size.x - 58, size.y), Color.white);
            }

            return button;
        }

        static Image Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var image = Image(parent, name, color, pos, size);
            UiBoxOutline.Apply(image.transform, EdgeColor, 2f);
            var inset = Image(image.transform, "Inset Glow", new Color(1f, 0.92f, 0.58f, 0.08f), Vector2.zero, new Vector2(Mathf.Max(0, size.x - 8), Mathf.Max(0, size.y - 8)));
            inset.raycastTarget = false;
            return image;
        }

        static Text Label(Transform parent, string name, string value, int fontSize, Vector2 pos, Vector2 size, Color color)
        {
            var text = new GameObject(name).AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.rectTransform.anchoredPosition = pos;
            text.rectTransform.sizeDelta = size;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.02f, 0.025f, 0.018f, 0.82f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        static Image Image(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.rectTransform.anchoredPosition = pos;
            image.rectTransform.sizeDelta = size;
            image.raycastTarget = false;
            return image;
        }

        static void Icon(Transform parent, string name, string resource, Vector2 pos, Vector2 size)
        {
            var sprite = GeneratedSpriteLoader.IsGeneratedPath(resource)
                ? GeneratedSpriteLoader.Load(resource)
                : Resources.Load<Sprite>(resource);
            if (sprite == null) return;
            var image = Image(parent, name, Color.white, pos, size);
            image.sprite = sprite;
            image.preserveAspect = true;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void EnsureCamera()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<Camera>(true) != null) return;
            }

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.14f, 0.11f);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.tag = "MainCamera";
        }

        static void EnsureEventSystem()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<EventSystem>(true) != null) return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
