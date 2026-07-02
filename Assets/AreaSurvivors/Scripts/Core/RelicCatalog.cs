using System;
using UnityEngine;

namespace AreaSurvivors
{
    public enum RelicType
    {
        None = 0,
        WarriorCharm = 1,
        VitalCore = 2,
        WindBoots = 3,
        ScholarLens = 4,
        GoldenSeal = 5,
        MasonStrikeSigil = 6,
        SwordsmanGlove = 7,
        Hawkfeather = 8,
        ChantingHourglass = 9,
        GuardianRivet = 10,
        UnchippedEdge = 11,
        TwinArrowQuiver = 12,
        EmberRing = 13,
        CirclingShieldShard = 14,
        RallyBannerSigil = 15,
        ReturningBladeRing = 16,
        EchoSwordSeal = 17,
        RaincallerPlume = 18,
        BlackIronBullet = 19,
        FrostspreadCrystal = 20,
        ThunderCore = 21,
        MerchantContract = 22,
        HarmonyCrest = 23,
        UnwoundedVowSeal = 24,
        SolitaryBlade = 25,
        DominionCrown = 26,
        RulerSight = 27,
        RegeneratingWallstone = 28,
        SlayerMedal = 29,
        WealthWarSeal = 30
    }

    public enum RelicEffectKind
    {
        AttackMultiplier,
        MaxHpBonus,
        MoveSpeedMultiplier,
        XpGainMultiplier,
        EndTokenMultiplier,
        BuildingAttackBonus,
        WeaponAttackBonus,
        WeaponCooldownMultiplier,
        WeaponProjectileCountBonus,
        WeaponRangeBonus,
        WeaponDurationBonus,
        NormalEnemyTokenDropChance,
        DistinctWeaponCategoryAttackMultiplier,
        FullHpAttackMultiplier,
        SingleWeaponAttackMultiplier,
        CenterTowerPaintAttackMultiplier,
        BallistaPaintAttackMultiplier,
        WallAutoRegenBonus,
        KillAttackBonusPerHundred,
        RunTokenAttackBonusPerTen
    }

    public enum RelicRarity
    {
        Common,
        Uncommon,
        Rare
    }

    [Serializable]
    public sealed class RelicDefinition
    {
        public RelicType type;
        public string displayName;
        public string description;
        public string effectText;
        public string iconPath;
        public RelicRarity rarity;
        public RelicEffectKind effectKind;
        public float value;
        public WeaponType targetWeapon;
        public WeaponAttributeType targetAttribute;

        public RelicDefinition(
            RelicType type,
            string displayName,
            string description,
            string effectText,
            string iconPath,
            RelicRarity rarity,
            RelicEffectKind effectKind,
            float value,
            WeaponType targetWeapon = WeaponType.Slash,
            WeaponAttributeType targetAttribute = WeaponAttributeType.None)
        {
            this.type = type;
            this.displayName = displayName;
            this.description = description;
            this.effectText = effectText;
            this.iconPath = iconPath;
            this.rarity = rarity;
            this.effectKind = effectKind;
            this.value = value;
            this.targetWeapon = targetWeapon;
            this.targetAttribute = targetAttribute;
        }
    }

    public static class RelicCatalog
    {
        const int CommonDropWeight = 60;
        const int UncommonDropWeight = 30;
        const int RareDropWeight = 10;

        static readonly RelicDefinition[] Definitions =
        {
            new RelicDefinition(RelicType.WarriorCharm, "戦士の護符", "古い戦場で使われていた護符。装備者の一撃に迷いをなくします。", "武器攻撃力 +10%", "RelicWarriorCharm", RelicRarity.Uncommon, RelicEffectKind.AttackMultiplier, 0.1f),
            new RelicDefinition(RelicType.VitalCore, "生命の核", "淡く脈打つ結晶。身体の奥から生命力を押し上げます。", "最大HP +20", "RelicVitalCore", RelicRarity.Uncommon, RelicEffectKind.MaxHpBonus, 20f),
            new RelicDefinition(RelicType.WindBoots, "風走りの靴", "風を編み込んだ軽い靴。危険地帯を駆け抜けやすくなります。", "移動速度 +8%", "RelicWindBoots", RelicRarity.Uncommon, RelicEffectKind.MoveSpeedMultiplier, 0.08f),
            new RelicDefinition(RelicType.ScholarLens, "学びのレンズ", "経験の流れを見通す小さなレンズ。戦いから多くを学べます。", "経験値獲得量 +10%", "RelicScholarLens", RelicRarity.Uncommon, RelicEffectKind.XpGainMultiplier, 0.1f),
            new RelicDefinition(RelicType.GoldenSeal, "黄金の印章", "勝利の報酬を呼び込む黄金の印。遠征後の成果が増えます。", "クリア/敗北時トークン +10%", "RelicGoldenSeal", RelicRarity.Uncommon, RelicEffectKind.EndTokenMultiplier, 0.1f),
            new RelicDefinition(RelicType.MasonStrikeSigil, "石工の打撃符", "職人の槌跡が刻まれた札。建造物の一撃をわずかに重くします。", "建造物の攻撃力 +1", "RelicMasonStrikeSigil", RelicRarity.Common, RelicEffectKind.BuildingAttackBonus, 1f),
            new RelicDefinition(RelicType.SwordsmanGlove, "剣士の小手", "握りを安定させる革の小手。近接武器の威力を高めます。", "近接系武器の攻撃力 +1", "RelicSwordsmanGlove", RelicRarity.Common, RelicEffectKind.WeaponAttackBonus, 1f, WeaponType.Slash, WeaponAttributeType.Melee),
            new RelicDefinition(RelicType.Hawkfeather, "鷹目の羽根", "矢筋を読む羽根飾り。遠距離武器の攻撃間隔を短縮します。", "遠距離系武器の攻撃間隔 -5%", "RelicHawkfeather", RelicRarity.Common, RelicEffectKind.WeaponCooldownMultiplier, 0.95f, WeaponType.Arrow, WeaponAttributeType.Ranged),
            new RelicDefinition(RelicType.ChantingHourglass, "詠唱の砂時計", "砂が落ちるたびに詠唱が整います。魔法武器の攻撃間隔を短縮します。", "魔法系武器の攻撃間隔 -5%", "RelicChantingHourglass", RelicRarity.Common, RelicEffectKind.WeaponCooldownMultiplier, 0.95f, WeaponType.Fireball, WeaponAttributeType.Magic),
            new RelicDefinition(RelicType.GuardianRivet, "守護者の鋲", "盾に打ち込まれた古い鋲。防御武器の反撃を強めます。", "防御系武器の攻撃力 +1", "RelicGuardianRivet", RelicRarity.Common, RelicEffectKind.WeaponAttackBonus, 1f, WeaponType.Shield, WeaponAttributeType.Defense),
            new RelicDefinition(RelicType.UnchippedEdge, "刃こぼれ知らず", "欠けない刃を象った小さな護符。スラッシュの威力を高めます。", "スラッシュの攻撃力 +2", "RelicUnchippedEdge", RelicRarity.Common, RelicEffectKind.WeaponAttackBonus, 2f, WeaponType.Slash),
            new RelicDefinition(RelicType.TwinArrowQuiver, "双矢の矢筒", "二本目の矢が自然に手に馴染む矢筒です。", "弓の矢の本数 +1", "RelicTwinArrowQuiver", RelicRarity.Common, RelicEffectKind.WeaponProjectileCountBonus, 1f, WeaponType.Arrow),
            new RelicDefinition(RelicType.EmberRing, "火種の指輪", "消えない火種を宿す指輪。ファイアボールの詠唱を早めます。", "ファイアボールの攻撃間隔 -5%", "RelicEmberRing", RelicRarity.Common, RelicEffectKind.WeaponCooldownMultiplier, 0.95f, WeaponType.Fireball),
            new RelicDefinition(RelicType.CirclingShieldShard, "巡る盾片", "宙を巡る盾の欠片。守りの輪をひとつ増やします。", "シールド数 +1", "RelicCirclingShieldShard", RelicRarity.Common, RelicEffectKind.WeaponProjectileCountBonus, 1f, WeaponType.Shield),
            new RelicDefinition(RelicType.RallyBannerSigil, "鼓舞の紋旗", "周囲の士気を高める小さな紋章。旗の影響範囲を広げます。", "旗の攻撃範囲 +1", "RelicRallyBannerSigil", RelicRarity.Common, RelicEffectKind.WeaponRangeBonus, 1f, WeaponType.Flag),
            new RelicDefinition(RelicType.ReturningBladeRing, "返り刃の輪", "投げた刃を呼び戻す輪。ブーメランソードの本数を増やします。", "ブーメランソードの剣本数 +1", "RelicReturningBladeRing", RelicRarity.Common, RelicEffectKind.WeaponProjectileCountBonus, 1f, WeaponType.BoomerangSword),
            new RelicDefinition(RelicType.EchoSwordSeal, "残響の剣印", "斬撃の余韻を残す印。オーラソードの攻撃回数を増やします。", "オーラソードの攻撃回数 +1", "RelicEchoSwordSeal", RelicRarity.Common, RelicEffectKind.WeaponProjectileCountBonus, 1f, WeaponType.AuraSword),
            new RelicDefinition(RelicType.RaincallerPlume, "雨呼びの羽飾り", "矢の雨を広げる羽飾り。アローレインの範囲を広げます。", "アローレインの攻撃範囲 +1", "RelicRaincallerPlume", RelicRarity.Common, RelicEffectKind.WeaponRangeBonus, 1f, WeaponType.ArrowRain),
            new RelicDefinition(RelicType.BlackIronBullet, "黒鉄の弾丸", "重く黒い弾丸。銃撃に鋭い衝撃を与えます。", "銃の攻撃力 +2", "RelicBlackIronBullet", RelicRarity.Common, RelicEffectKind.WeaponAttackBonus, 2f, WeaponType.Gun),
            new RelicDefinition(RelicType.FrostspreadCrystal, "霜広げの結晶", "冷気を薄く広げる結晶。フロストの範囲を広げます。", "フロストの攻撃範囲 +1", "RelicFrostspreadCrystal", RelicRarity.Common, RelicEffectKind.WeaponRangeBonus, 1f, WeaponType.Frost),
            new RelicDefinition(RelicType.ThunderCore, "蓄雷の核", "雷を内に溜める小さな核。サンダーボールを長く残します。", "サンダーボールの持続時間 +1", "RelicThunderCore", RelicRarity.Common, RelicEffectKind.WeaponDurationBonus, 1f, WeaponType.ThunderBall),
            new RelicDefinition(RelicType.MerchantContract, "旅商人の契約書", "戦場で拾った戦利品を商人が買い取る契約書です。", "通常敵が2%でトークンを落とす", "RelicMerchantContract", RelicRarity.Uncommon, RelicEffectKind.NormalEnemyTokenDropChance, 0.02f),
            new RelicDefinition(RelicType.HarmonyCrest, "調和の紋章", "異なる武器の力を響き合わせる紋章です。", "3武器のカテゴリが全て異なる時、攻撃力 +15%", "RelicHarmonyCrest", RelicRarity.Uncommon, RelicEffectKind.DistinctWeaponCategoryAttackMultiplier, 0.15f),
            new RelicDefinition(RelicType.UnwoundedVowSeal, "無傷の誓印", "傷ひとつない戦いを誓う印です。", "HP最大時、攻撃力 +10%", "RelicUnwoundedVowSeal", RelicRarity.Uncommon, RelicEffectKind.FullHpAttackMultiplier, 0.1f),
            new RelicDefinition(RelicType.SolitaryBlade, "孤高の刃", "ただ一振りに全てを託す者の刃です。", "武器が1つだけの時、攻撃力 2倍", "RelicSolitaryBlade", RelicRarity.Uncommon, RelicEffectKind.SingleWeaponAttackMultiplier, 2f),
            new RelicDefinition(RelicType.DominionCrown, "王域の王冠", "支配した土地の力を中心塔へ集める王冠です。", "塗ったエリアに応じて中心塔の攻撃力上昇（最大2倍）", "RelicDominionCrown", RelicRarity.Rare, RelicEffectKind.CenterTowerPaintAttackMultiplier, 1f),
            new RelicDefinition(RelicType.RulerSight, "支配者の照準器", "領域の広がりを弩の狙いへ変える照準器です。", "塗ったエリアに応じてバリスタの攻撃力上昇（最大2倍）", "RelicRulerSight", RelicRarity.Rare, RelicEffectKind.BallistaPaintAttackMultiplier, 1f),
            new RelicDefinition(RelicType.RegeneratingWallstone, "再生する城壁石", "欠けてもゆっくり形を戻す不思議な石材です。", "壁の自動回復 +3", "RelicRegeneratingWallstone", RelicRarity.Rare, RelicEffectKind.WallAutoRegenBonus, 3f),
            new RelicDefinition(RelicType.SlayerMedal, "討伐者の勲章", "倒した敵の数だけ重みを増す勲章です。", "敵撃破100ごとに攻撃力 +1（最大+10）", "RelicSlayerMedal", RelicRarity.Rare, RelicEffectKind.KillAttackBonusPerHundred, 1f),
            new RelicDefinition(RelicType.WealthWarSeal, "富豪の戦印", "戦場で得た富を力へ変える戦印です。", "ラン中トークン10ごとに攻撃力 +1（最大+10）", "RelicWealthWarSeal", RelicRarity.Rare, RelicEffectKind.RunTokenAttackBonusPerTen, 1f)
        };

        public static RelicDefinition[] All => Definitions;

        public static bool TryGet(RelicType type, out RelicDefinition definition)
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].type != type) continue;
                definition = Definitions[i];
                return true;
            }

            definition = null;
            return false;
        }

        public static RelicDefinition Get(RelicType type)
        {
            return TryGet(type, out var definition) ? definition : null;
        }

        public static bool TryPickRandom(out RelicDefinition definition)
        {
            if (Definitions.Length == 0)
            {
                definition = null;
                return false;
            }

            RelicRarity rarity = PickAvailableRarity();
            return TryPickRandomByRarity(rarity, out definition);
        }

        public static bool TryGetFirstUnowned(out RelicDefinition definition)
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (ProgressionStore.HasRelic(Definitions[i].type)) continue;
                definition = Definitions[i];
                return true;
            }

            definition = null;
            return false;
        }

        public static int GetDuplicateTokenReward(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common:
                    return 5;
                case RelicRarity.Uncommon:
                    return 10;
                case RelicRarity.Rare:
                    return 30;
                default:
                    return 0;
            }
        }

        public static string GetRarityDisplayName(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common:
                    return "コモン";
                case RelicRarity.Uncommon:
                    return "アンコモン";
                case RelicRarity.Rare:
                    return "レア";
                default:
                    return string.Empty;
            }
        }

        static RelicRarity PickAvailableRarity()
        {
            int commonWeight = CountByRarity(RelicRarity.Common) > 0 ? CommonDropWeight : 0;
            int uncommonWeight = CountByRarity(RelicRarity.Uncommon) > 0 ? UncommonDropWeight : 0;
            int rareWeight = CountByRarity(RelicRarity.Rare) > 0 ? RareDropWeight : 0;
            int totalWeight = commonWeight + uncommonWeight + rareWeight;
            if (totalWeight <= 0) return RelicRarity.Common;

            int roll = UnityEngine.Random.Range(0, totalWeight);
            if (roll < commonWeight) return RelicRarity.Common;
            roll -= commonWeight;
            if (roll < uncommonWeight) return RelicRarity.Uncommon;
            return RelicRarity.Rare;
        }

        static bool TryPickRandomByRarity(RelicRarity rarity, out RelicDefinition definition)
        {
            int count = CountByRarity(rarity);
            if (count <= 0)
            {
                definition = null;
                return false;
            }

            int index = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].rarity != rarity) continue;
                if (index == 0)
                {
                    definition = Definitions[i];
                    return true;
                }

                index--;
            }

            definition = null;
            return false;
        }

        static int CountByRarity(RelicRarity rarity)
        {
            int count = 0;
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].rarity == rarity) count++;
            }

            return count;
        }
    }
}
