using System;
using System.IO;
using System.Text;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors
{
    public sealed class SteamAchievementRuntime : MonoBehaviour
    {
        public const uint AppId = 4980380;
        const float StatsInitializationRetryIntervalSeconds = 0.5f;
        const float StatsInitializationWarningDelaySeconds = 10f;
        const string DiagnosticDirectoryName = "SteamAchievementReports";

        static SteamAchievementRuntime instance;
        static readonly Encoding DiagnosticEncoding = new UTF8Encoding(false);

        Callback<UserStatsStored_t> userStatsStored;
        Callback<UserAchievementStored_t> achievementStored;
        SteamAchievementService service;
        CGameID gameId;
        bool steamInitialized;
        bool shuttingDown;
        bool statsInitializationWarningLogged;
        float statsInitializationStartedTime;
        float nextStatsInitializationAttemptTime;
        string diagnosticSessionId;
        string diagnosticPath;
        string currentEvaluationTrigger = string.Empty;
        int currentEffectiveTotalKills = -1;
        bool diagnosticsDisabled;
        bool diagnosticsFailureLogged;

        public static void ReportTotalKills(int totalKills)
        {
            if (instance == null || instance.service == null || !instance.service.IsReady) return;
            instance.EvaluateAndStore("total_kills", totalKills);
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning("Duplicate SteamAchievementRuntime was disabled.");
                enabled = false;
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            ProgressionStore.Saved += OnProgressionSaved;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            InitializeDiagnostics();
            WriteDiagnostic(
                "runtime_awake",
                string.Empty,
                "appVersion=" + Application.version + ";unityVersion=" + Application.unityVersion);
            InitializeSteam();
        }

        void InitializeSteam()
        {
#if !UNITY_EDITOR
            if (SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
            {
                WriteDiagnostic("steam_restart_required", string.Empty, "true");
                Application.Quit();
                return;
            }
#endif
            try
            {
                if (!SteamAPI.Init())
                {
                    WriteDiagnostic("steam_init", string.Empty, "failed");
                    Debug.LogWarning("Steam achievements are unavailable because SteamAPI.Init failed.");
                    return;
                }
            }
            catch (DllNotFoundException exception)
            {
                WriteDiagnostic("steam_init", string.Empty, "native_library_missing:" + exception.Message);
                Debug.LogWarning("Steam achievements are unavailable because the Steam native library was not found: " + exception.Message);
                return;
            }

            steamInitialized = true;
            AppId_t actualAppId = SteamUtils.GetAppID();
            if (actualAppId.m_AppId != AppId)
            {
                WriteDiagnostic("steam_init", string.Empty, "app_id_mismatch:" + actualAppId.m_AppId);
                Debug.LogError($"Steam App ID mismatch. Expected {AppId}, actual {actualAppId.m_AppId}.");
                ShutdownSteam();
                return;
            }

            WriteDiagnostic("steam_init", string.Empty, "success");
            gameId = new CGameID(actualAppId);
            service = new SteamAchievementService(new SteamworksAchievementBackend(
                OnSetAchievementAttempted,
                OnStoreStatsAttempted));
            userStatsStored = Callback<UserStatsStored_t>.Create(OnUserStatsStored);
            achievementStored = Callback<UserAchievementStored_t>.Create(OnAchievementStored);
            statsInitializationStartedTime = Time.realtimeSinceStartup;
            TryInitializeAchievements();
        }

        void Update()
        {
            if (!steamInitialized) return;
            SteamAPI.RunCallbacks();

            if (service == null || service.IsReady || Time.realtimeSinceStartup < nextStatsInitializationAttemptTime) return;
            TryInitializeAchievements();
        }

        void TryInitializeAchievements()
        {
            if (service == null || service.IsReady) return;
            if (service.InitializeFromSteam(logReadFailures: false))
            {
                WriteDiagnostic("stats_initialization", string.Empty, "ready", true);
                EvaluateAndStore("initial_sync");
                return;
            }

            nextStatsInitializationAttemptTime = Time.realtimeSinceStartup + StatsInitializationRetryIntervalSeconds;
            if (statsInitializationWarningLogged ||
                Time.realtimeSinceStartup - statsInitializationStartedTime < StatsInitializationWarningDelaySeconds) return;

            statsInitializationWarningLogged = true;
            WriteDiagnostic("stats_initialization", string.Empty, "waiting");
            Debug.LogWarning("Steam achievement data is still unavailable. Initialization will continue retrying in the background.");
        }

        void OnUserStatsStored(UserStatsStored_t callback)
        {
            if (callback.m_nGameID != gameId.m_GameID) return;
            WriteDiagnostic(
                "user_stats_stored_callback",
                string.Empty,
                callback.m_eResult.ToString(),
                true);
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning("Steam achievement data could not be stored: " + callback.m_eResult);
            }
        }

        void OnAchievementStored(UserAchievementStored_t callback)
        {
            if (callback.m_nGameID != gameId.m_GameID) return;
            WriteDiagnostic(
                "achievement_stored_callback",
                callback.m_rgchAchievementName,
                "received",
                true);
            Debug.Log("Steam achievement stored: " + callback.m_rgchAchievementName);
        }

        void OnProgressionSaved()
        {
            if (service == null || !service.IsReady) return;
            EvaluateAndStore("progression_saved");
        }

        void OnApplicationQuit()
        {
            shuttingDown = true;
            FlushPendingStore("application_quit");
        }

        void OnDestroy()
        {
            if (instance != this) return;
            ProgressionStore.Saved -= OnProgressionSaved;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            WriteDiagnostic("runtime_destroyed", string.Empty, "received");
            ShutdownSteam();
            instance = null;
        }

        void ShutdownSteam()
        {
            userStatsStored?.Dispose();
            achievementStored?.Dispose();
            userStatsStored = null;
            achievementStored = null;
            if (!steamInitialized) return;
            if (!shuttingDown) FlushPendingStore("runtime_shutdown");
            WriteDiagnostic("steam_shutdown", string.Empty, "started");
            SteamAPI.Shutdown();
            steamInitialized = false;
        }

        int EvaluateAndStore(string trigger, int effectiveTotalKills = -1)
        {
            if (service == null || !service.IsReady) return 0;

            string previousTrigger = currentEvaluationTrigger;
            int previousEffectiveTotalKills = currentEffectiveTotalKills;
            currentEvaluationTrigger = trigger ?? string.Empty;
            currentEffectiveTotalKills = effectiveTotalKills;
            try
            {
                int newlyUnlocked = service.EvaluateAndStore(ProgressionStore.Data, effectiveTotalKills);
                if (newlyUnlocked > 0)
                {
                    WriteDiagnostic(
                        "evaluation_completed",
                        string.Empty,
                        "newlyUnlocked=" + newlyUnlocked,
                        true);
                }
                return newlyUnlocked;
            }
            finally
            {
                currentEvaluationTrigger = previousTrigger;
                currentEffectiveTotalKills = previousEffectiveTotalKills;
            }
        }

        bool FlushPendingStore(string trigger)
        {
            if (service == null) return true;

            string previousTrigger = currentEvaluationTrigger;
            currentEvaluationTrigger = trigger ?? string.Empty;
            try
            {
                return service.FlushPendingStore();
            }
            finally
            {
                currentEvaluationTrigger = previousTrigger;
            }
        }

        void OnSetAchievementAttempted(string apiName, bool succeeded)
        {
            WriteDiagnostic(
                "set_achievement",
                apiName,
                succeeded ? "success" : "failed",
                true);
        }

        void OnStoreStatsAttempted(bool accepted)
        {
            WriteDiagnostic(
                "store_stats_requested",
                string.Empty,
                accepted ? "accepted" : "rejected",
                true);
        }

        void OnActiveSceneChanged(Scene previous, Scene current)
        {
            WriteDiagnostic(
                "scene_changed",
                string.Empty,
                "previous=" + previous.name + ";current=" + current.name);
        }

        void InitializeDiagnostics()
        {
            try
            {
                diagnosticSessionId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") +
                                      "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                string directory = Path.Combine(Application.persistentDataPath, DiagnosticDirectoryName);
                Directory.CreateDirectory(directory);
                diagnosticPath = Path.Combine(
                    directory,
                    "steam-achievement-session-" + diagnosticSessionId + ".jsonl");
                File.WriteAllText(diagnosticPath, string.Empty, DiagnosticEncoding);
                Debug.Log("[SteamAchievementDiagnostics] Session log: " + diagnosticPath);
            }
            catch (Exception exception)
            {
                DisableDiagnostics(exception);
            }
        }

        void WriteDiagnostic(
            string phase,
            string apiName,
            string result,
            bool includeProgressionSnapshot = false)
        {
            if (diagnosticsDisabled || string.IsNullOrEmpty(diagnosticPath)) return;

            try
            {
                int savedTotalKills = -1;
                int playCount = -1;
                int highestClearedStage = -1;
                if (includeProgressionSnapshot)
                {
                    SaveData data = ProgressionStore.Data;
                    if (data != null)
                    {
                        savedTotalKills = data.totalKills;
                        playCount = data.playCount;
                        highestClearedStage = data.highestClearedStage;
                    }
                }

                var entry = new SteamAchievementDiagnosticEntry
                {
                    utc = DateTime.UtcNow.ToString("O"),
                    sessionId = diagnosticSessionId,
                    phase = phase ?? string.Empty,
                    trigger = currentEvaluationTrigger ?? string.Empty,
                    apiName = apiName ?? string.Empty,
                    result = result ?? string.Empty,
                    scene = SceneManager.GetActiveScene().name,
                    frame = Time.frameCount,
                    realtimeSeconds = Time.realtimeSinceStartup,
                    effectiveTotalKills = currentEffectiveTotalKills,
                    savedTotalKills = savedTotalKills,
                    playCount = playCount,
                    highestClearedStage = highestClearedStage
                };
                File.AppendAllText(
                    diagnosticPath,
                    JsonUtility.ToJson(entry) + Environment.NewLine,
                    DiagnosticEncoding);
            }
            catch (Exception exception)
            {
                DisableDiagnostics(exception);
            }
        }

        void DisableDiagnostics(Exception exception)
        {
            diagnosticsDisabled = true;
            if (diagnosticsFailureLogged) return;
            diagnosticsFailureLogged = true;
            Debug.LogWarning("Steam achievement diagnostics were disabled: " + exception.Message);
        }

        [Serializable]
        sealed class SteamAchievementDiagnosticEntry
        {
            public string utc;
            public string sessionId;
            public string phase;
            public string trigger;
            public string apiName;
            public string result;
            public string scene;
            public int frame;
            public float realtimeSeconds;
            public int effectiveTotalKills;
            public int savedTotalKills;
            public int playCount;
            public int highestClearedStage;
        }

        sealed class SteamworksAchievementBackend : ISteamAchievementBackend
        {
            readonly Action<string, bool> setAchievementAttempted;
            readonly Action<bool> storeStatsAttempted;

            public SteamworksAchievementBackend(
                Action<string, bool> setAchievementAttempted,
                Action<bool> storeStatsAttempted)
            {
                this.setAchievementAttempted = setAchievementAttempted;
                this.storeStatsAttempted = storeStatsAttempted;
            }

            public bool TryGetAchievement(string apiName, out bool unlocked)
            {
                return SteamUserStats.GetAchievement(apiName, out unlocked);
            }

            public bool SetAchievement(string apiName)
            {
                bool succeeded = SteamUserStats.SetAchievement(apiName);
                setAchievementAttempted?.Invoke(apiName, succeeded);
                return succeeded;
            }

            public bool StoreStats()
            {
                bool accepted = SteamUserStats.StoreStats();
                storeStatsAttempted?.Invoke(accepted);
                return accepted;
            }
        }
    }
}
