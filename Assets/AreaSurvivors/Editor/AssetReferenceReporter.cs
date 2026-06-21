using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class AssetReferenceReporter
    {
        const string GeneratedCatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string GeneratedSpritesPath = "Assets/AreaSurvivors/Sprites/Generated";

        static readonly string[] CandidateRoots =
        {
            "Assets/AreaSurvivors/Sprites/Generated",
            "Assets/AreaSurvivors/Sprites/External",
            "Assets/AreaSurvivors/Prefabs",
            "Assets/AreaSurvivors/Resources",
        };

        static readonly string[] ReferenceRoots =
        {
            "Assets/AreaSurvivors/Scenes",
            "Assets/AreaSurvivors/Prefabs",
            "Assets/AreaSurvivors/Resources",
        };

        static readonly string[] TextExtensions =
        {
            ".unity",
            ".prefab",
            ".asset",
            ".cs",
        };

        [MenuItem("Area Survivors/Reports/Asset References")]
        public static void LogAssetReferences()
        {
            var report = BuildReport();
            Debug.Log(ReportOutputUtility.SaveAndSummarize("Asset reference report", report, "asset-references"));
        }

        static string BuildReport()
        {
            var report = new StringBuilder(16384);
            var catalog = AssetDatabase.LoadAssetAtPath<GeneratedSpriteCatalog>(GeneratedCatalogPath);
            var catalogSprites = BuildCatalogSpriteSet(catalog);
            var generatedSpriteNames = BuildGeneratedSpriteNameSet();
            var referenceFiles = GetReferenceFiles();
            var candidates = GetCandidateAssets(24);

            report.AppendLine("AreaSurvivors Asset Reference Report");
            report.AppendLine($"GeneratedSpriteCatalog: {(catalog != null ? "yes" : "no")}");
            report.AppendLine($"Catalog sprite refs: {catalogSprites.Count}");
            report.AppendLine($"Generated sprite names: {generatedSpriteNames.Count}");
            report.AppendLine($"Reference files scanned: {referenceFiles.Count}");
            report.AppendLine($"Candidate assets scanned: {candidates.Count}");

            foreach (var candidate in candidates)
            {
                AppendCandidate(report, candidate, catalogSprites, generatedSpriteNames, referenceFiles);
            }

            return report.ToString();
        }

        static void AppendCandidate(
            StringBuilder report,
            AssetCandidate candidate,
            HashSet<string> catalogSprites,
            HashSet<string> generatedSpriteNames,
            List<string> referenceFiles)
        {
            var guidRefs = CountGuidReferences(candidate.Guid, candidate.AssetPath, referenceFiles);
            var codeNameRefs = CountCodeNameReferences(candidate.AssetName);
            var inCatalog = catalogSprites.Contains(candidate.AssetPath);
            var generatedNameMatch = generatedSpriteNames.Contains(candidate.AssetName);
            var status = BuildStatus(candidate, inCatalog, generatedNameMatch, guidRefs, codeNameRefs);

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

        static string BuildStatus(AssetCandidate candidate, bool inCatalog, bool generatedNameMatch, int guidRefs, int codeNameRefs)
        {
            if (guidRefs > 0) return "referenced-by-guid";
            if (inCatalog) return "referenced-by-catalog";
            if (codeNameRefs > 0) return "referenced-by-code-name";
            if (candidate.AssetPath.StartsWith(GeneratedSpritesPath, StringComparison.Ordinal) && generatedNameMatch) return "generated-name-known";
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

        static List<AssetCandidate> GetCandidateAssets(int topPerRoot)
        {
            var byPath = new Dictionary<string, AssetCandidate>(StringComparer.Ordinal);
            foreach (var root in CandidateRoots)
            {
                var fullRoot = Path.GetFullPath(root);
                if (!Directory.Exists(fullRoot)) continue;

                var files = Directory.EnumerateFiles(fullRoot, "*.*", SearchOption.AllDirectories)
                    .Where(IsCandidateAsset)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(info => info.Length)
                    .Take(topPerRoot);

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

        static int CountGuidReferences(string guid, string assetPath, List<string> referenceFiles)
        {
            if (string.IsNullOrEmpty(guid)) return 0;

            int count = 0;
            foreach (var referencePath in referenceFiles)
            {
                if (referencePath == assetPath) continue;
                var text = File.ReadAllText(referencePath);
                if (text.IndexOf(guid, StringComparison.Ordinal) >= 0) count++;
            }
            return count;
        }

        static int CountCodeNameReferences(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return 0;

            int count = 0;
            var fullRoot = Path.GetFullPath("Assets/AreaSurvivors");
            foreach (var file in Directory.EnumerateFiles(fullRoot, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
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
