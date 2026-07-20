using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class ProgressionCloudSaveValidator
    {
        const string SuccessMarkerPath = "TokenReports/Validation/progression-cloud-save-validator.success";
        const string ScratchDirectoryPath = "Library/AreaSafeUnity/ProgressionCloudSaveValidator";

        [MenuItem("Area Survivors/Validate/Progression Cloud Save")]
        public static void ValidateFromMenu()
        {
            DeleteIfExists(SuccessMarkerPath);
            var failures = new List<string>();

            ValidateProductionPaths(failures);
            ValidateFileRoundTrip(failures);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Progression Cloud Save validation failed:\n- " + string.Join("\n- ", failures));
            }

            string markerDirectory = Path.GetDirectoryName(SuccessMarkerPath);
            if (!string.IsNullOrEmpty(markerDirectory)) Directory.CreateDirectory(markerDirectory);
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Progression Cloud Save validation passed.");
        }

        static void ValidateProductionPaths(List<string> failures)
        {
            string expectedRoot = Path.GetFullPath(Application.persistentDataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            ValidatePath(ProgressionStore.CloudSavePath, expectedRoot, ProgressionStore.CloudSaveFileName, failures);
            ValidatePath(ProgressionStore.CloudSaveBackupPath, expectedRoot, ProgressionStore.CloudSaveBackupFileName, failures);
            ValidatePath(ProgressionStore.CloudSaveTempPath, expectedRoot, ProgressionStore.CloudSaveTempFileName, failures);

            if (!ProgressionStore.CloudSaveFileName.EndsWith(".json", StringComparison.Ordinal) ||
                !ProgressionStore.CloudSaveBackupFileName.EndsWith(".json", StringComparison.Ordinal))
            {
                failures.Add("Cloud-synchronized progression files must use the .json extension.");
            }
            if (ProgressionStore.CloudSaveTempFileName.EndsWith(".json", StringComparison.Ordinal))
            {
                failures.Add("Temporary save file must not match the Steam Auto-Cloud JSON pattern.");
            }
        }

        static void ValidatePath(string path, string expectedRoot, string expectedFileName, List<string> failures)
        {
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.Equals(directory, expectedRoot, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add(expectedFileName + " is not stored directly under Application.persistentDataPath.");
            }
            if (!string.Equals(Path.GetFileName(fullPath), expectedFileName, StringComparison.Ordinal))
            {
                failures.Add("Unexpected progression save filename: " + fullPath);
            }
        }

        static void ValidateFileRoundTrip(List<string> failures)
        {
            string scratchRoot = Path.GetFullPath(ScratchDirectoryPath);
            string mainPath = Path.Combine(scratchRoot, ProgressionStore.CloudSaveFileName);
            string backupPath = Path.Combine(scratchRoot, ProgressionStore.CloudSaveBackupFileName);
            string tempPath = Path.Combine(scratchRoot, ProgressionStore.CloudSaveTempFileName);

            Directory.CreateDirectory(scratchRoot);
            DeleteIfExists(mainPath);
            DeleteIfExists(backupPath);
            DeleteIfExists(tempPath);

            try
            {
                var first = new SaveData { tokens = 123, totalKills = 456, highestUnlockedStage = 2 };
                if (!ProgressionFileStorage.TryWrite(mainPath, backupPath, tempPath, first, out string firstWriteError))
                {
                    failures.Add("Initial file write failed: " + firstWriteError);
                    return;
                }
                if (!ProgressionFileStorage.TryRead(mainPath, out SaveData firstRead, out string firstReadError) ||
                    firstRead.tokens != first.tokens || firstRead.totalKills != first.totalKills)
                {
                    failures.Add("Initial file round-trip failed: " + firstReadError);
                    return;
                }

                var second = new SaveData { tokens = 789, totalKills = 999, highestUnlockedStage = 4 };
                if (!ProgressionFileStorage.TryWrite(mainPath, backupPath, tempPath, second, out string secondWriteError))
                {
                    failures.Add("Second file write failed: " + secondWriteError);
                    return;
                }
                if (!ProgressionFileStorage.TryRead(mainPath, out SaveData secondRead, out string secondReadError) ||
                    secondRead.tokens != second.tokens || secondRead.highestUnlockedStage != second.highestUnlockedStage)
                {
                    failures.Add("Updated file round-trip failed: " + secondReadError);
                }
                if (!ProgressionFileStorage.TryRead(backupPath, out SaveData backupRead, out string backupReadError) ||
                    backupRead.tokens != first.tokens)
                {
                    failures.Add("Previous save was not retained as a readable backup: " + backupReadError);
                }

                File.WriteAllText(mainPath, "invalid-json");
                if (ProgressionFileStorage.TryRead(mainPath, out _, out _))
                {
                    failures.Add("Corrupted main save was incorrectly accepted.");
                }
                if (!ProgressionFileStorage.TryRead(backupPath, out SaveData recovered, out string recoveryError) ||
                    recovered.tokens != first.tokens)
                {
                    failures.Add("Backup recovery contract failed: " + recoveryError);
                }
            }
            finally
            {
                DeleteIfExists(mainPath);
                DeleteIfExists(backupPath);
                DeleteIfExists(tempPath);
                if (Directory.Exists(scratchRoot)) Directory.Delete(scratchRoot, false);
            }
        }

        static void DeleteIfExists(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
