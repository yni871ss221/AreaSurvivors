using System;
using System.IO;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class ReportOutputUtility
    {
        const string ReportDirectory = "TokenReports/UnityReports";

        public static string SaveAndSummarize(string title, string report, string fileNamePrefix)
        {
            Directory.CreateDirectory(ReportDirectory);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var path = Path.Combine(ReportDirectory, $"{fileNamePrefix}-{timestamp}.md").Replace('\\', '/');
            File.WriteAllText(path, report);

            var lines = string.IsNullOrEmpty(report) ? 0 : report.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length;
            var chars = report != null ? report.Length : 0;
            return $"{title} saved: {path} (lines={lines}, chars={chars})";
        }
    }
}
