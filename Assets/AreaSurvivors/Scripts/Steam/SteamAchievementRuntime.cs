using System;
using Steamworks;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class SteamAchievementRuntime : MonoBehaviour
    {
        public const uint AppId = 4980380;

        static SteamAchievementRuntime instance;

        Callback<UserStatsStored_t> userStatsStored;
        Callback<UserAchievementStored_t> achievementStored;
        SteamAchievementService service;
        CGameID gameId;
        bool steamInitialized;
        bool shuttingDown;

        public static void ReportTotalKills(int totalKills)
        {
            if (instance == null || instance.service == null || !instance.service.IsReady) return;
            instance.service.EvaluateAndStore(ProgressionStore.Data, totalKills);
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
            InitializeSteam();
        }

        void InitializeSteam()
        {
#if !UNITY_EDITOR
            if (SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
            {
                Application.Quit();
                return;
            }
#endif
            try
            {
                if (!SteamAPI.Init())
                {
                    Debug.LogWarning("Steam achievements are unavailable because SteamAPI.Init failed.");
                    return;
                }
            }
            catch (DllNotFoundException exception)
            {
                Debug.LogWarning("Steam achievements are unavailable because the Steam native library was not found: " + exception.Message);
                return;
            }

            steamInitialized = true;
            AppId_t actualAppId = SteamUtils.GetAppID();
            if (actualAppId.m_AppId != AppId)
            {
                Debug.LogError($"Steam App ID mismatch. Expected {AppId}, actual {actualAppId.m_AppId}.");
                ShutdownSteam();
                return;
            }

            gameId = new CGameID(actualAppId);
            service = new SteamAchievementService(new SteamworksAchievementBackend());
            userStatsStored = Callback<UserStatsStored_t>.Create(OnUserStatsStored);
            achievementStored = Callback<UserAchievementStored_t>.Create(OnAchievementStored);
            service.InitializeFromSteam();
            service.EvaluateAndStore(ProgressionStore.Data);
        }

        void Update()
        {
            if (steamInitialized) SteamAPI.RunCallbacks();
        }

        void OnUserStatsStored(UserStatsStored_t callback)
        {
            if (callback.m_nGameID != gameId.m_GameID) return;
            if (callback.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning("Steam achievement data could not be stored: " + callback.m_eResult);
            }
        }

        void OnAchievementStored(UserAchievementStored_t callback)
        {
            if (callback.m_nGameID != gameId.m_GameID) return;
            Debug.Log("Steam achievement stored: " + callback.m_rgchAchievementName);
        }

        void OnProgressionSaved()
        {
            if (service == null || !service.IsReady) return;
            service.EvaluateAndStore(ProgressionStore.Data);
        }

        void OnApplicationQuit()
        {
            shuttingDown = true;
            service?.FlushPendingStore();
        }

        void OnDestroy()
        {
            if (instance != this) return;
            ProgressionStore.Saved -= OnProgressionSaved;
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
            if (!shuttingDown) service?.FlushPendingStore();
            SteamAPI.Shutdown();
            steamInitialized = false;
        }

        sealed class SteamworksAchievementBackend : ISteamAchievementBackend
        {
            public bool TryGetAchievement(string apiName, out bool unlocked)
            {
                return SteamUserStats.GetAchievement(apiName, out unlocked);
            }

            public bool SetAchievement(string apiName)
            {
                return SteamUserStats.SetAchievement(apiName);
            }

            public bool StoreStats()
            {
                return SteamUserStats.StoreStats();
            }
        }
    }
}
