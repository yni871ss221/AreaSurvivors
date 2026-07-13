using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [ExecuteAlways]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SkillLinkView : MaskableGraphic
    {
        public enum LinkAnchor
        {
            Auto,
            Top,
            Right,
            Bottom,
            Left
        }

        static readonly Vector3[] Corners = new Vector3[4];

        public UpgradeType prerequisite;
        public SkillNodeView fromNode;
        public SkillNodeView toNode;
        public LinkAnchor fromAnchor;
        public LinkAnchor toAnchor;
        public Vector2[] waypoints;
        [Min(1f)] public float thickness = 5f;
        [Min(0f)] public float cornerRadius = 10f;
        [Range(1, 12)] public int cornerSegments = 5;
        public Color activeColor = new Color(0.50f, 0.92f, 0.72f, 0.85f);
        public Color inactiveColor = new Color(0.30f, 0.36f, 0.34f, 0.75f);

        float revealProgress = 1f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            raycastTarget = false;
            SetVerticesDirty();
        }
#endif

        void LateUpdate()
        {
            if (!Application.isPlaying) SetVerticesDirty();
        }

        public void ApplyState(bool active)
        {
            color = active ? activeColor : inactiveColor;
            SetVerticesDirty();
        }

        public void SetRevealProgress(float progress)
        {
            float nextProgress = Mathf.Clamp01(progress);
            if (Mathf.Approximately(revealProgress, nextProgress)) return;
            revealProgress = nextProgress;
            SetVerticesDirty();
        }

        public void ApplyDirectionalAnchors(float verticalThreshold = 20f)
        {
            if (fromNode == null || toNode == null || fromNode.RectTransform == null || toNode.RectTransform == null)
            {
                fromAnchor = LinkAnchor.Auto;
                toAnchor = LinkAnchor.Auto;
                return;
            }

            var fromRect = RectInLocal(fromNode.RectTransform);
            var toRect = RectInLocal(toNode.RectTransform);
            Vector2 delta = toRect.center - fromRect.center;
            if (Mathf.Abs(delta.y) >= verticalThreshold)
            {
                bool toBelow = delta.y < 0f;
                fromAnchor = toBelow ? LinkAnchor.Bottom : LinkAnchor.Top;
                toAnchor = toBelow ? LinkAnchor.Top : LinkAnchor.Bottom;
            }
            else if (Mathf.Abs(delta.x) > 0.01f)
            {
                bool toRight = delta.x > 0f;
                fromAnchor = toRight ? LinkAnchor.Right : LinkAnchor.Left;
                toAnchor = toRight ? LinkAnchor.Left : LinkAnchor.Right;
            }
            else
            {
                fromAnchor = LinkAnchor.Auto;
                toAnchor = LinkAnchor.Auto;
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (fromNode == null || toNode == null || fromNode.RectTransform == null || toNode.RectTransform == null) return;

            var points = BuildVisibleRoute(BuildRoundedRoute(BuildRoute()));
            if (points.Count < 2) return;

            for (int i = 0; i < points.Count - 1; i++)
            {
                AddSegment(vh, points[i], points[i + 1]);
            }

            float radius = Mathf.Max(1f, thickness) * 0.5f;
            for (int i = 0; i < points.Count; i++)
            {
                AddDisc(vh, points[i], radius);
            }
        }

        List<Vector2> BuildVisibleRoute(List<Vector2> route)
        {
            if (route == null || route.Count < 2 || revealProgress >= 0.999f) return route;
            if (revealProgress <= 0.001f) return new List<Vector2>();

            float totalLength = 0f;
            for (int i = 0; i < route.Count - 1; i++)
            {
                totalLength += Vector2.Distance(route[i], route[i + 1]);
            }

            if (totalLength <= 0.01f) return route;

            float targetLength = totalLength * revealProgress;
            float accumulatedLength = 0f;
            var visibleRoute = new List<Vector2>(route.Count) { route[0] };
            for (int i = 0; i < route.Count - 1; i++)
            {
                Vector2 start = route[i];
                Vector2 end = route[i + 1];
                float segmentLength = Vector2.Distance(start, end);
                if (segmentLength <= 0.001f) continue;

                if (accumulatedLength + segmentLength < targetLength)
                {
                    visibleRoute.Add(end);
                    accumulatedLength += segmentLength;
                    continue;
                }

                float segmentProgress = Mathf.Clamp01((targetLength - accumulatedLength) / segmentLength);
                visibleRoute.Add(Vector2.Lerp(start, end, segmentProgress));
                break;
            }

            return visibleRoute;
        }

        List<Vector2> BuildRoute()
        {
            var fromRect = RectInLocal(fromNode.RectTransform);
            var toRect = RectInLocal(toNode.RectTransform);
            var route = new List<Vector2>(6);
            Vector2 fromCenter = fromRect.center;
            Vector2 toCenter = toRect.center;

            if (waypoints != null && waypoints.Length > 0)
            {
                Vector2 first = waypoints[0];
                route.Add(AnchorPoint(fromRect, fromAnchor, first));
                route.Add(first);
                for (int i = 1; i < waypoints.Length; i++) route.Add(waypoints[i]);
                route.Add(AnchorPoint(toRect, toAnchor, route[route.Count - 1]));
                return route;
            }

            Vector2 start = AnchorPoint(fromRect, fromAnchor, toCenter);
            Vector2 end = AnchorPoint(toRect, toAnchor, fromCenter);
            route.Add(start);

            const float alignedTolerance = 0.1f;
            bool hasHorizontalGap = Mathf.Abs(start.x - end.x) > alignedTolerance;
            bool hasVerticalGap = Mathf.Abs(start.y - end.y) > alignedTolerance;
            if (hasHorizontalGap && hasVerticalGap)
            {
                float trunkY = (start.y + end.y) * 0.5f;
                route.Add(new Vector2(start.x, trunkY));
                route.Add(new Vector2(end.x, trunkY));
            }

            route.Add(end);
            return route;
        }

        List<Vector2> BuildRoundedRoute(List<Vector2> route)
        {
            if (route == null || route.Count < 3 || cornerRadius <= 0.01f) return route;

            var rounded = new List<Vector2>(route.Count * (cornerSegments + 1));
            rounded.Add(route[0]);

            for (int i = 1; i < route.Count - 1; i++)
            {
                Vector2 previous = route[i - 1];
                Vector2 corner = route[i];
                Vector2 next = route[i + 1];
                Vector2 incoming = corner - previous;
                Vector2 outgoing = next - corner;
                float incomingLength = incoming.magnitude;
                float outgoingLength = outgoing.magnitude;

                if (incomingLength < 0.01f || outgoingLength < 0.01f)
                {
                    rounded.Add(corner);
                    continue;
                }

                Vector2 incomingDirection = incoming / incomingLength;
                Vector2 outgoingDirection = outgoing / outgoingLength;
                if (Vector2.Dot(incomingDirection, outgoingDirection) > 0.999f)
                {
                    rounded.Add(corner);
                    continue;
                }

                float radius = Mathf.Min(cornerRadius, incomingLength * 0.5f, outgoingLength * 0.5f);
                Vector2 entry = corner - incomingDirection * radius;
                Vector2 exit = corner + outgoingDirection * radius;
                rounded.Add(entry);

                int steps = Mathf.Max(1, cornerSegments);
                for (int step = 1; step <= steps; step++)
                {
                    float t = step / (float)steps;
                    rounded.Add(QuadraticBezier(entry, corner, exit, t));
                }
            }

            rounded.Add(route[route.Count - 1]);
            return rounded;
        }

        static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * start + 2f * oneMinusT * t * control + t * t * end;
        }

        Rect RectInLocal(RectTransform target)
        {
            target.GetWorldCorners(Corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < Corners.Length; i++)
            {
                Vector2 local = rectTransform.InverseTransformPoint(Corners[i]);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static Vector2 AnchorPoint(Rect rect, LinkAnchor anchor, Vector2 toward)
        {
            switch (anchor)
            {
                case LinkAnchor.Top:
                    return new Vector2(rect.center.x, rect.yMax);
                case LinkAnchor.Right:
                    return new Vector2(rect.xMax, rect.center.y);
                case LinkAnchor.Bottom:
                    return new Vector2(rect.center.x, rect.yMin);
                case LinkAnchor.Left:
                    return new Vector2(rect.xMin, rect.center.y);
                default:
                    return AutoEdgeCenter(rect, toward);
            }
        }

        static Vector2 AutoEdgeCenter(Rect rect, Vector2 toward)
        {
            Vector2 delta = toward - rect.center;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                return new Vector2(delta.x >= 0f ? rect.xMax : rect.xMin, rect.center.y);
            }

            return new Vector2(rect.center.x, delta.y >= 0f ? rect.yMax : rect.yMin);
        }

        void AddSegment(VertexHelper vh, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.01f) return;

            Vector2 normal = new Vector2(-delta.y, delta.x).normalized * (Mathf.Max(1f, thickness) * 0.5f);
            int index = vh.currentVertCount;
            AddVertex(vh, from - normal);
            AddVertex(vh, from + normal);
            AddVertex(vh, to + normal);
            AddVertex(vh, to - normal);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }

        void AddVertex(VertexHelper vh, Vector2 position)
        {
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vh.AddVert(vertex);
        }

        void AddDisc(VertexHelper vh, Vector2 center, float radius)
        {
            const int segments = 10;
            int startIndex = vh.currentVertCount;
            AddVertex(vh, center);
            for (int i = 0; i <= segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                AddVertex(vh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }

            for (int i = 1; i <= segments; i++)
            {
                vh.AddTriangle(startIndex, startIndex + i, startIndex + i + 1);
            }
        }
    }
}
