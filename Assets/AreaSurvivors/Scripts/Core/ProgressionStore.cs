using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AreaSurvivors
{
    public static class ProgressionStore
    {
        public const int BaseLevelUpRerollCount = 3;

        const string LegacySaveKey = "AreaSurvivors.Save.v1";
        public const int ImplementedStageCount = 4;
        public const string CloudSaveFileName = "progression-save-v1.json";
        public const string CloudSaveBackupFileName = "progression-save-v1.backup.json";
        public const string CloudSaveTempFileName = "progression-save-v1.tmp";
        public const int MinStageDifficulty = 1;
        public const int MaxStageDifficulty = 5;
        static SaveData cached;

        public static event Action Saved;

        public static string CloudSavePath => Path.Combine(Application.persistentDataPath, CloudSaveFileName);
        public static string CloudSaveBackupPath => Path.Combine(Application.persistentDataPath, CloudSaveBackupFileName);
        public static string CloudSaveTempPath => Path.Combine(Application.persistentDataPath, CloudSaveTempFileName);

        public static SaveData Data
        {
            get
            {
                if (cached == null)
                {
                    cached = LoadData();
                }

                return cached;
            }
        }

        static SaveData LoadData()
        {
            if (ProgressionFileStorage.TryRead(CloudSavePath, out SaveData cloudData, out string cloudError))
            {
                return Normalize(cloudData);
            }
            if (!string.IsNullOrEmpty(cloudError))
            {
                Debug.LogWarning("Progression cloud save could not be read: " + cloudError);
            }

            if (ProgressionFileStorage.TryRead(CloudSaveBackupPath, out SaveData backupData, out string backupError))
            {
                Debug.LogWarning("Progression cloud save was recovered from the local backup.");
                return Normalize(backupData);
            }
            if (!string.IsNullOrEmpty(backupError))
            {
                Debug.LogWarning("Progression cloud save backup could not be read: " + backupError);
            }

            string legacyJson = PlayerPrefs.GetString(LegacySaveKey, string.Empty);
            if (!string.IsNullOrEmpty(legacyJson))
            {
                try
                {
                    SaveData legacyData = JsonUtility.FromJson<SaveData>(legacyJson);
                    if (legacyData == null)
                    {
                        throw new System.InvalidOperationException("Legacy progression JSON did not contain save data.");
                    }
                    legacyData.endingCreditsViewedWasSerialized =
                        legacyJson.IndexOf("\"endingCreditsViewed\"", StringComparison.Ordinal) >= 0;
                    legacyData = Normalize(legacyData);
                    if (TryWriteCloudSave(legacyData))
                    {
                        PlayerPrefs.DeleteKey(LegacySaveKey);
                        PlayerPrefs.Save();
                    }
                    return legacyData;
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning("Legacy progression save could not be migrated: " + exception.Message);
                }
            }

            return Normalize(new SaveData());
        }

        static SaveData Normalize(SaveData data)
        {
            if (data == null) data = new SaveData();
            if (data.upgrades == null) data.upgrades = new List<UpgradeLevel>();
            if (data.relics == null) data.relics = new List<RelicRecord>();
            if (data.discoveredWeaponEvolutions == null) data.discoveredWeaponEvolutions = new List<WeaponEvolutionRecord>();
            if (data.stageDifficulties == null) data.stageDifficulties = new List<StageDifficultyRecord>();
            if (data.highestUnlockedStage < 1) data.highestUnlockedStage = 1;
            if (data.selectedStage < 1) data.selectedStage = 1;
            foreach (var record in data.stageDifficulties)
            {
                if (record == null) continue;
                record.stage = Mathf.Clamp(record.stage, 1, ImplementedStageCount);
                record.difficulty = Mathf.Clamp(record.difficulty, MinStageDifficulty, MaxStageDifficulty);
                record.maxUnlockedDifficulty = Mathf.Clamp(record.maxUnlockedDifficulty, MinStageDifficulty, MaxStageDifficulty);
                int inferredMinimum = data.highestClearedStage >= record.stage ? MinStageDifficulty : 0;
                record.highestClearedDifficulty = Mathf.Clamp(
                    Mathf.Max(record.highestClearedDifficulty, inferredMinimum),
                    0,
                    MaxStageDifficulty);
            }
            if (!data.endingCreditsViewedWasSerialized)
            {
                // Saves created before the one-shot flag already showed the credits when
                // all stages first reached difficulty 5. Preserve that history on migration.
                data.endingCreditsViewed = AreAllStagesClearedAtMaxDifficulty(data);
                data.endingCreditsViewedWasSerialized = true;
            }
            return data;
        }

        static bool TryWriteCloudSave(SaveData data)
        {
            if (ProgressionFileStorage.TryWrite(
                    CloudSavePath,
                    CloudSaveBackupPath,
                    CloudSaveTempPath,
                    data,
                    out string error))
            {
                return true;
            }

            Debug.LogError("Progression cloud save could not be written: " + error);
            return false;
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

        public static bool HasDiscoveredEvolution(WeaponType type)
        {
            if (!WeaponCatalog.IsEvolution(type)) return false;
            if (Data.discoveredWeaponEvolutions == null) Data.discoveredWeaponEvolutions = new List<WeaponEvolutionRecord>();
            foreach (var evolution in Data.discoveredWeaponEvolutions)
            {
                if (evolution != null && evolution.type == type) return true;
            }

            return false;
        }

        public static bool MarkEvolutionDiscovered(WeaponType type)
        {
            if (!WeaponCatalog.IsEvolution(type) || HasDiscoveredEvolution(type)) return false;
            if (Data.discoveredWeaponEvolutions == null) Data.discoveredWeaponEvolutions = new List<WeaponEvolutionRecord>();
            Data.discoveredWeaponEvolutions.Add(new WeaponEvolutionRecord { type = type });
            Save();
            return true;
        }

        public static void ResetWeaponEvolutionsForTesting()
        {
            if (Data.discoveredWeaponEvolutions == null) Data.discoveredWeaponEvolutions = new List<WeaponEvolutionRecord>();
            Data.discoveredWeaponEvolutions.Clear();
            Save();
        }

        public static int OwnedRelicCount()
        {
            if (Data.relics == null) return 0;
            var owned = new HashSet<RelicType>();
            foreach (var relic in Data.relics)
            {
                if (relic != null && relic.type != RelicType.None) owned.Add(relic.type);
            }
            return owned.Count;
        }

        public static int DiscoveredEvolutionCount()
        {
            if (Data.discoveredWeaponEvolutions == null) return 0;
            var discovered = new HashSet<WeaponType>();
            foreach (var record in Data.discoveredWeaponEvolutions)
            {
                if (record != null && WeaponCatalog.IsEvolution(record.type)) discovered.Add(record.type);
            }
            return discovered.Count;
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
                case UpgradeType.UnlockArcher:
                case UpgradeType.UnlockMage:
                case UpgradeType.UnlockShield:
                case UpgradeType.UnlockArrowRain:
                case UpgradeType.UnlockGun:
                case UpgradeType.UnlockFrost:
                case UpgradeType.UnlockThunderBall:
                case UpgradeType.UnlockFlag:
                case UpgradeType.UnlockBoomerangSword:
                case UpgradeType.UnlockAuraSword:
                case UpgradeType.ReviveBuildingsOnBossDefeat:
                case UpgradeType.UnlockOpeningRelicChest:
                    return 1;
                case UpgradeType.StartingWeaponLevel:
                case UpgradeType.EliteSpawnCount:
                case UpgradeType.WatchTowerDamage:
                    return 4;
                case UpgradeType.OpeningPlayerLevel:
                case UpgradeType.PaintAreaTokenGain:
                    return 3;
                case UpgradeType.ReviveSpeed:
                case UpgradeType.MoveSpeed:
                case UpgradeType.PaintRadius:
                case UpgradeType.MovePenaltyReduction:
                case UpgradeType.LevelUpRerollCount:
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
                case UpgradeType.OpeningPlayerLevel:
                    return FixedLevelCost(level, 50, 60, 70);
                case UpgradeType.LevelUpRerollCount:
                    return FixedLevelCost(level, 80, 90, 100, 115, 130);
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
                case UpgradeType.UnlockArcher:
                case UpgradeType.UnlockMage:
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
                case UpgradeType.OpeningPlayerLevel:
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
                case UpgradeType.LevelUpRerollCount:
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

        public static int GetInitialLevelUpRerollCount()
        {
            return CalculateInitialLevelUpRerollCount(GetLevel(UpgradeType.LevelUpRerollCount));
        }

        public static int CalculateInitialLevelUpRerollCount(int skillLevel)
        {
            return BaseLevelUpRerollCount + Mathf.Clamp(skillLevel, 0, GetMaxLevel(UpgradeType.LevelUpRerollCount));
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

        public static bool ShouldShowOpeningStory => !Data.openingStoryCompleted && Data.playCount <= 0;

        public static void MarkOpeningStoryCompleted()
        {
            if (Data.openingStoryCompleted) return;
            Data.openingStoryCompleted = true;
            Save();
        }

        public static bool HasViewedEndingCredits => Data.endingCreditsViewed;

        public static bool TryMarkEndingCreditsViewed()
        {
            if (Data.endingCreditsViewed) return false;
            Data.endingCreditsViewed = true;
            Data.endingCreditsViewedWasSerialized = true;
            Save();
            return true;
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

        public static bool MarkStageCleared(int stage, int clearedDifficulty = MinStageDifficulty)
        {
            if (stage < 1) return false;
            clearedDifficulty = Mathf.Clamp(clearedDifficulty, MinStageDifficulty, MaxStageDifficulty);
            int nextUnlockedStage = Mathf.Min(stage + 1, ImplementedStageCount);
            bool unlockedNewStage = Data.highestUnlockedStage < nextUnlockedStage;
            Data.highestClearedStage = Mathf.Max(Data.highestClearedStage, stage);
            Data.highestUnlockedStage = Mathf.Max(Data.highestUnlockedStage, nextUnlockedStage);
            var difficultyRecord = EnsureStageDifficultyRecord(stage);
            difficultyRecord.highestClearedDifficulty = Mathf.Max(
                difficultyRecord.highestClearedDifficulty,
                clearedDifficulty);
            difficultyRecord.maxUnlockedDifficulty = Mathf.Max(difficultyRecord.maxUnlockedDifficulty, 2);
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
                var difficultyRecord = EnsureStageDifficultyRecord(stage);
                difficultyRecord.highestClearedDifficulty = Mathf.Max(
                    difficultyRecord.highestClearedDifficulty,
                    MinStageDifficulty);
                difficultyRecord.maxUnlockedDifficulty = Mathf.Max(difficultyRecord.maxUnlockedDifficulty, 2);
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
            var record = FindStageDifficultyRecord(stage);
            int maxUnlocked = GetStageMaxUnlockedDifficulty(stage);
            return Mathf.Clamp(record != null ? record.difficulty : MinStageDifficulty, MinStageDifficulty, maxUnlocked);
        }

        public static int GetStageHighestClearedDifficulty(int stage)
        {
            stage = Mathf.Clamp(stage, 1, ImplementedStageCount);
            var record = FindStageDifficultyRecord(stage);
            int inferredMinimum = IsStageCleared(stage) ? MinStageDifficulty : 0;
            int recorded = record != null ? record.highestClearedDifficulty : 0;
            return Mathf.Clamp(Mathf.Max(recorded, inferredMinimum), 0, MaxStageDifficulty);
        }

        public static bool AreAllStagesClearedAtMaxDifficulty()
        {
            return AreAllStagesClearedAtMaxDifficulty(Data);
        }

        static bool AreAllStagesClearedAtMaxDifficulty(SaveData data)
        {
            for (int stage = 1; stage <= ImplementedStageCount; stage++)
            {
                StageDifficultyRecord matchingRecord = null;
                if (data != null && data.stageDifficulties != null)
                {
                    foreach (var record in data.stageDifficulties)
                    {
                        if (record != null && record.stage == stage)
                        {
                            matchingRecord = record;
                            break;
                        }
                    }
                }

                int inferredMinimum = data != null && data.highestClearedStage >= stage
                    ? MinStageDifficulty
                    : 0;
                int recorded = matchingRecord != null ? matchingRecord.highestClearedDifficulty : 0;
                int highestClearedDifficulty = Mathf.Clamp(
                    Mathf.Max(recorded, inferredMinimum),
                    0,
                    MaxStageDifficulty);
                if (highestClearedDifficulty < MaxStageDifficulty) return false;
            }

            return true;
        }

        public static int GetStageMaxUnlockedDifficulty(int stage)
        {
            stage = Mathf.Clamp(stage, 1, ImplementedStageCount);
            var record = FindStageDifficultyRecord(stage);
            int baseline = IsStageCleared(stage) ? 2 : MinStageDifficulty;
            int unlocked = record != null ? record.maxUnlockedDifficulty : baseline;
            return Mathf.Clamp(Mathf.Max(baseline, unlocked), MinStageDifficulty, MaxStageDifficulty);
        }

        public static void SetStageDifficulty(int stage, int difficulty)
        {
            stage = Mathf.Clamp(stage, 1, ImplementedStageCount);
            difficulty = Mathf.Clamp(difficulty, MinStageDifficulty, GetStageMaxUnlockedDifficulty(stage));
            var record = EnsureStageDifficultyRecord(stage);
            record.difficulty = difficulty;
            Save();
        }

        public static bool UnlockStageDifficulty(int stage, int difficulty)
        {
            stage = Mathf.Clamp(stage, 1, ImplementedStageCount);
            difficulty = Mathf.Clamp(difficulty, MinStageDifficulty, MaxStageDifficulty);
            var record = EnsureStageDifficultyRecord(stage);
            int before = GetStageMaxUnlockedDifficulty(stage);
            record.maxUnlockedDifficulty = Mathf.Max(record.maxUnlockedDifficulty, difficulty);
            int after = GetStageMaxUnlockedDifficulty(stage);
            if (after <= before) return false;

            Save();
            return true;
        }

        static StageDifficultyRecord FindStageDifficultyRecord(int stage)
        {
            if (Data.stageDifficulties == null) Data.stageDifficulties = new List<StageDifficultyRecord>();
            foreach (var record in Data.stageDifficulties)
            {
                if (record != null && record.stage == stage) return record;
            }

            return null;
        }

        static StageDifficultyRecord EnsureStageDifficultyRecord(int stage)
        {
            var record = FindStageDifficultyRecord(stage);
            if (record != null) return record;

            record = new StageDifficultyRecord
            {
                stage = stage,
                difficulty = MinStageDifficulty,
                maxUnlockedDifficulty = IsStageCleared(stage) ? 2 : MinStageDifficulty,
                highestClearedDifficulty = IsStageCleared(stage) ? MinStageDifficulty : 0
            };
            Data.stageDifficulties.Add(record);
            return record;
        }

        public static void AddTokensForTesting(int tokens)
        {
            AddTokens(tokens);
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
            Data.endingCreditsViewed = false;
            Data.endingCreditsViewedWasSerialized = true;
            if (Data.stageDifficulties != null) Data.stageDifficulties.Clear();
            Save();
        }

        public static void ResetPlayData()
        {
            cached = new SaveData();
            if (!ProgressionFileStorage.TryDelete(
                    out string error,
                    CloudSavePath,
                    CloudSaveBackupPath,
                    CloudSaveTempPath))
            {
                Debug.LogError("Progression cloud save could not be reset: " + error);
            }
            PlayerPrefs.DeleteKey(LegacySaveKey);
            PlayerPrefs.Save();
        }

        public static void Save()
        {
            if (TryWriteCloudSave(Data)) Saved?.Invoke();
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
