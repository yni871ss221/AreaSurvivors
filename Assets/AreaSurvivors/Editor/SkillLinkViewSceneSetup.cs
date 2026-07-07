using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.Editor
{
    public static class SkillLinkViewSceneSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";

        [MenuItem("Area Survivors/Setup/Rebuild Skill Link Views")]
        public static void Apply()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var nodeGroups = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SkillNodeView>(true))
                .Where(node => node != null && node.transform.parent != null)
                .GroupBy(node => node.transform.parent)
                .Where(group => group.Count() > 1)
                .ToArray();

            foreach (var group in nodeGroups)
            {
                RebuildPanelLinks(group.Key, group.ToArray());
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != ScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }

            Debug.Log($"Rebuilt skill link views for {nodeGroups.Length} skill tree panels.");
        }

        static void RebuildPanelLinks(Transform panelRoot, SkillNodeView[] nodes)
        {
            var linkRoot = EnsureLinkRoot(panelRoot);
            if (linkRoot == null) return;

            for (int i = linkRoot.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(linkRoot.GetChild(i).gameObject);
            }

            foreach (var node in nodes)
            {
                if (node == null) continue;
                var routes = node.linkRoutes;
                if (routes != null && routes.Length > 0)
                {
                    foreach (var route in routes)
                    {
                        CreateLink(nodes, linkRoot, node, route.prerequisite, route.waypoints);
                    }
                }
                else
                {
                    foreach (var prerequisite in node.EffectivePrerequisites())
                    {
                        CreateLink(nodes, linkRoot, node, prerequisite, null);
                    }
                }
            }

            linkRoot.SetAsFirstSibling();
            EditorUtility.SetDirty(panelRoot);
        }

        static RectTransform EnsureLinkRoot(Transform panelRoot)
        {
            if (panelRoot == null) return null;
            var existing = panelRoot.Find("Skill Links")
                ?? panelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == "Skill Links");
            if (existing is RectTransform existingRect)
            {
                StretchToParent(existingRect);
                return existingRect;
            }

            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var rect = new GameObject("Skill Links", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(panelRoot, false);
            rect.SetAsFirstSibling();
            StretchToParent(rect);
            return rect;
        }

        static void CreateLink(SkillNodeView[] nodes, Transform linkRoot, SkillNodeView toNode, UpgradeType prerequisite, Vector2Int[] waypoints)
        {
            var fromNode = nodes.FirstOrDefault(node => node != null && node.type == prerequisite);
            if (fromNode == null || fromNode.RectTransform == null || toNode?.RectTransform == null) return;

            var link = new GameObject($"{fromNode.type} to {toNode.type}", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkillLinkView))
                .GetComponent<SkillLinkView>();
            link.transform.SetParent(linkRoot, false);
            StretchToParent((RectTransform)link.transform);
            link.prerequisite = prerequisite;
            link.fromNode = fromNode;
            link.toNode = toNode;
            link.thickness = 5f;
            link.cornerRadius = 12f;
            link.cornerSegments = 6;

            if (waypoints != null && waypoints.Length > 0)
            {
                link.waypoints = waypoints.Select(toNode.GridToAnchored).ToArray();
            }

            link.ApplyDirectionalAnchors();
            link.ApplyState(false);
        }

        static void StretchToParent(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }
}
