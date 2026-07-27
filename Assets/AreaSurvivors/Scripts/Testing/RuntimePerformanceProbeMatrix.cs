using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AreaSurvivors.Testing
{
    public sealed class RuntimePerformanceProbeMatrix : MonoBehaviour
    {
        struct EnemyState
        {
            public EnemyController enemy;
            public Rigidbody2D body;
            public Vector3 position;
            public Quaternion rotation;
            public Vector2 velocity;
            public float angularVelocity;
        }

        static RuntimePerformanceProbeMatrix activeMatrix;

        readonly List<EnemyState> enemyStates = new List<EnemyState>();
        readonly List<string> results = new List<string>();

        RuntimePerformanceProbeMode[] modes = Array.Empty<RuntimePerformanceProbeMode>();
        string scenarioName;
        float durationSeconds;
        float warmupSeconds;
        float transitionSeconds;
        float transitionRemaining;
        int nextModeIndex;
        int runningModeIndex = -1;
        bool waitingForNextMode;

        public static string LastResultFilePath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(
                    projectRoot,
                    "Library",
                    "AreaSafeUnity",
                    "combat-performance-matrix-last.txt");
            }
        }

        public static void Begin(
            string sourceScenarioName,
            RuntimePerformanceProbeMode[] probeModes,
            float probeDurationSeconds,
            float probeWarmupSeconds,
            float betweenModesSeconds)
        {
            if (probeModes == null || probeModes.Length == 0)
            {
                Debug.LogError("[PerformanceProbeMatrix] At least one probe mode is required.");
                return;
            }

            if (activeMatrix != null)
            {
                Destroy(activeMatrix.gameObject);
                activeMatrix = null;
            }

            var go = new GameObject("Runtime Performance Probe Matrix");
            DontDestroyOnLoad(go);
            activeMatrix = go.AddComponent<RuntimePerformanceProbeMatrix>();
            activeMatrix.StartMatrix(
                sourceScenarioName,
                probeModes,
                probeDurationSeconds,
                probeWarmupSeconds,
                betweenModesSeconds);
        }

        void StartMatrix(
            string sourceScenarioName,
            RuntimePerformanceProbeMode[] probeModes,
            float probeDurationSeconds,
            float probeWarmupSeconds,
            float betweenModesSeconds)
        {
            scenarioName = string.IsNullOrWhiteSpace(sourceScenarioName)
                ? "UnnamedScenario"
                : sourceScenarioName;
            modes = (RuntimePerformanceProbeMode[])probeModes.Clone();
            durationSeconds = Mathf.Max(0.5f, probeDurationSeconds);
            warmupSeconds = Mathf.Max(0f, probeWarmupSeconds);
            transitionSeconds = Mathf.Max(0f, betweenModesSeconds);
            CaptureEnemyStates();
            RuntimePerformanceProbe.Completed += OnProbeCompleted;
            StartNextMode();
        }

        void Update()
        {
            if (!waitingForNextMode || RuntimePerformanceProbe.IsRunning) return;

            transitionRemaining -= Time.unscaledDeltaTime;
            if (transitionRemaining > 0f) return;

            waitingForNextMode = false;
            StartNextMode();
        }

        void CaptureEnemyStates()
        {
            enemyStates.Clear();
            var enemies = FindObjectsOfType<EnemyController>();
            Array.Sort(enemies, (left, right) => left.GetInstanceID().CompareTo(right.GetInstanceID()));
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null) continue;

                var body = enemy.GetComponent<Rigidbody2D>();
                enemyStates.Add(new EnemyState
                {
                    enemy = enemy,
                    body = body,
                    position = enemy.transform.position,
                    rotation = enemy.transform.rotation,
                    velocity = body != null ? body.velocity : Vector2.zero,
                    angularVelocity = body != null ? body.angularVelocity : 0f
                });
            }
        }

        void RestoreEnemyStates()
        {
            for (int i = 0; i < enemyStates.Count; i++)
            {
                var state = enemyStates[i];
                if (state.enemy == null) continue;

                state.enemy.transform.SetPositionAndRotation(state.position, state.rotation);
                if (state.body == null) continue;

                state.body.position = state.position;
                state.body.rotation = state.rotation.eulerAngles.z;
                state.body.velocity = state.velocity;
                state.body.angularVelocity = state.angularVelocity;
                state.body.WakeUp();
            }

            Physics2D.SyncTransforms();
        }

        void StartNextMode()
        {
            if (nextModeIndex >= modes.Length)
            {
                CompleteMatrix();
                return;
            }

            RestoreEnemyStates();
            runningModeIndex = nextModeIndex;
            var mode = modes[nextModeIndex++];
            Debug.Log(
                $"[PerformanceProbeMatrix] Starting {runningModeIndex + 1}/{modes.Length}: " +
                $"scenario={scenarioName}, mode={mode}, enemies={enemyStates.Count}.");
            RuntimePerformanceProbe.Begin(mode, durationSeconds, warmupSeconds);
        }

        void OnProbeCompleted(RuntimePerformanceProbeMode completedMode, string result)
        {
            if (runningModeIndex < 0 || runningModeIndex >= modes.Length) return;
            if (modes[runningModeIndex] != completedMode)
            {
                Debug.LogError(
                    $"[PerformanceProbeMatrix] Mode mismatch: expected={modes[runningModeIndex]}, " +
                    $"actual={completedMode}.");
                return;
            }

            results.Add(
                $"sequence={runningModeIndex + 1}; scenario={scenarioName}; " +
                $"expectedEnemies={enemyStates.Count}; {result}");
            runningModeIndex = -1;
            transitionRemaining = transitionSeconds;
            waitingForNextMode = true;
        }

        void CompleteMatrix()
        {
            RuntimePerformanceProbe.Completed -= OnProbeCompleted;
            string report = BuildReport();
            WriteReport(report);
            Debug.Log(
                $"[PerformanceProbeMatrix] Completed scenario={scenarioName}, " +
                $"modes={results.Count}, report={LastResultFilePath}");
            activeMatrix = null;
            Destroy(gameObject);
        }

        string BuildReport()
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("format=AreaSurvivorsPerformanceMatrixV1");
            builder.AppendLine($"createdUtc={DateTime.UtcNow:O}");
            builder.AppendLine($"scenario={scenarioName}");
            builder.AppendLine($"enemyCount={enemyStates.Count}");
            builder.AppendLine($"durationSeconds={durationSeconds:0.###}");
            builder.AppendLine($"warmupSeconds={warmupSeconds:0.###}");
            builder.AppendLine($"transitionSeconds={transitionSeconds:0.###}");
            for (int i = 0; i < results.Count; i++) builder.AppendLine(results[i]);
            return builder.ToString();
        }

        void WriteReport(string report)
        {
#if UNITY_EDITOR
            string lastResultPath = LastResultFilePath;
            string directory = Path.GetDirectoryName(lastResultPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(lastResultPath, report);

            string safeScenarioName = scenarioName;
            foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            {
                safeScenarioName = safeScenarioName.Replace(invalidCharacter, '_');
            }

            string archivePath = Path.Combine(
                directory ?? string.Empty,
                $"combat-performance-matrix-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{safeScenarioName}.txt");
            File.WriteAllText(archivePath, report);
#endif
        }

        void OnDestroy()
        {
            RuntimePerformanceProbe.Completed -= OnProbeCompleted;
            if (activeMatrix == this) activeMatrix = null;
        }
    }
}
