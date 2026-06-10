using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public static class ProgressionStore
    {
        const string SaveKey = "AreaSurvivors.Save.v1";
        static SaveData cached;

        public static SaveData Data
        {
            get
            {
                if (cached == null)
                {
                    var json = PlayerPrefs.GetString(SaveKey, string.Empty);
                    cached = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
                    if (cached.upgrades == null) cached.upgrades = new List<UpgradeLevel>();
                }

                return cached;
            }
        }

        public static int GetLevel(UpgradeType type)
        {
            foreach (var upgrade in Data.upgrades)
            {
                if (upgrade.type == type) return upgrade.level;
            }
            return 0;
        }

        public static bool TryBuy(UpgradeType type)
        {
            int level = GetLevel(type);
            if (level >= GetMaxLevel(type)) return false;
            int cost = GetCost(type, level);
            if (Data.tokens < cost) return false;

            Data.tokens -= cost;
            SetLevel(type, level + 1);
            Save();
            return true;
        }

        public static int GetCost(UpgradeType type, int level)
        {
            return BaseCost(type) + level * 3;
        }

        public static int GetMaxLevel(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.UnlockBallista:
                case UpgradeType.UnlockWatchTower:
                case UpgradeType.UnlockLargeWorkshop:
                case UpgradeType.UnlockTowerCannon:
                case UpgradeType.UnlockTowerUpgrade:
                case UpgradeType.UnlockDefenseCharacter:
                case UpgradeType.UnlockCarpenterHut:
                case UpgradeType.UnlockAutoBuild:
                case UpgradeType.UnlockWorkerHut:
                case UpgradeType.UnlockClassChange:
                    return 1;
                default:
                    return 10;
            }
        }

        public static bool IsUnlocked(UpgradeType type)
        {
            return GetLevel(type) > 0;
        }

        static int BaseCost(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.UnlockBallista: return 3;
                case UpgradeType.UnlockWatchTower:
                case UpgradeType.UnlockLargeWorkshop:
                case UpgradeType.UnlockTowerCannon:
                case UpgradeType.UnlockTowerUpgrade:
                case UpgradeType.UnlockDefenseCharacter:
                case UpgradeType.UnlockCarpenterHut:
                case UpgradeType.UnlockAutoBuild:
                case UpgradeType.UnlockWorkerHut:
                case UpgradeType.UnlockClassChange:
                    return 8;
                case UpgradeType.TowerMaxHp:
                case UpgradeType.TowerAutoRegen:
                case UpgradeType.EndTokenGain:
                case UpgradeType.EliteSpawnRate:
                    return 6;
                default:
                    return 4;
            }
        }

        public static void AddRunRewards(int kills, int divisor)
        {
            int gained = Mathf.Max(0, kills / Mathf.Max(1, divisor));
            AddRunTokens(kills, gained);
        }

        public static void AddRunTokens(int kills, int tokens)
        {
            Data.tokens += Mathf.Max(0, tokens);
            Data.totalKills += kills;
            Save();
        }

        public static void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        static void SetLevel(UpgradeType type, int level)
        {
            foreach (var upgrade in Data.upgrades)
            {
                if (upgrade.type == type)
                {
                    upgrade.level = level;
                    return;
                }
            }

            Data.upgrades.Add(new UpgradeLevel { type = type, level = level });
        }
    }
}
