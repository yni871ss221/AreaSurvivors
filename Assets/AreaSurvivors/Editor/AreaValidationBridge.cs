using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AreaSurvivors.EditorTools
{
    [Serializable]
    public sealed class AreaValidationRequest
    {
        public int schema_version;
        public string run_id;
        public string validator_id;
        public string menu_path;
        public string requested_at;
    }

    [Serializable]
    public sealed class AreaValidationIssue
    {
        public string code;
        public string severity;
        public string subject;
        public string message;
        public string expected;
        public string actual;
        public string asset_path;
    }

    [Serializable]
    public sealed class AreaValidationResult
    {
        public int schema_version;
        public string run_id;
        public string validator_id;
        public string menu_path;
        public string status;
        public string adapter_mode;
        public string started_at;
        public string finished_at;
        public long duration_ms;
        public bool check_count_known;
        public int check_count;
        public int passed_count;
        public int failed_count;
        public int warning_count;
        public int error_count;
        public List<AreaValidationIssue> issues = new List<AreaValidationIssue>();
    }

    public static class AreaValidationBridge
    {
        public const string MenuPath =
            "Area Survivors/Internal/Execute Structured Validator Request";

        const int SchemaVersion = 1;
        const string PendingRequestRelativePath =
            "Library/AreaValidation/pending-request.json";
        const string ResultDirectoryRelativePath =
            "Library/AreaValidation/Results";

        [ThreadStatic]
        static AreaValidationResult activeResult;

        public static bool Require(
            bool condition,
            string code,
            string subject,
            string message,
            string expected = "",
            string actual = "",
            string assetPath = "")
        {
            if (activeResult == null)
            {
                if (!condition)
                {
                    Debug.LogError($"{code}: {message}");
                }
                return condition;
            }

            activeResult.adapter_mode = "structured_context";
            activeResult.check_count_known = true;
            activeResult.check_count++;
            if (condition)
            {
                activeResult.passed_count++;
                return true;
            }

            activeResult.issues.Add(new AreaValidationIssue
            {
                code = string.IsNullOrWhiteSpace(code)
                    ? "validator.requirement_failed"
                    : code,
                severity = "failure",
                subject = subject ?? string.Empty,
                message = NormalizeMessage(message),
                expected = expected ?? string.Empty,
                actual = actual ?? string.Empty,
                asset_path = assetPath ?? string.Empty
            });
            return false;
        }

        public static void Warn(
            string code,
            string subject,
            string message,
            string assetPath = "")
        {
            if (activeResult == null)
            {
                Debug.LogWarning($"{code}: {message}");
                return;
            }

            activeResult.issues.Add(new AreaValidationIssue
            {
                code = string.IsNullOrWhiteSpace(code)
                    ? "validator.warning"
                    : code,
                severity = "warning",
                subject = subject ?? string.Empty,
                message = NormalizeMessage(message),
                expected = string.Empty,
                actual = string.Empty,
                asset_path = assetPath ?? string.Empty
            });
        }

        [MenuItem(MenuPath)]
        public static void ExecutePendingRequest()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string requestPath = Path.Combine(
                projectRoot,
                PendingRequestRelativePath);
            if (!File.Exists(requestPath))
            {
                Debug.LogError(
                    "AreaValidationBridge: pending request was not found.");
                return;
            }

            AreaValidationRequest request;
            try
            {
                request = JsonUtility.FromJson<AreaValidationRequest>(
                    File.ReadAllText(requestPath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "AreaValidationBridge: request JSON could not be read. " +
                    exception.Message);
                return;
            }

            if (!TryValidateRequest(request, out string requestError))
            {
                Debug.LogError(
                    "AreaValidationBridge: invalid request. " + requestError);
                return;
            }

            File.Delete(requestPath);
            AreaValidationResult result = CreateResult(request);
            Stopwatch stopwatch = Stopwatch.StartNew();
            bool exceptionObserved = false;
            Application.LogCallback logCallback = (condition, stackTrace, type) =>
            {
                if (type != LogType.Warning &&
                    type != LogType.Error &&
                    type != LogType.Assert &&
                    type != LogType.Exception)
                {
                    return;
                }

                string severity = type == LogType.Warning
                    ? "warning"
                    : type == LogType.Exception
                        ? "error"
                        : "failure";
                if (type == LogType.Exception)
                {
                    exceptionObserved = true;
                }

                result.issues.Add(new AreaValidationIssue
                {
                    code = GetIssueCode(type),
                    severity = severity,
                    subject = request.menu_path,
                    message = NormalizeMessage(condition),
                    expected = string.Empty,
                    actual = string.Empty,
                    asset_path = string.Empty
                });
            };

            Application.logMessageReceived += logCallback;
            activeResult = result;
            try
            {
                if (!EditorApplication.ExecuteMenuItem(request.menu_path))
                {
                    result.issues.RemoveAll(issue =>
                        issue.code == "validator.console_error" &&
                        issue.message.StartsWith(
                            "ExecuteMenuItem failed because",
                            StringComparison.Ordinal));
                    result.issues.Add(new AreaValidationIssue
                    {
                        code = "validator.menu_not_found",
                        severity = "error",
                        subject = request.menu_path,
                        message = "The requested validator MenuItem was not found.",
                        expected = "registered MenuItem",
                        actual = "not found",
                        asset_path = string.Empty
                    });
                    exceptionObserved = true;
                }
            }
            catch (Exception exception)
            {
                exceptionObserved = true;
                result.issues.Add(new AreaValidationIssue
                {
                    code = "validator.unhandled_exception",
                    severity = "error",
                    subject = request.menu_path,
                    message = NormalizeMessage(exception.Message),
                    expected = "validator completes without exception",
                    actual = exception.GetType().FullName,
                    asset_path = string.Empty
                });
            }
            finally
            {
                activeResult = null;
                Application.logMessageReceived -= logCallback;
            }

            stopwatch.Stop();
            CompleteResult(result, stopwatch.ElapsedMilliseconds, exceptionObserved);
            WriteResult(projectRoot, result);

            if (result.status == "passed")
            {
                Debug.Log(
                    $"AreaValidationBridge: passed. validator={result.validator_id}; " +
                    $"warnings={result.warning_count}; duration_ms={result.duration_ms}.");
            }
            else
            {
                Debug.LogWarning(
                    $"AreaValidationBridge: {result.status}. " +
                    $"validator={result.validator_id}; failed={result.failed_count}; " +
                    $"errors={result.error_count}; result={GetResultPath(projectRoot, result.run_id)}.");
            }
        }

        static bool TryValidateRequest(
            AreaValidationRequest request,
            out string error)
        {
            if (request == null)
            {
                error = "request is null";
                return false;
            }
            if (request.schema_version != SchemaVersion)
            {
                error = "unsupported schema_version";
                return false;
            }
            if (!Guid.TryParseExact(request.run_id, "N", out _))
            {
                error = "run_id is not a GUID in N format";
                return false;
            }
            if (string.IsNullOrWhiteSpace(request.validator_id))
            {
                error = "validator_id is empty";
                return false;
            }
            if (string.IsNullOrWhiteSpace(request.menu_path) ||
                string.Equals(request.menu_path, MenuPath, StringComparison.Ordinal))
            {
                error = "menu_path is empty or recursive";
                return false;
            }

            error = string.Empty;
            return true;
        }

        static AreaValidationResult CreateResult(AreaValidationRequest request)
        {
            return new AreaValidationResult
            {
                schema_version = SchemaVersion,
                run_id = request.run_id,
                validator_id = request.validator_id,
                menu_path = request.menu_path,
                status = "error",
                adapter_mode = "legacy_menu_log_capture",
                started_at = DateTime.UtcNow.ToString("O"),
                check_count_known = false
            };
        }

        static void CompleteResult(
            AreaValidationResult result,
            long durationMilliseconds,
            bool exceptionObserved)
        {
            result.duration_ms = durationMilliseconds;
            result.finished_at = DateTime.UtcNow.ToString("O");
            foreach (AreaValidationIssue issue in result.issues)
            {
                if (issue.severity == "warning")
                {
                    result.warning_count++;
                }
                else if (issue.severity == "error")
                {
                    result.error_count++;
                }
                else
                {
                    result.failed_count++;
                }
            }

            if (exceptionObserved || result.error_count > 0)
            {
                result.status = "error";
            }
            else if (result.failed_count > 0)
            {
                result.status = "failed";
            }
            else
            {
                result.status = "passed";
            }
        }

        static void WriteResult(string projectRoot, AreaValidationResult result)
        {
            string resultPath = GetResultPath(projectRoot, result.run_id);
            Directory.CreateDirectory(Path.GetDirectoryName(resultPath));
            string temporaryPath = resultPath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                JsonUtility.ToJson(result, true),
                new UTF8Encoding(false));
            if (File.Exists(resultPath))
            {
                File.Delete(resultPath);
            }
            File.Move(temporaryPath, resultPath);
        }

        static string GetResultPath(string projectRoot, string runId)
        {
            return Path.Combine(
                projectRoot,
                ResultDirectoryRelativePath,
                runId + ".json");
        }

        static string GetIssueCode(LogType type)
        {
            switch (type)
            {
                case LogType.Warning:
                    return "validator.console_warning";
                case LogType.Exception:
                    return "validator.console_exception";
                case LogType.Assert:
                    return "validator.console_assert";
                default:
                    return "validator.console_error";
            }
        }

        static string NormalizeMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return "(empty message)";
            }

            string normalized = message.Replace("\r", " ").Replace("\n", " ").Trim();
            return normalized.Length <= 1000
                ? normalized
                : normalized.Substring(0, 1000);
        }
    }
}
