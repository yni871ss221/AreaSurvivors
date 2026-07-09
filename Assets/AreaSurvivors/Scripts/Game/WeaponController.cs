using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class WeaponController : MonoBehaviour
    {
        public GameObject arrowPrefab;
        public GameObject fireballPrefab;
        public GameObject slashPrefab;
        public Transform slashOrigin;
        public const int MaxEquippedWeapons = 3;
        const float FireballProjectileVisualScale = 0.38f;
        public const float SlashRangeUpgradeAmount = 0.2f;
        public const float SlashKnockbackUpgradeAmount = 1f;
        public const float ProjectileRangeUpgradeAmount = 0.75f;
        public const float FireballExplosionUpgradeAmount = 0.25f;
        public const float ShieldKnockbackUpgradeAmount = 1f;
        public const float ShieldRotationSpeedUpgradeAmount = 20f;
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
        int slashLevel = 1;
        int arrowLevel;
        int fireballLevel;
        int shieldLevel;
        readonly Dictionary<WeaponType, int> advancedWeaponLevels = new Dictionary<WeaponType, int>();
        readonly Dictionary<WeaponType, WeaponRunUpgradeState> advancedWeaponUpgrades = new Dictionary<WeaponType, WeaponRunUpgradeState>();
        readonly List<WeaponType> acquiredWeaponOrder = new List<WeaponType>();
        int slashAttackBonus;
        int arrowAttackBonus;
        int fireballAttackBonus;
        int shieldAttackBonus;
        float slashCooldownMultiplier = 1f;
        float arrowCooldownMultiplier = 1f;
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
            advancedWeaponLevels.Clear();
            shieldOrbit = GetComponent<ShieldOrbitController>();
            if (shieldOrbit != null) shieldOrbit.Configure(this, transform, config);
            advancedRuntime = GetComponent<AdvancedWeaponRuntime>();
            if (advancedRuntime != null) advancedRuntime.Configure(this, owner, gameConfig);
            runtimeStopped = false;
            acquiredWeaponOrder.Clear();
            if (RunState.TryConsumeNextTestStartingWeapon(out var testStartingWeapon))
            {
                ApplyTestStartingWeapon(testStartingWeapon);
            }
            else if (SlashUnlocked)
            {
                RegisterAcquiredWeapon(WeaponType.Slash);
            }
            ResetRunWeaponUpgrades();
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
            slashStats = config.GetWeaponStats(WeaponType.Slash, Mathf.Max(1, slashLevel));
            arrowStats = config.GetWeaponStats(WeaponType.Arrow, Mathf.Max(1, arrowLevel));
            fireballStats = config.GetWeaponStats(WeaponType.Fireball, Mathf.Max(1, fireballLevel));
            shieldStats = config.GetWeaponStats(WeaponType.Shield, Mathf.Max(1, shieldLevel));
            ApplyRunWeaponUpgrades();
            SyncShieldOrbit();
            SyncAdvancedWeapons();
        }

        void ApplyRunWeaponUpgrades()
        {
            slashStats.attackPower += slashAttackBonus;
            slashStats.cooldownSeconds = Mathf.Max(0.05f, slashStats.cooldownSeconds * slashCooldownMultiplier);
            slashStats.knockback += slashKnockbackBonus;
            slashStats.range += slashRangeBonus;

            arrowStats.attackPower += arrowAttackBonus;
            arrowStats.cooldownSeconds = Mathf.Max(0.05f, arrowStats.cooldownSeconds * arrowCooldownMultiplier);
            arrowStats.projectileCount = Mathf.Max(1, arrowStats.projectileCount + arrowProjectileCountBonus);
            arrowStats.range += arrowRangeBonus;

            fireballStats.attackPower += fireballAttackBonus;
            fireballStats.cooldownSeconds = Mathf.Max(0.05f, fireballStats.cooldownSeconds * fireballCooldownMultiplier);
            fireballStats.explosionRadius += fireballExplosionRadiusBonus;
            fireballStats.range += fireballRangeBonus;

            shieldStats.attackPower += shieldAttackBonus;
            shieldStats.projectileCount = Mathf.Max(1, shieldStats.projectileCount + shieldCountBonus);
            shieldStats.knockback += shieldKnockbackBonus;
            shieldStats.rotationSpeed += shieldRotationSpeedBonus;

            slashStats = RelicEffects.ApplyWeaponStatBonuses(WeaponType.Slash, slashStats);
            arrowStats = RelicEffects.ApplyWeaponStatBonuses(WeaponType.Arrow, arrowStats);
            fireballStats = RelicEffects.ApplyWeaponStatBonuses(WeaponType.Fireball, fireballStats);
            shieldStats = RelicEffects.ApplyWeaponStatBonuses(WeaponType.Shield, shieldStats);
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
                case WeaponType.Arrow: return UnlockArrow();
                case WeaponType.Fireball: return UnlockFireball();
                case WeaponType.Shield: return UnlockShield();
                case WeaponType.Slash: return false;
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
                case WeaponType.Arrow: return ArrowUnlocked;
                case WeaponType.Fireball: return FireballUnlocked;
                case WeaponType.Shield: return ShieldUnlocked;
                default:
                    return advancedWeaponLevels.TryGetValue(type, out var level) && level > 0;
            }
        }

        public WeaponStatBlock GetWeaponStatsFor(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Slash: return slashStats;
                case WeaponType.Arrow: return arrowStats;
                case WeaponType.Fireball: return fireballStats;
                case WeaponType.Shield: return shieldStats;
            }

            int level = advancedWeaponLevels.TryGetValue(type, out var value) ? Mathf.Max(1, value) : 1;
            var stats = config != null ? config.GetWeaponStats(type, level) : default;
            return ApplyAdvancedRunUpgrades(type, stats);
        }

        public WeaponStatBlock GetEffectiveWeaponStatsFor(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Slash: return EffectiveSlashStats;
                case WeaponType.Arrow: return EffectiveArrowStats;
                case WeaponType.Fireball: return EffectiveFireballStats;
                case WeaponType.Shield: return EffectiveShieldStats;
                default: return ApplyAdvancedSpecialEffect(type, GetWeaponStatsFor(type));
            }
        }

        void RegisterAcquiredWeapon(WeaponType type)
        {
            if (!acquiredWeaponOrder.Contains(type))
            {
                acquiredWeaponOrder.Add(type);
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
            slashLevel = 0;
            arrowLevel = 0;
            fireballLevel = 0;
            shieldLevel = 0;
            advancedWeaponLevels.Clear();

            switch (type)
            {
                case WeaponType.Slash:
                    slashLevel = 1;
                    break;
                case WeaponType.Arrow:
                    arrowLevel = 1;
                    break;
                case WeaponType.Fireball:
                    fireballLevel = 1;
                    break;
                case WeaponType.Shield:
                    shieldLevel = 1;
                    break;
                default:
                    if (WeaponCatalog.IsAdvanced(type)) advancedWeaponLevels[type] = 1;
                    else
                    {
                        type = WeaponType.Slash;
                        slashLevel = 1;
                    }
                    break;
            }

            RegisterAcquiredWeapon(type);
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
                if (player != null && !player.IsReviving && SlashUnlocked) KnightSlash(slashStats);
                yield return new WaitForSeconds(GetCooldown(slashStats));
            }
        }

        IEnumerator ArrowLoop()
        {
            while (true)
            {
                if (player != null && !player.IsReviving && ArrowUnlocked) ShootArrowsAtNearestTargets(arrowPrefab, arrowStats);
                yield return new WaitForSeconds(GetCooldown(arrowStats));
            }
        }

        IEnumerator FireballLoop()
        {
            while (true)
            {
                if (player != null && !player.IsReviving && FireballUnlocked) ShootForward(fireballPrefab, fireballStats);
                yield return new WaitForSeconds(GetCooldown(fireballStats));
            }
        }

        static float GetCooldown(WeaponStatBlock stats)
        {
            return Mathf.Max(0.05f, stats.cooldownSeconds);
        }

        void KnightSlash(WeaponStatBlock stats)
        {
            AudioManager.PlaySfx(SfxTrack.SlashSwing);
            var effectiveStats = ApplySlashSpecialEffect(stats);
            var direction = player.Facing.sqrMagnitude > 0.01f ? player.Facing.normalized : Vector2.down;
            float range = Mathf.Max(0.01f, effectiveStats.range);
            float baseRange = config != null ? Mathf.Max(0.01f, config.slashRange) : range;
            int damage = effectiveStats.attackPower + config.slashDamageBonus;
            float knockback = effectiveStats.knockback * config.knockbackForceUnit;
            SlashView.Flash(slashPrefab, transform.position, direction, range, baseRange, damage, knockback, config.knockbackDuration);
        }

        void ShootArrowsAtNearestTargets(GameObject prefab, WeaponStatBlock stats)
        {
            if (prefab == null) return;
            var effectiveStats = ApplyArrowSpecialEffect(stats);
            var targets = CollectArrowTargetsInRange(effectiveStats);
            if (targets.Count <= 0) return;

            float projectileSpeed = Mathf.Max(0.01f, effectiveStats.projectileSpeed);
            float range = Mathf.Max(0.01f, effectiveStats.range);
            float lifetime = Mathf.Max(0.05f, range / projectileSpeed);
            int projectileCount = Mathf.Min(Mathf.Max(1, effectiveStats.projectileCount), targets.Count);
            for (int i = 0; i < projectileCount; i++)
            {
                var enemy = targets[i].enemy;
                if (enemy == null) continue;
                if (i == 0) AudioManager.PlaySfx(SfxTrack.ArrowShot);
                var shotDirection = (Vector2)(enemy.transform.position - transform.position);
                LaunchProjectile(prefab, false, effectiveStats, shotDirection.normalized, projectileSpeed, 0f, lifetime);
            }
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
            LaunchProjectile(prefab, true, effectiveStats, direction, projectileSpeed, radius, lifetime);
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

        void LaunchProjectile(GameObject prefab, bool explosive, WeaponStatBlock stats, Vector2 direction, float projectileSpeed, float radius, float lifetime)
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
                projectile.SetDamageSource(RunDamageSource.ForWeapon(explosive ? WeaponType.Fireball : WeaponType.Arrow));
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
