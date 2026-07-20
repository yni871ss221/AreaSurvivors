using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AreaSurvivors
{
    public static class ProgressionFileStorage
    {
        static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static bool TryRead(string path, out SaveData data, out string error)
        {
            data = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "Save file is empty: " + path;
                    return false;
                }

                data = JsonUtility.FromJson<SaveData>(json);
                if (data != null) return true;

                error = "Save JSON did not contain progression data: " + path;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public static bool TryWrite(
            string savePath,
            string backupPath,
            string tempPath,
            SaveData data,
            out string error)
        {
            error = string.Empty;
            if (data == null)
            {
                error = "Progression data is null.";
                return false;
            }

            try
            {
                string directory = Path.GetDirectoryName(savePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    error = "Save path does not have a directory: " + savePath;
                    return false;
                }

                Directory.CreateDirectory(directory);
                File.WriteAllText(tempPath, JsonUtility.ToJson(data), Utf8WithoutBom);

                if (TryRead(savePath, out _, out _))
                {
                    File.Copy(savePath, backupPath, true);
                }

                File.Copy(tempPath, savePath, true);
                File.Delete(tempPath);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                TryDeleteSilently(tempPath);
                return false;
            }
        }

        public static bool TryDelete(out string error, params string[] paths)
        {
            error = string.Empty;
            try
            {
                if (paths == null) return true;
                foreach (string path in paths)
                {
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        static void TryDeleteSilently(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Keep the original write error as the actionable failure.
            }
        }
    }
}
