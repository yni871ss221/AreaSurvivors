using UnityEngine;

namespace AreaSurvivors
{
    public sealed class TowerCannonController : MonoBehaviour
    {
        public GameConfig config;
        public Sprite cannonballSprite;
        public Sprite explosionSprite;
        public Vector3 muzzleOffset = Vector3.zero;
        int damageBonus;
        float explosionRadiusMultiplier = 1f;
        float cooldown;
        bool configured;

        public void Configure(GameConfig gameConfig)
        {
            config = gameConfig;
            cannonballSprite = Resources.Load<Sprite>("Generated/Cannonball");
            explosionSprite = Resources.Load<Sprite>("Generated/CannonExplosion");
            cooldown = Mathf.Min(0.75f, CooldownSeconds());
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
            var enemies = FindObjectsOfType<EnemyController>();
            var origin = transform.position;
            foreach (var enemy in enemies)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                float d = (enemy.transform.position - origin).sqrMagnitude;
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
            var direction = ((Vector2)(target.transform.position - origin)).normalized;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.down;

            var projectileObject = new GameObject("Tower Cannonball");
            projectileObject.transform.position = origin;
            projectileObject.transform.localScale = Vector3.one;

            var body = projectileObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.drag = 0f;

            var collider = projectileObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.16f;

            var visualObject = new GameObject("Paper Visual");
            visualObject.transform.SetParent(projectileObject.transform, false);
            visualObject.AddComponent<PaperBillboard>();
            var visual = visualObject.AddComponent<PaperMeshVisual>();
            visual.Configure(cannonballSprite, Color.white, WeaponSortingOrders.Projectile);

            var outline = visualObject.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.018f;

            var projectile = projectileObject.AddComponent<Projectile>();
            projectile.impactSprite = explosionSprite;
            projectile.impactColor = new Color(1f, 0.76f, 0.24f, 0.96f);
            projectile.impactVisualScale = 0.9f * explosionRadiusMultiplier;
            projectile.knockback = config != null ? config.towerCannonKnockback : 2.2f;
            projectile.knockbackDuration = config != null ? config.knockbackDuration : 0.16f;
            projectile.Launch(
                direction,
                (config != null ? config.towerCannonDamage : 8) + damageBonus,
                config != null ? config.towerCannonProjectileSpeed : 9.5f,
                true,
                (config != null ? config.towerCannonExplosionRadius : 1.25f) * explosionRadiusMultiplier,
                config != null ? config.towerCannonProjectileLifetime : 4.2f,
                config != null ? config.towerCannonProjectileVisualScale : 0.32f);
        }
    }
}
