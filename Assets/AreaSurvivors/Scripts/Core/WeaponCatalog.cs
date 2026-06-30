namespace AreaSurvivors
{
    public static class WeaponCatalog
    {
        public static readonly WeaponType[] TestableWeapons =
        {
            WeaponType.Slash,
            WeaponType.Arrow,
            WeaponType.Fireball,
            WeaponType.Shield,
            WeaponType.Flag,
            WeaponType.BoomerangSword,
            WeaponType.AuraSword,
            WeaponType.ArrowRain,
            WeaponType.Gun,
            WeaponType.Frost,
            WeaponType.ThunderBall
        };

        public static readonly WeaponType[] UnlockableWeapons =
        {
            WeaponType.Arrow,
            WeaponType.Fireball,
            WeaponType.Shield,
            WeaponType.ArrowRain,
            WeaponType.Gun,
            WeaponType.Frost,
            WeaponType.ThunderBall,
            WeaponType.Flag,
            WeaponType.BoomerangSword,
            WeaponType.AuraSword
        };

        public static string DisplayName(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Arrow: return "弓";
                case WeaponType.Fireball: return "ファイアボール";
                case WeaponType.Shield: return "シールド";
                case WeaponType.Flag: return "旗";
                case WeaponType.BoomerangSword: return "ブーメランソード";
                case WeaponType.AuraSword: return "オーラソード";
                case WeaponType.ArrowRain: return "アローレイン";
                case WeaponType.Gun: return "銃";
                case WeaponType.Frost: return "フロスト";
                case WeaponType.ThunderBall: return "サンダーボール";
                default: return "スラッシュ";
            }
        }

        public static string IconResource(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Arrow: return "ArrowHudIcon";
                case WeaponType.Fireball: return "FireballHudIcon";
                case WeaponType.Shield: return "Shield";
                case WeaponType.Flag: return "Flag";
                case WeaponType.BoomerangSword: return "BoomerangSword";
                case WeaponType.AuraSword: return "AuraSword";
                case WeaponType.ArrowRain: return "ArrowRain";
                case WeaponType.Gun: return "Gun";
                case WeaponType.Frost: return "Frost";
                case WeaponType.ThunderBall: return "ThunderBall";
                default: return "Slash_0";
            }
        }

        public static UpgradeType UnlockUpgrade(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Arrow: return UpgradeType.UnlockArrow;
                case WeaponType.Fireball: return UpgradeType.UnlockFireball;
                case WeaponType.Shield: return UpgradeType.UnlockShield;
                case WeaponType.ArrowRain: return UpgradeType.UnlockArrowRain;
                case WeaponType.Gun: return UpgradeType.UnlockGun;
                case WeaponType.Frost: return UpgradeType.UnlockFrost;
                case WeaponType.ThunderBall: return UpgradeType.UnlockThunderBall;
                case WeaponType.Flag: return UpgradeType.UnlockFlag;
                case WeaponType.BoomerangSword: return UpgradeType.UnlockBoomerangSword;
                case WeaponType.AuraSword: return UpgradeType.UnlockAuraSword;
                default: return UpgradeType.StartingWeaponLevel;
            }
        }

        public static bool IsAdvanced(WeaponType type)
        {
            return type == WeaponType.Flag ||
                type == WeaponType.BoomerangSword ||
                type == WeaponType.AuraSword ||
                type == WeaponType.ArrowRain ||
                type == WeaponType.Gun ||
                type == WeaponType.Frost ||
                type == WeaponType.ThunderBall;
        }
    }
}
