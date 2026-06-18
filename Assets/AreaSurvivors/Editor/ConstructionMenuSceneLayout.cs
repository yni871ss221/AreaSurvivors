using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class ConstructionMenuSceneLayout
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        static readonly Color PanelColor = new Color(0.035f, 0.05f, 0.045f, 0.72f);
        static readonly Color SlotColor = new Color(0.09f, 0.16f, 0.12f, 0.92f);
        static readonly Color EdgeColor = new Color(0.58f, 0.68f, 0.40f, 0.9f);
        static readonly Vector2 StatusPanelPosition = new Vector2(14f, 15f);
        static readonly Vector2 StatusPanelSize = new Vector2(82f, 66f);
        const float SlotStartX = 110f;
        const float SlotY = 15f;
        const float SlotSpacing = 70f;

        public static void ApplyToGameScene()
        {
            var previousScene = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            ApplyToOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!string.IsNullOrEmpty(previousScene) && previousScene != GameScenePath)
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }

        public static void ApplyIconSpritesToGameScene()
        {
            var previousScene = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            ApplyIconSpritesToOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!string.IsNullOrEmpty(previousScene) && previousScene != GameScenePath)
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }

        public static void ApplyIconSpritesToOpenScene()
        {
            var menuObject = GameObject.Find("Construction Menu");
            if (menuObject == null)
            {
                Debug.LogWarning("Construction Menu was not found in the active scene.");
                return;
            }

            var menu = menuObject.GetComponent<RectTransform>();
            if (menu == null) return;

            SetSlotIconSprite(menu, 1, "WoodenWall");
            SetSlotIconSprite(menu, 2, "WoodenGateClosed");
            SetSlotIconSprite(menu, 3, "Ballista");
            SetSlotIconSprite(menu, 4, "WatchTower");
            SetSlotIconSprite(menu, 5, "CarpenterHut");
            SetSlotIconSprite(menu, 6, "WorkerHut");
        }

        public static void ApplyToOpenScene()
        {
            var menuObject = GameObject.Find("Construction Menu");
            if (menuObject == null)
            {
                Debug.LogWarning("Construction Menu was not found in the active scene.");
                return;
            }

            var menu = menuObject.GetComponent<RectTransform>();
            if (menu == null) return;
            menu.sizeDelta = new Vector2(Mathf.Max(menu.sizeDelta.x, 600f), Mathf.Max(menu.sizeDelta.y, 96f));

            var statusText = EnsureStatusPanel(menu);
            ConfigureSlot(menu, 1, "WoodenWall", "1", "木10", new Vector2(46f, 44f), true);
            ConfigureSlot(menu, 2, "WoodenGateClosed", "2", "木20", new Vector2(46f, 44f), true);
            ConfigureSlot(menu, 3, "Ballista", "3", "ロック", new Vector2(46f, 44f), false);
            ConfigureSlot(menu, 4, "WatchTower", "4", "ロック", new Vector2(46f, 44f), false);
            ConfigureSlot(menu, 5, "CarpenterHut", "5", "ロック", new Vector2(46f, 44f), false);
            ConfigureSlot(menu, 6, "WorkerHut", "6", "ロック", new Vector2(46f, 44f), false);

            if (statusText != null)
            {
                statusText.text = "1 木の城壁\n木10\n選択待ち";
                statusText.transform.parent.SetAsLastSibling();
                statusText.transform.SetAsLastSibling();
            }
        }

        static Text EnsureStatusPanel(RectTransform menu)
        {
            var panel = menu.Find("Build Status Panel") as RectTransform;
            if (panel == null)
            {
                var panelObject = new GameObject("Build Status Panel");
                panelObject.transform.SetParent(menu, false);
                panel = panelObject.AddComponent<RectTransform>();
                panelObject.AddComponent<Image>();
            }

            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;
            panel.anchoredPosition = StatusPanelPosition;
            panel.sizeDelta = StatusPanelSize;
            var panelImage = panel.GetComponent<Image>();
            if (panelImage == null) panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = SlotColor;
            panelImage.raycastTarget = false;
            UiBoxOutline.Apply(panel, EdgeColor, 2f);

            var legacy = menu.Find("Build Status");
            if (legacy != null && legacy.parent != panel) legacy.SetParent(panel, false);

            var statusTransform = panel.Find("Build Status");
            Text status = statusTransform != null ? statusTransform.GetComponent<Text>() : null;
            if (status == null)
            {
                var statusObject = new GameObject("Build Status");
                statusObject.transform.SetParent(panel, false);
                status = statusObject.AddComponent<Text>();
                status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                status.color = Color.white;
                var outline = statusObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            status.name = "Build Status";
            status.fontSize = 12;
            status.alignment = TextAnchor.MiddleCenter;
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            status.verticalOverflow = VerticalWrapMode.Truncate;
            status.raycastTarget = false;
            status.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            status.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            status.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            status.rectTransform.anchoredPosition = Vector2.zero;
            status.rectTransform.sizeDelta = new Vector2(74f, 58f);
            return status;
        }

        static void ConfigureSlot(RectTransform menu, int number, string spriteName, string key, string stock, Vector2 iconSize, bool iconVisible)
        {
            var slot = menu.Find("Build Slot " + number) as RectTransform;
            if (slot == null)
            {
                var source = menu.Find("Build Slot 1");
                if (source == null) return;
                slot = Object.Instantiate(source.gameObject, menu).GetComponent<RectTransform>();
                slot.name = "Build Slot " + number;
            }

            slot.anchorMin = Vector2.zero;
            slot.anchorMax = Vector2.zero;
            slot.pivot = Vector2.zero;
            slot.anchoredPosition = new Vector2(SlotStartX + SlotSpacing * (number - 1), SlotY);
            slot.sizeDelta = new Vector2(58f, 66f);
            var backplate = slot.GetComponent<Image>();
            if (backplate != null) backplate.color = SlotColor;
            UiBoxOutline.Apply(slot, EdgeColor, 2f);

            SetText(slot, "Key", key, 16, new Vector2(-18f, 22f), new Vector2(24f, 22f));
            SetText(slot, "Stock", stock, 11, new Vector2(0f, -22f), new Vector2(58f, 18f));

            var iconTransform = slot.Find("Icon") as RectTransform;
            if (iconTransform == null)
            {
                var iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(slot, false);
                iconTransform = iconObject.AddComponent<RectTransform>();
                iconObject.AddComponent<Image>();
            }

            var icon = iconTransform.GetComponent<Image>();
            if (icon == null) icon = iconTransform.gameObject.AddComponent<Image>();
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AreaSurvivors/Sprites/Generated/" + spriteName + ".png");
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = iconVisible;
            iconTransform.anchoredPosition = new Vector2(0f, -2f);
            iconTransform.sizeDelta = iconSize;
        }

        static void SetSlotIconSprite(RectTransform menu, int number, string spriteName)
        {
            var slot = menu.Find("Build Slot " + number) as RectTransform;
            if (slot == null) return;

            var iconTransform = slot.Find("Icon") as RectTransform;
            if (iconTransform == null) return;

            var icon = iconTransform.GetComponent<Image>();
            if (icon == null) icon = iconTransform.gameObject.AddComponent<Image>();
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/AreaSurvivors/Sprites/Generated/" + spriteName + ".png");
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        static void SetText(RectTransform parent, string childName, string value, int fontSize, Vector2 position, Vector2 size)
        {
            var child = parent.Find(childName);
            Text text = child != null ? child.GetComponent<Text>() : null;
            if (text == null)
            {
                var textObject = new GameObject(childName);
                textObject.transform.SetParent(parent, false);
                text = textObject.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.color = Color.white;
                var outline = textObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
                outline.effectDistance = new Vector2(1f, -1f);
            }

            text.text = value;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
        }
    }
}
