using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class WindowsDistributionBuildMenu
    {
        const string ProductName = "Area Survivors";
        const string BuildRootDirectoryName = "Build";
        const string LocalTestDirectoryName = "LocalTest";
        const string SteamDirectoryName = "Steam";
        const string SteamAppIdFileName = "steam_appid.txt";

        enum DistributionKind
        {
            LocalTest,
            Steam
        }

        [MenuItem("Area Survivors/Build/Build Local Test + Steam Distribution")]
        public static void BuildAllDistributions()
        {
            string timestamp = CreateTimestamp();
            Build(DistributionKind.LocalTest, timestamp);
            Build(DistributionKind.Steam, timestamp);
        }

        static string CreateTimestamp()
        {
            return DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        }

        static void Build(DistributionKind distributionKind, string timestamp)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before starting a distribution build.");
            }

            string projectRoot = GetProjectRoot();
            IReadOnlyList<string> scenes = GetEnabledBuildScenes(projectRoot);
            ValidateSteamAppIdSource(projectRoot, distributionKind);

            string distributionDirectoryName = distributionKind == DistributionKind.LocalTest
                ? LocalTestDirectoryName
                : SteamDirectoryName;
            string distributionRoot = Path.Combine(projectRoot, BuildRootDirectoryName, distributionDirectoryName);
            string outputDirectory = Path.Combine(distributionRoot, timestamp);
            ValidateOutputPath(projectRoot, outputDirectory);

            Directory.CreateDirectory(outputDirectory);
            string executablePath = Path.Combine(outputDirectory, ProductName + ".exe");
            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Windows build failed. result={report.summary.result}, errors={report.summary.totalErrors}, output={outputDirectory}");
            }

            string steamAppIdDestination = Path.Combine(outputDirectory, SteamAppIdFileName);
            if (distributionKind == DistributionKind.LocalTest)
            {
                string steamAppIdSource = Path.Combine(projectRoot, SteamAppIdFileName);
                File.Copy(steamAppIdSource, steamAppIdDestination, true);
            }

            ValidateBuildOutput(outputDirectory, distributionKind);

            string archiveLabel = distributionKind == DistributionKind.LocalTest
                ? "AreaSurvivors-LocalTest"
                : "AreaSurvivors-Steam";
            string zipPath = Path.Combine(distributionRoot, $"{archiveLabel}-{timestamp}.zip");
            if (File.Exists(zipPath))
            {
                throw new InvalidOperationException("Distribution ZIP path already exists: " + zipPath);
            }

            ZipFile.CreateFromDirectory(
                outputDirectory,
                zipPath,
                System.IO.Compression.CompressionLevel.Optimal,
                false);
            if (!File.Exists(zipPath) || new FileInfo(zipPath).Length == 0)
            {
                throw new InvalidOperationException("Distribution ZIP was not created correctly: " + zipPath);
            }

            AssetDatabase.Refresh();
            EditorUtility.RevealInFinder(outputDirectory);
            Debug.Log($"{distributionKind} Windows build completed. content={outputDirectory}, archive={zipPath}");
        }

        static string GetProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            if (parent == null)
            {
                throw new InvalidOperationException("Unity project root could not be resolved.");
            }

            return parent.FullName;
        }

        static IReadOnlyList<string> GetEnabledBuildScenes(string projectRoot)
        {
            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled) continue;

                string fullPath = Path.Combine(projectRoot, scene.path);
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException("Enabled build scene does not exist.", fullPath);
                }

                scenes.Add(scene.path);
            }

            if (scenes.Count == 0)
            {
                throw new InvalidOperationException("No enabled scenes were found in Build Settings.");
            }

            return scenes;
        }

        static void ValidateSteamAppIdSource(string projectRoot, DistributionKind distributionKind)
        {
            if (distributionKind != DistributionKind.LocalTest) return;

            string sourcePath = Path.Combine(projectRoot, SteamAppIdFileName);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Local test build requires steam_appid.txt at the project root.", sourcePath);
            }

            string value = File.ReadAllText(sourcePath).Trim();
            if (!uint.TryParse(value, out uint appId) || appId != SteamAchievementRuntime.AppId)
            {
                throw new InvalidOperationException(
                    $"steam_appid.txt must contain only {SteamAchievementRuntime.AppId}. actual={value}");
            }
        }

        static void ValidateOutputPath(string projectRoot, string outputDirectory)
        {
            string buildRoot = Path.GetFullPath(Path.Combine(projectRoot, BuildRootDirectoryName))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string resolvedOutput = Path.GetFullPath(outputDirectory);
            string requiredPrefix = buildRoot + Path.DirectorySeparatorChar;
            if (!resolvedOutput.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Build output must remain under the project Build directory: " + resolvedOutput);
            }

            if (Directory.Exists(resolvedOutput) || File.Exists(resolvedOutput))
            {
                throw new InvalidOperationException("Build output already exists and will not be overwritten: " + resolvedOutput);
            }
        }

        static void ValidateBuildOutput(string outputDirectory, DistributionKind distributionKind)
        {
            string executablePath = Path.Combine(outputDirectory, ProductName + ".exe");
            string dataDirectory = Path.Combine(outputDirectory, ProductName + "_Data");
            string unityPlayerPath = Path.Combine(outputDirectory, "UnityPlayer.dll");
            string steamAppIdPath = Path.Combine(outputDirectory, SteamAppIdFileName);

            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException("Built executable was not found.", executablePath);
            }

            if (!Directory.Exists(dataDirectory))
            {
                throw new DirectoryNotFoundException("Built data directory was not found: " + dataDirectory);
            }

            if (!File.Exists(unityPlayerPath))
            {
                throw new FileNotFoundException("UnityPlayer.dll was not found.", unityPlayerPath);
            }

            bool shouldContainSteamAppId = distributionKind == DistributionKind.LocalTest;
            if (File.Exists(steamAppIdPath) != shouldContainSteamAppId)
            {
                string expectation = shouldContainSteamAppId ? "present" : "absent";
                throw new InvalidOperationException($"steam_appid.txt must be {expectation}: {steamAppIdPath}");
            }

            if (shouldContainSteamAppId)
            {
                string value = File.ReadAllText(steamAppIdPath).Trim();
                if (value != SteamAchievementRuntime.AppId.ToString())
                {
                    throw new InvalidOperationException(
                        $"Local test steam_appid.txt is invalid. expected={SteamAchievementRuntime.AppId}, actual={value}");
                }
            }
        }
    }
}
