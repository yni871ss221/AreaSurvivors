using System;
using System.Collections.Generic;
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
        DisableEnemyYSort = 9
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

        readonly List<float> frameMs = new List<float>(1024);
        readonly List<BehaviourState> behaviourStates = new List<BehaviourState>();
        readonly List<GameObjectState> gameObjectStates = new List<GameObjectState>();

        RuntimePerformanceProbeMode mode;
        float durationSeconds;
        float elapsedSeconds;
        int startGc0;
        int startGc1;
        int startGc2;
        int enemyCountAtStart;

        public static void Begin(RuntimePerformanceProbeMode probeMode, float duration)
        {
            if (activeProbe != null)
            {
                activeProbe.RestoreStates();
                Destroy(activeProbe.gameObject);
                activeProbe = null;
            }

            var go = new GameObject("Runtime Performance Probe");
            DontDestroyOnLoad(go);
            activeProbe = go.AddComponent<RuntimePerformanceProbe>();
            activeProbe.StartProbe(probeMode, duration);
        }

        void StartProbe(RuntimePerformanceProbeMode probeMode, float duration)
        {
            mode = probeMode;
            durationSeconds = Mathf.Max(0.5f, duration);
            elapsedSeconds = 0f;
            frameMs.Clear();
            startGc0 = GC.CollectionCount(0);
            startGc1 = GC.CollectionCount(1);
            startGc2 = GC.CollectionCount(2);
            enemyCountAtStart = FindObjectsOfType<EnemyController>().Length;
            ApplyMode();
            LastResult = $"Running mode={mode}, enemies={enemyCountAtStart}, duration={durationSeconds:0.0}s";
        }

        void Update()
        {
            float delta = Time.unscaledDeltaTime;
            elapsedSeconds += delta;
            frameMs.Add(delta * 1000f);
            if (elapsedSeconds < durationSeconds) return;

            Complete();
        }

        void ApplyMode()
        {
            EnemyController.ResetPerformanceProbeOverrides();

            bool disableOcclusion = mode == RuntimePerformanceProbeMode.DisableOcclusion ||
                mode == RuntimePerformanceProbeMode.DisableOcclusionAndOutline;
            bool disableOutline = mode == RuntimePerformanceProbeMode.DisableOutline ||
                mode == RuntimePerformanceProbeMode.DisableOcclusionAndOutline;
            bool disableEnemyController = mode == RuntimePerformanceProbeMode.DisableEnemyController;

            EnemyController.ProbeDisableContactCheck = mode == RuntimePerformanceProbeMode.DisableEnemyContactCheck;
            EnemyController.ProbeDisableMoveMultiplier = mode == RuntimePerformanceProbeMode.DisableEnemyMoveMultiplier;
            EnemyController.ProbeDisablePaint = mode == RuntimePerformanceProbeMode.DisableEnemyPaint;
            EnemyController.ProbeDisableAnimation = mode == RuntimePerformanceProbeMode.DisableEnemyAnimation;
            EnemyController.ProbeDisableYSort = mode == RuntimePerformanceProbeMode.DisableEnemyYSort;

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

        void Complete()
        {
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

            LastResult =
                $"mode={mode}; enemies={enemyCountAtStart}->{enemyCountAtEnd}; frames={frameCount}; " +
                $"avgMs={average:0.00}; p95Ms={p95:0.00}; maxMs={max:0.00}; " +
                $"over33={over33}; over50={over50}; over100={over100}; " +
                $"gc0={GC.CollectionCount(0) - startGc0}; gc1={GC.CollectionCount(1) - startGc1}; gc2={GC.CollectionCount(2) - startGc2}";

            activeProbe = null;
            Destroy(gameObject);
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
            EnemyController.ResetPerformanceProbeOverrides();
        }

        static float Percentile(List<float> sortedValues, float percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0) return 0f;
            int index = Mathf.Clamp(Mathf.CeilToInt(sortedValues.Count * percentile) - 1, 0, sortedValues.Count - 1);
            return sortedValues[index];
        }
    }
}
