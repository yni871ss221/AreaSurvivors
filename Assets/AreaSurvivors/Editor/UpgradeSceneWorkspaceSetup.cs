using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class UpgradeSceneWorkspaceSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";
        const float HeaderHeight = 76f;
        const float SkillTreeTopPadding = HeaderHeight + 14f;
        const float SkillTreeInitialZoom = 0.84f;
        static readonly Color WorkspaceBackgroundColor = new Color(0.015f, 0.035f, 0.032f, 0.78f);
        static readonly Color HeaderBackgroundColor = new Color(0.025f, 0.055f, 0.048f, 0.94f);
        static readonly Color HeaderEdgeColor = new Color(0.42f, 0.52f, 0.38f, 0.86f);
        static readonly Color TitleColor = Color.white;
        static readonly Color TokenColor = new Color(0.96f, 0.90f, 0.62f, 1f);

        [MenuItem("Area Survivors/Setup/Apply Upgrade Scene Workspace")]
        public static void Apply()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyWorkspace(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != ScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        static void ApplyWorkspace(Scene scene)
        {
            var upgradeUi = FindInScene(scene, "Upgrade UI") as RectTransform;
            if (upgradeUi == null)
            {
                Debug.LogError("Upgrade UI was not found. Upgrade scene workspace setup was skipped.");
                return;
            }

            SetActive(upgradeUi, "Background", false);
            SetActive(upgradeUi, "Vignette", false);
            SetActive(upgradeUi, "Top Shade", false);
            SetActive(upgradeUi, "Upgrade Board", false);

            var viewport = FindDeep(upgradeUi, "Skill Tree Viewport") as RectTransform;
            if (viewport == null)
            {
                Debug.LogError("Skill Tree Viewport was not found. Upgrade scene workspace setup was skipped.");
                return;
            }

            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.anchoredPosition = Vector2.zero;
            viewport.SetAsFirstSibling();

            var viewportImage = viewport.GetComponent<Image>();
            if (viewportImage != null)
            {
                viewportImage.color = WorkspaceBackgroundColor;
                viewportImage.raycastTarget = true;
                EditorUtility.SetDirty(viewportImage);
            }

            var viewportOutline = viewport.GetComponent<UiBoxOutline>();
            if (viewportOutline == null) viewportOutline = viewport.gameObject.AddComponent<UiBoxOutline>();
            viewportOutline.color = HeaderEdgeColor;
            viewportOutline.thickness = 2f;
            EditorUtility.SetDirty(viewportOutline);

            var mask = viewport.GetComponent<RectMask2D>();
            if (mask != null)
            {
                mask.enabled = false;
                EditorUtility.SetDirty(mask);
            }

            var content = FindDeep(viewport, "Skill Tree") as RectTransform;
            if (content != null)
            {
                content.anchorMin = new Vector2(0.5f, 1f);
                content.anchorMax = new Vector2(0.5f, 1f);
                content.pivot = new Vector2(0.5f, 1f);
                content.anchoredPosition = new Vector2(0f, -SkillTreeTopPadding);
                content.localScale = Vector3.one * SkillTreeInitialZoom;
                EditorUtility.SetDirty(content);
            }

            var controller = viewport.GetComponent<SkillTreeViewportController>();
            if (controller != null)
            {
                controller.content = content;
                EditorUtility.SetDirty(controller);
            }

            var header = GetOrCreateHeader(upgradeUi);
            ConfigureHeader(header, upgradeUi);
            BringFixedUiToFront(upgradeUi, header);
            EditorUtility.SetDirty(viewport);
        }

        static RectTransform GetOrCreateHeader(RectTransform upgradeUi)
        {
            var header = upgradeUi.Find("Upgrade Header") as RectTransform;
            if (header != null) return header;

            var go = new GameObject("Upgrade Header");
            go.transform.SetParent(upgradeUi, false);
            header = go.AddComponent<RectTransform>();
            go.AddComponent<CanvasRenderer>();
            go.AddComponent<Image>();
            go.AddComponent<UiBoxOutline>();
            return header;
        }

        static void ConfigureHeader(RectTransform header, RectTransform upgradeUi)
        {
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, HeaderHeight);

            var image = header.GetComponent<Image>();
            if (image != null)
            {
                image.color = HeaderBackgroundColor;
                image.raycastTarget = false;
                EditorUtility.SetDirty(image);
            }

            var outline = header.GetComponent<UiBoxOutline>();
            if (outline != null)
            {
                outline.color = HeaderEdgeColor;
                outline.thickness = 2f;
                EditorUtility.SetDirty(outline);
            }

            ConfigureTitle(upgradeUi, header);
            ConfigureTokenLabel(upgradeUi, header);
            EditorUtility.SetDirty(header);
        }

        static void ConfigureTitle(RectTransform upgradeUi, RectTransform header)
        {
            var label = FindDeep(upgradeUi, "Label") as RectTransform;
            if (label == null) return;
            label.SetParent(header, false);
            label.anchorMin = new Vector2(0.5f, 0.5f);
            label.anchorMax = new Vector2(0.5f, 0.5f);
            label.pivot = new Vector2(0.5f, 0.5f);
            label.anchoredPosition = Vector2.zero;
            label.sizeDelta = new Vector2(460f, 48f);

            var text = label.GetComponent<Text>();
            if (text != null)
            {
                text.text = "永続強化";
                text.fontSize = 32;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = TitleColor;
                text.raycastTarget = false;
                EditorUtility.SetDirty(text);
            }

            EditorUtility.SetDirty(label);
        }

        static void ConfigureTokenLabel(RectTransform upgradeUi, RectTransform header)
        {
            var token = FindDeep(upgradeUi, "TokenLabel") as RectTransform;
            if (token == null) return;
            token.SetParent(header, false);
            token.anchorMin = new Vector2(1f, 0.5f);
            token.anchorMax = new Vector2(1f, 0.5f);
            token.pivot = new Vector2(1f, 0.5f);
            token.anchoredPosition = new Vector2(-28f, 0f);
            token.sizeDelta = new Vector2(340f, 36f);

            var text = token.GetComponent<Text>();
            if (text != null)
            {
                text.fontSize = 20;
                text.alignment = TextAnchor.MiddleRight;
                text.color = TokenColor;
                text.raycastTarget = false;
                EditorUtility.SetDirty(text);
            }

            EditorUtility.SetDirty(token);
        }

        static void BringFixedUiToFront(RectTransform upgradeUi, RectTransform header)
        {
            if (header != null) header.SetAsLastSibling();
            var tooltip = FindDeep(upgradeUi, "Tooltip");
            if (tooltip != null) tooltip.SetAsLastSibling();
            var lobbyButton = FindDeep(upgradeUi, "ロビーへ");
            if (lobbyButton != null) lobbyButton.SetAsLastSibling();
        }

        static void SetActive(Transform root, string childName, bool active)
        {
            var child = FindDeep(root, childName);
            if (child == null) return;
            child.gameObject.SetActive(active);
            EditorUtility.SetDirty(child.gameObject);
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
