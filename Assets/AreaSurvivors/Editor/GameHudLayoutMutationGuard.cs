using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class GameHudLayoutMutationGuard
    {
        const string EditorRoot = "Assets/AreaSurvivors/Editor";
        const string SelfFileName = "GameHudLayoutMutationGuard.cs";
        const string SuccessMarkerPath = "TokenReports/Validation/hud-layout-mutation-guard.success";

        static readonly string[] GuardedFileNameParts =
        {
            "GameHud"
        };

        static readonly Regex ForbiddenLayoutAssignment = new Regex(
            @"\.(?:anchorMin|anchorMax|pivot|anchoredPosition|sizeDelta|offsetMin|offsetMax|localPosition|localScale|localRotation|rotation)\s*=",
            RegexOptions.Compiled);

        static readonly Regex ForbiddenLayoutHelper = new Regex(
            @"\b(?:SetRect|SetAnchored|Stretch|CreatePlayerStatusPanel|CreateRunResourcePanels|RestoreTokenHud|RestoreWeaponStatusHud|EnsureTokenResourcePanel|EnsureBossHud|EnsureWeaponStatusPanels)\s*\(",
            RegexOptions.Compiled);

        [MenuItem("Area Survivors/Validate/HUD Layout Mutation Guard")]
        public static void ValidateFromMenu()
        {
            DeleteSuccessMarker();
            var violations = FindViolations();
            if (violations.Count == 0)
            {
                WriteSuccessMarker();
                Debug.Log("HUD Layout Mutation Guard passed. No forbidden HUD layout mutations were found in Editor scripts.");
                return;
            }

            Debug.LogError(BuildReport(violations));
        }

        static void DeleteSuccessMarker()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
        }

        static void WriteSuccessMarker()
        {
            var directory = Path.GetDirectoryName(SuccessMarkerPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(SuccessMarkerPath, System.DateTime.UtcNow.ToString("O"));
        }

        public static List<string> FindViolations()
        {
            var violations = new List<string>();
            if (!Directory.Exists(EditorRoot)) return violations;

            foreach (var path in Directory.GetFiles(EditorRoot, "*.cs", SearchOption.AllDirectories))
            {
                var normalizedPath = path.Replace('\\', '/');
                if (normalizedPath.EndsWith(SelfFileName)) continue;
                if (!IsGuardedHudEditorFile(normalizedPath)) continue;

                var text = File.ReadAllText(path);
                var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = StripLineComment(lines[i]).Trim();
                    if (line.Length == 0) continue;
                    if (ForbiddenLayoutAssignment.IsMatch(line) || ForbiddenLayoutHelper.IsMatch(line))
                    {
                        violations.Add($"{normalizedPath}:{i + 1}: {line}");
                    }
                }
            }

            return violations;
        }

        static bool IsGuardedHudEditorFile(string normalizedPath)
        {
            var fileName = Path.GetFileName(normalizedPath);
            if (fileName.Contains("Reporter")) return false;
            if (fileName.Contains("Guard")) return false;

            for (int i = 0; i < GuardedFileNameParts.Length; i++)
            {
                if (fileName.Contains(GuardedFileNameParts[i])) return true;
            }

            return false;
        }

        static string StripLineComment(string line)
        {
            var index = line.IndexOf("//");
            return index >= 0 ? line.Substring(0, index) : line;
        }

        static string BuildReport(IReadOnlyList<string> violations)
        {
            var report = new StringBuilder(2048);
            report.AppendLine("HUD Layout Mutation Guard failed.");
            report.AppendLine("Existing HUD layout is Scene-authored only. Do not write RectTransform or Transform layout values from Editor scripts.");
            report.AppendLine();
            for (int i = 0; i < violations.Count; i++)
            {
                report.Append("- ");
                report.AppendLine(violations[i]);
            }

            return report.ToString();
        }
    }
}
