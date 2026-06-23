using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class WeaponController : MonoBehaviour
    {
        public GameObject arrowPrefab;
        public GameObject fireballPrefab;
        public Transform slashOrigin;
        const float MageProjectileVisualScale = 0.38f;
        GameConfig config;
        PlayerController player;
        WeaponStatBlock slashStats;
        WeaponStatBlock arrowStats;
        WeaponStatBlock fireballStats;
        int slashLevel = 1;
        int arrowLevel;
        int fireballLevel;
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

        public void Configure(GameConfig gameConfig, PlayerController owner)
        {
            config = gameConfig;
            player = owner;
            if (config != null) config.EnsureWeaponLevelDefaults();
            slashLevel = Mathf.Clamp(1 + ProgressionStore.GetLevel(UpgradeType.StartingWeaponLevel), 1, GameConfig.MaxWeaponLevel);
            arrowLevel = 0;
            fireballLevel = 0;
            RefreshFromStats();
            StopAllCoroutines();
            StartCoroutine(SlashLoop());
            StartCoroutine(ArrowLoop());
            StartCoroutine(FireballLoop());
        }

        public void RefreshFromStats()
        {
            if (config == null || player == null) return;
            slashStats = config.GetWeaponStats(CharacterType.Knight, slashLevel);
            arrowStats = config.GetWeaponStats(CharacterType.Archer, Mathf.Max(1, arrowLevel));
            fireballStats = config.GetWeaponStats(CharacterType.Mage, Mathf.Max(1, fireballLevel));
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
                if (player != null && !player.IsReviving && ArrowUnlocked) ShootAtNearest(arrowPrefab, false, arrowStats);
                yield return new WaitForSeconds(GetCooldown(arrowStats));
            }
        }

        IEnumerator FireballLoop()
        {
            while (true)
            {
                if (player != null && !player.IsReviving && FireballUnlocked) ShootAtNearest(fireballPrefab, true, fireballStats);
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
            float baseRange = config != null ? Mathf.Max(0.01f, config.knightSlashRange) : range;
            int damage = stats.attackPower + config.knightDamageBonus;
            float knockback = stats.knockback * config.knockbackForceUnit;
            SlashView.Flash(transform.position, direction, range, baseRange, damage, knockback, config.knockbackDuration);
        }

        void ShootAtNearest(GameObject prefab, bool explosive, WeaponStatBlock stats)
        {
            if (prefab == null) return;
            var enemies = FindObjectsOfType<EnemyController>();
            EnemyController nearest = null;
            float best = float.MaxValue;
            foreach (var enemy in enemies)
            {
                float d = (enemy.transform.position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = enemy;
                }
            }

            var dir = nearest == null ? player.Facing : (Vector2)(nearest.transform.position - transform.position);
            var go = Instantiate(prefab, transform.position, Quaternion.identity);
            float projectileSpeed = Mathf.Max(0.01f, stats.projectileSpeed);
            float lifetime = explosive ? config.projectileLifetime : Mathf.Max(0.05f, stats.range / projectileSpeed);
            float radius = explosive ? stats.range : config.mageExplosionRadius;
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
                    ? MageProjectileVisualScale
                    : config.projectileVisualScale;
                projectile.Launch(dir.normalized, stats.attackPower, projectileSpeed, explosive, radius, lifetime, projectileVisualScale);
                projectile.knockback = stats.knockback * config.knockbackForceUnit;
                projectile.knockbackDuration = config.knockbackDuration;
            }
        }

    }
}
