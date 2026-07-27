using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public interface ISteamAchievementBackend
    {
        bool TryGetAchievement(string apiName, out bool unlocked);
        bool SetAchievement(string apiName);
        bool StoreStats();
    }

    public sealed class SteamAchievementService
    {
        readonly ISteamAchievementBackend backend;
        readonly HashSet<string> unlocked = new HashSet<string>();
        bool ready;
        bool pendingStore;

        public SteamAchievementService(ISteamAchievementBackend backend)
        {
            this.backend = backend;
        }

        public bool IsReady => ready;

        public bool InitializeFromSteam(bool logReadFailures = true)
        {
            ready = false;
            unlocked.Clear();
            bool allAchievementsRead = true;
            foreach (var definition in SteamAchievementCatalog.Definitions)
            {
                if (!backend.TryGetAchievement(definition.ApiName, out bool isUnlocked))
                {
                    if (logReadFailures)
                    {
                        Debug.LogWarning("Steam achievement could not be read: " + definition.ApiName);
                    }
                    allAchievementsRead = false;
                    continue;
                }

                if (isUnlocked) unlocked.Add(definition.ApiName);
            }

            ready = allAchievementsRead;
            return ready;
        }

        public int EvaluateAndStore(SaveData data, int effectiveTotalKills = -1)
        {
            if (!ready || data == null) return 0;
            var snapshot = new SteamAchievementSnapshot(data, effectiveTotalKills);
            int newlyUnlocked = 0;

            foreach (var definition in SteamAchievementCatalog.Definitions)
            {
                if (unlocked.Contains(definition.ApiName) || !definition.IsUnlocked(snapshot)) continue;
                if (!backend.SetAchievement(definition.ApiName))
                {
                    Debug.LogWarning("Steam achievement could not be unlocked: " + definition.ApiName);
                    continue;
                }

                unlocked.Add(definition.ApiName);
                pendingStore = true;
                newlyUnlocked++;
            }

            FlushPendingStore();
            return newlyUnlocked;
        }

        public bool FlushPendingStore()
        {
            if (!ready || !pendingStore) return true;
            if (!backend.StoreStats()) return false;
            pendingStore = false;
            return true;
        }
    }
}
