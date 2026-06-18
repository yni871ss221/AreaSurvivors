using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public static class ProgressionStore
    {
        const string SaveKey = "AreaSurvivors.Save.v1";
        const int ImplementedStageCount = 2;
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
                    if (cached.stageSpeedSettings == null) cached.stageSpeedSettings = new List<StageSpeedSetting>();
                    if (cached.highestUnlockedStage < 1) cached.highestUnlockedStage = 1;
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
                case UpgradeType.AutoResourceInterval:
                case UpgradeType.AutoResourceGain:
                    return 2;
                case UpgradeType.StartingWeaponLevel:
                    return 4;
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
                case UpgradeType.StartingWeaponLevel:
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

        public static bool IsStageUnlocked(int stage)
        {
            return stage >= 1 && Data.highestUnlockedStage >= stage;
        }

        public static bool IsStageCleared(int stage)
        {
            return stage >= 1 && Data.highestClearedStage >= stage;
        }

        public static bool MarkStageCleared(int stage)
        {
            if (stage < 1) return false;
            int nextUnlockedStage = Mathf.Min(stage + 1, ImplementedStageCount);
            bool unlockedNewStage = Data.highestUnlockedStage < nextUnlockedStage;
            Data.highestClearedStage = Mathf.Max(Data.highestClearedStage, stage);
            Data.highestUnlockedStage = Mathf.Max(Data.highestUnlockedStage, nextUnlockedStage);
            Save();
            return unlockedNewStage;
        }

        public static bool IsFastStage(int stage)
        {
            foreach (var setting in Data.stageSpeedSettings)
            {
                if (setting != null && setting.stage == stage) return setting.fastMode && IsStageCleared(stage);
            }
            return false;
        }

        public static void SetFastStage(int stage, bool fastMode)
        {
            if (stage < 1 || !IsStageCleared(stage)) return;
            foreach (var setting in Data.stageSpeedSettings)
            {
                if (setting != null && setting.stage == stage)
                {
                    setting.fastMode = fastMode;
                    Save();
                    return;
                }
            }

            Data.stageSpeedSettings.Add(new StageSpeedSetting { stage = stage, fastMode = fastMode });
            Save();
        }

        public static void AddTokensForTesting(int tokens)
        {
            Data.tokens += Mathf.Max(0, tokens);
            Save();
        }

        public static void ResetUpgradesForTesting()
        {
            if (Data.upgrades == null) Data.upgrades = new List<UpgradeLevel>();
            Data.upgrades.Clear();
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
