using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class SkillNodePanelStatusLayoutSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";

        static readonly Vector2 MinimumNodeSize = new Vector2(64f, 76f);
        static readonly Vector2 ButtonSize = new Vector2(64f, 70f);
        static readonly Vector2 IconSize = new Vector2(44f, 44f);
        static readonly Vector2 StatusSize = new Vector2(64f, 16f);
        static readonly Vector2 NodeNumberSize = new Vector2(20f, 16f);

        [MenuItem("Area Survivors/Setup/Apply Skill Node Status Inside Panels")]
        public static void Apply()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var nodes = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SkillNodeView>(true))
                .ToArray();

            foreach (var node in nodes)
            {
                ApplyToNode(node);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != ScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            Debug.Log($"Applied skill node status layout to {nodes.Length} skill nodes.");
        }

        static void ApplyToNode(SkillNodeView node)
        {
            if (node == null) return;
            node.ResolveReferences();

            if (node.RectTransform != null)
            {
                node.RectTransform.sizeDelta = new Vector2(
                    Mathf.Max(node.RectTransform.sizeDelta.x, MinimumNodeSize.x),
                    Mathf.Max(node.RectTransform.sizeDelta.y, MinimumNodeSize.y));
                EditorUtility.SetDirty(node.RectTransform);
            }

            var buttonRect = node.button != null ? node.button.transform as RectTransform : null;
            if (buttonRect != null)
            {
                buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                buttonRect.pivot = new Vector2(0.5f, 0.5f);
                buttonRect.anchoredPosition = Vector2.zero;
                buttonRect.sizeDelta = ButtonSize;
                EditorUtility.SetDirty(buttonRect);
            }

            if (node.background != null)
            {
                node.panelOutline = EnsurePanelOutline(node.background);
                EditorUtility.SetDirty(node.background);
                EditorUtility.SetDirty(node);
            }

            if (node.icon != null)
            {
                var iconRect = node.icon.rectTransform;
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(0f, 8f);
                iconRect.sizeDelta = new Vector2(
                    Mathf.Max(iconRect.sizeDelta.x, IconSize.x),
                    Mathf.Max(iconRect.sizeDelta.y, IconSize.y));
                EditorUtility.SetDirty(iconRect);
            }

            if (node.statusText != null)
            {
                var statusRect = node.statusText.rectTransform;
                statusRect.anchorMin = new Vector2(0.5f, 0.5f);
                statusRect.anchorMax = new Vector2(0.5f, 0.5f);
                statusRect.pivot = new Vector2(0.5f, 0.5f);
                statusRect.anchoredPosition = new Vector2(0f, -25f);
                statusRect.sizeDelta = StatusSize;
                node.statusText.alignment = TextAnchor.MiddleCenter;
                node.statusText.fontSize = 9;
                node.statusText.fontStyle = FontStyle.Bold;
                node.statusText.color = new Color(0.92f, 1f, 0.95f, 1f);
                node.statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
                node.statusText.verticalOverflow = VerticalWrapMode.Overflow;
                node.statusText.raycastTarget = false;
                EnsureTextOutline(node.statusText);
                EditorUtility.SetDirty(node.statusText);
                EditorUtility.SetDirty(statusRect);
            }

            var numberText = node.transform.Find("Node Button/Node No")?.GetComponent<Text>();
            if (numberText != null)
            {
                numberText.gameObject.SetActive(true);
                var numberRect = numberText.rectTransform;
                numberRect.anchorMin = new Vector2(0.5f, 0.5f);
                numberRect.anchorMax = new Vector2(0.5f, 0.5f);
                numberRect.pivot = new Vector2(0.5f, 0.5f);
                numberRect.anchoredPosition = new Vector2(18f, 22f);
                numberRect.sizeDelta = NodeNumberSize;
                numberText.alignment = TextAnchor.MiddleCenter;
                numberText.fontSize = 8;
                numberText.raycastTarget = false;
                EditorUtility.SetDirty(numberText);
                EditorUtility.SetDirty(numberRect);
            }

            EditorUtility.SetDirty(node);
        }

        static void EnsureTextOutline(Text text)
        {
            var outline = text.GetComponent<Outline>();
            if (outline == null)
            {
                outline = text.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0f, 0.05f, 0.03f, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
            EditorUtility.SetDirty(outline);
        }

        static Outline EnsurePanelOutline(Image image)
        {
            var outline = image.GetComponent<Outline>();
            if (outline == null)
            {
                outline = image.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = false;
            EditorUtility.SetDirty(outline);
            return outline;
        }
    }
}
