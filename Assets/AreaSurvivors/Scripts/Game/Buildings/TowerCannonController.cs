using UnityEngine;

namespace AreaSurvivors
{
    public sealed class TowerCannonController : MonoBehaviour
    {
        public GameConfig config;
        public GameObject projectilePrefab;
        public Sprite cannonballSprite;
        public Sprite explosionSprite;
        public Vector3 muzzleOffset = Vector3.zero;
        public AttackBounceAnimator attackBounce;
        int damageBonus;
        float explosionRadiusMultiplier = 1f;
        float cooldown;
        bool configured;
        TileGrid grid;

        public void Configure(GameConfig gameConfig)
        {
            config = gameConfig;
            cooldown = Mathf.Min(0.75f, CooldownSeconds());
            grid = FindObjectOfType<TileGrid>();
            if (attackBounce == null) attackBounce = GetComponent<AttackBounceAnimator>();
            configured = true;
        }

        public void ApplyTowerUpgrade(int bonusDamage, float radiusMultiplier)
        {
            damageBonus = Mathf.Max(0, bonusDamage);
            explosionRadiusMultiplier = Mathf.Max(0.05f, radiusMultiplier);
        }

        void Update()
        {
            if (!configured || config == null) return;
            if (!ProgressionStore.IsUnlocked(UpgradeType.UnlockTowerCannon)) return;

            GameManager.Instance?.MarkBuildingDamageSourceActive(RunDamageBuildingSource.CenterTower);
            cooldown -= Time.deltaTime;
            if (cooldown > 0f) return;
            cooldown = CooldownSeconds();

            var target = FindNearestEnemy();
            if (target == null) return;
            FireAt(target);
        }

        float CooldownSeconds()
        {
            return Mathf.Max(0.1f, config != null ? config.towerCannonCooldown : 3f);
        }

        EnemyController FindNearestEnemy()
        {
            float range = config != null ? Mathf.Max(0.1f, config.towerCannonRange) : 10f;
            float best = range * range;
            EnemyController nearest = null;
            var enemies = EnemyController.ActiveEnemies;
            var origin = transform.position;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float d = (enemy.AttackTargetPosition - origin).sqrMagnitude;
                if (d > best) continue;
                best = d;
                nearest = enemy;
            }

            return nearest;
        }

        void FireAt(EnemyController target)
        {
            if (target == null) return;
            var origin = transform.position + muzzleOffset;
            var direction = ((Vector2)(target.AttackTargetPosition - origin)).normalized;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.down;

            if (projectilePrefab == null)
            {
                Debug.LogError("Tower cannon projectile prefab is missing. Assign TowerCannonController.projectilePrefab on the CenterTower prefab.");
                return;
            }

            var projectileObject = Instantiate(projectilePrefab, origin, Quaternion.identity);
            var projectile = projectileObject.GetComponent<Projectile>();
            if (projectile == null)
            {
                Debug.LogError("Tower cannon projectile prefab is missing Projectile.");
                Destroy(projectileObject);
                return;
            }

            projectile.impactColor = new Color(1f, 0.76f, 0.24f, 0.96f);
            projectile.impactVisualScale = 0.9f * explosionRadiusMultiplier;
            projectile.knockback = config != null ? config.towerCannonKnockback : 2.2f;
            projectile.knockbackDuration = config != null ? config.knockbackDuration : 0.16f;
            projectile.paintsTerritory = false;
            projectile.SetDamageSource(RunDamageSource.ForBuilding(RunDamageBuildingSource.CenterTower));
            int baseDamage = (config != null ? config.towerCannonDamage : 8) + damageBonus;
            projectile.Launch(
                direction,
                RelicEffects.ApplyCenterTowerDamage(baseDamage, grid),
                config != null ? config.towerCannonProjectileSpeed : 9.5f,
                true,
                (config != null ? config.towerCannonExplosionRadius : 1.25f) * explosionRadiusMultiplier,
                config != null ? config.towerCannonProjectileLifetime : 4.2f,
                config != null ? config.towerCannonProjectileVisualScale : 0.32f);
            attackBounce?.PlayBounce();
        }
    }
}
