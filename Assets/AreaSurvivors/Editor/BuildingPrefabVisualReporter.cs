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
            "Assets/AreaSurvivors/Prefabs/WoodenGate.prefab",
            "Assets/AreaSurvivors/Prefabs/BallistaTower.prefab",
            "Assets/AreaSurvivors/Prefabs/WatchTower.prefab",
            "Assets/AreaSurvivors/Prefabs/CarpenterHut.prefab",
            "Assets/AreaSurvivors/Prefabs/WorkerHut.prefab"
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
                report.AppendLine($"- visualSet: {Bool(visualSet != null)} base={VisualName(visualSet != null ? visualSet.completeVisual : null)} upgrade={VisualName(visualSet != null ? visualSet.upgradedCompleteVisual : null)} sparkle={VisualName(visualSet != null ? visualSet.sparkleVisual : null)} upgradeOpenSprite={SpriteName(visualSet != null ? visualSet.upgradedOpenSprite : null)}");
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
                lines.Add($"- mesh {PathFromRoot(root.transform, mesh.transform)} sprite={SpriteName(mesh.sprite)} order={mesh.order}");
            }

            var barrier = root.GetComponent<WoodenBarrier>();
            if (barrier != null)
            {
                lines.Add($"- woodenBarrier barrierSprite={SpriteName(barrier.barrierSprite)} openSprite={SpriteName(barrier.openGateSprite)} gate={Bool(barrier.gate)}");
            }

            if (root.GetComponent<BallistaTower>() != null) lines.Add("- component BallistaTower=yes");
            if (root.GetComponent<WatchTower>() != null) lines.Add("- component WatchTower=yes");
            if (root.GetComponent<CarpenterHut>() != null) lines.Add("- component CarpenterHut=yes");
            if (root.GetComponent<WorkerHut>() != null) lines.Add("- component WorkerHut=yes");

            report.AppendLine($"- spriteUsers: {lines.Count}");
            foreach (var line in lines)
            {
                report.AppendLine(line);
            }
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
    }
}
