using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class AssetReferenceReporter
    {
        const string GeneratedCatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string GeneratedSpritesPath = "Assets/AreaSurvivors/Sprites/Generated";
        const string ExternalSpritesPath = "Assets/AreaSurvivors/Sprites/External";

        static readonly string[] CandidateRoots =
        {
            "Assets/AreaSurvivors",
        };

        static readonly string[] MonitoredCleanupRoots =
        {
            "Assets/AreaSurvivors/TilePalette",
        };

        static readonly string[] ReferenceRoots =
        {
            "Assets/AreaSurvivors",
        };

        static readonly string[] TextExtensions =
        {
            ".unity",
            ".prefab",
            ".asset",
            ".cs",
            ".mat",
            ".anim",
            ".controller",
            ".overrideController",
            ".physicsMaterial2D",
        };

        [MenuItem("Area Survivors/Reports/Asset References")]
        public static void LogAssetReferences()
        {
            var report = BuildReport();
            Debug.Log(ReportOutputUtility.SaveAndSummarize("Asset reference report", report, "asset-references"));
        }

        static string BuildReport()
        {
            var report = new StringBuilder(131072);
            var catalog = AssetDatabase.LoadAssetAtPath<GeneratedSpriteCatalog>(GeneratedCatalogPath);
            var catalogSprites = BuildCatalogSpriteSet(catalog);
            var generatedSpriteNames = BuildGeneratedSpriteNameSet();
            var referenceTexts = LoadTextFiles(GetReferenceFiles());
            var codeTexts = LoadTextFiles(GetCodeFiles());
            var candidates = GetCandidateAssets();

            report.AppendLine("AreaSurvivors Asset Reference Report");
            report.AppendLine($"GeneratedSpriteCatalog: {(catalog != null ? "yes" : "no")}");
            report.AppendLine($"Catalog sprite refs: {catalogSprites.Count}");
            report.AppendLine($"Generated sprite names: {generatedSpriteNames.Count}");
            report.AppendLine($"Reference files scanned: {referenceTexts.Count}");
            report.AppendLine($"Code files scanned: {codeTexts.Count}");
            report.AppendLine($"Candidate assets scanned: {candidates.Count}");
            AppendIntegritySummary(report, referenceTexts);
            AppendExternalDependencySummary(report);

            foreach (var candidate in candidates)
            {
                AppendCandidate(report, candidate, catalogSprites, generatedSpriteNames, referenceTexts, codeTexts);
            }

            return report.ToString();
        }

        static void AppendExternalDependencySummary(StringBuilder report)
        {
            var dependents = MonitoredCleanupRoots.ToDictionary(
                root => root,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);

            foreach (var path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/AreaSurvivors/", StringComparison.Ordinal)) continue;
                foreach (var dependency in AssetDatabase.GetDependencies(path, false))
                {
                    foreach (var root in MonitoredCleanupRoots)
                    {
                        if (path.StartsWith(root + "/", StringComparison.Ordinal) || path == root) continue;
                        if (dependency.StartsWith(root + "/", StringComparison.Ordinal) || dependency == root)
                            dependents[root].Add(path);
                    }
                }
            }

            foreach (var root in MonitoredCleanupRoots)
            {
                report.AppendLine($"External dependents [{root}]: {dependents[root].Count}");
                foreach (var path in dependents[root].OrderBy(path => path, StringComparer.Ordinal))
                    report.AppendLine($"- externalDependent: {path}");
            }
        }

        static void AppendIntegritySummary(StringBuilder report, Dictionary<string, string> referenceTexts)
        {
            var unresolvedGuidFiles = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var entry in referenceTexts)
            {
                foreach (Match match in Regex.Matches(entry.Value, @"guid:\s*([0-9a-fA-F]{32})"))
                {
                    var guid = match.Groups[1].Value.ToLowerInvariant();
                    if (guid.StartsWith("0000000000000000", StringComparison.Ordinal)) continue;
                    if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid))) continue;
                    if (!unresolvedGuidFiles.TryGetValue(guid, out var paths))
                    {
                        paths = new HashSet<string>(StringComparer.Ordinal);
                        unresolvedGuidFiles.Add(guid, paths);
                    }
                    paths.Add(entry.Key);
                }
            }

            var missingScriptPrefabs = new List<string>();
            foreach (var prefabGuid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/AreaSurvivors" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(prefabGuid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var missingCount = 0;
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    missingCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                }
                if (missingCount > 0) missingScriptPrefabs.Add($"{path} ({missingCount})");
            }

            report.AppendLine($"Unresolved serialized GUIDs: {unresolvedGuidFiles.Count}");
            foreach (var entry in unresolvedGuidFiles.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                report.AppendLine($"- unresolvedGuid: {entry.Key}");
                foreach (var path in entry.Value.OrderBy(path => path, StringComparer.Ordinal))
                    report.AppendLine($"  - {path}");
            }
            report.AppendLine($"Prefabs with missing scripts: {missingScriptPrefabs.Count}");
            foreach (var path in missingScriptPrefabs.OrderBy(path => path, StringComparer.Ordinal))
                report.AppendLine($"- missingScriptPrefab: {path}");
        }

        static void AppendCandidate(
            StringBuilder report,
            AssetCandidate candidate,
            HashSet<string> catalogSprites,
            HashSet<string> generatedSpriteNames,
            Dictionary<string, string> referenceTexts,
            Dictionary<string, string> codeTexts)
        {
            var guidRefs = CountGuidReferences(candidate.Guid, candidate.AssetPath, referenceTexts);
            var codeNameRefs = CountCodeNameReferences(candidate.AssetName, codeTexts);
            var inCatalog = catalogSprites.Contains(candidate.AssetPath);
            var generatedNameMatch = generatedSpriteNames.Contains(candidate.AssetName);
            var status = BuildStatus(candidate, inCatalog, guidRefs, codeNameRefs);

            report.AppendLine();
            report.AppendLine($"[Asset] {candidate.AssetPath}");
            report.AppendLine($"- type: {candidate.Extension.TrimStart('.')} kb={candidate.Kilobytes:F1}");
            report.AppendLine($"- guid: {candidate.Guid}");
            report.AppendLine($"- inGeneratedCatalog: {YesNo(inCatalog)}");
            report.AppendLine($"- generatedNameKnown: {YesNo(generatedNameMatch)}");
            report.AppendLine($"- scenePrefabAssetGuidRefs: {guidRefs}");
            report.AppendLine($"- codeNameRefs: {codeNameRefs}");
            report.AppendLine($"- status: {status}");
        }

        static string BuildStatus(AssetCandidate candidate, bool inCatalog, int guidRefs, int codeNameRefs)
        {
            if (guidRefs > 0) return "referenced-by-guid";
            if (inCatalog) return "referenced-by-catalog";
            if (codeNameRefs > 0) return "referenced-by-code-name";
            if (candidate.AssetPath.IndexOf("/Archive/", StringComparison.OrdinalIgnoreCase) >= 0) return "archive-review-candidate";
            if (candidate.AssetPath.StartsWith(ExternalSpritesPath, StringComparison.Ordinal)
                && candidate.AssetName.EndsWith("Source", StringComparison.Ordinal)) return "source-original-preserved";
            return "review-candidate";
        }

        static HashSet<string> BuildCatalogSpriteSet(GeneratedSpriteCatalog catalog)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (catalog == null || catalog.entries == null) return result;

            foreach (var entry in catalog.entries)
            {
                if (entry.sprite == null) continue;
                var path = AssetDatabase.GetAssetPath(entry.sprite);
                if (!string.IsNullOrEmpty(path)) result.Add(path);
            }

            return result;
        }

        static HashSet<string> BuildGeneratedSpriteNameSet()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { GeneratedSpritesPath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                result.Add(Path.GetFileNameWithoutExtension(path));
            }
            return result;
        }

        static List<string> GetReferenceFiles()
        {
            var files = new List<string>();
            foreach (var root in ReferenceRoots)
            {
                var fullRoot = Path.GetFullPath(root);
                if (!Directory.Exists(fullRoot)) continue;

                foreach (var file in Directory.EnumerateFiles(fullRoot, "*.*", SearchOption.AllDirectories))
                {
                    if (!ShouldRead(file)) continue;
                    files.Add(ToAssetPath(file));
                }
            }

            files.Sort(StringComparer.Ordinal);
            return files;
        }

        static Dictionary<string, string> LoadTextFiles(IEnumerable<string> paths)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                result[path] = File.ReadAllText(path);
            }
            return result;
        }

        static List<string> GetCodeFiles()
        {
            var root = Path.GetFullPath("Assets/AreaSurvivors");
            return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Select(ToAssetPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        static List<AssetCandidate> GetCandidateAssets()
        {
            var byPath = new Dictionary<string, AssetCandidate>(StringComparer.Ordinal);
            foreach (var root in CandidateRoots)
            {
                var fullRoot = Path.GetFullPath(root);
                if (!Directory.Exists(fullRoot)) continue;

                var files = Directory.EnumerateFiles(fullRoot, "*.*", SearchOption.AllDirectories)
                    .Where(IsCandidateAsset)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(info => info.Length);

                foreach (var file in files)
                {
                    var assetPath = ToAssetPath(file.FullName);
                    if (byPath.ContainsKey(assetPath)) continue;

                    var guid = AssetDatabase.AssetPathToGUID(assetPath);
                    byPath.Add(assetPath, new AssetCandidate
                    {
                        AssetPath = assetPath,
                        AssetName = Path.GetFileNameWithoutExtension(assetPath),
                        Extension = Path.GetExtension(assetPath),
                        Guid = guid,
                        Kilobytes = file.Length / 1024f,
                    });
                }
            }

            return byPath.Values
                .OrderByDescending(candidate => candidate.Kilobytes)
                .ThenBy(candidate => candidate.AssetPath, StringComparer.Ordinal)
                .ToList();
        }

        static int CountGuidReferences(string guid, string assetPath, Dictionary<string, string> referenceTexts)
        {
            if (string.IsNullOrEmpty(guid)) return 0;

            int count = 0;
            foreach (var entry in referenceTexts)
            {
                if (entry.Key == assetPath) continue;
                if (entry.Value.IndexOf(guid, StringComparison.Ordinal) >= 0) count++;
            }
            return count;
        }

        static int CountCodeNameReferences(string assetName, Dictionary<string, string> codeTexts)
        {
            if (string.IsNullOrEmpty(assetName)) return 0;

            int count = 0;
            foreach (var text in codeTexts.Values)
            {
                if (text.IndexOf(assetName, StringComparison.Ordinal) >= 0) count++;
            }
            return count;
        }

        static bool IsCandidateAsset(string path)
        {
            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase);
        }

        static bool ShouldRead(string path)
        {
            var extension = Path.GetExtension(path);
            foreach (var allowed in TextExtensions)
            {
                if (string.Equals(extension, allowed, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        static string ToAssetPath(string fullPath)
        {
            return fullPath.Replace('\\', '/').Replace(Path.GetFullPath(".").Replace('\\', '/') + "/", string.Empty);
        }

        static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        sealed class AssetCandidate
        {
            public string AssetPath;
            public string AssetName;
            public string Extension;
            public string Guid;
            public float Kilobytes;
        }
    }
}
