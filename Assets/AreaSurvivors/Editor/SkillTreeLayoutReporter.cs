using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class SkillTreeLayoutReporter
    {
        const string UpgradeScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";
        const int MaxIssueRows = 80;

        [MenuItem("Area Survivors/Reports/Skill Tree Layout")]
        public static void LogSkillTreeLayout()
        {
            var report = BuildReport();
            Debug.Log(ReportOutputUtility.SaveAndSummarize("Skill tree layout report", report, "skill-tree-layout"));
        }

        static string BuildReport()
        {
            var report = new StringBuilder(16384);
            report.AppendLine("AreaSurvivors Skill Tree Layout");
            var scene = OpenSceneIfNeeded(UpgradeScenePath, out var openedHere);
            if (!scene.IsValid())
            {
                report.AppendLine($"- missing scene: {UpgradeScenePath}");
                return report.ToString();
            }

            var nodes = FindSceneNodes(scene);
            report.AppendLine($"Scene: {UpgradeScenePath}");
            report.AppendLine($"Nodes: {nodes.Count}");
            AppendNodeSummary(report, nodes);
            AppendIssues(report, nodes);

            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            return report.ToString();
        }

        static Scene OpenSceneIfNeeded(string path, out bool openedHere)
        {
            openedHere = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == path) return loaded;
            }

            openedHere = true;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        static List<SkillNodeView> FindSceneNodes(Scene scene)
        {
            var nodes = new List<SkillNodeView>();
            foreach (var root in scene.GetRootGameObjects())
            {
                nodes.AddRange(root.GetComponentsInChildren<SkillNodeView>(true));
            }

            nodes.Sort((a, b) => string.Compare(a.type.ToString(), b.type.ToString(), System.StringComparison.Ordinal));
            return nodes;
        }

        static void AppendNodeSummary(StringBuilder report, List<SkillNodeView> nodes)
        {
            report.AppendLine();
            report.AppendLine("[Nodes]");
            foreach (var node in nodes)
            {
                if (node == null) continue;
                var rect = node.RectTransform;
                var icon = ResolveIcon(node);
                var statusText = ResolveStatusText(node);
                var iconSprite = icon != null ? SpriteName(icon.sprite) : "missing";
                var status = statusText != null ? Sanitize(statusText.text) : "missing";
                var prerequisites = node.EffectivePrerequisites();
                report.Append("- ");
                report.Append(node.type);
                report.Append($" name=\"{node.name}\"");
                report.Append($" parent=\"{ParentName(rect)}\"");
                if (rect != null)
                {
                    report.Append($" pos={Vector(rect.anchoredPosition)} size={Vector(rect.sizeDelta)}");
                }
                report.Append($" grid={node.gridPosition} implemented={node.implemented}");
                report.Append($" icon={iconSprite} status=\"{status}\"");
                report.Append($" prereq=[{string.Join(",", prerequisites)}]");
                report.AppendLine();
            }
        }

        static void AppendIssues(StringBuilder report, List<SkillNodeView> nodes)
        {
            var issues = new List<string>();
            CheckDuplicateTypes(nodes, issues);
            CheckMissingReferences(nodes, issues);
            CheckOverlaps(nodes, issues);
            CheckLinks(nodes, issues);

            report.AppendLine();
            report.AppendLine("[Issues]");
            report.AppendLine($"Count: {issues.Count}");
            int shown = Mathf.Min(issues.Count, MaxIssueRows);
            for (int i = 0; i < shown; i++) report.AppendLine("- " + issues[i]);
            if (issues.Count > shown) report.AppendLine($"- ... truncated {issues.Count - shown} more");
        }

        static void CheckDuplicateTypes(List<SkillNodeView> nodes, List<string> issues)
        {
            var seen = new Dictionary<UpgradeType, SkillNodeView>();
            foreach (var node in nodes)
            {
                if (node == null) continue;
                if (seen.TryGetValue(node.type, out var first))
                {
                    issues.Add($"duplicate type {node.type}: \"{first.name}\" and \"{node.name}\"");
                    continue;
                }
                seen[node.type] = node;
            }
        }

        static void CheckMissingReferences(List<SkillNodeView> nodes, List<string> issues)
        {
            foreach (var node in nodes)
            {
                if (node == null) continue;
                var button = ResolveButton(node);
                var icon = ResolveIcon(node);
                var statusText = ResolveStatusText(node);
                if (button == null) issues.Add($"{node.type}: missing button");
                if (icon == null) issues.Add($"{node.type}: missing icon Image");
                else if (icon.sprite == null) issues.Add($"{node.type}: icon sprite is null");
                if (statusText == null) issues.Add($"{node.type}: missing status text");
                if (node.RectTransform == null) issues.Add($"{node.type}: missing RectTransform");
            }
        }

        static void CheckOverlaps(List<SkillNodeView> nodes, List<string> issues)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var a = NodeRect(nodes[i]);
                if (a.width <= 0f || a.height <= 0f) continue;
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    if (!SharesRectParent(nodes[i], nodes[j])) continue;
                    var b = NodeRect(nodes[j]);
                    if (b.width <= 0f || b.height <= 0f) continue;
                    if (a.Overlaps(b))
                    {
                        issues.Add($"overlap: {nodes[i].type} \"{nodes[i].name}\" and {nodes[j].type} \"{nodes[j].name}\"");
                    }
                }
            }
        }

        static void CheckLinks(List<SkillNodeView> nodes, List<string> issues)
        {
            var byType = new Dictionary<UpgradeType, SkillNodeView>();
            foreach (var node in nodes)
            {
                if (node != null && !byType.ContainsKey(node.type)) byType.Add(node.type, node);
            }

            foreach (var node in nodes)
            {
                if (node == null) continue;
                var routes = node.linkRoutes;
                if (routes != null && routes.Length > 0)
                {
                    foreach (var route in routes)
                    {
                        CheckLinkRoute(byType, node, route.prerequisite, route.waypoints, issues);
                    }
                }
                else
                {
                    foreach (var prerequisite in node.EffectivePrerequisites())
                    {
                        CheckLinkRoute(byType, node, prerequisite, null, issues);
                    }
                }
            }
        }

        static void CheckLinkRoute(Dictionary<UpgradeType, SkillNodeView> byType, SkillNodeView node, UpgradeType prerequisite, Vector2Int[] waypoints, List<string> issues)
        {
            if (!byType.TryGetValue(prerequisite, out var parent))
            {
                issues.Add($"{node.type}: missing prerequisite node {prerequisite}");
                return;
            }

            var previous = parent.GridToAnchored(parent.gridPosition);
            if (waypoints != null)
            {
                foreach (var waypoint in waypoints)
                {
                    var current = node.GridToAnchored(waypoint);
                    CheckSegment(parent.type, node.type, previous, current, issues);
                    previous = current;
                }
            }

            CheckSegment(parent.type, node.type, previous, node.GridToAnchored(node.gridPosition), issues);
        }

        static void CheckSegment(UpgradeType parent, UpgradeType child, Vector2 from, Vector2 to, List<string> issues)
        {
            if (IsAllowedSegment(from, to)) return;
            issues.Add($"non-grid link: {parent} -> {child} {Vector(from)} to {Vector(to)}");
        }

        static Rect NodeRect(SkillNodeView node)
        {
            if (node == null || node.RectTransform == null) return new Rect();
            var rect = node.RectTransform;
            var size = rect.sizeDelta;
            return new Rect(rect.anchoredPosition - size * 0.5f, size);
        }

        static bool SharesRectParent(SkillNodeView a, SkillNodeView b)
        {
            var aRect = a != null ? a.RectTransform : null;
            var bRect = b != null ? b.RectTransform : null;
            return aRect != null && bRect != null && aRect.parent == bRect.parent;
        }

        static bool IsAllowedSegment(Vector2 from, Vector2 to)
        {
            float dx = Mathf.Abs(to.x - from.x);
            float dy = Mathf.Abs(to.y - from.y);
            const float tolerance = 0.1f;
            return dx <= tolerance || dy <= tolerance || Mathf.Abs(dx - dy) <= tolerance;
        }

        static string SpriteName(Sprite sprite)
        {
            return sprite != null ? sprite.name : "null";
        }

        static string ParentName(RectTransform rect)
        {
            return rect != null && rect.parent != null ? rect.parent.name : "missing";
        }

        static Button ResolveButton(SkillNodeView node)
        {
            if (node == null) return null;
            if (node.button != null) return node.button;
            var child = node.transform.Find("Node Button");
            return child != null ? child.GetComponent<Button>() : null;
        }

        static Image ResolveIcon(SkillNodeView node)
        {
            if (node == null) return null;
            if (node.icon != null) return node.icon;
            var child = node.transform.Find("Node Button/Icon");
            return child != null ? child.GetComponent<Image>() : null;
        }

        static Text ResolveStatusText(SkillNodeView node)
        {
            if (node == null) return null;
            if (node.statusText != null) return node.statusText;
            var child = node.transform.Find("Node Cost");
            return child != null ? child.GetComponent<Text>() : null;
        }

        static string Sanitize(string text)
        {
            return string.IsNullOrEmpty(text) ? "" : text.Replace("\r", " ").Replace("\n", " ");
        }

        static string Vector(Vector2 value)
        {
            return $"({value.x:0.#},{value.y:0.#})";
        }
    }
}
