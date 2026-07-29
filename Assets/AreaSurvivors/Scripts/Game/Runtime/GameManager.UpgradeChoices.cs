using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed partial class GameManager
    {        List<RunUpgradeChoice> RollUpgrades()
        {
            var pool = new List<RunUpgradeChoice>();
            var weapon = Player != null ? Player.weapon : null;
            var evolutionChoices = new List<RunUpgradeChoice>();
            if (weapon != null)
            {
                bool canAcquireNewWeapon = weapon.HasOpenWeaponSlot;
                if (canAcquireNewWeapon)
                {
                    if (!weapon.SlashUnlocked)
                    {
                        pool.Add(RunUpgradeChoice.NewWeapon(WeaponType.Slash, () => weapon.UnlockSlash()));
                    }

                    foreach (var weaponType in WeaponCatalog.UnlockableWeapons)
                    {
                        if (weapon.IsWeaponUnlocked(weaponType)) continue;
                        if (!ProgressionStore.IsUnlocked(WeaponCatalog.UnlockUpgrade(weaponType))) continue;
                        var capturedType = weaponType;
                        pool.Add(RunUpgradeChoice.NewWeapon(capturedType, () => weapon.UnlockWeapon(capturedType)));
                    }
                }

                if (weapon.CanEvolveSlash)
                {
                    evolutionChoices.Add(RunUpgradeChoice.Evolution(WeaponType.SwordRush, () => weapon.EvolveSlash()));
                }
                if (weapon.CanEvolveBoomerangSword)
                {
                    evolutionChoices.Add(RunUpgradeChoice.Evolution(WeaponType.Banana, () => weapon.EvolveBoomerangSword()));
                }
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.AuraSword, WeaponType.Excalibur);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Arrow, WeaponType.GoldenBow);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.ArrowRain, WeaponType.ArrowShower);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Gun, WeaponType.MachineGun);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Fireball, WeaponType.FireMissile);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Frost, WeaponType.FrostStorm);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.ThunderBall, WeaponType.ThunderStorm);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Shield, WeaponType.DualShield);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Flag, WeaponType.GoddessBlessing);

                if (weapon.SlashUnlocked) AddSlashUpgradeChoices(pool, weapon);
                if (weapon.ArrowUnlocked) AddArrowUpgradeChoices(pool, weapon);
                if (weapon.FireballUnlocked) AddFireballUpgradeChoices(pool, weapon);
                if (weapon.ShieldUnlocked) AddShieldUpgradeChoices(pool, weapon);
                foreach (var weaponType in WeaponCatalog.UnlockableWeapons)
                {
                    if (!WeaponCatalog.IsAdvanced(weaponType) || !weapon.IsWeaponUnlocked(weaponType)) continue;
                    AddAdvancedWeaponUpgradeChoices(pool, weapon, weaponType);
                }
            }

            var result = new List<RunUpgradeChoice>();
            for (int i = 0; i < evolutionChoices.Count && result.Count < 3; i++) result.Add(evolutionChoices[i]);
            while (result.Count < 3 && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }

        static void AddEvolutionChoice(List<RunUpgradeChoice> choices, WeaponController weapon, WeaponType sourceType, WeaponType evolutionType)
        {
            if (weapon == null || !weapon.CanEvolveWeapon(sourceType)) return;
            choices.Add(RunUpgradeChoice.Evolution(evolutionType, () => weapon.EvolveWeapon(sourceType)));
        }

        RunUpgradeChoice CreateDiminishingAdditiveChoice(
            WeaponController weapon,
            WeaponType sourceType,
            WeaponType displayType,
            RunWeaponUpgradeStat stat,
            string label,
            float currentValue,
            float baseAmount,
            string iconResource,
            Func<float, string> formatter,
            Action<float> apply)
        {
            float amount = weapon.GetDiminishedAdditiveUpgrade(sourceType, stat, baseAmount);
            return new RunUpgradeChoice(
                displayType,
                label + formatter(currentValue) + ">" + formatter(currentValue + amount),
                iconResource,
                () => apply(amount),
                stat);
        }

        RunUpgradeChoice CreateDiminishingCooldownChoice(
            WeaponController weapon,
            WeaponType sourceType,
            WeaponType displayType,
            string label,
            float currentValue,
            float baseMultiplier,
            Action<float> apply)
        {
            float multiplier = weapon.GetDiminishedCooldownMultiplier(sourceType, baseMultiplier);
            return new RunUpgradeChoice(
                displayType,
                label + Seconds(currentValue) + ">" + Seconds(currentValue * multiplier),
                StatIconCatalog.Cooldown,
                () => apply(multiplier),
                RunWeaponUpgradeStat.Cooldown);
        }

        void AddSlashUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.SlashStats;
            var displayType = weapon.GetDisplayWeaponType(WeaponType.Slash);
            int attackBonus = config.GetRunAttackPowerBonus(WeaponType.Slash);
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃力 " + weapon.SlashAttackPower + ">" + (weapon.SlashAttackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddSlashAttack(attackBonus)));
            pool.Add(CreateDiminishingCooldownChoice(
                weapon, WeaponType.Slash, displayType, "攻撃間隔 ",
                stats.cooldownSeconds, config.runAttackCooldownMultiplier, weapon.MultiplySlashCooldown));
            pool.Add(CreateDiminishingAdditiveChoice(
                weapon, WeaponType.Slash, displayType, RunWeaponUpgradeStat.Knockback, "ノックバック ",
                stats.knockback, config.runWeaponKnockbackBonus, StatIconCatalog.Knockback, Number, weapon.AddSlashKnockback));
            pool.Add(CreateDiminishingAdditiveChoice(
                weapon, WeaponType.Slash, displayType, RunWeaponUpgradeStat.Range, "攻撃範囲 ",
                stats.range, config.runMediumRangeBonus, StatIconCatalog.Range, Number, weapon.AddSlashRange));
        }

        void AddArrowUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.ArrowStats;
            var displayType = weapon.GetDisplayWeaponType(WeaponType.Arrow);
            int attackBonus = config.GetRunAttackPowerBonus(WeaponType.Arrow);
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddArrowAttack(attackBonus)));
            pool.Add(CreateDiminishingCooldownChoice(
                weapon, WeaponType.Arrow, displayType, "攻撃間隔 ",
                stats.cooldownSeconds, config.runAttackCooldownMultiplier, weapon.MultiplyArrowCooldown));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "矢の本数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus),
                StatIconCatalog.Projectile,
                () => weapon.AddArrowProjectileCount(config.runProjectileCountBonus)));
            pool.Add(CreateDiminishingAdditiveChoice(
                weapon, WeaponType.Arrow, displayType, RunWeaponUpgradeStat.ProjectileRange, "射程 ",
                stats.range, config.runProjectileRangeBonus, StatIconCatalog.Range, Number, weapon.AddArrowRange));
        }

        void AddFireballUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.FireballStats;
            var displayType = weapon.GetDisplayWeaponType(WeaponType.Fireball);
            int attackBonus = config.GetRunAttackPowerBonus(WeaponType.Fireball);
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddFireballAttack(attackBonus)));
            pool.Add(CreateDiminishingCooldownChoice(
                weapon, WeaponType.Fireball, displayType, "攻撃間隔 ",
                stats.cooldownSeconds, config.runAttackCooldownMultiplier, weapon.MultiplyFireballCooldown));
            pool.Add(CreateDiminishingAdditiveChoice(
                weapon, WeaponType.Fireball, displayType, RunWeaponUpgradeStat.ExplosionRange, "爆発範囲 ",
                stats.explosionRadius, config.runExplosionRadiusBonus, StatIconCatalog.Range, Number, weapon.AddFireballExplosionRadius));
            pool.Add(CreateDiminishingAdditiveChoice(
                weapon, WeaponType.Fireball, displayType, RunWeaponUpgradeStat.ProjectileRange, "射程 ",
                weapon.FireballRange, config.runProjectileRangeBonus, StatIconCatalog.Range, Number, weapon.AddFireballRange));
        }

        void AddShieldUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.ShieldStats;
            var displayType = weapon.GetDisplayWeaponType(WeaponType.Shield);
            int attackBonus = config.GetRunAttackPowerBonus(WeaponType.Shield);
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddShieldAttack(attackBonus)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "シールド数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus),
                StatIconCatalog.Defense,
                () => weapon.AddShieldCount(config.runProjectileCountBonus)));
            pool.Add(CreateDiminishingAdditiveChoice(
                weapon, WeaponType.Shield, displayType, RunWeaponUpgradeStat.Knockback, "ノックバック ",
                stats.knockback, config.runWeaponKnockbackBonus, StatIconCatalog.Knockback, Number, weapon.AddShieldKnockback));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "回転速度 " + Number(stats.rotationSpeed) + ">" + Number(stats.rotationSpeed + config.runShieldRotationSpeedBonus),
                StatIconCatalog.MoveSpeed,
                () => weapon.AddShieldRotationSpeed(config.runShieldRotationSpeedBonus)));
        }

        void AddAdvancedWeaponUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon, WeaponType type)
        {
            var stats = weapon.GetWeaponStatsFor(type);
            var displayType = weapon.GetDisplayWeaponType(type);
            int attackBonus = config.GetRunAttackPowerBonus(type);
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddWeaponAttack(type, attackBonus)));

            switch (type)
            {
                case WeaponType.Flag:
                    pool.Add(CreateDiminishingAdditiveChoice(
                        weapon, type, displayType, RunWeaponUpgradeStat.Range, "攻撃範囲 ",
                        stats.range, config.runAreaRangeBonus, StatIconCatalog.Range, Number,
                        amount => weapon.AddWeaponRange(type, amount)));
                    pool.Add(CreateDiminishingAdditiveChoice(
                        weapon, type, displayType, RunWeaponUpgradeStat.Slow, "速度低下 ",
                        stats.slowAmount, config.runSlowBonus, StatIconCatalog.MoveSpeed, Percent,
                        amount => weapon.AddWeaponSlow(type, amount)));
                    pool.Add(CreateDiminishingCooldownChoice(
                        weapon, type, displayType, "攻撃間隔 ",
                        stats.damageIntervalSeconds, config.runAttackCooldownMultiplier,
                        multiplier => weapon.MultiplyWeaponDamageInterval(type, multiplier)));
                    break;
                case WeaponType.BoomerangSword:
                    pool.Add(new RunUpgradeChoice(displayType, "剣本数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, config.runProjectileCountBonus)));
                    pool.Add(CreateDiminishingAdditiveChoice(
                        weapon, type, displayType, RunWeaponUpgradeStat.Range, "攻撃範囲 ",
                        stats.range, config.runMediumRangeBonus, StatIconCatalog.Range, Number,
                        amount => weapon.AddWeaponRange(type, amount)));
                    pool.Add(CreateDiminishingCooldownChoice(
                        weapon, type, displayType, "攻撃間隔 ",
                        stats.cooldownSeconds, config.runAttackCooldownMultiplier,
                        multiplier => weapon.MultiplyWeaponCooldown(type, multiplier)));
                    break;
                case WeaponType.AuraSword:
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃回数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, config.runProjectileCountBonus)));
                    pool.Add(CreateDiminishingAdditiveChoice(
                        weapon, type, displayType, RunWeaponUpgradeStat.Range, "攻撃範囲 ",
                        stats.range, config.runAreaRangeBonus, StatIconCatalog.Range, Number,
                        amount => weapon.AddWeaponRange(type, amount)));
                    pool.Add(CreateDiminishingAdditiveChoice(
                        weapon, type, displayType, RunWeaponUpgradeStat.ProjectileRange, "攻撃距離 ",
                        stats.distance, config.runProjectileRangeBonus, StatIconCatalog.Range, Number,
                        amount => weapon.AddWeaponDistance(type, amount)));
                    break;
                case WeaponType.ArrowRain:
                    pool.Add(CreateDiminishingAdditiveChoice(
                        weapon, type, displayType, RunWeaponUpgradeStat.Range, "攻撃範囲 ",
                        stats.range, config.runMediumRangeBonus, StatIconCatalog.Range, Number,
                        amount => weapon.AddWeaponRange(type, amount)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃時間 " + Seconds(stats.durationSeconds) + ">" + Seconds(stats.durationSeconds + config.runArrowRainDurationBonus), StatIconCatalog.Cooldown, () => weapon.AddWeaponDuration(type, config.runArrowRainDurationBonus)));
                    pool.Add(CreateDiminishingCooldownChoice(
                        weapon, type, displayType, "攻撃間隔 ",
                        stats.cooldownSeconds, config.runAttackCooldownMultiplier,
                        multiplier => weapon.MultiplyWeaponCooldown(type, multiplier)));
                    break;
                case WeaponType.Gun:
                    pool.Add(CreateDiminishingCooldownChoice(
                        weapon, type, displayType, "攻撃間隔 ",
                        stats.cooldownSeconds, config.runAttackCooldownMultiplier,
                        multiplier => weapon.MultiplyWeaponCooldown(type, multiplier)));
                    pool.Add(CreateDiminishingAdditiveChoice(
                        weapon, type, displayType, RunWeaponUpgradeStat.ProjectileRange, "攻撃距離 ",
                        stats.distance, config.runProjectileRangeBonus, StatIconCatalog.Range, Number,
                        amount => weapon.AddWeaponDistance(type, amount)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃回数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, config.runProjectileCountBonus)));
                    break;
                case WeaponType.Frost:
                    pool.Add(CreateDiminishingAdditiveChoice(
                        weapon, type, displayType, RunWeaponUpgradeStat.Range, "攻撃範囲 ",
                        stats.range, config.runAreaRangeBonus, StatIconCatalog.Range, Number,
                        amount => weapon.AddWeaponRange(type, amount)));
                    pool.Add(CreateDiminishingAdditiveChoice(
                        weapon, type, displayType, RunWeaponUpgradeStat.Slow, "速度低下 ",
                        stats.slowAmount, config.runSlowBonus, StatIconCatalog.MoveSpeed, Percent,
                        amount => weapon.AddWeaponSlow(type, amount)));
                    pool.Add(CreateDiminishingCooldownChoice(
                        weapon, type, displayType, "攻撃間隔 ",
                        stats.cooldownSeconds, config.runAttackCooldownMultiplier,
                        multiplier => weapon.MultiplyWeaponCooldown(type, multiplier)));
                    break;
                case WeaponType.ThunderBall:
                    pool.Add(CreateDiminishingAdditiveChoice(
                        weapon, type, displayType, RunWeaponUpgradeStat.Range, "攻撃範囲 ",
                        stats.range, config.runAreaRangeBonus, StatIconCatalog.Range, Number,
                        amount => weapon.AddWeaponRange(type, amount)));
                    pool.Add(new RunUpgradeChoice(displayType, "弾数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, config.runProjectileCountBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "持続時間 " + Seconds(stats.durationSeconds) + ">" + Seconds(stats.durationSeconds + config.runThunderBallDurationBonus), StatIconCatalog.Cooldown, () => weapon.AddWeaponDuration(type, config.runThunderBallDurationBonus)));
                    break;
            }
        }

        static string Number(float value)
        {
            return value.ToString("0.##");
        }

        static string Seconds(float value)
        {
            return value.ToString("0.##") + "s";
        }

        static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

    }
}
