namespace AreaSurvivors
{
    public static class WeaponAttributeCatalog
    {
        public const string MeleeIcon = "UI/WeaponTypeMelee";
        public const string RangedIcon = "UI/WeaponTypeRanged";
        public const string MagicIcon = "UI/WeaponTypeMagic";
        public const string DefenseIcon = "UI/WeaponTypeDefense";

        public static WeaponAttributeType ForWeapon(WeaponType weaponType)
        {
            switch (weaponType)
            {
                case WeaponType.Slash:
                case WeaponType.SwordRush:
                case WeaponType.Banana:
                case WeaponType.Excalibur:
                case WeaponType.BoomerangSword:
                case WeaponType.AuraSword:
                    return WeaponAttributeType.Melee;
                case WeaponType.Arrow:
                case WeaponType.GoldenBow:
                case WeaponType.ArrowShower:
                case WeaponType.MachineGun:
                case WeaponType.ArrowRain:
                case WeaponType.Gun:
                    return WeaponAttributeType.Ranged;
                case WeaponType.Fireball:
                case WeaponType.FireMissile:
                case WeaponType.FrostStorm:
                case WeaponType.ThunderStorm:
                case WeaponType.Frost:
                case WeaponType.ThunderBall:
                    return WeaponAttributeType.Magic;
                case WeaponType.Shield:
                case WeaponType.Flag:
                case WeaponType.DualShield:
                case WeaponType.GoddessBlessing:
                    return WeaponAttributeType.Defense;
                default:
                    return WeaponAttributeType.None;
            }
        }

        public static string IconResource(WeaponAttributeType attributeType)
        {
            switch (attributeType)
            {
                case WeaponAttributeType.Melee:
                    return MeleeIcon;
                case WeaponAttributeType.Ranged:
                    return RangedIcon;
                case WeaponAttributeType.Magic:
                    return MagicIcon;
                case WeaponAttributeType.Defense:
                    return DefenseIcon;
                default:
                    return null;
            }
        }
    }
}
