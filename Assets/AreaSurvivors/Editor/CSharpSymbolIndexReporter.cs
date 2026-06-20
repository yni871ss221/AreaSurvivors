using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class CSharpSymbolIndexReporter
    {
        const string ScriptsRoot = "Assets/AreaSurvivors/Scripts";
        static readonly Regex TypeRegex = new Regex(@"\b(public|internal|private|protected)?\s*(sealed\s+|static\s+|abstract\s+|partial\s+)*\b(class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        static readonly Regex MethodRegex = new Regex(@"\b(public|internal|private|protected)\s+(static\s+|virtual\s+|override\s+|sealed\s+|async\s+)*[A-Za-z_][A-Za-z0-9_<>,\[\]\.?]*\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);
        static readonly Regex FieldRegex = new Regex(@"\b(public|internal|private|protected)\s+(static\s+|readonly\s+|const\s+|serializedField\s+)*[A-Za-z_][A-Za-z0-9_<>,\[\]\.?]*\s+([A-Za-z_][A-Za-z0-9_]*)\s*(=|;)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex SerializedFieldRegex = new Regex(@"\[SerializeField\][\s\r\n]*(private|protected|public|internal)?\s*[A-Za-z_][A-Za-z0-9_<>,\[\]\.?]*\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);

        [MenuItem("Area Survivors/Reports/C# Symbol Overview")]
        public static void LogSymbolOverview()
        {
            var report = BuildReport(0, false);
            Debug.Log(ReportOutputUtility.SaveAndSummarize("C# symbol overview", report, "csharp-symbol-overview"));
        }

        [MenuItem("Area Survivors/Reports/C# Symbol Index")]
        public static void LogSymbolIndex()
        {
            var report = BuildReport(30, false);
            Debug.Log(ReportOutputUtility.SaveAndSummarize("C# symbol index", report, "csharp-symbol-index"));
        }

        [MenuItem("Area Survivors/Reports/Copy C# Symbol Index")]
        public static void CopySymbolIndex()
        {
            var report = BuildReport(int.MaxValue, true);
            EditorGUIUtility.systemCopyBuffer = report;
            Debug.Log("C# symbol index copied to clipboard.");
        }

        static string BuildReport(int maxFiles, bool includeMethods)
        {
            var files = AssetDatabase.FindAssets("t:MonoScript", new[] { ScriptsRoot });
            var summaries = new List<FileSummary>();
            int totalTypes = 0;
            int totalMethods = 0;
            int totalFields = 0;
            int totalSerializedFields = 0;

            foreach (var guid in files)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".cs")) continue;
                var text = File.ReadAllText(path);
                var summary = new FileSummary
                {
                    path = path,
                    types = Names(TypeRegex, text, 4),
                    methods = Names(MethodRegex, text, 3),
                    fields = Names(FieldRegex, text, 3),
                    serializedFields = Names(SerializedFieldRegex, text, 2)
                };
                totalTypes += summary.types.Count;
                totalMethods += summary.methods.Count;
                totalFields += summary.fields.Count;
                totalSerializedFields += summary.serializedFields.Count;
                if (summary.types.Count > 0 || summary.methods.Count > 0 || summary.serializedFields.Count > 0) summaries.Add(summary);
            }

            summaries.Sort((a, b) => string.CompareOrdinal(a.path, b.path));

            var report = new StringBuilder(8192);
            report.AppendLine("AreaSurvivors C# Symbol Index");
            report.AppendLine($"Files: {summaries.Count}, types: {totalTypes}, methods: {totalMethods}, fields: {totalFields}, serializedFields: {totalSerializedFields}");
            report.AppendLine();

            if (maxFiles > 0)
            {
                int shown = 0;
                foreach (var summary in summaries)
                {
                    if (shown >= maxFiles) break;
                    shown++;
                    report.AppendLine($"- {summary.path}");
                    if (summary.types.Count > 0) report.AppendLine($"  types: {string.Join(", ", summary.types)}");
                    if (summary.serializedFields.Count > 0) report.AppendLine($"  serialized: {JoinLimited(summary.serializedFields, 12)}");
                    if (includeMethods && summary.methods.Count > 0) report.AppendLine($"  methods: {JoinLimited(summary.methods, 12)}");
                }
                if (shown < summaries.Count)
                {
                    report.AppendLine($"... {summaries.Count - shown} more files omitted. Use Copy C# Symbol Index for the full report.");
                }
            }
            else
            {
                report.AppendLine("Overview only. Use C# Symbol Index for first 30 files, or Copy C# Symbol Index for full detail.");
            }

            return report.ToString();
        }

        static List<string> Names(Regex regex, string text, int group)
        {
            var names = new List<string>();
            var seen = new HashSet<string>();
            foreach (Match match in regex.Matches(text))
            {
                var name = match.Groups[group].Value;
                if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;
                names.Add(name);
            }
            return names;
        }

        static string JoinLimited(List<string> names, int max)
        {
            if (names.Count <= max) return string.Join(", ", names);
            return string.Join(", ", names.GetRange(0, max)) + $" ... (+{names.Count - max})";
        }

        sealed class FileSummary
        {
            public string path;
            public List<string> types;
            public List<string> methods;
            public List<string> fields;
            public List<string> serializedFields;
        }
    }
}
