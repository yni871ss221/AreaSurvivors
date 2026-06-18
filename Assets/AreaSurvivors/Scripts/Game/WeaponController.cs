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
        WeaponStatBlock currentStats;
        int weaponLevel = 1;
        public int WeaponLevel => weaponLevel;
        public bool CanLevelUp => weaponLevel < GameConfig.MaxWeaponLevel;
        public int AttackPower => currentStats.attackPower;
        public float CurrentCooldown => currentStats.cooldownSeconds;
        public float ProjectileSpeed => currentStats.projectileSpeed;
        public float WeaponRange => currentStats.range;
        public float Knockback => currentStats.knockback;

        public void Configure(GameConfig gameConfig, PlayerController owner)
        {
            config = gameConfig;
            player = owner;
            if (config != null) config.EnsureWeaponLevelDefaults();
            weaponLevel = Mathf.Clamp(1 + ProgressionStore.GetLevel(UpgradeType.StartingWeaponLevel), 1, GameConfig.MaxWeaponLevel);
            RefreshFromStats();
            StopAllCoroutines();
            StartCoroutine(AttackLoop());
        }

        public void RefreshFromStats()
        {
            if (config == null || player == null) return;
            currentStats = config.GetWeaponStats(player.characterType, weaponLevel);
        }

        public bool LevelUp()
        {
            if (!CanLevelUp) return false;
            weaponLevel++;
            RefreshFromStats();
            return true;
        }

        IEnumerator AttackLoop()
        {
            while (true)
            {
                if (player != null && !player.IsReviving)
                {
                    if (player.characterType == CharacterType.Knight) KnightSlash();
                    if (player.characterType == CharacterType.Archer) ShootAtNearest(arrowPrefab, false);
                    if (player.characterType == CharacterType.Mage) ShootAtNearest(fireballPrefab, true);
                }

                yield return new WaitForSeconds(GetCooldown());
            }
        }

        float GetCooldown()
        {
            return Mathf.Max(0.05f, currentStats.cooldownSeconds);
        }

        void KnightSlash()
        {
            var direction = player.Facing.sqrMagnitude > 0.01f ? player.Facing.normalized : Vector2.down;
            float range = Mathf.Max(0.01f, currentStats.range);
            float baseRange = config != null ? Mathf.Max(0.01f, config.knightSlashRange) : range;
            int damage = currentStats.attackPower + config.knightDamageBonus;
            float knockback = currentStats.knockback * config.knockbackForceUnit;
            SlashView.Flash(transform.position, direction, range, baseRange, damage, knockback, config.knockbackDuration);
        }

        void ShootAtNearest(GameObject prefab, bool explosive)
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
            float projectileSpeed = Mathf.Max(0.01f, currentStats.projectileSpeed);
            float lifetime = player.characterType == CharacterType.Archer
                ? Mathf.Max(0.05f, currentStats.range / projectileSpeed)
                : config.projectileLifetime;
            float radius = player.characterType == CharacterType.Mage ? currentStats.range : config.mageExplosionRadius;
            var projectile = go.GetComponent<Projectile>();
            if (projectile != null)
            {
                if (player.characterType == CharacterType.Mage)
                {
                    projectile.impactSprite = GeneratedSpriteLoader.Load("CannonExplosion");
                    projectile.impactColor = new Color(1f, 0.76f, 0.24f, 0.96f);
                    projectile.impactVisualScale = Mathf.Clamp(radius * 0.55f, 0.55f, 1.2f);
                }

                float projectileVisualScale = player.characterType == CharacterType.Mage
                    ? MageProjectileVisualScale
                    : config.projectileVisualScale;
                projectile.Launch(dir.normalized, currentStats.attackPower, projectileSpeed, explosive, radius, lifetime, projectileVisualScale);
                projectile.knockback = currentStats.knockback * config.knockbackForceUnit;
                projectile.knockbackDuration = config.knockbackDuration;
            }
        }

    }
}
