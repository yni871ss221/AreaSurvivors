using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public static class ProgressionStore
    {
        const string SaveKey = "AreaSurvivors.Save.v1";
        const int ImplementedStageCount = 4;
        public const int MinStageDifficulty = 1;
        public const int MaxStageDifficulty = 5;
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
                    if (cached.relics == null) cached.relics = new List<RelicRecord>();
                    if (cached.stageDifficulties == null) cached.stageDifficulties = new List<StageDifficultyRecord>();
                    if (cached.stageBuildings == null) cached.stageBuildings = new List<StageBuildingSet>();
                    if (cached.highestUnlockedStage < 1) cached.highestUnlockedStage = 1;
                    if (cached.selectedStage < 1) cached.selectedStage = 1;
                }

                return cached;
            }
        }

        public static bool HasRelic(RelicType type)
        {
            if (type == RelicType.None) return false;
            if (Data.relics == null) Data.relics = new List<RelicRecord>();
            foreach (var relic in Data.relics)
            {
                if (relic != null && relic.type == type) return true;
            }

            return false;
        }

        public static bool UnlockRelic(RelicType type)
        {
            if (type == RelicType.None || HasRelic(type)) return false;
            if (Data.relics == null) Data.relics = new List<RelicRecord>();
            Data.relics.Add(new RelicRecord { type = type });
            Save();
            return true;
        }

        public static bool ToggleRelicForTesting(RelicType type, out bool isOwned)
        {
            isOwned = false;
            if (type == RelicType.None) return false;

            if (HasRelic(type))
            {
                bool locked = LockRelicForTesting(type);
                isOwned = false;
                return locked;
            }

            bool unlocked = UnlockRelic(type);
            isOwned = unlocked;
            return unlocked;
        }

        public static bool LockRelicForTesting(RelicType type)
        {
            if (type == RelicType.None) return false;
            if (Data.relics == null) Data.relics = new List<RelicRecord>();
            bool removed = false;
            for (int i = Data.relics.Count - 1; i >= 0; i--)
            {
                var relic = Data.relics[i];
                if (relic == null || relic.type != type) continue;
                Data.relics.RemoveAt(i);
                removed = true;
            }

            if (!removed) return false;
            Save();
            return true;
        }

        public static void ResetRelicsForTesting()
        {
            if (Data.relics == null) Data.relics = new List<RelicRecord>();
            Data.relics.Clear();
            Save();
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
            int overrideCost = FixedCostOverride(type, level);
            if (overrideCost > 0) return overrideCost;

            int depth = UpgradeDepth(type);
            int safeLevel = Mathf.Max(0, level);
            float rawCost = 5f + 3.5f * (
                Mathf.Pow(depth, 1.7f)
                + Mathf.Pow(safeLevel, 1.35f)
                + depth * safeLevel * 0.32f);
            return Mathf.Max(5, Mathf.RoundToInt(rawCost / 5f) * 5);
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
                case UpgradeType.ReviveBuildingsOnBossDefeat:
                case UpgradeType.UnlockOpeningRelicChest:
                    return 1;
                case UpgradeType.StartingWeaponLevel:
                case UpgradeType.EliteSpawnCount:
                case UpgradeType.WatchTowerDamage:
                    return 4;
                case UpgradeType.PaintAreaTokenGain:
                    return 3;
                case UpgradeType.ReviveSpeed:
                case UpgradeType.MoveSpeed:
                case UpgradeType.PaintRadius:
                case UpgradeType.MovePenaltyReduction:
                case UpgradeType.WallMaxHp1:
                case UpgradeType.WallMaxHp2:
                case UpgradeType.WallMaxHp3:
                case UpgradeType.MoveSpeedAdvanced:
                case UpgradeType.PaintRadiusAdvanced:
                    return 5;
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
                case UpgradeType.RetiredWatchTowerMaxHp:
                    return true;
                default:
                    return false;
            }
        }

        static int FixedCostOverride(UpgradeType type, int level)
        {
            switch (type)
            {
                case UpgradeType.ReviveSpeed:
                    return FixedLevelCost(level, 30, 35, 45, 55, 65);
                case UpgradeType.MovePenaltyReduction:
                    return FixedLevelCost(level, 40, 50, 60, 70, 85);
                case UpgradeType.WallMaxHp1:
                    return FixedLevelCost(level, 10, 15, 20, 25, 30);
                case UpgradeType.WallMaxHp2:
                    return FixedLevelCost(level, 20, 25, 30, 35, 40);
                case UpgradeType.WallMaxHp3:
                    return FixedLevelCost(level, 30, 35, 40, 45, 50);
            }

            if (level != 0) return 0;
            switch (type)
            {
                case UpgradeType.UnlockShield:
                    return 50;
                case UpgradeType.UnlockTowerUpgrade:
                    return 100;
                case UpgradeType.ReviveBuildingsOnBossDefeat:
                    return 60;
                case UpgradeType.UnlockFlag:
                    return 100;
                case UpgradeType.UnlockOpeningRelicChest:
                    return 250;
                default:
                    return 0;
            }
        }

        static int FixedLevelCost(int level, params int[] costs)
        {
            if (level < 0 || costs == null || level >= costs.Length) return 0;
            return Mathf.Max(1, costs[level]);
        }

        static int UpgradeDepth(UpgradeType type)
        {
            switch (type)
            {
                case UpgradeType.MaxHp:
                case UpgradeType.TowerMaxHp:
                case UpgradeType.UnlockWall:
                    return 0;
                case UpgradeType.Defense:
                case UpgradeType.MoveSpeed:
                case UpgradeType.UnlockBallista:
                case UpgradeType.UnlockWatchTower:
                case UpgradeType.WallMaxHp1:
                case UpgradeType.TowerAutoRegen:
                case UpgradeType.EndTokenGain:
                    return 1;
                case UpgradeType.AutoRegen:
                case UpgradeType.PaintRadius:
                case UpgradeType.BallistaRange:
                case UpgradeType.UnlockTowerCannon:
                case UpgradeType.EliteSpawnCount:
                case UpgradeType.WallMaxHp2:
                case UpgradeType.WatchTowerRange:
                case UpgradeType.UnlockArrow:
                case UpgradeType.UnlockFireball:
                    return 2;
                case UpgradeType.ReviveSpeed:
                case UpgradeType.XpGain:
                case UpgradeType.WallMaxHp3:
                case UpgradeType.BallistaDamage:
                case UpgradeType.UnlockShield:
                case UpgradeType.WatchTowerDamage:
                    return 3;
                case UpgradeType.BuildingAutoRegen:
                case UpgradeType.MovePenaltyReduction:
                case UpgradeType.ReviveBuildingsOnBossDefeat:
                case UpgradeType.PaintAreaTokenGain:
                    return 4;
                case UpgradeType.UnlockTowerUpgrade:
                case UpgradeType.WallUpgrade:
                case UpgradeType.BallistaUpgrade:
                case UpgradeType.WatchTowerUpgrade:
                case UpgradeType.UnlockArrowRain:
                case UpgradeType.UnlockFrost:
                case UpgradeType.UnlockFlag:
                case UpgradeType.UnlockBoomerangSword:
                    return 5;
                case UpgradeType.UnlockWall2:
                case UpgradeType.RemoveStartingSlash:
                case UpgradeType.UnlockOpeningRelicChest:
                case UpgradeType.MoveSpeedAdvanced:
                case UpgradeType.PaintRadiusAdvanced:
                    return 6;
                case UpgradeType.Wall2Upgrade:
                case UpgradeType.UnlockGun:
                case UpgradeType.UnlockThunderBall:
                case UpgradeType.UnlockAuraSword:
                    return 7;
                default:
                    return 0;
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

        public static void AddTokens(int tokens)
        {
            Data.tokens += Mathf.Max(0, tokens);
            Save();
        }

        public static void IncrementPlayCount()
        {
            Data.playCount = Mathf.Max(0, Data.playCount) + 1;
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

        public static bool SetStageClearedForTesting(int stage, bool cleared)
        {
            stage = Mathf.Clamp(stage, 1, ImplementedStageCount);
            if (cleared)
            {
                Data.highestClearedStage = Mathf.Max(Data.highestClearedStage, stage);
                Data.highestUnlockedStage = Mathf.Max(Data.highestUnlockedStage, Mathf.Min(stage + 1, ImplementedStageCount));
            }
            else
            {
                Data.highestClearedStage = Mathf.Min(Data.highestClearedStage, stage - 1);
                Data.highestUnlockedStage = Mathf.Clamp(Data.highestUnlockedStage, 1, stage);
                Data.selectedStage = Mathf.Clamp(Data.selectedStage, 1, Data.highestUnlockedStage);
            }

            Save();
            return IsStageCleared(stage);
        }

        public static bool ToggleStageClearedForTesting(int stage)
        {
            return SetStageClearedForTesting(stage, !IsStageCleared(stage));
        }

        public static int GetStageDifficulty(int stage)
        {
            stage = Mathf.Clamp(stage, 1, ImplementedStageCount);
            if (Data.stageDifficulties == null) Data.stageDifficulties = new List<StageDifficultyRecord>();
            foreach (var record in Data.stageDifficulties)
            {
                if (record != null && record.stage == stage)
                {
                    return Mathf.Clamp(record.difficulty, MinStageDifficulty, MaxStageDifficulty);
                }
            }

            return MinStageDifficulty;
        }

        public static void SetStageDifficulty(int stage, int difficulty)
        {
            stage = Mathf.Clamp(stage, 1, ImplementedStageCount);
            difficulty = Mathf.Clamp(difficulty, MinStageDifficulty, MaxStageDifficulty);
            if (Data.stageDifficulties == null) Data.stageDifficulties = new List<StageDifficultyRecord>();
            foreach (var record in Data.stageDifficulties)
            {
                if (record == null || record.stage != stage) continue;
                record.difficulty = difficulty;
                Save();
                return;
            }

            Data.stageDifficulties.Add(new StageDifficultyRecord { stage = stage, difficulty = difficulty });
            Save();
        }

        public static void AddTokensForTesting(int tokens)
        {
            AddTokens(tokens);
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

        public static void ResetStageClearStateForTesting()
        {
            Data.highestClearedStage = 0;
            Data.highestUnlockedStage = 1;
            Data.selectedStage = 1;
            if (Data.stageDifficulties != null) Data.stageDifficulties.Clear();
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
