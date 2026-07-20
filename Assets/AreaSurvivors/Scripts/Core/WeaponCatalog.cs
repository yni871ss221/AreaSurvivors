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
            WeaponType.ThunderBall,
            WeaponType.SwordRush,
            WeaponType.Banana,
            WeaponType.Excalibur,
            WeaponType.GoldenBow,
            WeaponType.ArrowShower,
            WeaponType.MachineGun,
            WeaponType.FireMissile,
            WeaponType.FrostStorm,
            WeaponType.ThunderStorm,
            WeaponType.DualShield,
            WeaponType.GoddessBlessing
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
            return LocalizationService.LocalizeSource(DisplayNameSource(type));
        }

        public static string DisplayNameSource(WeaponType type)
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
                case WeaponType.SwordRush: return "ソードラッシュ";
                case WeaponType.Banana: return "バナナ";
                case WeaponType.Excalibur: return "エクスカリバー";
                case WeaponType.GoldenBow: return "黄金の弓";
                case WeaponType.ArrowShower: return "アローシャワー";
                case WeaponType.MachineGun: return "マシンガン";
                case WeaponType.FireMissile: return "ファイアミサイル";
                case WeaponType.FrostStorm: return "フロストストーム";
                case WeaponType.ThunderStorm: return "サンダーストーム";
                case WeaponType.DualShield: return "デュアルシールド";
                case WeaponType.GoddessBlessing: return "女神の祝福";
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
                case WeaponType.SwordRush: return "SwordRushIcon";
                case WeaponType.Banana: return "Weapons/BananaIcon";
                case WeaponType.Excalibur: return "Weapons/ExcaliburIcon";
                case WeaponType.GoldenBow: return "Weapons/GoldenBowIcon";
                case WeaponType.ArrowShower: return "Weapons/ArrowShowerIcon";
                case WeaponType.MachineGun: return "Weapons/MachineGunIcon";
                case WeaponType.FireMissile: return "Weapons/FireMissileIcon";
                case WeaponType.FrostStorm: return "Weapons/FrostStormIcon";
                case WeaponType.ThunderStorm: return "Weapons/ThunderStormIcon";
                case WeaponType.DualShield: return "Weapons/DualShieldIcon";
                case WeaponType.GoddessBlessing: return "Weapons/GoddessBlessingIcon";
                default: return "Slash_0";
            }
        }

        public static bool IsEvolution(WeaponType type)
        {
            return type == WeaponType.SwordRush || type == WeaponType.Banana ||
                type == WeaponType.Excalibur || type == WeaponType.GoldenBow ||
                type == WeaponType.ArrowShower || type == WeaponType.MachineGun ||
                type == WeaponType.FireMissile || type == WeaponType.FrostStorm ||
                type == WeaponType.ThunderStorm || type == WeaponType.DualShield ||
                type == WeaponType.GoddessBlessing;
        }

        public static WeaponType BaseWeaponOf(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.SwordRush: return WeaponType.Slash;
                case WeaponType.Banana: return WeaponType.BoomerangSword;
                case WeaponType.Excalibur: return WeaponType.AuraSword;
                case WeaponType.GoldenBow: return WeaponType.Arrow;
                case WeaponType.ArrowShower: return WeaponType.ArrowRain;
                case WeaponType.MachineGun: return WeaponType.Gun;
                case WeaponType.FireMissile: return WeaponType.Fireball;
                case WeaponType.FrostStorm: return WeaponType.Frost;
                case WeaponType.ThunderStorm: return WeaponType.ThunderBall;
                case WeaponType.DualShield: return WeaponType.Shield;
                case WeaponType.GoddessBlessing: return WeaponType.Flag;
                default: return type;
            }
        }

        public static WeaponType EvolutionOf(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Slash: return WeaponType.SwordRush;
                case WeaponType.BoomerangSword: return WeaponType.Banana;
                case WeaponType.AuraSword: return WeaponType.Excalibur;
                case WeaponType.Arrow: return WeaponType.GoldenBow;
                case WeaponType.ArrowRain: return WeaponType.ArrowShower;
                case WeaponType.Gun: return WeaponType.MachineGun;
                case WeaponType.Fireball: return WeaponType.FireMissile;
                case WeaponType.Frost: return WeaponType.FrostStorm;
                case WeaponType.ThunderBall: return WeaponType.ThunderStorm;
                case WeaponType.Shield: return WeaponType.DualShield;
                case WeaponType.Flag: return WeaponType.GoddessBlessing;
                default: return type;
            }
        }

        public static string EvolutionDescriptionSource(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.SwordRush: return "五月雨の剣撃を5回繰り出す。攻撃力と攻撃範囲が大幅に上昇する。";
                case WeaponType.Banana: return "無数のバナナを周囲へ投げる。剣本数が3本増加し、攻撃範囲が大幅に上昇する。";
                case WeaponType.Excalibur: return "前方へ巨大な斬撃を放ち、触れている敵へ持続ダメージを与える。";
                case WeaponType.GoldenBow: return "攻撃力が大幅に上昇した金色の矢が、射程内の敵を貫通する。";
                case WeaponType.ArrowShower: return "攻撃時間中、画面内の敵へ範囲ダメージを持つ矢を降らせる。";
                case WeaponType.MachineGun: return "進行方向へ0.2秒間隔で大量の銃弾を連射する。";
                case WeaponType.FireMissile: return "射程内の敵を追尾する炎の玉を放つ。";
                case WeaponType.FrostStorm: return "画面内の敵5体へ氷のトゲを出現させ、周囲を凍結する。";
                case WeaponType.ThunderStorm: return "雷球を放ちながら、3つの雷球をプレイヤーの周囲に回転させる。";
                case WeaponType.DualShield: return "紫色のシールドが倍の数と速度、広い軌道で回転する。";
                case WeaponType.GoddessBlessing: return "光の魔法陣が敵を攻撃し、プレイヤーと範囲内の建造物を5回復する。";
                default: return string.Empty;
            }
        }

        public static string[] EvolutionRequirementSources(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.SwordRush: return new[] { "武器Lv.10", "ゲームプレイ回数5回以上" };
                case WeaponType.Banana: return new[] { "武器Lv.10", "ゲームプレイ中の撃破数300" };
                case WeaponType.Excalibur: return new[] { "武器Lv.10", "レリックを10個以上所持" };
                case WeaponType.GoldenBow: return new[] { "武器Lv.10", "ゲームプレイ中の獲得トークン数50" };
                case WeaponType.ArrowShower: return new[] { "武器Lv.10", "塗り自陣エリア50%以上" };
                case WeaponType.MachineGun: return new[] { "武器Lv.10", "プレイヤーLv.30" };
                case WeaponType.FireMissile: return new[] { "武器Lv.10", "ボス出現中" };
                case WeaponType.FrostStorm: return new[] { "武器Lv.10", "進化武器を3つ以上アンロック" };
                case WeaponType.ThunderStorm: return new[] { "武器Lv.10", "累計討伐数10000" };
                case WeaponType.DualShield: return new[] { "武器Lv.10", "プレイヤーのHPが満タンではない" };
                case WeaponType.GoddessBlessing: return new[] { "武器Lv.10", "中心塔のHPが半分以下" };
                default: return System.Array.Empty<string>();
            }
        }

        public static string AreaControlSpecialEffectDescriptionSource(WeaponType type)
        {
            switch (BaseWeaponOf(type))
            {
                case WeaponType.Slash:
                    return "エリア占有率が50%以上の時、ノックバック２倍";
                case WeaponType.Arrow:
                    return "エリア占有率が50%以上の時、射程２倍";
                case WeaponType.Fireball:
                    return "エリア占有率が50%以上の時、爆発範囲２倍";
                case WeaponType.Shield:
                    return "エリア占有率が50%以上の時、回転速度２倍";
                case WeaponType.Flag:
                case WeaponType.AuraSword:
                case WeaponType.ArrowRain:
                case WeaponType.Frost:
                    return "エリア占有率が50%以上の時、エリア取得範囲に応じて攻撃範囲拡大";
                case WeaponType.BoomerangSword:
                    return "エリア占有率が70%以上の時、剣本数２倍";
                case WeaponType.Gun:
                    return "エリア占有率が70%以上の時、攻撃力２倍";
                case WeaponType.ThunderBall:
                    return "エリア占有率が70%以上の時、攻撃範囲２倍";
                default:
                    return string.Empty;
            }
        }

        public static string EvolutionUndiscoveredHintSource()
        {
            return "条件を満たすと進化することができます。";
        }

        public static string EvolutionChoiceDescriptionSource(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.SwordRush: return "スラッシュを進化させる";
                case WeaponType.Banana: return "ブーメランソードを進化させる";
                case WeaponType.Excalibur: return "オーラソードを進化させる";
                case WeaponType.GoldenBow: return "弓を進化させる";
                case WeaponType.ArrowShower: return "アローレインを進化させる";
                case WeaponType.MachineGun: return "銃を進化させる";
                case WeaponType.FireMissile: return "ファイアボールを進化させる";
                case WeaponType.FrostStorm: return "フロストを進化させる";
                case WeaponType.ThunderStorm: return "サンダーボールを進化させる";
                case WeaponType.DualShield: return "シールドを進化させる";
                case WeaponType.GoddessBlessing: return "旗を進化させる";
                default: return string.Empty;
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
