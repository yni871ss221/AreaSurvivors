using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AreaSurvivors.Testing
{
    public enum RuntimePerformanceProbeMode
    {
        Baseline = 0,
        DisableOcclusion = 1,
        DisableOutline = 2,
        DisableOcclusionAndOutline = 3,
        DisableEnemyController = 4,
        DisableEnemyContactCheck = 5,
        DisableEnemyMoveMultiplier = 6,
        DisableEnemyPaint = 7,
        DisableEnemyAnimation = 8,
        DisableEnemyYSort = 9,
        DisableDamagePopups = 10,
        DisableHitFlash = 11,
        DisableDamageFeedback = 12,
        DisableEnemyEnemyCollision = 13,
        EnablePhysicsMultithreading = 14
    }

    public sealed class RuntimePerformanceProbe : MonoBehaviour
    {
        struct BehaviourState
        {
            public Behaviour behaviour;
            public bool enabled;
        }

        struct GameObjectState
        {
            public GameObject gameObject;
            public bool activeSelf;
        }

        static RuntimePerformanceProbe activeProbe;

        public static string LastResult { get; private set; } = "Not measured";
        public static bool IsRunning => activeProbe != null;
        public static event Action<RuntimePerformanceProbeMode, string> Completed;

        readonly List<float> frameMs = new List<float>(1024);
        readonly List<BehaviourState> behaviourStates = new List<BehaviourState>();
        readonly List<GameObjectState> gameObjectStates = new List<GameObjectState>();

        RuntimePerformanceProbeMode mode;
        float durationSeconds;
        float warmupSecondsRemaining;
        float elapsedSeconds;
        bool recording;
        int startGc0;
        int startGc1;
        int startGc2;
        int enemyCountAtStart;
        long managedBytesAtStart;
        int popupCountAtStart;
        int hitFlashCountAtStart;
        int areaCountAtStart;
        int projectileCountAtStart;
        double legacyEnemyQueryMicroseconds;
        double registryEnemyQueryMicroseconds;
        int enemyQueryChecksum;
        PhysicsJobOptions2D previousPhysicsJobOptions;
        bool restorePhysicsJobOptions;
        int enemyCollisionLayer = -1;
        bool previousEnemyLayerCollisionIgnored;
        bool restoreEnemyLayerCollision;

        public static string LastResultFilePath
        {
            get
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(projectRoot, "Library", "AreaSafeUnity", "combat-performance-probe-last.txt");
            }
        }

        public static void Begin(RuntimePerformanceProbeMode probeMode, float duration, float warmupSeconds = 0f)
        {
            if (activeProbe != null)
            {
                if (activeProbe.recording) CombatPerformanceDiagnostics.EndRecording();
                activeProbe.RestoreStates();
                Destroy(activeProbe.gameObject);
                activeProbe = null;
            }

            var go = new GameObject("Runtime Performance Probe");
            DontDestroyOnLoad(go);
            activeProbe = go.AddComponent<RuntimePerformanceProbe>();
            activeProbe.StartProbe(probeMode, duration, warmupSeconds);
        }

        void StartProbe(RuntimePerformanceProbeMode probeMode, float duration, float warmupSeconds)
        {
            mode = probeMode;
            durationSeconds = Mathf.Max(0.5f, duration);
            warmupSecondsRemaining = Mathf.Max(0f, warmupSeconds);
            frameMs.Clear();
            ApplyMode();
            if (warmupSecondsRemaining <= 0f)
            {
                BeginRecording();
                return;
            }

            LastResult = $"Warming up mode={mode}, duration={warmupSecondsRemaining:0.0}s";
        }

        void BeginRecording()
        {
            elapsedSeconds = 0f;
            recording = true;
            enemyCountAtStart = FindObjectsOfType<EnemyController>().Length;
            MeasureEnemyQueryPaths();
            startGc0 = GC.CollectionCount(0);
            startGc1 = GC.CollectionCount(1);
            startGc2 = GC.CollectionCount(2);
            managedBytesAtStart = GC.GetTotalMemory(false);
            popupCountAtStart = FindObjectsOfType<DamagePopup>().Length;
            hitFlashCountAtStart = FindObjectsOfType<EnemyHitFlash>().Length;
            areaCountAtStart = FindObjectsOfType<AdvancedWeaponArea>().Length;
            projectileCountAtStart = FindObjectsOfType<AdvancedWeaponProjectile>().Length;
            CombatPerformanceDiagnostics.BeginRecording();
            ApplyModeOverrides();
            LastResult = $"Running mode={mode}, enemies={enemyCountAtStart}, duration={durationSeconds:0.0}s";
        }

        void Update()
        {
            float delta = Time.unscaledDeltaTime;
            if (!recording)
            {
                warmupSecondsRemaining -= delta;
                if (warmupSecondsRemaining <= 0f) BeginRecording();
                return;
            }

            elapsedSeconds += delta;
            frameMs.Add(delta * 1000f);
            if (elapsedSeconds < durationSeconds) return;

            Complete();
        }

        void ApplyMode()
        {
            bool disableOcclusion = mode == RuntimePerformanceProbeMode.DisableOcclusion ||
                mode == RuntimePerformanceProbeMode.DisableOcclusionAndOutline;
            bool disableOutline = mode == RuntimePerformanceProbeMode.DisableOutline ||
                mode == RuntimePerformanceProbeMode.DisableOcclusionAndOutline;
            bool disableEnemyController = mode == RuntimePerformanceProbeMode.DisableEnemyController;

            ApplyModeOverrides();

            if (mode == RuntimePerformanceProbeMode.DisableEnemyEnemyCollision)
            {
                DisableEnemyEnemyCollisions();
            }
            if (mode == RuntimePerformanceProbeMode.EnablePhysicsMultithreading)
            {
                previousPhysicsJobOptions = Physics2D.jobOptions;
                var jobOptions = previousPhysicsJobOptions;
                jobOptions.useMultithreading = true;
                Physics2D.jobOptions = jobOptions;
                restorePhysicsJobOptions = true;
            }

            if (disableOcclusion)
            {
                foreach (var reveal in FindObjectsOfType<CharacterOcclusionReveal>())
                {
                    if (reveal == null) continue;
                    behaviourStates.Add(new BehaviourState { behaviour = reveal, enabled = reveal.enabled });
                    reveal.enabled = false;
                }
            }

            if (disableOutline)
            {
                foreach (var outline in FindObjectsOfType<RuntimeSpriteOutline>())
                {
                    if (outline == null) continue;
                    behaviourStates.Add(new BehaviourState { behaviour = outline, enabled = outline.enabled });
                    outline.enabled = false;

                    var outlineChild = outline.transform.Find("Runtime Outline");
                    if (outlineChild == null) continue;
                    gameObjectStates.Add(new GameObjectState { gameObject = outlineChild.gameObject, activeSelf = outlineChild.gameObject.activeSelf });
                    outlineChild.gameObject.SetActive(false);
                }
            }

            if (disableEnemyController)
            {
                foreach (var enemy in FindObjectsOfType<EnemyController>())
                {
                    if (enemy == null) continue;
                    behaviourStates.Add(new BehaviourState { behaviour = enemy, enabled = enemy.enabled });
                    enemy.enabled = false;
                }
            }
        }

        void ApplyModeOverrides()
        {
            EnemyController.ResetPerformanceProbeOverrides();
            EnemyController.ProbeDisableContactCheck = mode == RuntimePerformanceProbeMode.DisableEnemyContactCheck;
            EnemyController.ProbeDisableMoveMultiplier = mode == RuntimePerformanceProbeMode.DisableEnemyMoveMultiplier;
            EnemyController.ProbeDisablePaint = mode == RuntimePerformanceProbeMode.DisableEnemyPaint;
            EnemyController.ProbeDisableAnimation = mode == RuntimePerformanceProbeMode.DisableEnemyAnimation;
            EnemyController.ProbeDisableYSort = mode == RuntimePerformanceProbeMode.DisableEnemyYSort;
            CombatPerformanceDiagnostics.SuppressDamagePopups =
                mode == RuntimePerformanceProbeMode.DisableDamagePopups ||
                mode == RuntimePerformanceProbeMode.DisableDamageFeedback;
            CombatPerformanceDiagnostics.SuppressHitFlash =
                mode == RuntimePerformanceProbeMode.DisableHitFlash ||
                mode == RuntimePerformanceProbeMode.DisableDamageFeedback;
        }

        void DisableEnemyEnemyCollisions()
        {
            enemyCollisionLayer = LayerMask.NameToLayer(EnemyController.EnemyLayerName);
            if (enemyCollisionLayer < 0) return;

            previousEnemyLayerCollisionIgnored =
                Physics2D.GetIgnoreLayerCollision(enemyCollisionLayer, enemyCollisionLayer);
            Physics2D.IgnoreLayerCollision(enemyCollisionLayer, enemyCollisionLayer, true);
            restoreEnemyLayerCollision = true;
        }

        void Complete()
        {
            var combatSnapshot = CombatPerformanceDiagnostics.EndRecording();
            recording = false;
            RestoreStates();

            int frameCount = frameMs.Count;
            float sum = 0f;
            float max = 0f;
            int over33 = 0;
            int over50 = 0;
            int over100 = 0;
            for (int i = 0; i < frameMs.Count; i++)
            {
                float ms = frameMs[i];
                sum += ms;
                max = Mathf.Max(max, ms);
                if (ms >= 33.33f) over33++;
                if (ms >= 50f) over50++;
                if (ms >= 100f) over100++;
            }

            var sorted = new List<float>(frameMs);
            sorted.Sort();
            float average = frameCount > 0 ? sum / frameCount : 0f;
            float p95 = Percentile(sorted, 0.95f);
            int enemyCountAtEnd = FindObjectsOfType<EnemyController>().Length;
            int popupCountAtEnd = FindObjectsOfType<DamagePopup>().Length;
            int hitFlashCountAtEnd = FindObjectsOfType<EnemyHitFlash>().Length;
            int areaCountAtEnd = FindObjectsOfType<AdvancedWeaponArea>().Length;
            int projectileCountAtEnd = FindObjectsOfType<AdvancedWeaponProjectile>().Length;
            long managedBytesDelta = GC.GetTotalMemory(false) - managedBytesAtStart;

            LastResult =
                $"mode={mode}; enemies={enemyCountAtStart}->{enemyCountAtEnd}; frames={frameCount}; " +
                $"avgMs={average:0.00}; p95Ms={p95:0.00}; maxMs={max:0.00}; " +
                $"over33={over33}; over50={over50}; over100={over100}; " +
                $"gc0={GC.CollectionCount(0) - startGc0}; gc1={GC.CollectionCount(1) - startGc1}; gc2={GC.CollectionCount(2) - startGc2}; " +
                $"managedDelta={managedBytesDelta}; popups={popupCountAtStart}->{popupCountAtEnd}; " +
                $"hitFlashes={hitFlashCountAtStart}->{hitFlashCountAtEnd}; " +
                $"areas={areaCountAtStart}->{areaCountAtEnd}; projectiles={projectileCountAtStart}->{projectileCountAtEnd}; " +
                $"enemyQueryLegacyUs={legacyEnemyQueryMicroseconds:0.00}; " +
                $"enemyQueryRegistryUs={registryEnemyQueryMicroseconds:0.00}; enemyQueryChecksum={enemyQueryChecksum}; " +
                combatSnapshot.ToCompactString();
            Debug.Log($"[PerformanceProbe] {LastResult}");
            WriteLastResult();

            var completedMode = mode;
            var completedResult = LastResult;
            activeProbe = null;
            Destroy(gameObject);
            Completed?.Invoke(completedMode, completedResult);
        }

        void MeasureEnemyQueryPaths()
        {
            const int iterations = 16;
            int checksum = 0;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var enemies = FindObjectsOfType<EnemyController>();
                for (int i = 0; i < enemies.Length; i++)
                {
                    var enemy = enemies[i];
                    if (enemy != null && enemy.IsAlive) checksum = unchecked(checksum * 31 + enemy.GetInstanceID());
                }
            }
            stopwatch.Stop();
            legacyEnemyQueryMicroseconds = stopwatch.Elapsed.TotalMilliseconds * 1000.0 / iterations;

            stopwatch.Restart();
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var enemies = EnemyController.ActiveEnemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var enemy = enemies[i];
                    if (enemy != null && enemy.IsAlive) checksum = unchecked(checksum * 31 + enemy.GetInstanceID());
                }
            }
            stopwatch.Stop();
            registryEnemyQueryMicroseconds = stopwatch.Elapsed.TotalMilliseconds * 1000.0 / iterations;
            enemyQueryChecksum = checksum;
        }

        void RestoreStates()
        {
            for (int i = 0; i < behaviourStates.Count; i++)
            {
                var state = behaviourStates[i];
                if (state.behaviour != null) state.behaviour.enabled = state.enabled;
            }

            for (int i = 0; i < gameObjectStates.Count; i++)
            {
                var state = gameObjectStates[i];
                if (state.gameObject != null) state.gameObject.SetActive(state.activeSelf);
            }

            behaviourStates.Clear();
            gameObjectStates.Clear();
            if (restoreEnemyLayerCollision && enemyCollisionLayer >= 0)
            {
                Physics2D.IgnoreLayerCollision(
                    enemyCollisionLayer,
                    enemyCollisionLayer,
                    previousEnemyLayerCollisionIgnored);
            }
            enemyCollisionLayer = -1;
            restoreEnemyLayerCollision = false;
            if (restorePhysicsJobOptions)
            {
                Physics2D.jobOptions = previousPhysicsJobOptions;
                restorePhysicsJobOptions = false;
            }
            EnemyController.ResetPerformanceProbeOverrides();
            CombatPerformanceDiagnostics.ResetModeOverrides();
            recording = false;
        }

        static void WriteLastResult()
        {
#if UNITY_EDITOR
            string path = LastResultFilePath;
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, LastResult);
#endif
        }

        static float Percentile(List<float> sortedValues, float percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0) return 0f;
            int index = Mathf.Clamp(Mathf.CeilToInt(sortedValues.Count * percentile) - 1, 0, sortedValues.Count - 1);
            return sortedValues[index];
        }
    }
}
