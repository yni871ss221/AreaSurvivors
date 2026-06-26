using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class BuildingPrefabVisualReporter
    {
        static readonly string[] PrefabPaths =
        {
            "Assets/AreaSurvivors/Prefabs/WoodenWall.prefab",
            "Assets/AreaSurvivors/Prefabs/BallistaTower.prefab",
            "Assets/AreaSurvivors/Prefabs/WatchTower.prefab"
        };

        [MenuItem("Area Survivors/Reports/Building Prefab Visuals")]
        public static void LogBuildingPrefabVisuals()
        {
            var report = BuildReport();
            Debug.Log(ReportOutputUtility.SaveAndSummarize("Building prefab visual report", report, "building-prefab-visuals"));
        }

        static string BuildReport()
        {
            var report = new StringBuilder(8192);
            report.AppendLine("AreaSurvivors Building Prefab Visuals");
            foreach (var path in PrefabPaths)
            {
                AppendPrefab(report, path);
            }
            return report.ToString();
        }

        static void AppendPrefab(StringBuilder report, string path)
        {
            report.AppendLine();
            report.AppendLine($"[Prefab] {path}");
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                report.AppendLine("- missing");
                return;
            }

            try
            {
                var marker = root.GetComponent<GridObjectMarker>();
                var gridVisual = root.GetComponent<GridObjectVisual>();
                var visualSet = root.GetComponent<BuildingPrefabVisualSet>();
                var colliders = root.GetComponentsInChildren<Collider2D>(true);

                report.AppendLine($"- name: {root.name}");
                report.AppendLine($"- marker: {Bool(marker != null)} footprint={Footprint(marker)}");
                report.AppendLine($"- gridVisual: {Bool(gridVisual != null)} footprint={GridVisualFootprint(gridVisual)} fitWidth={Bool(gridVisual != null && gridVisual.fitVisualWidthToFootprint)} resetOffset={Bool(gridVisual != null && gridVisual.resetVisualOffset)}");
                report.AppendLine($"- visualSet: {Bool(visualSet != null)} base={VisualName(visualSet != null ? visualSet.completeVisual : null)} upgrade={VisualName(visualSet != null ? visualSet.upgradedCompleteVisual : null)} sparkle={VisualName(visualSet != null ? visualSet.sparkleVisual : null)}");
                AppendTransformHealth(report, root);
                report.AppendLine($"- colliders: {colliders.Length}");
                foreach (var line in ColliderLines(colliders))
                {
                    report.AppendLine(line);
                }

                AppendSpriteUsers(report, root);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void AppendSpriteUsers(StringBuilder report, GameObject root)
        {
            var lines = new List<string>();
            foreach (var mesh in root.GetComponentsInChildren<PaperMeshVisual>(true))
            {
                lines.Add($"- mesh {PathFromRoot(root.transform, mesh.transform)} sprite={SpriteName(mesh.sprite)} order={mesh.order} {SpriteMetrics(mesh)}");
            }

            var barrier = root.GetComponent<WoodenBarrier>();
            if (barrier != null)
            {
                lines.Add($"- woodenBarrier barrierSprite={SpriteName(barrier.barrierSprite)}");
            }

            if (root.GetComponent<BallistaTower>() != null) lines.Add("- component BallistaTower=yes");
            if (root.GetComponent<WatchTower>() != null) lines.Add("- component WatchTower=yes");

            report.AppendLine($"- spriteUsers: {lines.Count}");
            foreach (var line in lines)
            {
                report.AppendLine(line);
            }
        }

        static void AppendTransformHealth(StringBuilder report, GameObject root)
        {
            var warnings = new List<string>();
            int allowedScaleExceptions = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == root.transform) continue;
                if (!Approximately(transform.localRotation, Quaternion.identity))
                {
                    warnings.Add($"rotation path={PathFromRoot(root.transform, transform)} localEuler={transform.localEulerAngles}");
                }

                if (!Approximately(transform.localScale, Vector3.one))
                {
                    if (IsAllowedScaleException(transform))
                    {
                        allowedScaleExceptions++;
                        continue;
                    }

                    warnings.Add($"scale path={PathFromRoot(root.transform, transform)} localScale={transform.localScale}");
                }
            }

            report.AppendLine($"- transformWarnings: {warnings.Count}");
            report.AppendLine($"- allowedScaleExceptions: {allowedScaleExceptions}");
            foreach (var warning in warnings)
            {
                report.AppendLine($"  - {warning}");
            }
        }

        static bool IsAllowedScaleException(Transform transform)
        {
            return transform != null && transform.name == "Completion Sparkle";
        }

        static IEnumerable<string> ColliderLines(Collider2D[] colliders)
        {
            foreach (var collider in colliders)
            {
                if (collider == null) continue;
                yield return $"- collider {collider.GetType().Name} path={PathFromRoot(collider.transform.root, collider.transform)} enabled={Bool(collider.enabled)} trigger={Bool(collider.isTrigger)}";
            }
        }

        static string PathFromRoot(Transform root, Transform target)
        {
            if (root == null || target == null) return "missing";
            if (root == target) return target.name;
            var names = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                names.Add(current.name);
                current = current.parent;
            }
            names.Add(root.name);
            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        static string Bool(bool value)
        {
            return value ? "yes" : "no";
        }

        static string Footprint(GridObjectMarker marker)
        {
            return marker != null ? $"({marker.footprint.x},{marker.footprint.y})" : "missing";
        }

        static string GridVisualFootprint(GridObjectVisual visual)
        {
            return visual != null ? $"({visual.footprint.x},{visual.footprint.y})" : "missing";
        }

        static string VisualName(PaperMeshVisual visual)
        {
            return visual != null ? PathFromRoot(visual.transform.root, visual.transform) : "missing";
        }

        static string SpriteName(Sprite sprite)
        {
            return sprite != null ? sprite.name : "missing";
        }

        static string SpriteMetrics(PaperMeshVisual visual)
        {
            if (visual == null || visual.sprite == null) return "metrics=missing";
            var sprite = visual.sprite;
            var bounds = sprite.bounds.size;
            var rect = sprite.textureRect;
            var scale = visual.transform.localScale;
            var world = new Vector2(bounds.x * scale.x, bounds.y * scale.y);
            return $"px=({rect.width:0},{rect.height:0}) bounds=({bounds.x:0.###},{bounds.y:0.###}) scale=({scale.x:0.###},{scale.y:0.###},{scale.z:0.###}) world=({world.x:0.###},{world.y:0.###}) ppu={sprite.pixelsPerUnit:0.###}";
        }

        static bool Approximately(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f && Mathf.Abs(a.z - b.z) < 0.001f;
        }

        static bool Approximately(Quaternion a, Quaternion b)
        {
            return Mathf.Abs(Quaternion.Dot(a, b)) > 0.9999f;
        }
    }
}
