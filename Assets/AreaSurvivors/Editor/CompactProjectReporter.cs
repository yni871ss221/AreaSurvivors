using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class CompactProjectReporter
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string GameplayTestScenePath = "Assets/AreaSurvivors/Scenes/90_GameplayTest.unity";
        const string GameConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const string GeneratedCatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string GeneratedSpritesPath = "Assets/AreaSurvivors/Sprites/Generated";
        const string LegacyGeneratedResourcesPath = "Assets/AreaSurvivors/Resources/Generated";
        const string ReporterPath = "Assets/AreaSurvivors/Editor/CompactProjectReporter.cs";

        static readonly string[] ImportantPrefabPaths =
        {
            "Assets/AreaSurvivors/Prefabs/CenterTower.prefab",
            "Assets/AreaSurvivors/Prefabs/BallistaTower.prefab",
            "Assets/AreaSurvivors/Prefabs/WoodenWall.prefab",
            "Assets/AreaSurvivors/Prefabs/WoodenGate.prefab",
            "Assets/AreaSurvivors/Prefabs/CarpenterHut.prefab",
            "Assets/AreaSurvivors/Prefabs/WorkerHut.prefab",
            "Assets/AreaSurvivors/Prefabs/WatchTower.prefab",
            "Assets/AreaSurvivors/Prefabs/Player.prefab",
            "Assets/AreaSurvivors/Prefabs/Enemy.prefab",
        };

        static readonly string[] LegacyKeywords =
        {
            "Resources/Generated",
            "Build Fill",
            "TowerUpgradeConstruction",
            "CancelUpgradeIcon",
            "DefensiveFence",
        };

        static readonly string[] TextExtensions =
        {
            ".cs",
            ".prefab",
            ".unity",
            ".asset",
        };

        [MenuItem("Area Survivors/Reports/Compact Project Snapshot")]
        public static void LogCompactProjectSnapshot()
        {
            var report = BuildReport();
            Debug.Log(ReportOutputUtility.SaveAndSummarize("Compact project snapshot", report, "compact-project-snapshot"));
        }

        [MenuItem("Area Survivors/Reports/Copy Compact Project Snapshot")]
        public static void CopyCompactProjectSnapshot()
        {
            var report = BuildReport();
            EditorGUIUtility.systemCopyBuffer = report;
            Debug.Log("Compact project snapshot copied to clipboard.");
        }

        static string BuildReport()
        {
            var report = new StringBuilder(4096);
            report.AppendLine("AreaSurvivors Compact Project Snapshot");
            report.AppendLine($"Active scene: {EditorSceneManager.GetActiveScene().path}");
            report.AppendLine();

            AppendSceneSummary(report);
            AppendAssetSummary(report);
            AppendPrefabSummary(report);
            AppendLegacyKeywordSummary(report);
            return report.ToString();
        }

        static void AppendSceneSummary(StringBuilder report)
        {
            report.AppendLine("[Scenes]");
            AppendExists(report, "Game scene", GameScenePath);
            AppendExists(report, "Gameplay test scene", GameplayTestScenePath);

            int enabledSceneCount = 0;
            bool hasGameScene = false;
            bool hasGameplayTestScene = false;
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;
                enabledSceneCount++;
                hasGameScene |= scene.path == GameScenePath;
                hasGameplayTestScene |= scene.path == GameplayTestScenePath;
            }

            report.AppendLine($"- Build settings enabled scenes: {enabledSceneCount}");
            report.AppendLine($"- Build settings contains 05_Game: {YesNo(hasGameScene)}");
            report.AppendLine($"- Build settings contains 90_GameplayTest: {YesNo(hasGameplayTestScene)}");
            report.AppendLine();
        }

        static void AppendAssetSummary(StringBuilder report)
        {
            report.AppendLine("[Assets]");
            AppendExists(report, "GameConfig", GameConfigPath);

            var catalog = AssetDatabase.LoadAssetAtPath<GeneratedSpriteCatalog>(GeneratedCatalogPath);
            if (catalog == null)
            {
                report.AppendLine($"- GeneratedSpriteCatalog: missing ({GeneratedCatalogPath})");
            }
            else
            {
                SummarizeCatalog(report, catalog);
            }

            var generatedSpriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { GeneratedSpritesPath });
            report.AppendLine($"- Generated sprite assets: {generatedSpriteGuids.Length}");
            report.AppendLine($"- Legacy Resources/Generated folder exists: {YesNo(AssetDatabase.IsValidFolder(LegacyGeneratedResourcesPath))}");
            report.AppendLine();
        }

        static void SummarizeCatalog(StringBuilder report, GeneratedSpriteCatalog catalog)
        {
            int entryCount = catalog.entries != null ? catalog.entries.Length : 0;
            int nullSprites = 0;
            int duplicateNames = 0;
            var names = new HashSet<string>();
            if (catalog.entries != null)
            {
                foreach (var entry in catalog.entries)
                {
                    if (entry.sprite == null) nullSprites++;
                    if (!string.IsNullOrEmpty(entry.name) && !names.Add(entry.name)) duplicateNames++;
                }
            }

            report.AppendLine($"- GeneratedSpriteCatalog entries: {entryCount}");
            report.AppendLine($"- GeneratedSpriteCatalog null sprites: {nullSprites}");
            report.AppendLine($"- GeneratedSpriteCatalog duplicate names: {duplicateNames}");
        }

        static void AppendPrefabSummary(StringBuilder report)
        {
            report.AppendLine("[Important Prefabs]");
            foreach (var path in ImportantPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    report.AppendLine($"- {Path.GetFileName(path)}: missing");
                    continue;
                }

                int missingScripts = CountMissingScripts(prefab);
                var visualSet = prefab.GetComponentInChildren<BuildingPrefabVisualSet>(true);
                if (visualSet == null)
                {
                    report.AppendLine($"- {prefab.name}: ok, missingScripts={missingScripts}");
                    continue;
                }

                string baseSprite = SpriteName(visualSet.completeVisual != null ? visualSet.completeVisual.sprite : null);
                string upgradedSprite = SpriteName(visualSet.upgradedCompleteVisual != null ? visualSet.upgradedCompleteVisual.sprite : null);
                string openSprite = SpriteName(visualSet.upgradedOpenSprite);
                report.AppendLine($"- {prefab.name}: visualSet, missingScripts={missingScripts}, base={baseSprite}, upgraded={upgradedSprite}, open={openSprite}");
            }
            report.AppendLine();
        }

        static void AppendLegacyKeywordSummary(StringBuilder report)
        {
            report.AppendLine("[Legacy Keyword Scan]");
            foreach (var keyword in LegacyKeywords)
            {
                var paths = FindTextAssetPathsContaining(keyword, 5);
                report.AppendLine($"- {keyword}: {paths.Count} shown");
                foreach (var path in paths)
                {
                    report.AppendLine($"  - {path}");
                }
            }
        }

        static List<string> FindTextAssetPathsContaining(string keyword, int maxResults)
        {
            var results = new List<string>();
            var root = Path.GetFullPath("Assets/AreaSurvivors");
            foreach (var path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (results.Count >= maxResults) break;
                if (!ShouldReadTextAsset(path)) continue;
                if (ToAssetPath(path) == ReporterPath) continue;
                var text = File.ReadAllText(path);
                if (text.Contains(keyword))
                {
                    results.Add(ToAssetPath(path));
                }
            }
            return results;
        }

        static bool ShouldReadTextAsset(string path)
        {
            var extension = Path.GetExtension(path);
            foreach (var allowed in TextExtensions)
            {
                if (extension == allowed) return true;
            }
            return false;
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

        static void AppendExists(StringBuilder report, string label, string path)
        {
            report.AppendLine($"- {label}: {YesNo(AssetDatabase.LoadMainAssetAtPath(path) != null)} ({path})");
        }

        static string SpriteName(Sprite sprite)
        {
            return sprite != null ? sprite.name : "none";
        }

        static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        static string ToAssetPath(string fullPath)
        {
            return fullPath.Replace('\\', '/').Replace(Path.GetFullPath(".").Replace('\\', '/') + "/", string.Empty);
        }
    }
}
