using UnityEngine;

namespace AreaSurvivors
{
    public static class StatIconCatalog
    {
        const string Prefix = "StatIcons/";
        const string SkillPrefix = "SkillIcons/";

        public const string WeaponLevel = Prefix + "StatWeaponLevel";
        public const string Attack = Prefix + "StatAttack";
        public const string Cooldown = Prefix + "StatCooldown";
        public const string MoveSpeed = Prefix + "StatMoveSpeed";
        public const string Paint = Prefix + "StatPaint";
        public const string MaxHp = Prefix + "StatMaxHp";
        public const string Revive = Prefix + "StatRevive";
        public const string Projectile = Prefix + "StatProjectile";
        public const string Range = Prefix + "StatRange";
        public const string Knockback = Prefix + "StatKnockback";
        public const string Defense = Prefix + "StatDefense";
        public const string Xp = Prefix + "StatXp";
        public const string Regen = Prefix + "StatRegen";
        public const string CharacterArcher = "Characters/Archer";
        public const string CharacterMage = "Characters/Mage";
        public const string SkillPlayerFireball = SkillPrefix + "SkillPlayerFireball";
        public const string SkillWoodenWall = SkillPrefix + "SkillWoodenWall";
        public const string SkillWoodenWallHp = SkillPrefix + "SkillWoodenWallHp";
        public const string SkillWoodenWallRegen = SkillPrefix + "SkillWoodenWallRegen";
        public const string SkillWoodenWallUpgrade = SkillPrefix + "SkillWoodenWallUpgrade";
        public const string SkillBallista = SkillPrefix + "SkillBallista";
        public const string SkillBallistaRange = SkillPrefix + "SkillBallistaRange";
        public const string SkillBallistaDamage = SkillPrefix + "SkillBallistaDamage";
        public const string SkillBallistaUpgrade = SkillPrefix + "SkillBallistaUpgrade";
        public const string SkillWatchTower = SkillPrefix + "SkillWatchTower";
        public const string SkillWatchTowerPaint = SkillPrefix + "SkillWatchTowerPaint";
        public const string SkillWatchTowerShield = SkillPrefix + "SkillWatchTowerShield";
        public const string SkillWatchTowerUpgrade = SkillPrefix + "SkillWatchTowerUpgrade";
        public const string SkillTowerHp = SkillPrefix + "SkillTowerHp";
        public const string SkillTowerRegen = SkillPrefix + "SkillTowerRegen";
        public const string SkillCannonball = SkillPrefix + "SkillCannonball";
        public const string SkillToken = SkillPrefix + "SkillToken";
        public const string SkillEliteBoar = SkillPrefix + "SkillEliteBoar";
        public const string SkillTowerUpgrade = SkillPrefix + "SkillTowerUpgrade";
        public const string SkillOpeningLevelUp = SkillPrefix + "SkillOpeningLevelUp";
        public const string SkillReroll = SkillPrefix + "SkillReroll";
        public const string TreasureChest = "TreasureChest";

        public static Sprite Load(string resource)
        {
            return string.IsNullOrEmpty(resource) ? null : GeneratedSpriteLoader.Load(resource);
        }

        public static string ForHudText(string textName)
        {
            switch (textName)
            {
                case "Weapon Level Text": return WeaponLevel;
                case "Attack Text": return Attack;
                case "Cooldown Text": return Cooldown;
                case "Speed Text": return MoveSpeed;
                case "Paint Text": return Paint;
                case "Revive Text": return Revive;
                case "Projectile Text": return Projectile;
                case "Range Text": return Range;
                case "Knockback Text": return Knockback;
                case "Defense Text": return Defense;
                case "Xp Gain Text": return Xp;
                case "Regen Text": return Regen;
                default: return null;
            }
        }

        public static string ForUpgrade(UpgradeType type, string fallback = null)
        {
            switch (type)
            {
                case UpgradeType.StartingWeaponLevel: return WeaponLevel;
                case UpgradeType.UnlockArcher: return CharacterArcher;
                case UpgradeType.UnlockMage: return CharacterMage;
                case UpgradeType.UnlockShield: return "Shield";
                case UpgradeType.UnlockArrowRain: return "ArrowRain";
                case UpgradeType.UnlockGun: return "Gun";
                case UpgradeType.UnlockFrost: return "Frost";
                case UpgradeType.UnlockThunderBall: return "ThunderBall";
                case UpgradeType.UnlockFlag: return "Flag";
                case UpgradeType.UnlockBoomerangSword: return "BoomerangSword";
                case UpgradeType.UnlockAuraSword: return "AuraSword";
                case UpgradeType.LevelUpRerollCount: return SkillReroll;
                case UpgradeType.MoveSpeed: return MoveSpeed;
                case UpgradeType.MoveSpeedAdvanced: return MoveSpeed;
                case UpgradeType.MovePenaltyReduction: return MoveSpeed;
                case UpgradeType.PaintRadius: return Paint;
                case UpgradeType.PaintRadiusAdvanced: return Paint;
                case UpgradeType.MaxHp:
                    return MaxHp;
                case UpgradeType.TowerMaxHp:
                    return SkillTowerHp;
                case UpgradeType.WallMaxHp1:
                case UpgradeType.WallMaxHp2:
                case UpgradeType.WallMaxHp3:
                    return SkillWoodenWallHp;
                case UpgradeType.ReviveSpeed: return Revive;
                case UpgradeType.Defense: return Defense;
                case UpgradeType.XpGain: return Xp;
                case UpgradeType.AutoRegen: return Regen;
                case UpgradeType.TowerAutoRegen: return SkillTowerRegen;
                case UpgradeType.UnlockWall:
                    return SkillWoodenWall;
                case UpgradeType.UnlockBallista:
                    return SkillBallista;
                case UpgradeType.UnlockWatchTower:
                    return SkillWatchTower;
                case UpgradeType.BallistaRange:
                    return SkillBallistaRange;
                case UpgradeType.WatchTowerRange:
                    return SkillWatchTowerPaint;
                case UpgradeType.BallistaDamage:
                case UpgradeType.WatchTowerDamage:
                    return SkillBallistaDamage;
                case UpgradeType.UnlockTowerCannon:
                    return SkillCannonball;
                case UpgradeType.BuildingAutoRegen:
                    return SkillWoodenWallRegen;
                case UpgradeType.UnlockWall2:
                    return SkillWoodenWall;
                case UpgradeType.WallUpgrade:
                case UpgradeType.Wall2Upgrade:
                    return SkillWoodenWallUpgrade;
                case UpgradeType.BallistaUpgrade:
                    return SkillBallistaUpgrade;
                case UpgradeType.WatchTowerUpgrade:
                    return SkillWatchTowerUpgrade;
                case UpgradeType.UnlockTowerUpgrade:
                    return SkillTowerUpgrade;
                case UpgradeType.EndTokenGain:
                case UpgradeType.PaintAreaTokenGain:
                    return SkillToken;
                case UpgradeType.EliteSpawnCount:
                    return SkillEliteBoar;
                case UpgradeType.OpeningPlayerLevel:
                    return SkillOpeningLevelUp;
                case UpgradeType.ReviveBuildingsOnBossDefeat:
                    return SkillTowerRegen;
                case UpgradeType.UnlockOpeningRelicChest:
                    return TreasureChest;
                default:
                    return fallback;
            }
        }
    }
}
