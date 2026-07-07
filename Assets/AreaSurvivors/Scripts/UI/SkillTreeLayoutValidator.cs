using UnityEngine;

namespace AreaSurvivors
{
    public sealed class SkillTreeLayoutValidator : MonoBehaviour
    {
        public bool validateOnEdit = true;
        public Vector2 nodePadding = new Vector2(8f, 8f);

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!validateOnEdit) return;
            Validate();
        }

        [ContextMenu("Validate Skill Tree Layout")]
        public void Validate()
        {
            var nodes = GetComponentsInChildren<SkillNodeView>(true);
            ValidateDuplicateTypes(nodes);
            ValidateOverlaps(nodes);
            ValidateLinks(nodes);
        }

        [ContextMenu("Rebuild Scene Link Segments")]
        public void RebuildSceneLinkSegments()
        {
            var nodes = GetComponentsInChildren<SkillNodeView>(true);
            var linkRoot = EnsureLinkRoot();
            if (linkRoot == null)
            {
                return;
            }

            while (linkRoot.childCount > 0)
            {
                DestroyImmediate(linkRoot.GetChild(0).gameObject);
            }

            foreach (var node in nodes)
            {
                if (node == null) continue;
                var routes = node.linkRoutes;
                if (routes != null && routes.Length > 0)
                {
                    foreach (var route in routes)
                    {
                        BuildSceneLink(nodes, linkRoot, node, route.prerequisite, route.waypoints);
                    }
                }
                else
                {
                    foreach (var prerequisite in node.EffectivePrerequisites())
                    {
                        BuildSceneLink(nodes, linkRoot, node, prerequisite, null);
                    }
                }
            }

            linkRoot.SetAsFirstSibling();
            UnityEditor.EditorUtility.SetDirty(gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        RectTransform EnsureLinkRoot()
        {
            var existing = transform.Find("Skill Links");
            RectTransform linkRoot;
            if (existing == null)
            {
                linkRoot = new GameObject("Skill Links", typeof(RectTransform)).GetComponent<RectTransform>();
                linkRoot.SetParent(transform, false);
            }
            else
            {
                linkRoot = existing as RectTransform;
                if (linkRoot == null)
                {
                    DestroyImmediate(existing.gameObject);
                    linkRoot = new GameObject("Skill Links", typeof(RectTransform)).GetComponent<RectTransform>();
                    linkRoot.SetParent(transform, false);
                }
            }

            StretchToParent(linkRoot);
            return linkRoot;
        }

        void ValidateDuplicateTypes(SkillNodeView[] nodes)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                for (int j = i + 1; j < nodes.Length; j++)
                {
                    if (nodes[i] != null && nodes[j] != null && nodes[i].type == nodes[j].type)
                    {
                        Debug.LogWarning($"SkillTree: duplicate skill id {nodes[i].type} on '{nodes[i].name}' and '{nodes[j].name}'.", this);
                    }
                }
            }
        }

        void ValidateOverlaps(SkillNodeView[] nodes)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                var a = NodeRect(nodes[i]);
                for (int j = i + 1; j < nodes.Length; j++)
                {
                    var b = NodeRect(nodes[j]);
                    if (a.Overlaps(b))
                    {
                        Debug.LogWarning($"SkillTree: node overlap between '{nodes[i].name}' and '{nodes[j].name}'.", this);
                    }
                }
            }
        }

        void ValidateLinks(SkillNodeView[] nodes)
        {
            foreach (var node in nodes)
            {
                if (node == null) continue;
                var routes = node.linkRoutes;
                if (routes != null && routes.Length > 0)
                {
                    foreach (var route in routes)
                    {
                        ValidateLink(nodes, node, route.prerequisite, route.waypoints);
                    }
                }
                else
                {
                    foreach (var prerequisite in node.EffectivePrerequisites())
                    {
                        ValidateLink(nodes, node, prerequisite, null);
                    }
                }
            }
        }

        void ValidateLink(SkillNodeView[] nodes, SkillNodeView node, UpgradeType prerequisite, Vector2Int[] waypoints)
        {
            var parent = FindNode(nodes, prerequisite);
            if (parent == null)
            {
                Debug.LogWarning($"SkillTree: '{node.name}' references missing prerequisite {prerequisite}.", node);
                return;
            }

            var previous = parent.GridToAnchored(parent.gridPosition);
            if (waypoints != null)
            {
                foreach (var waypoint in waypoints)
                {
                    var current = node.GridToAnchored(waypoint);
                    WarnIfInvalidSegment(parent, node, previous, current);
                    previous = current;
                }
            }

            WarnIfInvalidSegment(parent, node, previous, node.GridToAnchored(node.gridPosition));
        }

        static SkillNodeView FindNode(SkillNodeView[] nodes, UpgradeType type)
        {
            foreach (var node in nodes)
            {
                if (node != null && node.type == type) return node;
            }

            return null;
        }

        static void BuildSceneLink(SkillNodeView[] nodes, Transform linkRoot, SkillNodeView node, UpgradeType prerequisite, Vector2Int[] waypoints)
        {
            var parent = FindNode(nodes, prerequisite);
            if (parent == null) return;

            var link = new GameObject($"{parent.type} to {node.type}", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkillLinkView))
                .GetComponent<SkillLinkView>();
            link.transform.SetParent(linkRoot, false);
            StretchToParent((RectTransform)link.transform);
            link.prerequisite = prerequisite;
            link.fromNode = parent;
            link.toNode = node;
            link.thickness = 5f;
            link.cornerRadius = 12f;
            link.cornerSegments = 6;

            if (waypoints != null)
            {
                var converted = new Vector2[waypoints.Length];
                int index = 0;
                foreach (var waypoint in waypoints)
                {
                    converted[index++] = node.GridToAnchored(waypoint);
                }

                link.waypoints = converted;
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

        Rect NodeRect(SkillNodeView node)
        {
            if (node == null || node.RectTransform == null) return new Rect();
            var rect = node.RectTransform;
            var position = rect.anchoredPosition;
            var size = rect.sizeDelta + nodePadding;
            return new Rect(position - size * 0.5f, size);
        }

        static void WarnIfInvalidSegment(SkillNodeView parent, SkillNodeView node, Vector2 from, Vector2 to)
        {
            if (IsAllowedSegment(from, to)) return;
            Debug.LogWarning($"SkillTree: link {parent.type} -> {node.type} has a non-grid segment ({from} -> {to}). Use horizontal, vertical, or 45 degree waypoints.", node);
        }

        static bool IsAllowedSegment(Vector2 from, Vector2 to)
        {
            float dx = Mathf.Abs(to.x - from.x);
            float dy = Mathf.Abs(to.y - from.y);
            const float tolerance = 0.1f;
            return dx <= tolerance || dy <= tolerance || Mathf.Abs(dx - dy) <= tolerance;
        }
#endif
    }
}
