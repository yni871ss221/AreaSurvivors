using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class WeaponController : MonoBehaviour
    {
        public GameObject arrowPrefab;
        public GameObject fireballPrefab;
        public Transform slashOrigin;
        const float FireballProjectileVisualScale = 0.38f;
        public const float SlashRangeUpgradeAmount = 0.08f;
        public const float SlashKnockbackUpgradeAmount = 1f;
        public const float ProjectileRangeUpgradeAmount = 0.75f;
        public const float FireballExplosionUpgradeAmount = 0.25f;
        GameConfig config;
        PlayerController player;
        WeaponStatBlock slashStats;
        WeaponStatBlock arrowStats;
        WeaponStatBlock fireballStats;
        int slashLevel = 1;
        int arrowLevel;
        int fireballLevel;
        int slashAttackBonus;
        int arrowAttackBonus;
        int fireballAttackBonus;
        float slashCooldownMultiplier = 1f;
        float arrowCooldownMultiplier = 1f;
        float fireballCooldownMultiplier = 1f;
        float slashKnockbackBonus;
        float slashRangeBonus;
        int arrowProjectileCountBonus;
        float arrowRangeBonus;
        float fireballExplosionRadiusBonus;
        float fireballRangeBonus;
        public int WeaponLevel => slashLevel;
        public int SlashLevel => slashLevel;
        public int ArrowLevel => arrowLevel;
        public int FireballLevel => fireballLevel;
        public bool ArrowUnlocked => arrowLevel > 0;
        public bool FireballUnlocked => fireballLevel > 0;
        public bool CanLevelUp => CanLevelUpSlash;
        public bool CanLevelUpSlash => slashLevel < GameConfig.MaxWeaponLevel;
        public bool CanLevelUpArrow => arrowLevel < GameConfig.MaxWeaponLevel;
        public bool CanLevelUpFireball => fireballLevel < GameConfig.MaxWeaponLevel;
        public int AttackPower => slashStats.attackPower;
        public float CurrentCooldown => slashStats.cooldownSeconds;
        public float ProjectileSpeed => Mathf.Max(arrowStats.projectileSpeed, fireballStats.projectileSpeed);
        public float WeaponRange => Mathf.Max(slashStats.range, Mathf.Max(arrowStats.range, fireballStats.range));
        public float Knockback => slashStats.knockback;
        public WeaponStatBlock SlashStats => slashStats;
        public WeaponStatBlock ArrowStats => arrowStats;
        public WeaponStatBlock FireballStats => fireballStats;
        public int SlashAttackPower => slashStats.attackPower + (config != null ? config.slashDamageBonus : 0);
        public float FireballRange => FireballFlightRange(fireballStats);

        public void Configure(GameConfig gameConfig, PlayerController owner)
        {
            config = gameConfig;
            player = owner;
            if (config != null) config.EnsureWeaponLevelDefaults();
            slashLevel = Mathf.Clamp(1 + ProgressionStore.GetLevel(UpgradeType.StartingWeaponLevel), 1, GameConfig.MaxWeaponLevel);
            arrowLevel = ProgressionStore.IsUnlocked(UpgradeType.StartingArrow) ? 1 : 0;
            fireballLevel = ProgressionStore.IsUnlocked(UpgradeType.StartingFireball) ? 1 : 0;
            ResetRunWeaponUpgrades();
            RefreshFromStats();
            StopAllCoroutines();
            StartCoroutine(SlashLoop());
            StartCoroutine(ArrowLoop());
            StartCoroutine(FireballLoop());
        }

        void ResetRunWeaponUpgrades()
        {
            slashAttackBonus = 0;
            arrowAttackBonus = 0;
            fireballAttackBonus = 0;
            slashCooldownMultiplier = 1f;
            arrowCooldownMultiplier = 1f;
            fireballCooldownMultiplier = 1f;
            slashKnockbackBonus = 0f;
            slashRangeBonus = 0f;
            arrowProjectileCountBonus = 0;
            arrowRangeBonus = 0f;
            fireballExplosionRadiusBonus = 0f;
            fireballRangeBonus = 0f;
        }

        public void RefreshFromStats()
        {
            if (config == null || player == null) return;
            slashStats = config.GetWeaponStats(WeaponType.Slash, slashLevel);
            arrowStats = config.GetWeaponStats(WeaponType.Arrow, Mathf.Max(1, arrowLevel));
            fireballStats = config.GetWeaponStats(WeaponType.Fireball, Mathf.Max(1, fireballLevel));
            ApplyRunWeaponUpgrades();
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
            arrowLevel++;
            RefreshFromStats();
            return true;
        }

        public bool LevelUpFireball()
        {
            if (!CanLevelUpFireball) return false;
            fireballLevel++;
            RefreshFromStats();
            return true;
        }

        public bool UnlockArrow()
        {
            if (ArrowUnlocked) return false;
            arrowLevel = 1;
            RefreshFromStats();
            return true;
        }

        public bool UnlockFireball()
        {
            if (FireballUnlocked) return false;
            fireballLevel = 1;
            RefreshFromStats();
            return true;
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

        IEnumerator SlashLoop()
        {
            while (true)
            {
                if (player != null && !player.IsReviving) KnightSlash(slashStats);
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
            var direction = player.Facing.sqrMagnitude > 0.01f ? player.Facing.normalized : Vector2.down;
            float range = Mathf.Max(0.01f, stats.range);
            float baseRange = config != null ? Mathf.Max(0.01f, config.slashRange) : range;
            int damage = stats.attackPower + config.slashDamageBonus;
            float knockback = stats.knockback * config.knockbackForceUnit;
            SlashView.Flash(transform.position, direction, range, baseRange, damage, knockback, config.knockbackDuration);
        }

        void ShootArrowsAtNearestTargets(GameObject prefab, WeaponStatBlock stats)
        {
            if (prefab == null) return;
            var targets = CollectArrowTargetsInRange(stats);
            if (targets.Count <= 0) return;

            float projectileSpeed = Mathf.Max(0.01f, stats.projectileSpeed);
            float range = Mathf.Max(0.01f, stats.range);
            float lifetime = Mathf.Max(0.05f, range / projectileSpeed);
            int projectileCount = Mathf.Min(Mathf.Max(1, stats.projectileCount), targets.Count);
            for (int i = 0; i < projectileCount; i++)
            {
                var enemy = targets[i].enemy;
                if (enemy == null) continue;
                var shotDirection = (Vector2)(enemy.transform.position - transform.position);
                LaunchProjectile(prefab, false, stats, shotDirection.normalized, projectileSpeed, 0f, lifetime);
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
            var direction = player.Facing.sqrMagnitude > 0.01f ? player.Facing.normalized : Vector2.down;
            float projectileSpeed = Mathf.Max(0.01f, stats.projectileSpeed);
            float lifetime = Mathf.Max(0.05f, FireballFlightRange(stats) / projectileSpeed);
            float radius = Mathf.Max(0.05f, stats.explosionRadius);
            LaunchProjectile(prefab, true, stats, direction, projectileSpeed, radius, lifetime);
        }

        float FireballFlightRange(WeaponStatBlock stats)
        {
            return Mathf.Max(0.05f, stats.range);
        }

        void LaunchProjectile(GameObject prefab, bool explosive, WeaponStatBlock stats, Vector2 direction, float projectileSpeed, float radius, float lifetime)
        {
            var go = Instantiate(prefab, transform.position, Quaternion.identity);
            var projectile = go.GetComponent<Projectile>();
            if (projectile != null)
            {
                if (explosive)
                {
                    projectile.impactSprite = GeneratedSpriteLoader.Load("CannonExplosion");
                    projectile.impactColor = new Color(1f, 0.76f, 0.24f, 0.96f);
                    projectile.impactVisualScale = Mathf.Clamp(radius * 0.55f, 0.55f, 1.2f);
                }

                float projectileVisualScale = explosive
                    ? FireballProjectileVisualScale
                    : config.projectileVisualScale;
                projectile.Launch(direction, stats.attackPower, projectileSpeed, explosive, radius, lifetime, projectileVisualScale);
                projectile.knockback = stats.knockback * config.knockbackForceUnit;
                projectile.knockbackDuration = config.knockbackDuration;
            }
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

    }
}
