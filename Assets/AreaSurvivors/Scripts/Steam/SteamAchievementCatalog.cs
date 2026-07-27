using System;
using System.Collections.Generic;

namespace AreaSurvivors
{
    public sealed class SteamAchievementDefinition
    {
        public SteamAchievementDefinition(string apiName, Func<SteamAchievementSnapshot, bool> isUnlocked)
        {
            ApiName = apiName;
            IsUnlocked = isUnlocked;
        }

        public string ApiName { get; }
        public Func<SteamAchievementSnapshot, bool> IsUnlocked { get; }
    }

    public sealed class SteamAchievementSnapshot
    {
        readonly SaveData data;
        readonly int effectiveTotalKills;

        public SteamAchievementSnapshot(SaveData data, int effectiveTotalKills = -1)
        {
            this.data = data ?? new SaveData();
            this.effectiveTotalKills = Math.Max(this.data.totalKills, effectiveTotalKills);
        }

        public int TotalKills => effectiveTotalKills;
        public int PlayCount => Math.Max(0, data.playCount);
        public int HighestClearedStage => Math.Max(0, data.highestClearedStage);

        public bool HasEvolution(WeaponType type)
        {
            if (data.discoveredWeaponEvolutions == null) return false;
            foreach (var record in data.discoveredWeaponEvolutions)
            {
                if (record != null && record.type == type) return true;
            }

            return false;
        }

        public bool HasRelic(RelicType type)
        {
            if (data.relics == null) return false;
            foreach (var record in data.relics)
            {
                if (record != null && record.type == type) return true;
            }

            return false;
        }

        public bool IsUpgradeMaxed(UpgradeType type)
        {
            int requiredLevel = ProgressionStore.GetMaxLevel(type);
            if (requiredLevel <= 0 || data.upgrades == null) return false;
            foreach (var record in data.upgrades)
            {
                if (record != null && record.type == type && record.level >= requiredLevel) return true;
            }

            return false;
        }

        public int HighestClearedDifficulty(int stage)
        {
            if (data.stageDifficulties != null)
            {
                foreach (var record in data.stageDifficulties)
                {
                    if (record != null && record.stage == stage)
                    {
                        return Math.Max(0, record.highestClearedDifficulty);
                    }
                }
            }

            return data.highestClearedStage >= stage ? ProgressionStore.MinStageDifficulty : 0;
        }
    }

    public static class SteamAchievementCatalog
    {
        public const string FirstSortie = "ACH_FIRST_SORTIE";
        public const string Kill100 = "ACH_KILL_100";
        public const string Kill1000 = "ACH_KILL_1000";
        public const string Kill10000 = "ACH_KILL_10000";
        public const string ClearStage1 = "ACH_CLEAR_STAGE_1";
        public const string ClearStage2 = "ACH_CLEAR_STAGE_2";
        public const string ClearStage3 = "ACH_CLEAR_STAGE_3";
        public const string ClearStage4 = "ACH_CLEAR_STAGE_4";
        public const string FirstEvolution = "ACH_FIRST_EVOLUTION";
        public const string AllEvolutions = "ACH_ALL_EVOLUTIONS";
        public const string MaxAllSkills = "ACH_MAX_ALL_SKILLS";
        public const string AllRelics = "ACH_ALL_RELICS";
        public const string ClearAllDifficulty5 = "ACH_CLEAR_ALL_DIFFICULTY_5";

        public static readonly UpgradeType[] RequiredMaxedUpgrades =
        {
            UpgradeType.MoveSpeed,
            UpgradeType.PaintRadius,
            UpgradeType.MaxHp,
            UpgradeType.TowerMaxHp,
            UpgradeType.ReviveSpeed,
            UpgradeType.Defense,
            UpgradeType.XpGain,
            UpgradeType.AutoRegen,
            UpgradeType.UnlockBallista,
            UpgradeType.UnlockWatchTower,
            UpgradeType.BallistaRange,
            UpgradeType.UnlockTowerCannon,
            UpgradeType.UnlockTowerUpgrade,
            UpgradeType.TowerAutoRegen,
            UpgradeType.EndTokenGain,
            UpgradeType.EliteSpawnCount,
            UpgradeType.UnlockWall,
            UpgradeType.WallMaxHp1,
            UpgradeType.WallMaxHp2,
            UpgradeType.WallMaxHp3,
            UpgradeType.BallistaDamage,
            UpgradeType.WatchTowerRange,
            UpgradeType.BuildingAutoRegen,
            UpgradeType.WallUpgrade,
            UpgradeType.BallistaUpgrade,
            UpgradeType.WatchTowerUpgrade,
            UpgradeType.MovePenaltyReduction,
            UpgradeType.UnlockArcher,
            UpgradeType.UnlockMage,
            UpgradeType.UnlockWall2,
            UpgradeType.Wall2Upgrade,
            UpgradeType.UnlockShield,
            UpgradeType.UnlockArrowRain,
            UpgradeType.UnlockGun,
            UpgradeType.UnlockFrost,
            UpgradeType.UnlockThunderBall,
            UpgradeType.UnlockFlag,
            UpgradeType.UnlockBoomerangSword,
            UpgradeType.UnlockAuraSword,
            UpgradeType.LevelUpRerollCount,
            UpgradeType.ReviveBuildingsOnBossDefeat,
            UpgradeType.UnlockOpeningRelicChest,
            UpgradeType.WatchTowerDamage,
            UpgradeType.PaintAreaTokenGain,
            UpgradeType.MoveSpeedAdvanced,
            UpgradeType.PaintRadiusAdvanced,
            UpgradeType.OpeningPlayerLevel
        };

        public static readonly RelicType[] RequiredRelics =
        {
            RelicType.WarriorCharm, RelicType.VitalCore, RelicType.WindBoots,
            RelicType.ScholarLens, RelicType.GoldenSeal, RelicType.MasonStrikeSigil,
            RelicType.SwordsmanGlove, RelicType.Hawkfeather, RelicType.ChantingHourglass,
            RelicType.GuardianRivet, RelicType.UnchippedEdge, RelicType.TwinArrowQuiver,
            RelicType.EmberRing, RelicType.CirclingShieldShard, RelicType.RallyBannerSigil,
            RelicType.ReturningBladeRing, RelicType.EchoSwordSeal, RelicType.RaincallerPlume,
            RelicType.BlackIronBullet, RelicType.FrostspreadCrystal, RelicType.ThunderCore,
            RelicType.MerchantContract, RelicType.HarmonyCrest, RelicType.UnwoundedVowSeal,
            RelicType.SolitaryBlade, RelicType.DominionCrown, RelicType.RulerSight,
            RelicType.RegeneratingWallstone, RelicType.SlayerMedal, RelicType.WealthWarSeal,
            RelicType.TriBladeCrest, RelicType.StarbowSightCrown, RelicType.TriSageCrystal,
            RelicType.ThousandSlayerLaurel
        };

        public static readonly WeaponType[] RequiredEvolutions =
        {
            WeaponType.SwordRush, WeaponType.Banana, WeaponType.Excalibur,
            WeaponType.GoldenBow, WeaponType.ArrowShower, WeaponType.MachineGun,
            WeaponType.FireMissile, WeaponType.FrostStorm, WeaponType.ThunderStorm,
            WeaponType.DualShield, WeaponType.GoddessBlessing
        };

        public static readonly int[] RequiredDifficulty5Stages = { 1, 2, 3, 4 };

        public static readonly IReadOnlyList<SteamAchievementDefinition> Definitions =
            new SteamAchievementDefinition[]
            {
                new SteamAchievementDefinition(FirstSortie, snapshot => snapshot.PlayCount >= 1),
                new SteamAchievementDefinition(Kill100, snapshot => snapshot.TotalKills >= 100),
                new SteamAchievementDefinition(Kill1000, snapshot => snapshot.TotalKills >= 1000),
                new SteamAchievementDefinition(Kill10000, snapshot => snapshot.TotalKills >= 10000),
                new SteamAchievementDefinition(ClearStage1, snapshot => snapshot.HighestClearedStage >= 1),
                new SteamAchievementDefinition(ClearStage2, snapshot => snapshot.HighestClearedStage >= 2),
                new SteamAchievementDefinition(ClearStage3, snapshot => snapshot.HighestClearedStage >= 3),
                new SteamAchievementDefinition(ClearStage4, snapshot => snapshot.HighestClearedStage >= 4),
                new SteamAchievementDefinition(FirstEvolution, HasAnyEvolution),
                new SteamAchievementDefinition(AllEvolutions, HasAllEvolutions),
                new SteamAchievementDefinition(MaxAllSkills, HasMaxedAllSkills),
                new SteamAchievementDefinition(AllRelics, HasAllRelics),
                new SteamAchievementDefinition(ClearAllDifficulty5, HasClearedAllDifficulty5)
            };

        static bool HasAnyEvolution(SteamAchievementSnapshot snapshot)
        {
            foreach (var type in RequiredEvolutions)
            {
                if (snapshot.HasEvolution(type)) return true;
            }

            return false;
        }

        static bool HasAllEvolutions(SteamAchievementSnapshot snapshot)
        {
            foreach (var type in RequiredEvolutions)
            {
                if (!snapshot.HasEvolution(type)) return false;
            }

            return true;
        }

        static bool HasMaxedAllSkills(SteamAchievementSnapshot snapshot)
        {
            foreach (var type in RequiredMaxedUpgrades)
            {
                if (!snapshot.IsUpgradeMaxed(type)) return false;
            }

            return true;
        }

        static bool HasAllRelics(SteamAchievementSnapshot snapshot)
        {
            foreach (var type in RequiredRelics)
            {
                if (!snapshot.HasRelic(type)) return false;
            }

            return true;
        }

        static bool HasClearedAllDifficulty5(SteamAchievementSnapshot snapshot)
        {
            foreach (int stage in RequiredDifficulty5Stages)
            {
                if (snapshot.HighestClearedDifficulty(stage) < ProgressionStore.MaxStageDifficulty) return false;
            }

            return true;
        }
    }
}
