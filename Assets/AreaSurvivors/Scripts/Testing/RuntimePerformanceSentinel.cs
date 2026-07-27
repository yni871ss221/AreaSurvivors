using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace AreaSurvivors.Testing
{
    public sealed class RuntimePerformanceSentinel : MonoBehaviour
    {
        [Serializable]
        public struct FrameSample
        {
            public float sessionSeconds;
            public float frameMs;
            public float mainThreadMs;
            public long gcAllocatedBytes;
            public long totalUsedMemoryBytes;
            public int enemyCount;
            public int gcCollections;
            public long areaQueries;
            public long areaCandidates;
            public long projectileQueries;
            public long projectileCandidates;
            public long projectileTriggerCallbacks;
            public long projectileDamageHits;
            public long excaliburProjectileDamageHits;
            public long bananaProjectileDamageHits;
            public long damageFeedbackEvents;
            public long popupRequests;
            public long popupSpawns;
            public long popupInstancesCreated;
            public long popupReuses;
            public long popupDrops;
            public int activeDamagePopups;
            public long hitFlashRequests;
            public long hitFlashCoalescedRequests;
            public int activeHitFlashes;
            public long enemyDeaths;
            public long xpOrbSpawns;
            public long pickupProximityScans;
            public long pickupScanCandidates;
            public long pickupAttractionsStarted;
            public long pickupMovementTicks;
            public long bananaOverlapQueries;
            public long bananaColliderCandidates;
            public long summonedEnemySpawnAttempts;
            public long summonedEnemySpawns;
            public long summonedEnemyCapBlocked;
        }

        [Serializable]
        public sealed class IncidentSummary
        {
            public int incidentIndex;
            public string fileName;
            public string reason;
            public float triggeredAtSeconds;
            public int stage;
            public int stageDifficulty;
            public int maxAliveEnemies;
            public float gameElapsedSeconds;
            public int frameCount;
            public float averageFrameMs;
            public float p95FrameMs;
            public float p99FrameMs;
            public float maxFrameMs;
            public int framesOver33Ms;
            public int framesOver50Ms;
            public int framesOver100Ms;
            public long gcAllocatedBytes;
            public int gcCollections;
            public int peakEnemies;
            public long areaQueries;
            public long areaCandidates;
            public long projectileQueries;
            public long projectileCandidates;
            public long projectileTriggerCallbacks;
            public long projectileDamageHits;
            public long excaliburProjectileDamageHits;
            public long bananaProjectileDamageHits;
            public long damageFeedbackEvents;
            public long popupRequests;
            public long popupSpawns;
            public long popupInstancesCreated;
            public long popupReuses;
            public long popupDrops;
            public int peakActiveDamagePopups;
            public long hitFlashRequests;
            public long hitFlashCoalescedRequests;
            public int peakActiveHitFlashes;
            public long enemyDeaths;
            public long xpOrbSpawns;
            public long pickupProximityScans;
            public long pickupScanCandidates;
            public long pickupAttractionsStarted;
            public long pickupMovementTicks;
            public long bananaOverlapQueries;
            public long bananaColliderCandidates;
            public long summonedEnemySpawnAttempts;
            public long summonedEnemySpawns;
            public long summonedEnemyCapBlocked;
        }

        [Serializable]
        public sealed class IncidentReport
        {
            public string sessionId;
            public string capturedUtc;
            public IncidentSummary summary;
            public string character;
            public string[] weapons;
            public string[] upgrades;
            public string[] relics;
            public float playerPositionX;
            public float playerPositionY;
            public int stageDifficulty;
            public int maxAliveEnemies;
            public int activeEnemies;
            public int activeProjectiles;
            public int activeAdvancedAreas;
            public int activeDamagePopups;
            public int activeHitFlashes;
            public int activeExperienceOrbs;
            public int activeTokenOrbs;
            public FrameSample[] samples;
        }

        [Serializable]
        public sealed class SessionReport
        {
            public string sessionId;
            public string startedUtc;
            public string endedUtc;
            public string unityVersion;
            public string platform;
            public string graphicsDevice;
            public string qualityLevel;
            public int screenWidth;
            public int screenHeight;
            public int vSyncCount;
            public int targetFrameRate;
            public int stageDifficulty;
            public int maxAliveEnemies;
            public float warmupSeconds;
            public float preCaptureSeconds;
            public float postCaptureSeconds;
            public float baselineP95Ms;
            public float normalSentinelAverageMicroseconds;
            public float normalSentinelMaxMicroseconds;
            public float maxIncidentWriteMilliseconds;
            public int normalSentinelSamples;
            public List<IncidentSummary> incidents = new List<IncidentSummary>();
        }

        [Header("Monitoring")]
        [SerializeField] bool monitoringEnabled = true;
        [SerializeField, Min(1f)] float warmupSeconds = 10f;
        [SerializeField, Min(1f)] float preCaptureSeconds = 5f;
        [SerializeField, Min(1f)] float postCaptureSeconds = 10f;
        [SerializeField, Min(1f)] float evaluationIntervalSeconds = 1f;
        [SerializeField, Min(1)] int maxIncidentsPerSession = 20;
        [SerializeField, Range(30, 480)] int maxTrackedFramesPerSecond = 240;

        [Header("Detection")]
        [SerializeField, Min(1f)] float slowFrameThresholdMs = 33.33f;
        [SerializeField, Min(1f)] float criticalFrameThresholdMs = 100f;
        [SerializeField, Min(1f)] float absoluteP95ThresholdMs = 33.33f;
        [SerializeField, Min(1f)] float relativeP95Multiplier = 1.6f;
        [SerializeField, Min(1)] int minimumSlowFramesInWindow = 5;
        [SerializeField, Min(0f)] float incidentCooldownSeconds = 20f;
        [SerializeField, Min(0f)] float focusRecoveryGraceSeconds = 2f;

        FrameSample[] ringSamples;
        float[] percentileScratch;
        int ringWriteIndex;
        int ringCount;
        List<FrameSample> activeIncidentSamples;
        readonly List<IncidentSummary> incidentSummaries = new List<IncidentSummary>();
        string activeIncidentReason;
        float activeIncidentTriggeredAt;
        float activeIncidentEndAt;
        float sessionStartRealtime;
        float nextEvaluationAt;
        float suppressDetectionUntil;
        float nextIncidentAllowedAt;
        float baselineP95Ms;
        string sessionId;
        string sessionDirectory;
        string sessionStartedUtc;
        bool sessionStarted;
        bool sessionFinished;
        bool incidentWriteOccurredThisFrame;
        bool previousContinuousRecording;
        double sentinelUpdateMicrosecondsTotal;
        double sentinelUpdateMicrosecondsMax;
        int sentinelUpdateSamples;
        double maxIncidentWriteMilliseconds;
        long lastManagedBytes;
        float nextManagedMemorySampleAt;
        int lastGc0;
        int lastGc1;
        int lastGc2;
        CombatPerformanceDiagnostics.Snapshot lastCombatSnapshot;
        ProfilerRecorder mainThreadRecorder;
        ProfilerRecorder gcAllocatedRecorder;
        ProfilerRecorder totalUsedMemoryRecorder;

        public static string LatestSessionDirectory { get; private set; } = string.Empty;
        public bool MonitoringEnabled => monitoringEnabled;
        public float WarmupSeconds => warmupSeconds;
        public float PreCaptureSeconds => preCaptureSeconds;
        public float PostCaptureSeconds => postCaptureSeconds;
        public float SlowFrameThresholdMs => slowFrameThresholdMs;
        public float CriticalFrameThresholdMs => criticalFrameThresholdMs;
        public float AbsoluteP95ThresholdMs => absoluteP95ThresholdMs;
        public float RelativeP95Multiplier => relativeP95Multiplier;
        public int MinimumSlowFramesInWindow => minimumSlowFramesInWindow;
        public int MaxIncidentsPerSession => maxIncidentsPerSession;

        void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!monitoringEnabled)
            {
                enabled = false;
                return;
            }
            BeginSession();
#else
            enabled = false;
#endif
        }

        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!sessionStarted || sessionFinished) return;
            long startedTicks = Stopwatch.GetTimestamp();
            incidentWriteOccurredThisFrame = false;
            SampleAndEvaluate();
            if (!incidentWriteOccurredThisFrame)
            {
                double elapsedMicroseconds = ElapsedMilliseconds(startedTicks) * 1000.0;
                sentinelUpdateMicrosecondsTotal += elapsedMicroseconds;
                sentinelUpdateMicrosecondsMax = Math.Max(sentinelUpdateMicrosecondsMax, elapsedMicroseconds);
                sentinelUpdateSamples++;
            }
#endif
        }

        void OnApplicationFocus(bool hasFocus)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (hasFocus) suppressDetectionUntil = Time.realtimeSinceStartup + focusRecoveryGraceSeconds;
#endif
        }

        void OnApplicationPause(bool paused)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!paused) suppressDetectionUntil = Time.realtimeSinceStartup + focusRecoveryGraceSeconds;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EndSession();
#endif
        }

        void OnApplicationQuit()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EndSession();
#endif
        }

        void BeginSession()
        {
            int ringCapacity = Mathf.Clamp(
                Mathf.CeilToInt(preCaptureSeconds * maxTrackedFramesPerSecond) + 1,
                120,
                7200);
            ringSamples = new FrameSample[ringCapacity];
            percentileScratch = new float[ringCapacity];
            sessionStartRealtime = Time.realtimeSinceStartup;
            nextEvaluationAt = sessionStartRealtime + warmupSeconds;
            suppressDetectionUntil = sessionStartRealtime + warmupSeconds;
            nextManagedMemorySampleAt = sessionStartRealtime;
            lastGc0 = GC.CollectionCount(0);
            lastGc1 = GC.CollectionCount(1);
            lastGc2 = GC.CollectionCount(2);
            lastManagedBytes = GC.GetTotalMemory(false);
            lastCombatSnapshot = CombatPerformanceDiagnostics.GetCurrentSnapshot();
            previousContinuousRecording = CombatPerformanceDiagnostics.ContinuousRecordingEnabled;
            CombatPerformanceDiagnostics.ContinuousRecordingEnabled = true;
            StartRecorder(ref mainThreadRecorder, ProfilerCategory.Internal, "Main Thread");
            StartRecorder(ref gcAllocatedRecorder, ProfilerCategory.Memory, "GC Allocated In Frame");
            StartRecorder(ref totalUsedMemoryRecorder, ProfilerCategory.Memory, "Total Used Memory");

            sessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            sessionStartedUtc = DateTime.UtcNow.ToString("O");
            sessionDirectory = Path.Combine(ResolveSessionRoot(), sessionId);
            Directory.CreateDirectory(sessionDirectory);
            LatestSessionDirectory = sessionDirectory;
            WriteTextSafely(Path.Combine(ResolveSessionRoot(), "latest-session.txt"), sessionDirectory);
            sessionStarted = true;
            WriteSessionReport(false);
            UnityEngine.Debug.Log($"[PerformanceSentinel] Monitoring started: {sessionDirectory}");
        }

        void EndSession()
        {
            if (!sessionStarted || sessionFinished) return;
            sessionFinished = true;
            if (activeIncidentSamples != null) FinalizeIncident();
            CombatPerformanceDiagnostics.ContinuousRecordingEnabled = previousContinuousRecording;
            DisposeRecorder(ref mainThreadRecorder);
            DisposeRecorder(ref gcAllocatedRecorder);
            DisposeRecorder(ref totalUsedMemoryRecorder);
            WriteSessionReport(true);
            UnityEngine.Debug.Log(
                $"[PerformanceSentinel] Monitoring finished: incidents={incidentSummaries.Count}; {sessionDirectory}");
        }

        void SampleAndEvaluate()
        {
            float now = Time.realtimeSinceStartup;
            var combatSnapshot = CombatPerformanceDiagnostics.GetCurrentSnapshot();
            int gc0 = GC.CollectionCount(0);
            int gc1 = GC.CollectionCount(1);
            int gc2 = GC.CollectionCount(2);
            if (now >= nextManagedMemorySampleAt)
            {
                lastManagedBytes = totalUsedMemoryRecorder.Valid
                    ? totalUsedMemoryRecorder.LastValue
                    : GC.GetTotalMemory(false);
                nextManagedMemorySampleAt = now + 1f;
            }

            var sample = new FrameSample
            {
                sessionSeconds = now - sessionStartRealtime,
                frameMs = Time.unscaledDeltaTime * 1000f,
                mainThreadMs = mainThreadRecorder.Valid
                    ? Mathf.Max(0f, (float)(mainThreadRecorder.LastValue / 1000000.0))
                    : Time.unscaledDeltaTime * 1000f,
                gcAllocatedBytes = gcAllocatedRecorder.Valid ? Math.Max(0L, gcAllocatedRecorder.LastValue) : 0L,
                totalUsedMemoryBytes = Math.Max(0L, lastManagedBytes),
                enemyCount = EnemyController.ActiveEnemies.Count,
                gcCollections = Math.Max(0, gc0 - lastGc0) + Math.Max(0, gc1 - lastGc1) + Math.Max(0, gc2 - lastGc2),
                areaQueries = PositiveDelta(combatSnapshot.areaOverlapQueries, lastCombatSnapshot.areaOverlapQueries),
                areaCandidates = PositiveDelta(combatSnapshot.areaColliderCandidates, lastCombatSnapshot.areaColliderCandidates),
                projectileQueries = PositiveDelta(combatSnapshot.projectileOverlapQueries, lastCombatSnapshot.projectileOverlapQueries),
                projectileCandidates = PositiveDelta(combatSnapshot.projectileColliderCandidates, lastCombatSnapshot.projectileColliderCandidates),
                projectileTriggerCallbacks = PositiveDelta(combatSnapshot.projectileTriggerCallbacks, lastCombatSnapshot.projectileTriggerCallbacks),
                projectileDamageHits = PositiveDelta(combatSnapshot.projectileDamageHits, lastCombatSnapshot.projectileDamageHits),
                excaliburProjectileDamageHits = PositiveDelta(combatSnapshot.excaliburProjectileDamageHits, lastCombatSnapshot.excaliburProjectileDamageHits),
                bananaProjectileDamageHits = PositiveDelta(combatSnapshot.bananaProjectileDamageHits, lastCombatSnapshot.bananaProjectileDamageHits),
                damageFeedbackEvents = PositiveDelta(combatSnapshot.damageFeedbackEvents, lastCombatSnapshot.damageFeedbackEvents),
                popupRequests = PositiveDelta(combatSnapshot.damagePopupRequests, lastCombatSnapshot.damagePopupRequests),
                popupSpawns = PositiveDelta(combatSnapshot.damagePopupSpawns, lastCombatSnapshot.damagePopupSpawns),
                popupInstancesCreated = PositiveDelta(combatSnapshot.damagePopupInstancesCreated, lastCombatSnapshot.damagePopupInstancesCreated),
                popupReuses = PositiveDelta(combatSnapshot.damagePopupReuses, lastCombatSnapshot.damagePopupReuses),
                popupDrops = PositiveDelta(combatSnapshot.damagePopupDrops, lastCombatSnapshot.damagePopupDrops),
                activeDamagePopups = DamagePopup.ActiveCount,
                hitFlashRequests = PositiveDelta(combatSnapshot.hitFlashPlayRequests, lastCombatSnapshot.hitFlashPlayRequests),
                hitFlashCoalescedRequests = PositiveDelta(combatSnapshot.hitFlashCoalescedRequests, lastCombatSnapshot.hitFlashCoalescedRequests),
                activeHitFlashes = EnemyHitFlash.ActiveFlashCount,
                enemyDeaths = PositiveDelta(combatSnapshot.enemyDeaths, lastCombatSnapshot.enemyDeaths),
                xpOrbSpawns = PositiveDelta(combatSnapshot.xpOrbSpawns, lastCombatSnapshot.xpOrbSpawns),
                pickupProximityScans = PositiveDelta(combatSnapshot.pickupProximityScans, lastCombatSnapshot.pickupProximityScans),
                pickupScanCandidates = PositiveDelta(combatSnapshot.pickupScanCandidates, lastCombatSnapshot.pickupScanCandidates),
                pickupAttractionsStarted = PositiveDelta(combatSnapshot.pickupAttractionsStarted, lastCombatSnapshot.pickupAttractionsStarted),
                pickupMovementTicks = PositiveDelta(combatSnapshot.pickupMovementTicks, lastCombatSnapshot.pickupMovementTicks),
                bananaOverlapQueries = PositiveDelta(combatSnapshot.bananaOverlapQueries, lastCombatSnapshot.bananaOverlapQueries),
                bananaColliderCandidates = PositiveDelta(combatSnapshot.bananaColliderCandidates, lastCombatSnapshot.bananaColliderCandidates),
                summonedEnemySpawnAttempts = PositiveDelta(combatSnapshot.summonedEnemySpawnAttempts, lastCombatSnapshot.summonedEnemySpawnAttempts),
                summonedEnemySpawns = PositiveDelta(combatSnapshot.summonedEnemySpawns, lastCombatSnapshot.summonedEnemySpawns),
                summonedEnemyCapBlocked = PositiveDelta(combatSnapshot.summonedEnemyCapBlocked, lastCombatSnapshot.summonedEnemyCapBlocked)
            };
            lastGc0 = gc0;
            lastGc1 = gc1;
            lastGc2 = gc2;
            lastCombatSnapshot = combatSnapshot;
            PushRingSample(sample);

            if (activeIncidentSamples != null)
            {
                activeIncidentSamples.Add(sample);
                if (now >= activeIncidentEndAt) FinalizeIncident();
                return;
            }

            if (now < nextEvaluationAt) return;
            nextEvaluationAt = now + evaluationIntervalSeconds;
            if (now < suppressDetectionUntil ||
                now < nextIncidentAllowedAt ||
                incidentSummaries.Count >= maxIncidentsPerSession ||
                Time.timeScale <= 0f ||
                !Application.isFocused)
            {
                return;
            }

            float rollingP95 = CalculateRingPercentile(0.95f);
            if (baselineP95Ms <= 0f)
            {
                baselineP95Ms = Mathf.Max(1f, rollingP95);
                return;
            }

            float maxFrameMs = 0f;
            int slowFrames = 0;
            int gcCollections = 0;
            long gcAllocatedBytes = 0;
            for (int i = 0; i < ringCount; i++)
            {
                var ringSample = GetRingSample(i);
                maxFrameMs = Mathf.Max(maxFrameMs, ringSample.frameMs);
                if (ringSample.frameMs >= slowFrameThresholdMs) slowFrames++;
                gcCollections += ringSample.gcCollections;
                gcAllocatedBytes += ringSample.gcAllocatedBytes;
            }

            string reason = null;
            if (maxFrameMs >= criticalFrameThresholdMs)
            {
                reason = $"critical-frame max={maxFrameMs:0.00}ms";
            }
            else
            {
                float requiredP95 = Mathf.Max(absoluteP95ThresholdMs, baselineP95Ms * relativeP95Multiplier);
                if (slowFrames >= minimumSlowFramesInWindow && rollingP95 >= requiredP95)
                {
                    reason =
                        $"sustained-slow p95={rollingP95:0.00}ms baseline={baselineP95Ms:0.00}ms slowFrames={slowFrames}";
                }
                else if (gcCollections > 0 &&
                         gcAllocatedBytes >= 8L * 1024L * 1024L &&
                         rollingP95 >= absoluteP95ThresholdMs)
                {
                    reason =
                        $"gc-pressure p95={rollingP95:0.00}ms collections={gcCollections} allocated={gcAllocatedBytes}";
                }
            }

            if (!string.IsNullOrEmpty(reason)) BeginIncident(reason, now);
        }

        void PushRingSample(FrameSample sample)
        {
            ringSamples[ringWriteIndex] = sample;
            ringWriteIndex = (ringWriteIndex + 1) % ringSamples.Length;
            ringCount = Mathf.Min(ringCount + 1, ringSamples.Length);
            PruneRingSamplesOlderThan(sample.sessionSeconds - preCaptureSeconds);
        }

        void PruneRingSamplesOlderThan(float earliestSessionSeconds)
        {
            while (ringCount > 1 && GetRingSample(0).sessionSeconds < earliestSessionSeconds)
                ringCount--;
        }

        FrameSample GetRingSample(int logicalIndex)
        {
            int oldestIndex = (ringWriteIndex - ringCount + ringSamples.Length) % ringSamples.Length;
            return ringSamples[(oldestIndex + logicalIndex) % ringSamples.Length];
        }

        void BeginIncident(string reason, float now)
        {
            activeIncidentReason = reason;
            activeIncidentTriggeredAt = now - sessionStartRealtime;
            activeIncidentEndAt = now + postCaptureSeconds;
            activeIncidentSamples = new List<FrameSample>(
                ringCount + Mathf.CeilToInt(postCaptureSeconds * 120f));
            for (int i = 0; i < ringCount; i++)
            {
                activeIncidentSamples.Add(GetRingSample(i));
            }
            UnityEngine.Debug.Log($"[PerformanceSentinel] Incident detected: {reason}");
        }

        void FinalizeIncident()
        {
            if (activeIncidentSamples == null || activeIncidentSamples.Count == 0)
            {
                activeIncidentSamples = null;
                return;
            }

            long writeStartedTicks = Stopwatch.GetTimestamp();
            int incidentIndex = incidentSummaries.Count + 1;
            var summary = BuildIncidentSummary(incidentIndex, activeIncidentSamples);
            string fileName = $"incident-{incidentIndex:000}.json";
            summary.fileName = fileName;
            var report = BuildIncidentReport(summary, activeIncidentSamples);
            WriteTextSafely(
                Path.Combine(sessionDirectory, fileName),
                JsonUtility.ToJson(report, true));
            incidentSummaries.Add(summary);
            activeIncidentSamples = null;
            activeIncidentReason = null;
            nextIncidentAllowedAt = Time.realtimeSinceStartup + incidentCooldownSeconds;
            WriteSessionReport(false);
            double writeMilliseconds = ElapsedMilliseconds(writeStartedTicks);
            maxIncidentWriteMilliseconds = Math.Max(maxIncidentWriteMilliseconds, writeMilliseconds);
            incidentWriteOccurredThisFrame = true;
        }

        IncidentSummary BuildIncidentSummary(int incidentIndex, List<FrameSample> samples)
        {
            int frameCount = samples.Count;
            var values = new float[frameCount];
            float sum = 0f;
            float max = 0f;
            int over33 = 0;
            int over50 = 0;
            int over100 = 0;
            long gcAllocatedBytes = 0;
            int gcCollections = 0;
            int peakEnemies = 0;
            long areaQueries = 0;
            long areaCandidates = 0;
            long projectileQueries = 0;
            long projectileCandidates = 0;
            long projectileTriggerCallbacks = 0;
            long projectileDamageHits = 0;
            long excaliburProjectileDamageHits = 0;
            long bananaProjectileDamageHits = 0;
            long damageFeedbackEvents = 0;
            long popupRequests = 0;
            long popupSpawns = 0;
            long popupInstancesCreated = 0;
            long popupReuses = 0;
            long popupDrops = 0;
            int peakActiveDamagePopups = 0;
            long hitFlashRequests = 0;
            long hitFlashCoalescedRequests = 0;
            int peakActiveHitFlashes = 0;
            long enemyDeaths = 0;
            long xpOrbSpawns = 0;
            long pickupProximityScans = 0;
            long pickupScanCandidates = 0;
            long pickupAttractionsStarted = 0;
            long pickupMovementTicks = 0;
            long bananaOverlapQueries = 0;
            long bananaColliderCandidates = 0;
            long summonedEnemySpawnAttempts = 0;
            long summonedEnemySpawns = 0;
            long summonedEnemyCapBlocked = 0;
            for (int i = 0; i < frameCount; i++)
            {
                var sample = samples[i];
                values[i] = sample.frameMs;
                sum += sample.frameMs;
                max = Mathf.Max(max, sample.frameMs);
                if (sample.frameMs >= 33.33f) over33++;
                if (sample.frameMs >= 50f) over50++;
                if (sample.frameMs >= 100f) over100++;
                gcAllocatedBytes += sample.gcAllocatedBytes;
                gcCollections += sample.gcCollections;
                peakEnemies = Mathf.Max(peakEnemies, sample.enemyCount);
                areaQueries += sample.areaQueries;
                areaCandidates += sample.areaCandidates;
                projectileQueries += sample.projectileQueries;
                projectileCandidates += sample.projectileCandidates;
                projectileTriggerCallbacks += sample.projectileTriggerCallbacks;
                projectileDamageHits += sample.projectileDamageHits;
                excaliburProjectileDamageHits += sample.excaliburProjectileDamageHits;
                bananaProjectileDamageHits += sample.bananaProjectileDamageHits;
                damageFeedbackEvents += sample.damageFeedbackEvents;
                popupRequests += sample.popupRequests;
                popupSpawns += sample.popupSpawns;
                popupInstancesCreated += sample.popupInstancesCreated;
                popupReuses += sample.popupReuses;
                popupDrops += sample.popupDrops;
                peakActiveDamagePopups = Mathf.Max(peakActiveDamagePopups, sample.activeDamagePopups);
                hitFlashRequests += sample.hitFlashRequests;
                hitFlashCoalescedRequests += sample.hitFlashCoalescedRequests;
                peakActiveHitFlashes = Mathf.Max(peakActiveHitFlashes, sample.activeHitFlashes);
                enemyDeaths += sample.enemyDeaths;
                xpOrbSpawns += sample.xpOrbSpawns;
                pickupProximityScans += sample.pickupProximityScans;
                pickupScanCandidates += sample.pickupScanCandidates;
                pickupAttractionsStarted += sample.pickupAttractionsStarted;
                pickupMovementTicks += sample.pickupMovementTicks;
                bananaOverlapQueries += sample.bananaOverlapQueries;
                bananaColliderCandidates += sample.bananaColliderCandidates;
                summonedEnemySpawnAttempts += sample.summonedEnemySpawnAttempts;
                summonedEnemySpawns += sample.summonedEnemySpawns;
                summonedEnemyCapBlocked += sample.summonedEnemyCapBlocked;
            }
            Array.Sort(values);
            var manager = GameManager.Instance;
            var spawner = manager != null ? manager.spawner : null;
            return new IncidentSummary
            {
                incidentIndex = incidentIndex,
                reason = activeIncidentReason,
                triggeredAtSeconds = activeIncidentTriggeredAt,
                stage = manager != null ? manager.CurrentStage : 0,
                stageDifficulty = spawner != null ? spawner.CurrentStageDifficulty : 0,
                maxAliveEnemies = spawner != null ? spawner.CurrentMaxAliveEnemies : 0,
                gameElapsedSeconds = manager != null ? manager.ElapsedSeconds : 0f,
                frameCount = frameCount,
                averageFrameMs = frameCount > 0 ? sum / frameCount : 0f,
                p95FrameMs = Percentile(values, 0.95f),
                p99FrameMs = Percentile(values, 0.99f),
                maxFrameMs = max,
                framesOver33Ms = over33,
                framesOver50Ms = over50,
                framesOver100Ms = over100,
                gcAllocatedBytes = gcAllocatedBytes,
                gcCollections = gcCollections,
                peakEnemies = peakEnemies,
                areaQueries = areaQueries,
                areaCandidates = areaCandidates,
                projectileQueries = projectileQueries,
                projectileCandidates = projectileCandidates,
                projectileTriggerCallbacks = projectileTriggerCallbacks,
                projectileDamageHits = projectileDamageHits,
                excaliburProjectileDamageHits = excaliburProjectileDamageHits,
                bananaProjectileDamageHits = bananaProjectileDamageHits,
                damageFeedbackEvents = damageFeedbackEvents,
                popupRequests = popupRequests,
                popupSpawns = popupSpawns,
                popupInstancesCreated = popupInstancesCreated,
                popupReuses = popupReuses,
                popupDrops = popupDrops,
                peakActiveDamagePopups = peakActiveDamagePopups,
                hitFlashRequests = hitFlashRequests,
                hitFlashCoalescedRequests = hitFlashCoalescedRequests,
                peakActiveHitFlashes = peakActiveHitFlashes,
                enemyDeaths = enemyDeaths,
                xpOrbSpawns = xpOrbSpawns,
                pickupProximityScans = pickupProximityScans,
                pickupScanCandidates = pickupScanCandidates,
                pickupAttractionsStarted = pickupAttractionsStarted,
                pickupMovementTicks = pickupMovementTicks,
                bananaOverlapQueries = bananaOverlapQueries,
                bananaColliderCandidates = bananaColliderCandidates,
                summonedEnemySpawnAttempts = summonedEnemySpawnAttempts,
                summonedEnemySpawns = summonedEnemySpawns,
                summonedEnemyCapBlocked = summonedEnemyCapBlocked
            };
        }

        IncidentReport BuildIncidentReport(IncidentSummary summary, List<FrameSample> samples)
        {
            var manager = GameManager.Instance;
            var spawner = manager != null ? manager.spawner : null;
            var player = manager != null ? manager.Player : null;
            var position = player != null ? player.transform.position : Vector3.zero;
            return new IncidentReport
            {
                sessionId = sessionId,
                capturedUtc = DateTime.UtcNow.ToString("O"),
                summary = summary,
                character = RunState.SelectedCharacter.ToString(),
                weapons = BuildWeaponSnapshot(player),
                upgrades = manager != null ? CopyStrings(manager.RunUpgrades) : Array.Empty<string>(),
                relics = manager != null ? CopyStrings(manager.RunRelics) : Array.Empty<string>(),
                playerPositionX = position.x,
                playerPositionY = position.y,
                stageDifficulty = spawner != null ? spawner.CurrentStageDifficulty : 0,
                maxAliveEnemies = spawner != null ? spawner.CurrentMaxAliveEnemies : 0,
                activeEnemies = EnemyController.ActiveEnemies.Count,
                activeProjectiles = CountActive<Projectile>(),
                activeAdvancedAreas = CountActive<AdvancedWeaponArea>(),
                activeDamagePopups = DamagePopup.ActiveCount,
                activeHitFlashes = EnemyHitFlash.ActiveFlashCount,
                activeExperienceOrbs = CountActive<ExperienceOrb>(),
                activeTokenOrbs = CountActive<TokenOrb>(),
                samples = samples.ToArray()
            };
        }

        void WriteSessionReport(bool ended)
        {
            if (string.IsNullOrEmpty(sessionDirectory)) return;
            var manager = GameManager.Instance;
            var spawner = manager != null ? manager.spawner : null;
            var report = new SessionReport
            {
                sessionId = sessionId,
                startedUtc = sessionStartedUtc,
                endedUtc = ended ? DateTime.UtcNow.ToString("O") : string.Empty,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                graphicsDevice = SystemInfo.graphicsDeviceName,
                qualityLevel = ResolveQualityLevel(),
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                stageDifficulty = spawner != null ? spawner.CurrentStageDifficulty : 0,
                maxAliveEnemies = spawner != null ? spawner.CurrentMaxAliveEnemies : 0,
                warmupSeconds = warmupSeconds,
                preCaptureSeconds = preCaptureSeconds,
                postCaptureSeconds = postCaptureSeconds,
                baselineP95Ms = baselineP95Ms,
                normalSentinelAverageMicroseconds = sentinelUpdateSamples > 0
                    ? (float)(sentinelUpdateMicrosecondsTotal / sentinelUpdateSamples)
                    : 0f,
                normalSentinelMaxMicroseconds = (float)sentinelUpdateMicrosecondsMax,
                maxIncidentWriteMilliseconds = (float)maxIncidentWriteMilliseconds,
                normalSentinelSamples = sentinelUpdateSamples,
                incidents = new List<IncidentSummary>(incidentSummaries)
            };
            WriteTextSafely(
                Path.Combine(sessionDirectory, "session.json"),
                JsonUtility.ToJson(report, true));
            WriteTextSafely(
                Path.Combine(sessionDirectory, "session-summary.md"),
                BuildMarkdownSummary(report));
        }

        string BuildMarkdownSummary(SessionReport report)
        {
            var builder = new StringBuilder();
            builder.AppendLine("# Performance Session");
            builder.AppendLine();
            builder.AppendLine($"- Session: `{report.sessionId}`");
            builder.AppendLine($"- Started UTC: `{report.startedUtc}`");
            builder.AppendLine($"- Ended UTC: `{report.endedUtc}`");
            builder.AppendLine($"- Environment: `{report.platform}` / Unity `{report.unityVersion}`");
            builder.AppendLine($"- Resolution: `{report.screenWidth}x{report.screenHeight}`");
            builder.AppendLine(
                $"- Stage difficulty / max alive enemies: `{report.stageDifficulty}` / `{report.maxAliveEnemies}`");
            builder.AppendLine($"- Baseline p95: `{report.baselineP95Ms:0.00} ms`");
            builder.AppendLine($"- Incidents: `{report.incidents.Count}`");
            builder.AppendLine(
                $"- Sentinel normal overhead: avg `{report.normalSentinelAverageMicroseconds:0.00} us`, " +
                $"max `{report.normalSentinelMaxMicroseconds:0.00} us`");
            builder.AppendLine();
            builder.AppendLine("## Incidents");
            builder.AppendLine();
            if (report.incidents.Count == 0)
            {
                builder.AppendLine("- No performance incidents detected.");
                return builder.ToString();
            }
            for (int i = 0; i < report.incidents.Count; i++)
            {
                var incident = report.incidents[i];
                builder.AppendLine(
                    $"- #{incident.incidentIndex}: `{incident.reason}` — Stage {incident.stage}, " +
                    $"difficulty `{incident.stageDifficulty}`, max alive `{incident.maxAliveEnemies}`, " +
                    $"avg `{incident.averageFrameMs:0.00} ms`, p95 `{incident.p95FrameMs:0.00} ms`, " +
                    $"p99 `{incident.p99FrameMs:0.00} ms`, max `{incident.maxFrameMs:0.00} ms`, " +
                    $"enemies `{incident.peakEnemies}`, file `{incident.fileName}`");
            }
            return builder.ToString();
        }

        float CalculateRingPercentile(float percentile)
        {
            if (ringCount <= 0) return 0f;
            for (int i = 0; i < ringCount; i++) percentileScratch[i] = GetRingSample(i).frameMs;
            Array.Sort(percentileScratch, 0, ringCount);
            int index = Mathf.Clamp(Mathf.CeilToInt(ringCount * percentile) - 1, 0, ringCount - 1);
            return percentileScratch[index];
        }

        static float Percentile(float[] sortedValues, float percentile)
        {
            if (sortedValues == null || sortedValues.Length == 0) return 0f;
            int index = Mathf.Clamp(
                Mathf.CeilToInt(sortedValues.Length * percentile) - 1,
                0,
                sortedValues.Length - 1);
            return sortedValues[index];
        }

        static long PositiveDelta(long current, long previous)
        {
            return current >= previous ? current - previous : 0L;
        }

        static string[] BuildWeaponSnapshot(PlayerController player)
        {
            if (player == null || player.weapon == null) return Array.Empty<string>();
            var order = player.weapon.AcquiredWeaponOrder;
            var result = new string[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                var type = order[i];
                result[i] = $"{type}:Lv{player.weapon.GetRunWeaponDisplayLevel(type)}";
            }
            return result;
        }

        static string[] CopyStrings(IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0) return Array.Empty<string>();
            var result = new string[values.Count];
            for (int i = 0; i < values.Count; i++) result[i] = values[i] ?? string.Empty;
            return result;
        }

        static int CountActive<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None).Length;
        }

        static string ResolveSessionRoot()
        {
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TokenReports", "PerformanceSessions"));
#else
            return Path.Combine(Application.persistentDataPath, "PerformanceSessions");
#endif
        }

        static string ResolveQualityLevel()
        {
            int index = QualitySettings.GetQualityLevel();
            var names = QualitySettings.names;
            return names != null && index >= 0 && index < names.Length ? names[index] : index.ToString();
        }

        static void StartRecorder(ref ProfilerRecorder recorder, ProfilerCategory category, string markerName)
        {
            try
            {
                recorder = ProfilerRecorder.StartNew(category, markerName);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"[PerformanceSentinel] Profiler recorder unavailable '{markerName}': {exception.Message}");
            }
        }

        static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid) recorder.Dispose();
            recorder = default;
        }

        static void WriteTextSafely(string path, string contents)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(path, contents ?? string.Empty, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"[PerformanceSentinel] Failed to write '{path}': {exception.Message}");
            }
        }

        static double ElapsedMilliseconds(long startedTicks)
        {
            return (Stopwatch.GetTimestamp() - startedTicks) * 1000.0 / Stopwatch.Frequency;
        }
    }
}
