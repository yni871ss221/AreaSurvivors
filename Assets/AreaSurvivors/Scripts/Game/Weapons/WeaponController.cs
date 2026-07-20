using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class WeaponController : MonoBehaviour
    {
        public GameObject arrowPrefab;
        public GameObject goldenArrowPrefab;
        public GameObject fireballPrefab;
        public GameObject fireMissilePrefab;
        public GameObject slashPrefab;
        public GameObject swordRushSlashPrefab;
        public Transform slashOrigin;
        public const int MaxEquippedWeapons = 3;
        const float FireballProjectileVisualScale = 0.38f;
        public const float SlashRangeUpgradeAmount = 0.2f;
        public const float SlashKnockbackUpgradeAmount = 1f;
        public const float ProjectileRangeUpgradeAmount = 0.75f;
        public const float FireballExplosionUpgradeAmount = 0.25f;
        public const float ShieldKnockbackUpgradeAmount = 1f;
        public const float ShieldRotationSpeedUpgradeAmount = 20f;
        public const int EvolutionTestUpgradeCount = 2;
        public const float ExcaliburBaseRangeMultiplier = 1f;
        GameConfig config;
        PlayerController player;
        TileGrid grid;
        ShieldOrbitController shieldOrbit;
        WeaponStatBlock slashStats;
        WeaponStatBlock arrowStats;
        WeaponStatBlock fireballStats;
        WeaponStatBlock shieldStats;
        AdvancedWeaponRuntime advancedRuntime;
        bool runtimeStopped;
        float nextArrowVolleyAt;
        readonly HashSet<WeaponType> evolvedWeapons = new HashSet<WeaponType>();
        int slashLevel = 1;
        int arrowLevel;
        int fireballLevel;
        int shieldLevel;
        readonly Dictionary<WeaponType, int> advancedWeaponLevels = new Dictionary<WeaponType, int>();
        readonly Dictionary<WeaponType, int> runWeaponDisplayLevels = new Dictionary<WeaponType, int>();
        readonly Dictionary<WeaponType, WeaponRunUpgradeState> advancedWeaponUpgrades = new Dictionary<WeaponType, WeaponRunUpgradeState>();
        readonly List<WeaponType> acquiredWeaponOrder = new List<WeaponType>();
        int slashAttackBonus;
        int arrowAttackBonus;
        int fireballAttackBonus;
        int shieldAttackBonus;
        float slashCooldownMultiplier = 1f;
        float arrowCooldownMultiplier = 1f;
        readonly Dictionary<WeaponType, int> testStatLevelOverrides = new Dictionary<WeaponType, int>();
        float fireballCooldownMultiplier = 1f;
        float slashKnockbackBonus;
        float slashRangeBonus;
        int arrowProjectileCountBonus;
        float arrowRangeBonus;
        float fireballExplosionRadiusBonus;
        float fireballRangeBonus;
        int shieldCountBonus;
        float shieldKnockbackBonus;
        float shieldRotationSpeedBonus;
        public int WeaponLevel => slashLevel;
        public int SlashLevel => slashLevel;
        public int ArrowLevel => arrowLevel;
        public int FireballLevel => fireballLevel;
        public int ShieldLevel => shieldLevel;
        public bool SlashUnlocked => slashLevel > 0;
        public bool ArrowUnlocked => arrowLevel > 0;
        public bool FireballUnlocked => fireballLevel > 0;
        public bool ShieldUnlocked => shieldLevel > 0;
        public bool SlashEvolved => IsEvolved(WeaponType.Slash);
        public bool BoomerangSwordEvolved => IsEvolved(WeaponType.BoomerangSword);
        public bool CanEvolveSlash => CanEvolveWeapon(WeaponType.Slash);
        public bool CanEvolveBoomerangSword => CanEvolveWeapon(WeaponType.BoomerangSword);
        public bool CanEvolveAuraSword => CanEvolveWeapon(WeaponType.AuraSword);
        public bool CanEvolveArrow => CanEvolveWeapon(WeaponType.Arrow);
        public bool CanEvolveArrowRain => CanEvolveWeapon(WeaponType.ArrowRain);
        public bool CanEvolveGun => CanEvolveWeapon(WeaponType.Gun);
        public bool CanEvolveFireball => CanEvolveWeapon(WeaponType.Fireball);
        public bool CanEvolveFrost => CanEvolveWeapon(WeaponType.Frost);
        public bool CanEvolveThunderBall => CanEvolveWeapon(WeaponType.ThunderBall);
        public bool CanEvolveShield => CanEvolveWeapon(WeaponType.Shield);
        public bool CanEvolveFlag => CanEvolveWeapon(WeaponType.Flag);
        public bool CanLevelUp => CanLevelUpSlash;
        public bool CanLevelUpSlash => SlashUnlocked && slashLevel < GameConfig.MaxWeaponLevel;
        public bool CanLevelUpArrow => arrowLevel < GameConfig.MaxWeaponLevel;
        public bool CanLevelUpFireball => fireballLevel < GameConfig.MaxWeaponLevel;
        public bool CanLevelUpShield => shieldLevel < GameConfig.MaxWeaponLevel;
        public int AttackPower => slashStats.attackPower;
        public float CurrentCooldown => slashStats.cooldownSeconds;
        public float ProjectileSpeed => Mathf.Max(arrowStats.projectileSpeed, fireballStats.projectileSpeed);
        public float WeaponRange => Mathf.Max(slashStats.range, Mathf.Max(arrowStats.range, fireballStats.range));
        public float Knockback => slashStats.knockback;
        public WeaponStatBlock SlashStats => slashStats;
        public WeaponStatBlock ArrowStats => arrowStats;
        public WeaponStatBlock FireballStats => fireballStats;
        public WeaponStatBlock ShieldStats => shieldStats;
        public IReadOnlyList<WeaponType> AcquiredWeaponOrder => acquiredWeaponOrder;
        public bool HasOpenWeaponSlot => acquiredWeaponOrder.Count < MaxEquippedWeapons;
        public int SlashAttackPower => EffectiveSlashStats.attackPower + (config != null ? config.slashDamageBonus : 0);
        public float FireballRange => FireballFlightRange(fireballStats);
        public bool AreaControlSpecialActive => IsPlayerAreaControlSpecialActive();
        public WeaponStatBlock EffectiveSlashStats => ApplySlashSpecialEffect(slashStats);
        public WeaponStatBlock EffectiveArrowStats => ApplyArrowSpecialEffect(arrowStats);
        public WeaponStatBlock EffectiveFireballStats => ApplyFireballSpecialEffect(fireballStats);
        public WeaponStatBlock EffectiveShieldStats => ApplyShieldSpecialEffect(shieldStats);

        public void Configure(GameConfig gameConfig, PlayerController owner)
        {
            config = gameConfig;
            player = owner;
            grid = FindObjectOfType<TileGrid>();
            if (config != null) config.EnsureWeaponLevelDefaults();
            slashLevel = ProgressionStore.IsUnlocked(UpgradeType.RemoveStartingSlash)
                ? 0
                : Mathf.Clamp(1 + ProgressionStore.GetLevel(UpgradeType.StartingWeaponLevel), 1, GameConfig.MaxWeaponLevel);
            arrowLevel = 0;
            fireballLevel = 0;
            shieldLevel = 0;
            evolvedWeapons.Clear();
            advancedWeaponLevels.Clear();
            runWeaponDisplayLevels.Clear();
            shieldOrbit = GetComponent<ShieldOrbitController>();
            if (shieldOrbit != null) shieldOrbit.Configure(this, transform, config);
            advancedRuntime = GetComponent<AdvancedWeaponRuntime>();
            if (advancedRuntime != null) advancedRuntime.Configure(this, owner, gameConfig);
            runtimeStopped = false;
            nextArrowVolleyAt = 0f;
            testStatLevelOverrides.Clear();
            acquiredWeaponOrder.Clear();
            WeaponType testStartingWeapon = default;
            bool hasTestStartingWeapon = RunState.TryConsumeNextTestStartingWeapon(out testStartingWeapon);
            if (hasTestStartingWeapon)
            {
                ApplyTestStartingWeapon(testStartingWeapon);
            }
            else if (SlashUnlocked)
            {
                RegisterAcquiredWeapon(WeaponType.Slash);
            }
            ResetRunWeaponUpgrades();
            if (hasTestStartingWeapon) ApplyTestStartingWeaponProfile(testStartingWeapon);
            RefreshFromStats();
            StopAllCoroutines();
            StartCoroutine(SlashLoop());
            StartCoroutine(ArrowLoop());
            StartCoroutine(FireballLoop());
            SyncShieldOrbit();
        }

        public void StopRuntimeWeapons()
        {
            runtimeStopped = true;
            StopAllCoroutines();
            if (shieldOrbit != null) shieldOrbit.SetActive(false);
            if (advancedRuntime != null) advancedRuntime.StopRuntimeWeapons();
        }

        void ResetRunWeaponUpgrades()
        {
            slashAttackBonus = 0;
            arrowAttackBonus = 0;
            fireballAttackBonus = 0;
            shieldAttackBonus = 0;
            slashCooldownMultiplier = 1f;
            arrowCooldownMultiplier = 1f;
            fireballCooldownMultiplier = 1f;
            slashKnockbackBonus = 0f;
            slashRangeBonus = 0f;
            arrowProjectileCountBonus = 0;
            arrowRangeBonus = 0f;
            fireballExplosionRadiusBonus = 0f;
            fireballRangeBonus = 0f;
            shieldCountBonus = 0;
            shieldKnockbackBonus = 0f;
            shieldRotationSpeedBonus = 0f;
            advancedWeaponUpgrades.Clear();
        }

        public void RefreshFromStats()
        {
            if (config == null || player == null) return;
            slashStats = config.GetWeaponStats(WeaponType.Slash, ResolveTestStatLevel(WeaponType.Slash, slashLevel));
            arrowStats = config.GetWeaponStats(WeaponType.Arrow, ResolveTestStatLevel(WeaponType.Arrow, arrowLevel));
            fireballStats = config.GetWeaponStats(WeaponType.Fireball, ResolveTestStatLevel(WeaponType.Fireball, fireballLevel));
            shieldStats = config.GetWeaponStats(WeaponType.Shield, ResolveTestStatLevel(WeaponType.Shield, shieldLevel));
            ApplyRunWeaponUpgrades();
            SyncShieldOrbit();
            SyncAdvancedWeapons();
        }

        void ApplyRunWeaponUpgrades()
        {
            ApplyStandardEvolutionBaseValues(WeaponType.Slash, ref slashStats);
            slashStats.attackPower += slashAttackBonus;
            slashStats.cooldownSeconds = Mathf.Max(0.05f, slashStats.cooldownSeconds * slashCooldownMultiplier);
            slashStats.knockback += slashKnockbackBonus;
            slashStats.range += slashRangeBonus;

            ApplyStandardEvolutionBaseValues(WeaponType.Arrow, ref arrowStats);
            arrowStats.attackPower += arrowAttackBonus;
            arrowStats.cooldownSeconds = Mathf.Max(0.05f, arrowStats.cooldownSeconds * arrowCooldownMultiplier);
            arrowStats.projectileCount = Mathf.Max(1, arrowStats.projectileCount + arrowProjectileCountBonus);
            arrowStats.range += arrowRangeBonus;

            ApplyStandardEvolutionBaseValues(WeaponType.Fireball, ref fireballStats);
            fireballStats.attackPower += fireballAttackBonus;
            fireballStats.cooldownSeconds = Mathf.Max(0.05f, fireballStats.cooldownSeconds * fireballCooldownMultiplier);
            fireballStats.explosionRadius += fireballExplosionRadiusBonus;
            fireballStats.range += fireballRangeBonus;

            ApplyStandardEvolutionBaseValues(WeaponType.Shield, ref shieldStats);
            shieldStats.attackPower += shieldAttackBonus;
            shieldStats.projectileCount = Mathf.Max(1, shieldStats.projectileCount + shieldCountBonus);
            shieldStats.knockback += shieldKnockbackBonus;
            shieldStats.rotationSpeed += shieldRotationSpeedBonus;

            slashStats = RelicEffects.ApplyWeaponStatBonuses(WeaponType.Slash, slashStats);
            arrowStats = RelicEffects.ApplyWeaponStatBonuses(WeaponType.Arrow, arrowStats);
            fireballStats = RelicEffects.ApplyWeaponStatBonuses(WeaponType.Fireball, fireballStats);
            shieldStats = RelicEffects.ApplyWeaponStatBonuses(WeaponType.Shield, shieldStats);
        }

        void ApplyStandardEvolutionBaseValues(WeaponType type, ref WeaponStatBlock stats)
        {
            if (!IsEvolved(type) || config == null) return;

            var baseStats = config.GetWeaponStats(type, 1);
            switch (type)
            {
                case WeaponType.Slash:
                    int slashBaseFinalAttackPower = baseStats.attackPower + config.slashDamageBonus;
                    stats.attackPower += Mathf.Max(0, config.swordRushBaseAttackPower) - slashBaseFinalAttackPower;
                    stats.range += Mathf.Max(0f, config.swordRushBaseRange) - baseStats.range;
                    break;
                case WeaponType.Arrow:
                    stats.attackPower += baseStats.attackPower;
                    break;
                case WeaponType.Fireball:
                    stats.cooldownSeconds = ResolveEvolutionBaseCooldown(stats.cooldownSeconds,
                        baseStats.cooldownSeconds, config.fireMissileBaseCooldownMultiplier);
                    stats.range += baseStats.range;
                    break;
                case WeaponType.Shield:
                    stats.projectileCount += Mathf.Max(1, baseStats.projectileCount);
                    stats.rotationSpeed += baseStats.rotationSpeed;
                    stats.range += baseStats.range;
                    break;
            }
            stats.attackPower = Mathf.Max(0, stats.attackPower);
            stats.range = Mathf.Max(0f, stats.range);
            stats.cooldownSeconds = Mathf.Max(0.05f, stats.cooldownSeconds);
        }

        public static float ResolveEvolutionBaseCooldown(float currentCooldown, float levelOneBaseCooldown,
            float evolutionBaseMultiplier)
        {
            float baseCooldown = Mathf.Max(0.05f, levelOneBaseCooldown);
            float evolvedBaseCooldown = Mathf.Max(0.05f, baseCooldown * Mathf.Max(0f, evolutionBaseMultiplier));
            return Mathf.Max(0.05f, currentCooldown + evolvedBaseCooldown - baseCooldown);
        }

        void Update()
        {
            if (runtimeStopped) return;
            MarkActiveWeaponSlots();
            SyncShieldOrbit();
        }

        public bool LevelUp()
        {
            return LevelUpSlash();
        }

        public bool LevelUpSlash()
        {
            if (!CanLevelUp) return false;
            slashLevel++;
            RefreshFromStats();
            return true;
        }

        public bool LevelUpArrow()
        {
            if (!CanLevelUpArrow) return false;
            bool wasUnlocked = ArrowUnlocked;
            arrowLevel++;
            if (!wasUnlocked) RegisterAcquiredWeapon(WeaponType.Arrow);
            RefreshFromStats();
            return true;
        }

        public bool LevelUpFireball()
        {
            if (!CanLevelUpFireball) return false;
            bool wasUnlocked = FireballUnlocked;
            fireballLevel++;
            if (!wasUnlocked) RegisterAcquiredWeapon(WeaponType.Fireball);
            RefreshFromStats();
            return true;
        }

        public bool LevelUpShield()
        {
            if (!CanLevelUpShield) return false;
            bool wasUnlocked = ShieldUnlocked;
            shieldLevel++;
            if (!wasUnlocked) RegisterAcquiredWeapon(WeaponType.Shield);
            RefreshFromStats();
            return true;
        }

        public bool UnlockSlash()
        {
            if (SlashUnlocked) return false;
            if (!HasOpenWeaponSlot) return false;
            slashLevel = 1;
            RegisterAcquiredWeapon(WeaponType.Slash);
            RefreshFromStats();
            return true;
        }

        public bool UnlockArrow()
        {
            if (ArrowUnlocked) return false;
            if (!HasOpenWeaponSlot) return false;
            arrowLevel = 1;
            RegisterAcquiredWeapon(WeaponType.Arrow);
            RefreshFromStats();
            return true;
        }

        public bool UnlockFireball()
        {
            if (FireballUnlocked) return false;
            if (!HasOpenWeaponSlot) return false;
            fireballLevel = 1;
            RegisterAcquiredWeapon(WeaponType.Fireball);
            RefreshFromStats();
            return true;
        }

        public bool UnlockShield()
        {
            if (ShieldUnlocked) return false;
            if (!HasOpenWeaponSlot) return false;
            shieldLevel = 1;
            RegisterAcquiredWeapon(WeaponType.Shield);
            RefreshFromStats();
            return true;
        }

        public bool UnlockWeapon(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Slash: return UnlockSlash();
                case WeaponType.Arrow: return UnlockArrow();
                case WeaponType.Fireball: return UnlockFireball();
                case WeaponType.Shield: return UnlockShield();
            }

            if (!WeaponCatalog.IsAdvanced(type) || IsWeaponUnlocked(type) || !HasOpenWeaponSlot) return false;
            advancedWeaponLevels[type] = 1;
            RegisterAcquiredWeapon(type);
            RefreshFromStats();
            return true;
        }

        public bool IsWeaponUnlocked(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Slash: return SlashUnlocked;
                case WeaponType.SwordRush: return SlashEvolved;
                case WeaponType.Banana: return BoomerangSwordEvolved;
                case WeaponType.Excalibur:
                case WeaponType.GoldenBow:
                case WeaponType.ArrowShower:
                case WeaponType.MachineGun:
                case WeaponType.FireMissile:
                case WeaponType.FrostStorm:
                case WeaponType.ThunderStorm:
                case WeaponType.DualShield:
                case WeaponType.GoddessBlessing:
                    return IsEvolved(WeaponCatalog.BaseWeaponOf(type));
                case WeaponType.Arrow: return ArrowUnlocked;
                case WeaponType.Fireball: return FireballUnlocked;
                case WeaponType.Shield: return ShieldUnlocked;
                default:
                    return advancedWeaponLevels.TryGetValue(type, out var level) && level > 0;
            }
        }

        public int GetRunWeaponDisplayLevel(WeaponType type)
        {
            type = EvolutionSourceType(type);
            return runWeaponDisplayLevels.TryGetValue(type, out var level) ? Mathf.Max(1, level) : 0;
        }

        public void RegisterRunWeaponUpgrade(WeaponType type)
        {
            type = EvolutionSourceType(type);
            if (!IsWeaponUnlocked(type)) return;
            runWeaponDisplayLevels[type] = GetRunWeaponDisplayLevel(type) + 1;
        }

        public WeaponType GetDisplayWeaponType(WeaponType type)
        {
            type = EvolutionSourceType(type);
            return IsEvolved(type) ? WeaponCatalog.EvolutionOf(type) : type;
        }

        public bool EvolveSlash()
        {
            return EvolveWeapon(WeaponType.Slash);
        }

        public bool EvolveBoomerangSword()
        {
            return EvolveWeapon(WeaponType.BoomerangSword);
        }

        public bool EvolveWeapon(WeaponType sourceType)
        {
            sourceType = EvolutionSourceType(sourceType);
            if (!CanEvolveWeapon(sourceType)) return false;
            evolvedWeapons.Add(sourceType);
            RefreshFromStats();
            ProgressionStore.MarkEvolutionDiscovered(WeaponCatalog.EvolutionOf(sourceType));
            return true;
        }

        static WeaponType EvolutionSourceType(WeaponType type)
        {
            return WeaponCatalog.BaseWeaponOf(type);
        }

        bool IsEvolved(WeaponType sourceType)
        {
            return evolvedWeapons.Contains(EvolutionSourceType(sourceType));
        }

        public bool CanEvolveWeapon(WeaponType sourceType)
        {
            sourceType = EvolutionSourceType(sourceType);
            if (WeaponCatalog.EvolutionOf(sourceType) == sourceType || IsEvolved(sourceType)) return false;
            if (!IsWeaponUnlocked(sourceType)) return false;
            return IsEvolutionRequirementMet(sourceType, 0) && IsEvolutionRequirementMet(sourceType, 1);
        }

        public bool IsEvolutionRequirementMet(WeaponType sourceType, int requirementIndex)
        {
            sourceType = EvolutionSourceType(sourceType);
            if (requirementIndex == 0)
            {
                return GetRunWeaponDisplayLevel(sourceType) >= GameConfig.MaxWeaponLevel;
            }

            if (requirementIndex != 1) return false;
            var manager = GameManager.Instance;
            switch (sourceType)
            {
                case WeaponType.Slash: return ProgressionStore.Data.playCount >= 5;
                case WeaponType.BoomerangSword: return manager != null && manager.Kills >= 300;
                case WeaponType.AuraSword: return ProgressionStore.OwnedRelicCount() >= 10;
                case WeaponType.Arrow: return manager != null && manager.RunTokens >= 50;
                case WeaponType.ArrowRain: return IsPlayerAreaControlSpecialActive(0.5f);
                case WeaponType.Gun: return manager != null && manager.CurrentLevel >= 30;
                case WeaponType.Fireball: return manager != null && manager.BossActive;
                case WeaponType.Frost: return ProgressionStore.DiscoveredEvolutionCount() >= 3;
                case WeaponType.ThunderBall: return ProgressionStore.Data.totalKills + (manager != null ? manager.Kills : 0) >= 10000;
                case WeaponType.Shield:
                    var playerHealth = player != null ? player.GetComponent<Health>() : null;
                    return playerHealth != null && playerHealth.Normalized < 1f;
                case WeaponType.Flag:
                    var towerHealth = manager != null && manager.Tower != null ? manager.Tower.GetComponent<Health>() : null;
                    return towerHealth != null && towerHealth.Normalized <= 0.5f;
                default: return false;
            }
        }

        public WeaponStatBlock GetWeaponStatsFor(WeaponType type)
        {
            type = EvolutionSourceType(type);
            switch (type)
            {
                case WeaponType.Slash: return slashStats;
                case WeaponType.Arrow: return arrowStats;
                case WeaponType.Fireball: return fireballStats;
                case WeaponType.Shield: return shieldStats;
            }

            int logicalLevel = advancedWeaponLevels.TryGetValue(type, out var value) ? Mathf.Max(1, value) : 1;
            int level = ResolveTestStatLevel(type, logicalLevel);
            var stats = config != null ? config.GetWeaponStats(type, level) : default;
            return ApplyAdvancedRunUpgrades(type, stats);
        }

        public WeaponStatBlock GetEffectiveWeaponStatsFor(WeaponType type)
        {
            type = EvolutionSourceType(type);
            switch (type)
            {
                case WeaponType.Slash: return EffectiveSlashStats;
                case WeaponType.Arrow: return EffectiveArrowStats;
                case WeaponType.Fireball: return EffectiveFireballStats;
                case WeaponType.Shield: return EffectiveShieldStats;
                default:
                    var effective = ApplyAdvancedSpecialEffect(type, GetWeaponStatsFor(type));
                    if (type == WeaponType.AuraSword && GetDisplayWeaponType(type) == WeaponType.Excalibur && config != null)
                    {
                        effective.cooldownSeconds = Mathf.Max(0.05f, config.excaliburCooldownSeconds);
                    }
                    return effective;
            }
        }

        void RegisterAcquiredWeapon(WeaponType type)
        {
            if (!acquiredWeaponOrder.Contains(type))
            {
                acquiredWeaponOrder.Add(type);
                runWeaponDisplayLevels[type] = 1;
                GameManager.Instance?.RegisterWeaponSlot(type, acquiredWeaponOrder.Count - 1);
            }
        }

        void MarkActiveWeaponSlots()
        {
            if (player == null || player.IsReviving) return;
            for (int i = 0; i < acquiredWeaponOrder.Count; i++)
            {
                var type = acquiredWeaponOrder[i];
                if (IsWeaponUnlocked(type)) GameManager.Instance?.MarkWeaponActive(type);
            }
        }

        void ApplyTestStartingWeapon(WeaponType type)
        {
            bool startEvolved = WeaponCatalog.IsEvolution(type);
            WeaponType requestedType = type;
            if (startEvolved) type = WeaponCatalog.BaseWeaponOf(type);
            slashLevel = 0;
            arrowLevel = 0;
            fireballLevel = 0;
            shieldLevel = 0;
            advancedWeaponLevels.Clear();

            switch (type)
            {
                case WeaponType.Slash:
                    slashLevel = startEvolved ? GameConfig.MaxWeaponLevel : 1;
                    break;
                case WeaponType.Arrow:
                    arrowLevel = startEvolved ? GameConfig.MaxWeaponLevel : 1;
                    break;
                case WeaponType.Fireball:
                    fireballLevel = startEvolved ? GameConfig.MaxWeaponLevel : 1;
                    break;
                case WeaponType.Shield:
                    shieldLevel = startEvolved ? GameConfig.MaxWeaponLevel : 1;
                    break;
                default:
                    if (WeaponCatalog.IsAdvanced(type))
                    {
                        advancedWeaponLevels[type] = startEvolved ? GameConfig.MaxWeaponLevel : 1;
                    }
                    else
                    {
                        type = WeaponType.Slash;
                        slashLevel = 1;
                    }
                    break;
            }

            RegisterAcquiredWeapon(type);
            if (startEvolved)
            {
                evolvedWeapons.Add(type);
                runWeaponDisplayLevels[type] = GameConfig.MaxWeaponLevel;
                ProgressionStore.MarkEvolutionDiscovered(requestedType);
            }
        }

        void ApplyTestStartingWeaponProfile(WeaponType requestedType)
        {
            if (config == null || !WeaponCatalog.IsEvolution(requestedType)) return;

            WeaponType sourceType = WeaponCatalog.BaseWeaponOf(requestedType);
            testStatLevelOverrides[sourceType] = 1;
            ApplyUniformEvolutionTestUpgrades(sourceType, EvolutionTestUpgradeCount);
        }

        int ResolveTestStatLevel(WeaponType type, int logicalLevel)
        {
            return Mathf.Max(1, testStatLevelOverrides.TryGetValue(type, out var level) ? level : logicalLevel);
        }

        public static int ResolveTestStatLevel(int logicalLevel, int? testStatLevelOverride)
        {
            return Mathf.Max(1, testStatLevelOverride ?? logicalLevel);
        }

        void ApplyUniformEvolutionTestUpgrades(WeaponType type, int upgradeCount)
        {
            int count = Mathf.Max(0, upgradeCount);
            int attackBonus = Mathf.Max(1, config.runAttackPowerBonus) * count;
            float cooldownMultiplier = Mathf.Pow(Mathf.Clamp(config.runAttackCooldownMultiplier, 0.05f, 1f), count);

            switch (type)
            {
                case WeaponType.Slash:
                    slashAttackBonus += attackBonus;
                    slashCooldownMultiplier *= cooldownMultiplier;
                    slashKnockbackBonus += SlashKnockbackUpgradeAmount * count;
                    slashRangeBonus += SlashRangeUpgradeAmount * count;
                    return;
                case WeaponType.Arrow:
                    arrowAttackBonus += attackBonus;
                    arrowCooldownMultiplier *= cooldownMultiplier;
                    arrowProjectileCountBonus += count;
                    arrowRangeBonus += ProjectileRangeUpgradeAmount * count;
                    return;
                case WeaponType.Fireball:
                    fireballAttackBonus += attackBonus;
                    fireballCooldownMultiplier *= cooldownMultiplier;
                    fireballExplosionRadiusBonus += FireballExplosionUpgradeAmount * count;
                    fireballRangeBonus += ProjectileRangeUpgradeAmount * count;
                    return;
                case WeaponType.Shield:
                    shieldAttackBonus += attackBonus;
                    shieldCountBonus += count;
                    shieldKnockbackBonus += ShieldKnockbackUpgradeAmount * count;
                    shieldRotationSpeedBonus += ShieldRotationSpeedUpgradeAmount * count;
                    return;
            }

            var upgrade = GetAdvancedUpgrade(type);
            upgrade.attackBonus += attackBonus;
            switch (type)
            {
                case WeaponType.Flag:
                    upgrade.rangeBonus += ProjectileRangeUpgradeAmount * count;
                    upgrade.slowBonus += 0.05f * count;
                    upgrade.damageIntervalMultiplier *= cooldownMultiplier;
                    break;
                case WeaponType.BoomerangSword:
                    upgrade.projectileCountBonus += count;
                    upgrade.rangeBonus += SlashRangeUpgradeAmount * count;
                    upgrade.cooldownMultiplier *= cooldownMultiplier;
                    break;
                case WeaponType.AuraSword:
                    upgrade.projectileCountBonus += count;
                    upgrade.rangeBonus += ProjectileRangeUpgradeAmount * count;
                    upgrade.distanceBonus += ProjectileRangeUpgradeAmount * count;
                    break;
                case WeaponType.ArrowRain:
                    upgrade.rangeBonus += ProjectileRangeUpgradeAmount * count;
                    upgrade.durationBonus += 0.4f * count;
                    upgrade.cooldownMultiplier *= cooldownMultiplier;
                    break;
                case WeaponType.Gun:
                    upgrade.cooldownMultiplier *= cooldownMultiplier;
                    upgrade.distanceBonus += ProjectileRangeUpgradeAmount * count;
                    upgrade.projectileCountBonus += count;
                    break;
                case WeaponType.Frost:
                    upgrade.rangeBonus += ProjectileRangeUpgradeAmount * count;
                    upgrade.slowBonus += 0.05f * count;
                    upgrade.cooldownMultiplier *= cooldownMultiplier;
                    break;
                case WeaponType.ThunderBall:
                    upgrade.rangeBonus += ProjectileRangeUpgradeAmount * count;
                    upgrade.projectileCountBonus += count;
                    upgrade.durationBonus += 0.5f * count;
                    break;
            }
        }

        public void AddSlashAttack(int amount)
        {
            slashAttackBonus += Mathf.Max(0, amount);
            RefreshFromStats();
        }

        public void MultiplySlashCooldown(float multiplier)
        {
            slashCooldownMultiplier *= Mathf.Clamp(multiplier, 0.05f, 1f);
            RefreshFromStats();
        }

        public void AddSlashKnockback(float amount)
        {
            slashKnockbackBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void AddSlashRange(float amount)
        {
            slashRangeBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void AddArrowAttack(int amount)
        {
            arrowAttackBonus += Mathf.Max(0, amount);
            RefreshFromStats();
        }

        public void MultiplyArrowCooldown(float multiplier)
        {
            arrowCooldownMultiplier *= Mathf.Clamp(multiplier, 0.05f, 1f);
            RefreshFromStats();
        }

        public void AddArrowProjectileCount(int amount)
        {
            arrowProjectileCountBonus += Mathf.Max(0, amount);
            RefreshFromStats();
        }

        public void AddArrowRange(float amount)
        {
            arrowRangeBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void AddFireballAttack(int amount)
        {
            fireballAttackBonus += Mathf.Max(0, amount);
            RefreshFromStats();
        }

        public void MultiplyFireballCooldown(float multiplier)
        {
            fireballCooldownMultiplier *= Mathf.Clamp(multiplier, 0.05f, 1f);
            RefreshFromStats();
        }

        public void AddFireballExplosionRadius(float amount)
        {
            fireballExplosionRadiusBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void AddFireballRange(float amount)
        {
            fireballRangeBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void AddShieldAttack(int amount)
        {
            shieldAttackBonus += Mathf.Max(0, amount);
            RefreshFromStats();
        }

        public void AddShieldCount(int amount)
        {
            shieldCountBonus += Mathf.Max(0, amount);
            RefreshFromStats();
        }

        public void AddShieldKnockback(float amount)
        {
            shieldKnockbackBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void AddShieldRotationSpeed(float amount)
        {
            shieldRotationSpeedBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void AddWeaponAttack(WeaponType type, int amount)
        {
            if (type == WeaponType.Slash) { AddSlashAttack(amount); return; }
            if (type == WeaponType.Arrow) { AddArrowAttack(amount); return; }
            if (type == WeaponType.Fireball) { AddFireballAttack(amount); return; }
            if (type == WeaponType.Shield) { AddShieldAttack(amount); return; }
            GetAdvancedUpgrade(type).attackBonus += Mathf.Max(0, amount);
            RefreshFromStats();
        }

        public void MultiplyWeaponCooldown(WeaponType type, float multiplier)
        {
            if (type == WeaponType.Slash) { MultiplySlashCooldown(multiplier); return; }
            if (type == WeaponType.Arrow) { MultiplyArrowCooldown(multiplier); return; }
            if (type == WeaponType.Fireball) { MultiplyFireballCooldown(multiplier); return; }
            GetAdvancedUpgrade(type).cooldownMultiplier *= Mathf.Clamp(multiplier, 0.05f, 1f);
            RefreshFromStats();
        }

        public void AddWeaponCount(WeaponType type, int amount)
        {
            if (type == WeaponType.Arrow) { AddArrowProjectileCount(amount); return; }
            if (type == WeaponType.Shield) { AddShieldCount(amount); return; }
            GetAdvancedUpgrade(type).projectileCountBonus += Mathf.Max(0, amount);
            RefreshFromStats();
        }

        public void AddWeaponRange(WeaponType type, float amount)
        {
            if (type == WeaponType.Slash) { AddSlashRange(amount); return; }
            if (type == WeaponType.Arrow) { AddArrowRange(amount); return; }
            if (type == WeaponType.Fireball) { AddFireballRange(amount); return; }
            GetAdvancedUpgrade(type).rangeBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void AddWeaponDuration(WeaponType type, float amount)
        {
            GetAdvancedUpgrade(type).durationBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void AddWeaponSlow(WeaponType type, float amount)
        {
            GetAdvancedUpgrade(type).slowBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void AddWeaponDistance(WeaponType type, float amount)
        {
            GetAdvancedUpgrade(type).distanceBonus += Mathf.Max(0f, amount);
            RefreshFromStats();
        }

        public void MultiplyWeaponDamageInterval(WeaponType type, float multiplier)
        {
            GetAdvancedUpgrade(type).damageIntervalMultiplier *= Mathf.Clamp(multiplier, 0.05f, 1f);
            RefreshFromStats();
        }

        WeaponRunUpgradeState GetAdvancedUpgrade(WeaponType type)
        {
            if (!advancedWeaponUpgrades.TryGetValue(type, out var state))
            {
                state = new WeaponRunUpgradeState();
                advancedWeaponUpgrades[type] = state;
            }

            return state;
        }

        IEnumerator SlashLoop()
        {
            while (true)
            {
                var stats = slashStats;
                float burstDuration = 0f;
                if (player != null && !player.IsReviving && SlashUnlocked)
                {
                    if (SlashEvolved)
                    {
                        burstDuration = SwordRushBurstDuration();
                        yield return SwordRush(stats);
                    }
                    else
                    {
                        KnightSlash(stats);
                    }
                }

                yield return new WaitForSeconds(Mathf.Max(0f, GetCooldown(stats) - burstDuration));
            }
        }

        IEnumerator ArrowLoop()
        {
            while (true)
            {
                float cooldown = GetCooldown(arrowStats);
                if (player != null && !player.IsReviving && ArrowUnlocked)
                {
                    var displayType = GetDisplayWeaponType(WeaponType.Arrow);
                    var prefab = displayType == WeaponType.GoldenBow && goldenArrowPrefab != null ? goldenArrowPrefab : arrowPrefab;
                    TryShootArrowVolley(prefab, arrowStats, displayType, cooldown);
                }
                yield return new WaitForSeconds(cooldown);
            }
        }

        IEnumerator FireballLoop()
        {
            while (true)
            {
                if (player != null && !player.IsReviving && FireballUnlocked)
                {
                    var displayType = GetDisplayWeaponType(WeaponType.Fireball);
                    if (displayType == WeaponType.FireMissile) ShootFireMissileAtNearestTarget(fireMissilePrefab != null ? fireMissilePrefab : fireballPrefab, fireballStats);
                    else ShootForward(fireballPrefab, fireballStats);
                }
                yield return new WaitForSeconds(GetCooldown(fireballStats));
            }
        }

        static float GetCooldown(WeaponStatBlock stats)
        {
            return Mathf.Max(0.05f, stats.cooldownSeconds);
        }

        void KnightSlash(WeaponStatBlock stats)
        {
            var direction = FacingDirection();
            PlaySlashStrike(stats, direction, slashPrefab, config != null ? config.slashRange : stats.range);
        }

        IEnumerator SwordRush(WeaponStatBlock stats)
        {
            var direction = FacingDirection();
            int strikeCount = config != null ? Mathf.Max(1, config.swordRushStrikeCount) : 5;
            float strikeInterval = config != null ? Mathf.Max(0f, config.swordRushStrikeIntervalSeconds) : 0.09f;
            float baseRange = config != null ? Mathf.Max(0.01f, config.swordRushBaseRange) : Mathf.Max(0.01f, stats.range);
            var prefab = swordRushSlashPrefab != null ? swordRushSlashPrefab : slashPrefab;
            for (int i = 0; i < strikeCount; i++)
            {
                if (runtimeStopped || player == null || player.IsReviving) yield break;
                PlaySlashStrike(stats, direction, prefab, baseRange, i % 2);
                if (i < strikeCount - 1 && strikeInterval > 0f) yield return new WaitForSeconds(strikeInterval);
            }
        }

        float SwordRushBurstDuration()
        {
            if (config == null) return 0.36f;
            return Mathf.Max(0, config.swordRushStrikeCount - 1) * Mathf.Max(0f, config.swordRushStrikeIntervalSeconds);
        }

        Vector2 FacingDirection()
        {
            return player != null && player.Facing.sqrMagnitude > 0.01f ? player.Facing.normalized : Vector2.down;
        }

        void PlaySlashStrike(WeaponStatBlock stats, Vector2 direction, GameObject prefab, float baseRange, int animationFrameIndex = -1)
        {
            AudioManager.PlaySfx(SfxTrack.SlashSwing);
            var effectiveStats = ApplySlashSpecialEffect(stats);
            float range = Mathf.Max(0.01f, effectiveStats.range);
            int damage = effectiveStats.attackPower + (config != null ? config.slashDamageBonus : 0);
            float knockback = effectiveStats.knockback * (config != null ? config.knockbackForceUnit : 1f);
            float knockbackDuration = config != null ? config.knockbackDuration : 0f;
            SlashView.Flash(prefab, transform.position, direction, range, Mathf.Max(0.01f, baseRange), damage, knockback, knockbackDuration, animationFrameIndex);
        }

        void TryShootArrowVolley(GameObject prefab, WeaponStatBlock stats, WeaponType displayType, float cooldown)
        {
            if (!TryConsumeArrowSchedule(Time.time, cooldown))
            {
                return;
            }

            int launchedCount = ShootArrowsAtNearestTargets(prefab, stats, displayType);
            if (launchedCount <= 0) return;
        }

        bool TryConsumeArrowSchedule(float now, float cooldown)
        {
            if (now + 0.0001f < nextArrowVolleyAt) return false;
            nextArrowVolleyAt = now + Mathf.Max(0.05f, cooldown);
            return true;
        }

        int ShootArrowsAtNearestTargets(GameObject prefab, WeaponStatBlock stats, WeaponType displayType)
        {
            if (prefab == null) return 0;
            var effectiveStats = ApplyArrowSpecialEffect(stats);
            var targets = CollectArrowTargetsInRange(effectiveStats);
            if (targets.Count <= 0) return 0;

            float projectileSpeed = Mathf.Max(0.01f, effectiveStats.projectileSpeed);
            float range = Mathf.Max(0.01f, effectiveStats.range);
            float lifetime = Mathf.Max(0.05f, range / projectileSpeed);
            int projectileCount = ResolveArrowVolleyProjectileCount(effectiveStats.projectileCount, targets.Count);
            int launchedCount = 0;
            for (int i = 0; i < projectileCount; i++)
            {
                var enemy = targets[i].enemy;
                if (enemy == null) continue;
                if (launchedCount == 0) AudioManager.PlaySfx(SfxTrack.ArrowShot);
                var shotDirection = (Vector2)(enemy.transform.position - transform.position);
                LaunchProjectile(prefab, false, effectiveStats, shotDirection.normalized, projectileSpeed, 0f, lifetime, displayType, enemy.transform);
                launchedCount++;
            }
            return launchedCount;
        }

        public static int ResolveArrowVolleyProjectileCount(int projectileCount, int targetCount)
        {
            if (targetCount <= 0) return 0;
            return Mathf.Min(Mathf.Max(1, projectileCount), targetCount);
        }

        void ShootFireMissileAtNearestTarget(GameObject prefab, WeaponStatBlock stats)
        {
            if (prefab == null) return;
            var effectiveStats = ApplyFireballSpecialEffect(stats);
            var targets = CollectArrowTargetsInRange(effectiveStats);
            if (targets.Count <= 0 || targets[0].enemy == null) return;
            var enemy = targets[0].enemy;
            var direction = ResolveFireMissileLaunchDirection(UnityEngine.Random.Range(0f, 360f));
            float speed = Mathf.Max(0.01f, effectiveStats.projectileSpeed);
            float lifetime = Mathf.Max(0.05f, FireballFlightRange(effectiveStats) / speed);
            AudioManager.PlaySfx(SfxTrack.FireballCast);
            LaunchProjectile(prefab, true, effectiveStats, direction, speed, Mathf.Max(0.05f, effectiveStats.explosionRadius), lifetime, WeaponType.FireMissile, enemy.transform);
        }

        public static Vector2 ResolveFireMissileLaunchDirection(float angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
        }

        List<ArrowTargetCandidate> CollectArrowTargetsInRange(WeaponStatBlock stats)
        {
            var targets = new List<ArrowTargetCandidate>();
            var enemies = FindObjectsOfType<EnemyController>();
            float range = Mathf.Max(0.01f, stats.range);
            float rangeSqr = range * range;
            foreach (var enemy in enemies)
            {
                if (enemy == null) continue;
                var health = enemy.GetComponent<Health>();
                if (health == null || health.IsDead) continue;
                float distanceSqr = (enemy.transform.position - transform.position).sqrMagnitude;
                if (distanceSqr > rangeSqr) continue;
                targets.Add(new ArrowTargetCandidate(enemy, distanceSqr));
            }

            targets.Sort((a, b) => a.distanceSqr.CompareTo(b.distanceSqr));
            return targets;
        }

        void ShootForward(GameObject prefab, WeaponStatBlock stats)
        {
            if (prefab == null || player == null) return;
            var effectiveStats = ApplyFireballSpecialEffect(stats);
            var direction = player.Facing.sqrMagnitude > 0.01f ? player.Facing.normalized : Vector2.down;
            float projectileSpeed = Mathf.Max(0.01f, effectiveStats.projectileSpeed);
            float lifetime = Mathf.Max(0.05f, FireballFlightRange(effectiveStats) / projectileSpeed);
            float radius = Mathf.Max(0.05f, effectiveStats.explosionRadius);
            AudioManager.PlaySfx(SfxTrack.FireballCast);
            LaunchProjectile(prefab, true, effectiveStats, direction, projectileSpeed, radius, lifetime, WeaponType.Fireball, null);
        }

        float FireballFlightRange(WeaponStatBlock stats)
        {
            return Mathf.Max(0.05f, stats.range);
        }

        WeaponStatBlock ApplySlashSpecialEffect(WeaponStatBlock stats)
        {
            stats.knockback *= AreaControlSpecialMultiplier();
            return ApplyRelicConditionalWeaponBonuses(WeaponType.Slash, stats);
        }

        WeaponStatBlock ApplyArrowSpecialEffect(WeaponStatBlock stats)
        {
            stats.range *= AreaControlSpecialMultiplier();
            return ApplyRelicConditionalWeaponBonuses(WeaponType.Arrow, stats);
        }

        WeaponStatBlock ApplyFireballSpecialEffect(WeaponStatBlock stats)
        {
            stats.explosionRadius *= AreaControlSpecialMultiplier();
            return ApplyRelicConditionalWeaponBonuses(WeaponType.Fireball, stats);
        }

        WeaponStatBlock ApplyShieldSpecialEffect(WeaponStatBlock stats)
        {
            stats.rotationSpeed *= AreaControlSpecialMultiplier();
            return ApplyRelicConditionalWeaponBonuses(WeaponType.Shield, stats);
        }

        void SyncShieldOrbit()
        {
            if (shieldOrbit == null) return;
            bool active = player != null && !player.IsReviving && ShieldUnlocked;
            shieldOrbit.SetEvolution(GetDisplayWeaponType(WeaponType.Shield) == WeaponType.DualShield);
            shieldOrbit.SetActive(active);
            if (active) shieldOrbit.SetStats(EffectiveShieldStats);
        }

        void SyncAdvancedWeapons()
        {
            if (advancedRuntime == null) return;
            advancedRuntime.Sync();
        }

        WeaponStatBlock ApplyAdvancedRunUpgrades(WeaponType type, WeaponStatBlock stats)
        {
            stats = ApplyAdvancedEvolutionBaseValues(type, stats);
            if (advancedWeaponUpgrades.TryGetValue(type, out var upgrade))
            {
                stats.attackPower += upgrade.attackBonus;
                stats.cooldownSeconds = Mathf.Max(0.05f, stats.cooldownSeconds * upgrade.cooldownMultiplier);
                stats.projectileCount = Mathf.Max(1, stats.projectileCount + upgrade.projectileCountBonus);
                stats.range += upgrade.rangeBonus;
                stats.durationSeconds += upgrade.durationBonus;
                stats.slowAmount = Mathf.Clamp01(stats.slowAmount + upgrade.slowBonus);
                stats.damageIntervalSeconds = Mathf.Max(0.05f, stats.damageIntervalSeconds * upgrade.damageIntervalMultiplier);
                stats.distance += upgrade.distanceBonus;
            }

            stats = RelicEffects.ApplyWeaponStatBonuses(type, stats);
            return stats;
        }

        WeaponStatBlock ApplyAdvancedEvolutionBaseValues(WeaponType type, WeaponStatBlock stats)
        {
            if (!IsEvolved(type) || config == null) return stats;

            var baseStats = config.GetWeaponStats(type, 1);
            switch (type)
            {
                case WeaponType.BoomerangSword:
                    stats.range += Mathf.Max(0f, config.bananaBaseRange) - baseStats.range;
                    stats.projectileCount += Mathf.Max(0, config.bananaBaseProjectileCountBonus);
                    break;
                case WeaponType.AuraSword:
                    stats.range += baseStats.range * (ExcaliburBaseRangeMultiplier - 1f);
                    stats.distance += baseStats.distance;
                    stats.damageIntervalSeconds = Mathf.Max(0.05f, config.excaliburDamageIntervalSeconds);
                    break;
                case WeaponType.ArrowRain:
                    stats.range += Mathf.Max(0.05f, config.evolvedGroundStrikeRadius) - baseStats.range;
                    break;
                case WeaponType.Gun:
                    stats.projectileCount += Mathf.Max(0, config.machineGunBaseAttackCountBonus);
                    break;
                case WeaponType.Frost:
                    stats.range += Mathf.Max(0.05f, config.evolvedGroundStrikeRadius) - baseStats.range;
                    break;
                case WeaponType.ThunderBall:
                    stats.projectileCount += Mathf.Max(0, config.thunderStormOrbitCount);
                    break;
                case WeaponType.Flag:
                    stats.attackPower += baseStats.attackPower;
                    break;
            }
            stats.range = Mathf.Max(0.05f, stats.range);
            stats.distance = Mathf.Max(0f, stats.distance);
            stats.projectileCount = Mathf.Max(1, stats.projectileCount);
            return stats;
        }

        WeaponStatBlock ApplyRelicConditionalWeaponBonuses(WeaponType type, WeaponStatBlock stats)
        {
            return RelicEffects.ApplyConditionalWeaponBonuses(type, stats, this, player, grid, GameManager.Instance);
        }

        WeaponStatBlock ApplyAdvancedSpecialEffect(WeaponType type, WeaponStatBlock stats)
        {
            switch (type)
            {
                case WeaponType.Flag:
                case WeaponType.AuraSword:
                case WeaponType.ArrowRain:
                case WeaponType.Frost:
                    if (IsPlayerAreaControlSpecialActive(0.5f)) stats.range += AreaPaintRangeBonus();
                    break;
                case WeaponType.BoomerangSword:
                    if (IsPlayerAreaControlSpecialActive(0.7f)) stats.projectileCount *= 2;
                    break;
                case WeaponType.Gun:
                    if (IsPlayerAreaControlSpecialActive(0.7f)) stats.attackPower *= 2;
                    break;
                case WeaponType.ThunderBall:
                    if (IsPlayerAreaControlSpecialActive(0.7f)) stats.range *= 2f;
                    break;
            }

            return ApplyRelicConditionalWeaponBonuses(type, stats);
        }

        public bool IsSpecialEffectActiveFor(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Slash:
                case WeaponType.Arrow:
                case WeaponType.Fireball:
                case WeaponType.Shield:
                case WeaponType.Flag:
                case WeaponType.AuraSword:
                case WeaponType.ArrowRain:
                case WeaponType.Frost:
                    return IsPlayerAreaControlSpecialActive(0.5f);
                case WeaponType.BoomerangSword:
                case WeaponType.Gun:
                case WeaponType.ThunderBall:
                    return IsPlayerAreaControlSpecialActive(0.7f);
                default:
                    return false;
            }
        }

        void LaunchProjectile(GameObject prefab, bool explosive, WeaponStatBlock stats, Vector2 direction, float projectileSpeed, float radius, float lifetime, WeaponType displayType, Transform homingTarget)
        {
            var go = Instantiate(prefab, transform.position, Quaternion.identity);
            var projectile = go.GetComponent<Projectile>();
            if (projectile != null)
            {
                if (explosive)
                {
                    projectile.impactColor = new Color(1f, 0.76f, 0.24f, 0.96f);
                    projectile.impactVisualScale = Mathf.Clamp(radius * 0.55f, 0.55f, 1.2f);
                }

                float projectileVisualScale = explosive
                    ? FireballProjectileVisualScale
                    : config.projectileVisualScale;
                projectile.SetDamageSource(RunDamageSource.ForWeapon(WeaponCatalog.BaseWeaponOf(displayType)));
                float homingTurnSpeed = displayType == WeaponType.FireMissile && config != null
                    ? config.fireMissileHomingTurnSpeedDegrees
                    : 180f;
                projectile.ConfigureWeaponBehavior(displayType, homingTarget, homingTurnSpeed);
                projectile.Launch(direction, stats.attackPower, projectileSpeed, explosive, radius, lifetime, projectileVisualScale);
                projectile.knockback = stats.knockback * config.knockbackForceUnit;
                projectile.knockbackDuration = config.knockbackDuration;
            }
        }

        float AreaControlSpecialMultiplier()
        {
            if (!IsPlayerAreaControlSpecialActive()) return 1f;
            return Mathf.Max(1f, config != null ? config.weaponSpecialEffectMultiplier : 2f);
        }

        bool IsPlayerAreaControlSpecialActive()
        {
            float threshold = Mathf.Clamp01(config != null ? config.weaponSpecialEffectControlThreshold : 0.5f);
            return IsPlayerAreaControlSpecialActive(threshold);
        }

        bool IsPlayerAreaControlSpecialActive(float threshold)
        {
            if (grid == null) grid = FindObjectOfType<TileGrid>();
            if (grid == null) return false;
            return grid.GetPlayerControlRatio() >= Mathf.Clamp01(threshold);
        }

        float AreaPaintRangeBonus()
        {
            int radius = player != null ? player.PaintRadius : config != null ? config.paintRadius : 1;
            return Mathf.Max(0, radius) * TileGrid.DefaultCellSize;
        }

        readonly struct ArrowTargetCandidate
        {
            public readonly EnemyController enemy;
            public readonly float distanceSqr;

            public ArrowTargetCandidate(EnemyController enemy, float distanceSqr)
            {
                this.enemy = enemy;
                this.distanceSqr = distanceSqr;
            }
        }

        sealed class WeaponRunUpgradeState
        {
            public int attackBonus;
            public float cooldownMultiplier = 1f;
            public int projectileCountBonus;
            public float rangeBonus;
            public float durationBonus;
            public float slowBonus;
            public float damageIntervalMultiplier = 1f;
            public float distanceBonus;
        }

    }
}
