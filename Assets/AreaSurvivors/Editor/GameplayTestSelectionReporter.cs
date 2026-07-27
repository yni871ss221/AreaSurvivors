using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class GameplayTestSelectionReporter
    {
        const string SelectedScenarioEditorPref = "AreaSurvivors.GameplayTestScenarioPath";
        const string MenuPath = "Area Survivors/Diagnostics/Gameplay Test/Report Selected Scenario";
        const string ReportRelativePath =
            "Library/AreaSafeUnity/gameplay-test-selected-scenario.txt";

        [MenuItem(MenuPath)]
        public static void Report()
        {
            string projectRoot =
                Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string reportPath = Path.Combine(
                projectRoot,
                ReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string scenarioPath = EditorPrefs.GetString(SelectedScenarioEditorPref, string.Empty);
            string activeScenePath = EditorSceneManager.GetActiveScene().path;

            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(
                reportPath,
                $"createdUtc={DateTime.UtcNow:O}{Environment.NewLine}" +
                $"scenarioPath={scenarioPath}{Environment.NewLine}" +
                $"activeScenePath={activeScenePath}{Environment.NewLine}");
            Debug.Log(
                $"[GameplayTestSelectionReporter] scenario={scenarioPath}, " +
                $"scene={activeScenePath}");
        }
    }
}
