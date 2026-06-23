using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.Editor
{
    public static class ScenePrefabSearchReporter
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string PrefabRoot = "Assets/AreaSurvivors/Prefabs";
        const string SearchQueryKey = "AreaSurvivors.Report.SearchQuery";
        const int MaxRows = 80;

        [MenuItem("Area Survivors/Reports/Scene Prefab Search")]
        public static void LogSearch()
        {
            var report = BuildReport(SearchQuery);
            Debug.Log(ReportOutputUtility.SaveAndSummarize("Scene/prefab search", report, "scene-prefab-search"));
        }

        [MenuItem("Area Survivors/Reports/Copy Scene Prefab Search")]
        public static void CopySearch()
        {
            var report = BuildReport(SearchQuery);
            EditorGUIUtility.systemCopyBuffer = report;
            Debug.Log("Scene/prefab search report copied to clipboard.");
        }

        static string SearchQuery
        {
            get
            {
                var query = EditorPrefs.GetString(SearchQueryKey, "Build");
                return string.IsNullOrWhiteSpace(query) ? "Build" : query.Trim();
            }
        }

        static string BuildReport(string query)
        {
            var report = new StringBuilder(4096);
            report.AppendLine("AreaSurvivors Scene/Prefab Search");
            report.AppendLine($"Query: {query}");
            report.AppendLine($"EditorPrefs key: {SearchQueryKey}");
            report.AppendLine();
            AppendSceneMatches(report, query);
            AppendPrefabMatches(report, query);
            return report.ToString();
        }

        static void AppendSceneMatches(StringBuilder report, string query)
        {
            report.AppendLine("[Scene Matches]");
            var scene = OpenSceneIfNeeded(GameScenePath, out var openedHere);
            if (!scene.IsValid())
            {
                report.AppendLine($"- missing: {GameScenePath}");
                return;
            }

            var rows = new List<string>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (IsMissingScriptShell(transform.gameObject)) continue;
                    if (!Matches(transform.gameObject, query)) continue;
                    rows.Add(FormatObjectRow(transform, scene.path));
                    if (rows.Count >= MaxRows) break;
                }
                if (rows.Count >= MaxRows) break;
            }

            report.AppendLine($"- shown: {rows.Count}, cappedAt: {MaxRows}");
            foreach (var row in rows) report.AppendLine(row);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            report.AppendLine();
        }

        static void AppendPrefabMatches(StringBuilder report, string query)
        {
            report.AppendLine("[Prefab Matches]");
            var rows = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    if (IsMissingScriptShell(transform.gameObject)) continue;
                    if (!Matches(transform.gameObject, query)) continue;
                    rows.Add(FormatObjectRow(transform, path));
                    if (rows.Count >= MaxRows) break;
                }
                if (rows.Count >= MaxRows) break;
            }

            report.AppendLine($"- shown: {rows.Count}, cappedAt: {MaxRows}");
            foreach (var row in rows) report.AppendLine(row);
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

        static bool IsMissingScriptShell(GameObject target)
        {
            var components = target.GetComponents<Component>();
            var hasMissingScript = false;
            foreach (var component in components)
            {
                if (component == null)
                {
                    hasMissingScript = true;
                    continue;
                }

                if (!(component is Transform))
                    return false;
            }

            return hasMissingScript;
        }

        static bool Matches(GameObject target, string query)
        {
            if (target.name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (var component in target.GetComponents<Component>())
            {
                if (component == null) continue;
                if (component.GetType().Name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        static string FormatObjectRow(Transform transform, string source)
        {
            var components = transform.GetComponents<Component>();
            var componentNames = new List<string>();
            foreach (var component in components)
            {
                componentNames.Add(component != null ? component.GetType().Name : "MissingScript");
            }

            var rect = transform as RectTransform;
            var rectInfo = rect != null
                ? $", rect=pos({rect.anchoredPosition.x:0.#},{rect.anchoredPosition.y:0.#}) size({rect.rect.width:0.#},{rect.rect.height:0.#})"
                : string.Empty;
            return $"- {source} :: {GetPath(transform)} :: components={string.Join(",", componentNames)}{rectInfo}";
        }

        static string GetPath(Transform transform)
        {
            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }
    }
}
