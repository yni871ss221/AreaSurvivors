using System;
using System.IO;
using System.Linq;
using AreaSurvivors.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.EditorTools
{
    public static class RuntimePerformanceSentinelSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string SetupMarkerRelativePath =
            "Library/AreaSafeUnity/runtime-performance-sentinel-setup.success";

        [MenuItem("Area Survivors/Setup/Runtime Performance Sentinel")]
        public static void Apply()
        {
            string markerPath = GetProjectPath(SetupMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty)
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                throw new InvalidOperationException(
                    "05_Game.unity has unsaved changes. Runtime Performance Sentinel setup did not modify it.");
            }

            try
            {
                GameManager[] managers = FindInScene<GameManager>(scene);
                if (managers.Length != 1)
                {
                    throw new InvalidOperationException(
                        "Runtime Performance Sentinel setup requires exactly one GameManager. found=" +
                        managers.Length);
                }

                RuntimePerformanceSentinel[] sentinels =
                    FindInScene<RuntimePerformanceSentinel>(scene);
                bool changed = false;
                if (sentinels.Length == 0)
                {
                    Undo.AddComponent<RuntimePerformanceSentinel>(managers[0].gameObject);
                    changed = true;
                }
                else if (sentinels.Length != 1 || sentinels[0].gameObject != managers[0].gameObject)
                {
                    throw new InvalidOperationException(
                        "Runtime Performance Sentinel setup found an invalid existing component placement.");
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    {
                        throw new InvalidOperationException(
                            "Failed to save 05_Game.unity after adding RuntimePerformanceSentinel.");
                    }
                }

                if (!RuntimePerformanceSentinelValidator.Validate(false))
                {
                    throw new InvalidOperationException(
                        "Runtime Performance Sentinel setup completed but validation failed.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
                Debug.Log(
                    "Runtime Performance Sentinel setup: Scene-authored monitor is installed and validated.");
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static T[] FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        static string GetProjectPath(string relativePath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), relativePath);
        }
    }

    public static class RuntimePerformanceSentinelValidator
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string RuntimeSourcePath =
            "Assets/AreaSurvivors/Scripts/Testing/RuntimePerformanceSentinel.cs";
        const string ReporterPath =
            "Tools/TokenUsage/performance-session-report.ps1";
        const string ValidatorMarkerRelativePath =
            "Library/AreaSafeUnity/runtime-performance-sentinel-validator.success";

        [MenuItem("Area Survivors/Validate/Runtime Performance Sentinel")]
        public static void ValidateMenu()
        {
            string markerPath = GetProjectPath(ValidatorMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);
            if (!Validate(true))
            {
                throw new InvalidOperationException(
                    "Runtime Performance Sentinel validation failed.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
        }

        public static bool Validate(bool logSuccess)
        {
            int errors = 0;
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                GameManager[] managers = FindInScene<GameManager>(scene);
                RuntimePerformanceSentinel[] sentinels =
                    FindInScene<RuntimePerformanceSentinel>(scene);

                if (managers.Length != 1)
                {
                    Debug.LogError(
                        "Runtime Performance Sentinel validator: expected exactly one GameManager. found=" +
                        managers.Length);
                    errors++;
                }

                if (sentinels.Length != 1)
                {
                    Debug.LogError(
                        "Runtime Performance Sentinel validator: expected exactly one sentinel. found=" +
                        sentinels.Length);
                    errors++;
                }
                else
                {
                    RuntimePerformanceSentinel sentinel = sentinels[0];
                    if (managers.Length == 1 && sentinel.gameObject != managers[0].gameObject)
                    {
                        Debug.LogError(
                            "Runtime Performance Sentinel validator: sentinel must share the GameManager object.");
                        errors++;
                    }

                    if (!sentinel.MonitoringEnabled ||
                        sentinel.WarmupSeconds < 1f ||
                        sentinel.PreCaptureSeconds < 1f ||
                        sentinel.PostCaptureSeconds < 1f ||
                        sentinel.SlowFrameThresholdMs <= 0f ||
                        sentinel.CriticalFrameThresholdMs <= sentinel.SlowFrameThresholdMs ||
                        sentinel.AbsoluteP95ThresholdMs <= 0f ||
                        sentinel.RelativeP95Multiplier <= 1f ||
                        sentinel.MinimumSlowFramesInWindow < 1 ||
                        sentinel.MaxIncidentsPerSession < 1)
                    {
                        Debug.LogError(
                            "Runtime Performance Sentinel validator: serialized monitoring thresholds are invalid.");
                        errors++;
                    }
                }

                string runtimeSourceFullPath = GetProjectPath(RuntimeSourcePath);
                if (!File.Exists(runtimeSourceFullPath))
                {
                    Debug.LogError(
                        "Runtime Performance Sentinel validator: runtime source is missing.");
                    errors++;
                }
                else
                {
                    string source = File.ReadAllText(runtimeSourceFullPath);
                    string[] requiredTokens =
                    {
                        "PruneRingSamplesOlderThan",
                        "GetRingSample",
                        "popupInstancesCreated",
                        "popupDrops",
                        "excaliburProjectileDamageHits",
                        "hitFlashCoalescedRequests",
                        "DamagePopup.ActiveCount",
                        "EnemyHitFlash.ActiveFlashCount"
                    };
                    for (int i = 0; i < requiredTokens.Length; i++)
                    {
                        if (source.Contains(requiredTokens[i])) continue;
                        Debug.LogError(
                            "Runtime Performance Sentinel validator: missing source token " +
                            requiredTokens[i]);
                        errors++;
                    }
                }

                if (!File.Exists(GetProjectPath(ReporterPath)))
                {
                    Debug.LogError(
                        "Runtime Performance Sentinel validator: session reporter is missing.");
                    errors++;
                }

                if (errors == 0 && logSuccess)
                {
                    Debug.Log("Runtime Performance Sentinel validator: passed.");
                }

                return errors == 0;
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static T[] FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        static string GetProjectPath(string relativePath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), relativePath);
        }
    }
}
