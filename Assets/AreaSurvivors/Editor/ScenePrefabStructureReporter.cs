using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class ScenePrefabStructureReporter
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string PrefabRoot = "Assets/AreaSurvivors/Prefabs";

        [MenuItem("Area Survivors/Reports/Scene Prefab Structure")]
        public static void LogStructure()
        {
            Debug.Log(BuildReport());
        }

        [MenuItem("Area Survivors/Reports/Copy Scene Prefab Structure")]
        public static void CopyStructure()
        {
            var report = BuildReport();
            EditorGUIUtility.systemCopyBuffer = report;
            Debug.Log("Scene/prefab structure report copied to clipboard.");
        }

        static string BuildReport()
        {
            var report = new StringBuilder(8192);
            report.AppendLine("AreaSurvivors Scene/Prefab Structure");
            AppendScene(report, GameScenePath);
            AppendPrefabs(report);
            return report.ToString();
        }

        static void AppendScene(StringBuilder report, string scenePath)
        {
            report.AppendLine();
            report.AppendLine("[Scene]");
            var wasLoaded = false;
            var scene = new Scene();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == scenePath)
                {
                    scene = loaded;
                    wasLoaded = true;
                    break;
                }
            }

            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            if (!scene.IsValid())
            {
                report.AppendLine($"- missing: {scenePath}");
                return;
            }

            var roots = scene.GetRootGameObjects();
            int objects = 0;
            int missingScripts = 0;
            int cameras = 0;
            int canvases = 0;
            int tilemaps = 0;
            int images = 0;
            int texts = 0;
            int gameManagers = 0;
            int buildControllers = 0;

            foreach (var root in roots)
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    objects++;
                    missingScripts += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                }
                cameras += root.GetComponentsInChildren<Camera>(true).Length;
                canvases += root.GetComponentsInChildren<Canvas>(true).Length;
                tilemaps += root.GetComponentsInChildren<Tilemap>(true).Length;
                images += root.GetComponentsInChildren<Image>(true).Length;
                texts += root.GetComponentsInChildren<Text>(true).Length;
                gameManagers += root.GetComponentsInChildren<GameManager>(true).Length;
                buildControllers += root.GetComponentsInChildren<BuildPlacementController>(true).Length;
            }

            report.AppendLine($"- path: {scenePath}");
            report.AppendLine($"- roots: {roots.Length}, objects: {objects}, missingScripts: {missingScripts}");
            report.AppendLine($"- cameras: {cameras}, canvases: {canvases}, tilemaps: {tilemaps}, images: {images}, texts: {texts}");
            report.AppendLine($"- GameManager: {gameManagers}, BuildPlacementController: {buildControllers}");
            report.AppendLine("- root objects:");
            foreach (var root in roots)
            {
                report.AppendLine($"  - {root.name}");
            }

            if (!wasLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void AppendPrefabs(StringBuilder report)
        {
            report.AppendLine();
            report.AppendLine("[Prefabs]");
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot });
            var rows = new List<string>();
            int totalMissingScripts = 0;
            int visualSets = 0;
            int paperVisuals = 0;
            int nullPaperSprites = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                int missing = CountMissingScripts(prefab);
                var visualSetCount = prefab.GetComponentsInChildren<BuildingPrefabVisualSet>(true).Length;
                var visuals = prefab.GetComponentsInChildren<PaperMeshVisual>(true);
                int nullSprites = 0;
                foreach (var visual in visuals)
                {
                    if (visual.sprite == null) nullSprites++;
                }

                totalMissingScripts += missing;
                visualSets += visualSetCount;
                paperVisuals += visuals.Length;
                nullPaperSprites += nullSprites;
                rows.Add($"- {Path.GetFileNameWithoutExtension(path)}: missingScripts={missing}, visualSets={visualSetCount}, paperVisuals={visuals.Length}, nullPaperSprites={nullSprites}");
            }

            rows.Sort();
            report.AppendLine($"- prefabs: {rows.Count}, missingScripts: {totalMissingScripts}, visualSets: {visualSets}, paperVisuals: {paperVisuals}, nullPaperSprites: {nullPaperSprites}");
            foreach (var row in rows)
            {
                report.AppendLine(row);
            }
        }

        static int CountMissingScripts(GameObject root)
        {
            int count = 0;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            }
            return count;
        }
    }
}
