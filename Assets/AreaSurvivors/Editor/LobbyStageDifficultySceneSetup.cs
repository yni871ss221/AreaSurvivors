using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class LobbyStageDifficultySceneSetup
    {
        const string LobbyScenePath = "Assets/AreaSurvivors/Scenes/03_Lobby.unity";
        static readonly Color ButtonColor = new Color(0.12f, 0.20f, 0.16f, 0.88f);
        static readonly Color EdgeColor = new Color(0.58f, 0.68f, 0.40f, 0.95f);
        static readonly Color TextColor = new Color(0.86f, 0.93f, 0.88f, 1f);

        [MenuItem("AreaSurvivors/Setup/Add Lobby Stage Difficulty Controls")]
        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(LobbyScenePath);
            int changed = 0;
            for (int stage = 1; stage <= 4; stage++)
            {
                var panel = FindSceneTransform("Stage " + stage + " Panel");
                if (panel == null) continue;
                if (EnsureDifficultyControl(panel)) changed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"Lobby stage difficulty controls applied: changed={changed}");
        }

        static Transform FindSceneTransform(string name)
        {
            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform == null || transform.name != name) continue;
                if (!transform.gameObject.scene.IsValid() || transform.gameObject.scene.path != LobbyScenePath) continue;
                return transform;
            }

            return null;
        }

        static bool EnsureDifficultyControl(Transform panel)
        {
            var panelRect = panel as RectTransform;
            if (panelRect == null) return false;

            var root = panel.Find("Difficulty Root") as RectTransform;
            bool created = root == null;
            if (root == null)
            {
                root = new GameObject("Difficulty Root").AddComponent<RectTransform>();
                root.SetParent(panel, false);
            }

            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, -panelRect.sizeDelta.y * 0.5f + 18f);
            root.sizeDelta = new Vector2(Mathf.Max(140f, panelRect.sizeDelta.x - 18f), 24f);

            EnsureButton(root, "Difficulty Down Button", "\u25c0", new Vector2(-root.sizeDelta.x * 0.38f, 0f), new Vector2(28f, 24f));
            EnsureLabel(root, "Difficulty Label", "\u96e3\u6613\u5ea61", Vector2.zero, new Vector2(96f, 24f), 15);
            EnsureButton(root, "Difficulty Up Button", "\u25b6", new Vector2(root.sizeDelta.x * 0.38f, 0f), new Vector2(28f, 24f));
            root.gameObject.SetActive(false);
            return created;
        }

        static void EnsureButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            var button = parent.Find(name)?.GetComponent<Button>();
            if (button == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                var image = go.AddComponent<Image>();
                image.color = ButtonColor;
                button = go.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
            }
            else
            {
                var image = button.GetComponent<Image>();
                if (image != null) image.color = ButtonColor;
                button.transition = Selectable.Transition.None;
            }

            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            UiBoxOutline.Apply(button.transform, EdgeColor, 2f);
            EnsureLabel(button.transform, "Label", label, Vector2.zero, size, 15);
        }

        static void EnsureLabel(Transform parent, string name, string label, Vector2 position, Vector2 size, int fontSize)
        {
            var text = parent.Find(name)?.GetComponent<Text>();
            if (text == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                text = go.AddComponent<Text>();
                text.raycastTarget = false;
            }

            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = TextColor;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
