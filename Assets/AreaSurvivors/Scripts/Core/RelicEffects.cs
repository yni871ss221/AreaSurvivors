using UnityEngine;

namespace AreaSurvivors
{
    public static class RelicEffects
    {
        public static float AttackMultiplier => 1f + Sum(RelicEffectKind.AttackMultiplier);
        public static int MaxHpBonus => Mathf.RoundToInt(Sum(RelicEffectKind.MaxHpBonus));
        public static float MoveSpeedMultiplier => 1f + Sum(RelicEffectKind.MoveSpeedMultiplier);
        public static float XpGainMultiplier => 1f + Sum(RelicEffectKind.XpGainMultiplier);
        public static float EndTokenMultiplier => 1f + Sum(RelicEffectKind.EndTokenMultiplier);
        public static int BuildingAttackBonus => Mathf.RoundToInt(Sum(RelicEffectKind.BuildingAttackBonus));
        public static int WallAutoRegenBonus => Mathf.RoundToInt(Sum(RelicEffectKind.WallAutoRegenBonus));
        public static float NormalEnemyTokenDropChance => Mathf.Clamp01(Sum(RelicEffectKind.NormalEnemyTokenDropChance));

        public static WeaponStatBlock ApplyWeaponStatBonuses(WeaponType type, WeaponStatBlock stats)
        {
            var definitions = RelicCatalog.All;
            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (!ProgressionStore.HasRelic(definition.type) || !MatchesWeapon(definition, type)) continue;
                switch (definition.effectKind)
                {
                    case RelicEffectKind.WeaponAttackBonus:
                        stats.attackPower += Mathf.RoundToInt(definition.value);
                        break;
                    case RelicEffectKind.WeaponCooldownMultiplier:
                        stats.cooldownSeconds = Mathf.Max(0.05f, stats.cooldownSeconds * Mathf.Clamp(definition.value, 0.05f, 1f));
                        break;
                    case RelicEffectKind.WeaponProjectileCountBonus:
                        stats.projectileCount = Mathf.Max(1, stats.projectileCount + Mathf.RoundToInt(definition.value));
                        break;
                    case RelicEffectKind.WeaponRangeBonus:
                        stats.range += Mathf.Max(0f, definition.value);
                        break;
                    case RelicEffectKind.WeaponDurationBonus:
                        stats.durationSeconds += Mathf.Max(0f, definition.value);
                        break;
                }
            }

            stats.attackPower = Mathf.Max(0, Mathf.RoundToInt(stats.attackPower * AttackMultiplier));
            return stats;
        }

        public static WeaponStatBlock ApplyConditionalWeaponBonuses(
            WeaponType type,
            WeaponStatBlock stats,
            WeaponController weapon,
            PlayerController player,
            TileGrid grid,
            GameManager gameManager)
        {
            stats.attackPower += KillAttackBonus(gameManager);
            stats.attackPower += RunTokenAttackBonus(gameManager);
            stats.attackPower += TripleCategoryAttackBonus(weapon, RelicType.TriBladeCrest, WeaponAttributeType.Melee, RelicEffectKind.TripleMeleeAttackBonus);
            stats.attackPower += TripleCategoryAttackBonus(weapon, RelicType.StarbowSightCrown, WeaponAttributeType.Ranged, RelicEffectKind.TripleRangedAttackBonus);
            stats.attackPower += TripleCategoryAttackBonus(weapon, RelicType.TriSageCrystal, WeaponAttributeType.Magic, RelicEffectKind.TripleMagicAttackBonus);
            stats.attackPower += TotalKillAttackBonus();

            float multiplier = 1f;
            if (Has(RelicType.HarmonyCrest) && HasThreeDistinctWeaponCategories(weapon)) multiplier += 0.15f;
            if (Has(RelicType.UnwoundedVowSeal) && IsPlayerAtFullHp(player)) multiplier += 0.1f;
            if (Has(RelicType.SolitaryBlade) && weapon != null && weapon.AcquiredWeaponOrder.Count == 1) multiplier *= 2f;

            stats.attackPower = Mathf.Max(0, Mathf.RoundToInt(stats.attackPower * multiplier));
            return stats;
        }

        public static int ApplyCenterTowerDamage(int baseDamage, TileGrid grid)
        {
            float multiplier = Has(RelicType.DominionCrown) ? 1f + PlayerControlRatio(grid) : 1f;
            return Mathf.Max(0, Mathf.RoundToInt((baseDamage + BuildingAttackBonus) * multiplier));
        }

        public static int ApplyBallistaDamage(int baseDamage, TileGrid grid)
        {
            float multiplier = Has(RelicType.RulerSight) ? 1f + PlayerControlRatio(grid) : 1f;
            return Mathf.Max(0, Mathf.RoundToInt((baseDamage + BuildingAttackBonus) * multiplier));
        }

        public static bool Has(RelicType type)
        {
            return ProgressionStore.HasRelic(type);
        }

        public static bool IsActive(
            RelicDefinition definition,
            WeaponController weapon,
            PlayerController player,
            TileGrid grid,
            GameManager gameManager)
        {
            if (definition == null || !Has(definition.type)) return false;
            switch (definition.effectKind)
            {
                case RelicEffectKind.AttackMultiplier:
                case RelicEffectKind.WeaponAttackBonus:
                case RelicEffectKind.WeaponCooldownMultiplier:
                case RelicEffectKind.WeaponProjectileCountBonus:
                case RelicEffectKind.WeaponRangeBonus:
                case RelicEffectKind.WeaponDurationBonus:
                    return HasMatchingAcquiredWeapon(definition, weapon);
                case RelicEffectKind.DistinctWeaponCategoryAttackMultiplier:
                    return HasThreeDistinctWeaponCategories(weapon);
                case RelicEffectKind.FullHpAttackMultiplier:
                    return IsPlayerAtFullHp(player);
                case RelicEffectKind.SingleWeaponAttackMultiplier:
                    return weapon != null && weapon.AcquiredWeaponOrder.Count == 1;
                case RelicEffectKind.TripleMeleeAttackBonus:
                    return HasThreeWeaponsOfAttribute(weapon, WeaponAttributeType.Melee);
                case RelicEffectKind.TripleRangedAttackBonus:
                    return HasThreeWeaponsOfAttribute(weapon, WeaponAttributeType.Ranged);
                case RelicEffectKind.TripleMagicAttackBonus:
                    return HasThreeWeaponsOfAttribute(weapon, WeaponAttributeType.Magic);
                case RelicEffectKind.CenterTowerPaintAttackMultiplier:
                case RelicEffectKind.BallistaPaintAttackMultiplier:
                    return PlayerControlRatio(grid) > 0.001f;
                case RelicEffectKind.KillAttackBonusPerHundred:
                    return KillAttackBonus(gameManager) > 0;
                case RelicEffectKind.RunTokenAttackBonusPerTen:
                    return RunTokenAttackBonus(gameManager) > 0;
                case RelicEffectKind.TotalKillAttackBonusPerThousand:
                    return TotalKillAttackBonus() > 0;
                default:
                    return true;
            }
        }

        static float Sum(RelicEffectKind kind)
        {
            float total = 0f;
            var definitions = RelicCatalog.All;
            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition.effectKind == kind && ProgressionStore.HasRelic(definition.type))
                {
                    total += definition.value;
                }
            }

            return total;
        }

        static bool MatchesWeapon(RelicDefinition definition, WeaponType type)
        {
            if (definition.targetAttribute != WeaponAttributeType.None)
            {
                return WeaponAttributeCatalog.ForWeapon(type) == definition.targetAttribute;
            }

            return definition.targetWeapon == type;
        }

        static bool HasMatchingAcquiredWeapon(RelicDefinition definition, WeaponController weapon)
        {
            if (definition == null || weapon == null || weapon.AcquiredWeaponOrder == null) return false;
            if (definition.effectKind == RelicEffectKind.AttackMultiplier) return weapon.AcquiredWeaponOrder.Count > 0;
            for (int i = 0; i < weapon.AcquiredWeaponOrder.Count; i++)
            {
                if (MatchesWeapon(definition, weapon.AcquiredWeaponOrder[i])) return true;
            }

            return false;
        }

        static bool HasThreeDistinctWeaponCategories(WeaponController weapon)
        {
            if (weapon == null || weapon.AcquiredWeaponOrder.Count != 3) return false;
            bool hasMelee = false;
            bool hasRanged = false;
            bool hasMagic = false;
            bool hasDefense = false;
            for (int i = 0; i < weapon.AcquiredWeaponOrder.Count; i++)
            {
                switch (WeaponAttributeCatalog.ForWeapon(weapon.AcquiredWeaponOrder[i]))
                {
                    case WeaponAttributeType.Melee:
                        hasMelee = true;
                        break;
                    case WeaponAttributeType.Ranged:
                        hasRanged = true;
                        break;
                    case WeaponAttributeType.Magic:
                        hasMagic = true;
                        break;
                    case WeaponAttributeType.Defense:
                        hasDefense = true;
                        break;
                }
            }

            int distinct = 0;
            if (hasMelee) distinct++;
            if (hasRanged) distinct++;
            if (hasMagic) distinct++;
            if (hasDefense) distinct++;
            return distinct == 3;
        }

        static bool HasThreeWeaponsOfAttribute(WeaponController weapon, WeaponAttributeType attribute)
        {
            if (weapon == null || weapon.AcquiredWeaponOrder == null || weapon.AcquiredWeaponOrder.Count != 3) return false;
            for (int i = 0; i < weapon.AcquiredWeaponOrder.Count; i++)
            {
                if (WeaponAttributeCatalog.ForWeapon(weapon.AcquiredWeaponOrder[i]) != attribute) return false;
            }

            return true;
        }

        static bool IsPlayerAtFullHp(PlayerController player)
        {
            var health = player != null ? player.Health : null;
            return health != null && health.currentHp >= health.maxHp;
        }

        static int KillAttackBonus(GameManager gameManager)
        {
            if (!Has(RelicType.SlayerMedal) || gameManager == null) return 0;
            return Mathf.Clamp(gameManager.Kills / 100, 0, 10);
        }

        static int RunTokenAttackBonus(GameManager gameManager)
        {
            if (!Has(RelicType.WealthWarSeal) || gameManager == null) return 0;
            return Mathf.Clamp(gameManager.RunTokens / 10, 0, 10);
        }

        static int TripleCategoryAttackBonus(
            WeaponController weapon,
            RelicType relicType,
            WeaponAttributeType attribute,
            RelicEffectKind effectKind)
        {
            if (!Has(relicType) || !HasThreeWeaponsOfAttribute(weapon, attribute)) return 0;
            return Mathf.RoundToInt(Sum(effectKind));
        }

        static int TotalKillAttackBonus()
        {
            if (!Has(RelicType.ThousandSlayerLaurel)) return 0;
            return Mathf.Clamp(ProgressionStore.Data.totalKills / 1000, 0, 20);
        }

        static float PlayerControlRatio(TileGrid grid)
        {
            return grid != null ? Mathf.Clamp01(grid.GetPlayerControlRatio()) : 0f;
        }
    }
}
