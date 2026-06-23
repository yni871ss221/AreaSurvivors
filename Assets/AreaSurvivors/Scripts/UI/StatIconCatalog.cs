using UnityEngine;

namespace AreaSurvivors
{
    public static class StatIconCatalog
    {
        const string Prefix = "StatIcons/";

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
        public const string Work = Prefix + "StatWork";
        public const string Resource = Prefix + "StatResource";

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
                case "Work Text": return Work;
                case "Resource Text": return Resource;
                default: return null;
            }
        }

        public static string ForUpgrade(UpgradeType type, string fallback = null)
        {
            switch (type)
            {
                case UpgradeType.StartingWeaponLevel: return WeaponLevel;
                case UpgradeType.MoveSpeed: return MoveSpeed;
                case UpgradeType.PaintRadius: return Paint;
                case UpgradeType.MaxHp:
                    return MaxHp;
                case UpgradeType.TowerMaxHp:
                    return fallback ?? MaxHp;
                case UpgradeType.ReviveSpeed: return Revive;
                case UpgradeType.Defense: return Defense;
                case UpgradeType.XpGain: return Xp;
                case UpgradeType.AutoRegen:
                case UpgradeType.TowerAutoRegen:
                    return Regen;
                case UpgradeType.RoundTimeLimit:
                    return Revive;
                case UpgradeType.WorkSpeed:
                    return Work;
                case UpgradeType.ResourceGain:
                case UpgradeType.StartingWood:
                case UpgradeType.StartingStone:
                    return Resource;
                case UpgradeType.BallistaRange:
                    return Range;
                default:
                    return fallback;
            }
        }
    }
}
