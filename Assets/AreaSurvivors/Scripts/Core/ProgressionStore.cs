using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public static class ProgressionStore
    {
        const string SaveKey = "AreaSurvivors.Save.v1";
        const int ImplementedStageCount = 4;
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
                    if (cached.stageBuildings == null) cached.stageBuildings = new List<StageBuildingSet>();
                    if (cached.highestUnlockedStage < 1) cached.highestUnlockedStage = 1;
                    if (cached.selectedStage < 1) cached.selectedStage = 1;
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
            if (IsRetiredUpgrade(type)) return false;
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
            if (IsRetiredUpgrade(type)) return 0;
            switch (type)
            {
                case UpgradeType.UnlockBallista:
                case UpgradeType.UnlockWatchTower:
                case UpgradeType.UnlockWall:
                case UpgradeType.UnlockLargeWorkshop:
                case UpgradeType.UnlockTowerCannon:
                case UpgradeType.UnlockTowerUpgrade:
                case UpgradeType.WallMaxHp1:
                case UpgradeType.WallMaxHp2:
                case UpgradeType.WallMaxHp3:
                case UpgradeType.WallUpgrade:
                case UpgradeType.UnlockWall2:
                case UpgradeType.Wall2Upgrade:
                case UpgradeType.BallistaUpgrade:
                case UpgradeType.WatchTowerUpgrade:
                case UpgradeType.UnlockArrow:
                case UpgradeType.UnlockFireball:
                case UpgradeType.UnlockShield:
                case UpgradeType.UnlockArrowRain:
                case UpgradeType.UnlockGun:
                case UpgradeType.UnlockFrost:
                case UpgradeType.UnlockThunderBall:
                case UpgradeType.UnlockFlag:
                case UpgradeType.UnlockBoomerangSword:
                case UpgradeType.UnlockAuraSword:
                case UpgradeType.RemoveStartingSlash:
                    return 1;
                case UpgradeType.StartingWeaponLevel:
                case UpgradeType.EliteSpawnCount:
                    return 4;
                default:
                    return 10;
            }
        }

        public static bool IsUnlocked(UpgradeType type)
        {
            return GetLevel(type) > 0;
        }

        public static bool IsRetiredUpgrade(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.UnlockCarpenterHut:
                case UpgradeType.UnlockAutoBuild:
                case UpgradeType.AutoBuildSpeed:
                case UpgradeType.UnlockWorkerHut:
                case UpgradeType.AutoResourceInterval:
                case UpgradeType.AutoResourceGain:
                case UpgradeType.UnlockDefenseCharacter:
                case UpgradeType.UnlockClassChange:
                case UpgradeType.RoundTimeLimit:
                    return true;
                default:
                    return false;
            }
        }

        static int BaseCost(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.UnlockWall: return 2;
                case UpgradeType.UnlockBallista: return 3;
                case UpgradeType.UnlockWatchTower:
                case UpgradeType.UnlockLargeWorkshop:
                case UpgradeType.UnlockTowerCannon:
                case UpgradeType.UnlockTowerUpgrade:
                case UpgradeType.WallUpgrade:
                case UpgradeType.UnlockWall2:
                case UpgradeType.Wall2Upgrade:
                case UpgradeType.BallistaUpgrade:
                case UpgradeType.WatchTowerUpgrade:
                    return 8;
                case UpgradeType.TowerMaxHp:
                case UpgradeType.WallMaxHp1:
                case UpgradeType.WallMaxHp2:
                case UpgradeType.WallMaxHp3:
                case UpgradeType.WatchTowerMaxHp:
                case UpgradeType.TowerAutoRegen:
                case UpgradeType.BuildingAutoRegen:
                case UpgradeType.EndTokenGain:
                case UpgradeType.EliteSpawnCount:
                case UpgradeType.StartingWeaponLevel:
                case UpgradeType.UnlockArrow:
                case UpgradeType.UnlockFireball:
                case UpgradeType.UnlockShield:
                case UpgradeType.UnlockArrowRain:
                case UpgradeType.UnlockGun:
                case UpgradeType.UnlockFrost:
                case UpgradeType.UnlockThunderBall:
                case UpgradeType.UnlockFlag:
                case UpgradeType.UnlockBoomerangSword:
                case UpgradeType.UnlockAuraSword:
                case UpgradeType.RemoveStartingSlash:
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

        public static int SelectedStage
        {
            get => Mathf.Clamp(Data.selectedStage <= 0 ? 1 : Data.selectedStage, 1, ImplementedStageCount);
            set
            {
                Data.selectedStage = Mathf.Clamp(value, 1, ImplementedStageCount);
                Save();
            }
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

        public static bool HasPersistentResources(int wood, int stone)
        {
            return Data.wood >= Mathf.Max(0, wood) && Data.stone >= Mathf.Max(0, stone);
        }

        public static bool TrySpendPersistentResources(int wood, int stone)
        {
            wood = Mathf.Max(0, wood);
            stone = Mathf.Max(0, stone);
            if (!HasPersistentResources(wood, stone)) return false;
            Data.wood -= wood;
            Data.stone -= stone;
            Save();
            return true;
        }

        public static void AddPersistentResources(int wood, int stone)
        {
            Data.wood += Mathf.Max(0, wood);
            Data.stone += Mathf.Max(0, stone);
            Save();
        }

        public static StageBuildingSet GetStageBuildings(int stage)
        {
            stage = Mathf.Max(1, stage);
            if (Data.stageBuildings == null) Data.stageBuildings = new List<StageBuildingSet>();
            foreach (var set in Data.stageBuildings)
            {
                if (set == null || set.stage != stage) continue;
                if (set.buildings == null) set.buildings = new List<SavedBuildingData>();
                return set;
            }

            var created = new StageBuildingSet { stage = stage, buildings = new List<SavedBuildingData>() };
            Data.stageBuildings.Add(created);
            return created;
        }

        public static void ReplaceStageBuildings(int stage, List<SavedBuildingData> buildings)
        {
            var set = GetStageBuildings(stage);
            set.buildings = buildings ?? new List<SavedBuildingData>();
            Save();
        }

        public static void ReviveStageBuildings(int stage)
        {
            var set = GetStageBuildings(stage);
            foreach (var building in set.buildings)
            {
                if (building != null) building.destroyed = false;
            }
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
