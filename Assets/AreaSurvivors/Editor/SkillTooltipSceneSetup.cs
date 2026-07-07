using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class SkillTooltipSceneSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";
        static readonly Vector2 TooltipSize = new Vector2(340f, 154f);
        static readonly Vector2 InnerSize = new Vector2(318f, 132f);

        [MenuItem("Area Survivors/Setup/Apply Skill Tooltip Layout")]
        public static void Apply()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyTooltipLayout(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != ScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        static void ApplyTooltipLayout(Scene scene)
        {
            var tooltip = FindInScene(scene, "Tooltip") as RectTransform;
            if (tooltip == null)
            {
                Debug.LogError("Tooltip was not found. Skill tooltip layout was skipped.");
                return;
            }

            tooltip.sizeDelta = TooltipSize;
            tooltip.gameObject.SetActive(false);
            DisableRaycastTargets(tooltip);

            var image = tooltip.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = false;
                EditorUtility.SetDirty(image);
            }

            ApplyChildRect(tooltip, "Inset Glow", Vector2.zero, InnerSize);
            ApplyText(tooltip, "Tooltip Title", new Vector2(0f, 48f), new Vector2(300f, 30f), 17, TextAnchor.MiddleLeft);
            ApplyText(tooltip, "Tooltip Description", new Vector2(0f, -22f), new Vector2(300f, 88f), 15, TextAnchor.UpperLeft);
            EditorUtility.SetDirty(tooltip);
        }

        static void ApplyChildRect(Transform root, string name, Vector2 position, Vector2 size)
        {
            var rect = FindDeep(root, name) as RectTransform;
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(rect);
        }

        static void ApplyText(Transform root, string name, Vector2 position, Vector2 size, int fontSize, TextAnchor alignment)
        {
            var rect = FindDeep(root, name) as RectTransform;
            if (rect == null) return;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var text = rect.GetComponent<Text>();
            if (text != null)
            {
                text.fontSize = fontSize;
                text.alignment = alignment;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
                EditorUtility.SetDirty(text);
            }

            EditorUtility.SetDirty(rect);
        }

        static void DisableRaycastTargets(Transform root)
        {
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic == null) continue;
                graphic.raycastTarget = false;
                EditorUtility.SetDirty(graphic);
            }
        }

        static Transform FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindDeep(root.transform, name);
                if (found != null) return found;
            }

            return null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }
    }
}
