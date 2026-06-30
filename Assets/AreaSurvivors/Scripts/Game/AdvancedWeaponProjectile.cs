using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class AdvancedWeaponProjectile : MonoBehaviour
    {
        readonly Dictionary<Health, float> hitTimers = new Dictionary<Health, float>();
        readonly HashSet<Health> piercedTargets = new HashSet<Health>();
        WeaponType type;
        WeaponStatBlock stats;
        GameConfig config;
        Vector2 direction;
        Vector2 boomerangVelocity;
        Vector2 boomerangAcceleration;
        float spawnTime;
        float lifetime;
        float outboundSeconds;
        float spinDegrees;
        TileGrid grid;

        public void Configure(WeaponType weaponType, Vector2 launchDirection, WeaponStatBlock weaponStats, GameConfig gameConfig)
        {
            type = weaponType;
            stats = weaponStats;
            config = gameConfig;
            grid = FindObjectOfType<TileGrid>();
            direction = launchDirection.sqrMagnitude > 0.01f ? launchDirection.normalized : Vector2.down;
            spawnTime = Time.time;
            float speed = Mathf.Max(0.1f, stats.projectileSpeed);
            float distance = Mathf.Max(stats.distance, stats.range);
            lifetime = type == WeaponType.ThunderBall
                ? Mathf.Max(0.1f, stats.durationSeconds)
                : Mathf.Max(0.1f, distance / speed);
            if (type == WeaponType.BoomerangSword) SetupBoomerangMotion(speed, distance);
            else outboundSeconds = lifetime;
            ApplyDirectionRoll(direction);
            ApplyVisualScale();
            if (type == WeaponType.ThunderBall)
            {
                var rangeVisual = GetComponentInChildren<ThunderBallRangeVisual>();
                if (rangeVisual != null) rangeVisual.Configure(stats.range);
            }
            PaintAttackTrail();
        }

        void Update()
        {
            if (Time.time - spawnTime >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (type == WeaponType.ThunderBall)
            {
                TickThunderBall();
                return;
            }

            if (type == WeaponType.BoomerangSword)
            {
                TickBoomerang();
                return;
            }

            var moveDirection = direction;
            transform.position += (Vector3)(moveDirection * Mathf.Max(0.1f, stats.projectileSpeed) * Time.deltaTime);
            PaintAttackTrail();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other);
        }

        void SetupBoomerangMotion(float speed, float distance)
        {
            var initialDirection = Random.insideUnitCircle;
            if (initialDirection.sqrMagnitude < 0.01f) initialDirection = Vector2.right;
            initialDirection.Normalize();

            direction = initialDirection;
            float travelDistance = Mathf.Max(0.35f, distance);
            outboundSeconds = Mathf.Clamp(2f * travelDistance / speed, 0.45f, 2.4f);
            lifetime = Mathf.Max(lifetime, outboundSeconds * 2.35f);

            boomerangVelocity = initialDirection * speed;
            boomerangAcceleration = -initialDirection * (speed / outboundSeconds);
        }

        void TickBoomerang()
        {
            boomerangVelocity += boomerangAcceleration * Time.deltaTime;
            transform.position += (Vector3)(boomerangVelocity * Time.deltaTime);
            PaintAttackTrail();

            spinDegrees += 1800f * Time.deltaTime;
            ApplyRoll(spinDegrees);
        }

        void TickThunderBall()
        {
            var target = FindNearestEnemy();
            if (target != null)
            {
                var desired = ((Vector2)(target.transform.position - transform.position)).normalized;
                direction = Vector2.Lerp(direction, desired, Time.deltaTime * 1.8f).normalized;
            }

            transform.position += (Vector3)(direction * Mathf.Max(0.1f, stats.projectileSpeed) * Time.deltaTime);
            ApplyDirectionRoll(direction);
            var colliders = Physics2D.OverlapCircleAll(transform.position, Mathf.Max(0.05f, stats.range));
            for (int i = 0; i < colliders.Length; i++)
            {
                TryDamage(colliders[i]);
            }
        }

        EnemyController FindNearestEnemy()
        {
            var enemies = FindObjectsOfType<EnemyController>();
            EnemyController best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null) continue;
                var health = enemy.GetComponent<Health>();
                if (health == null || health.IsDead) continue;
                var offset = (Vector2)(enemy.transform.position - transform.position);
                if (Vector2.Dot(direction, offset.normalized) < -0.25f) continue;
                float score = offset.sqrMagnitude;
                if (score >= bestScore) continue;
                bestScore = score;
                best = enemy;
            }

            return best;
        }

        void TryDamage(Collider2D other)
        {
            var enemy = other != null ? other.GetComponentInParent<EnemyController>() : null;
            if (enemy == null) return;
            var health = enemy.GetComponent<Health>();
            if (health == null || health.IsDead) return;
            if (type == WeaponType.Gun && piercedTargets.Contains(health)) return;
            if (!CanHit(health)) return;

            int dealt = health.Damage(Mathf.Max(0, stats.attackPower), enemy.transform.position);
            if (dealt <= 0) return;
            if (type == WeaponType.Gun) piercedTargets.Add(health);
            ApplyKnockback(enemy);
        }

        bool CanHit(Health health)
        {
            if (health == null) return false;
            float interval = type == WeaponType.ThunderBall
                ? Mathf.Max(0.05f, stats.damageIntervalSeconds)
                : type == WeaponType.Gun ? lifetime + 1f : 0.25f;
            if (hitTimers.TryGetValue(health, out var next) && Time.time < next) return false;
            hitTimers[health] = Time.time + interval;
            return true;
        }

        void ApplyKnockback(EnemyController enemy)
        {
            if (enemy == null || stats.knockback <= 0f || config == null) return;
            var receiver = enemy.GetComponent<KnockbackReceiver>();
            if (receiver == null) return;
            var knockDirection = ((Vector2)(enemy.transform.position - transform.position)).normalized;
            receiver.Apply(knockDirection, stats.knockback * config.knockbackForceUnit, config.knockbackDuration);
        }

        void PaintAttackTrail()
        {
            if (type != WeaponType.BoomerangSword && type != WeaponType.AuraSword) return;
            if (grid == null) grid = FindObjectOfType<TileGrid>();
            if (grid == null) return;
            float cellSize = Mathf.Max(0.01f, grid.cellSize);
            int radiusCells = Mathf.Max(1, Mathf.CeilToInt(stats.range / cellSize * 0.5f));
            grid.Paint(transform.position, TileOwner.Player, radiusCells);
        }

        void ApplyVisualScale()
        {
            if (type == WeaponType.Gun) return;
            float baseScale = type == WeaponType.Gun ? 0.32f : 0.42f;
            if (type == WeaponType.AuraSword) baseScale = Mathf.Clamp(stats.range * 0.35f, 0.5f, 1.6f);
            if (type == WeaponType.BoomerangSword) baseScale = Mathf.Clamp(stats.range * 0.42f, 0.36f, 1.2f);
            if (type == WeaponType.ThunderBall) baseScale = 0.42f;
            transform.localScale = Vector3.one * baseScale;
        }

        void ApplyDirectionRoll(Vector2 visualDirection)
        {
            var normalized = visualDirection.sqrMagnitude > 0.001f ? visualDirection.normalized : Vector2.right;
            ApplyRoll(Mathf.Atan2(normalized.y, normalized.x) * Mathf.Rad2Deg);
        }

        void ApplyRoll(float degrees)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, degrees);
            var billboards = GetComponentsInChildren<PaperBillboard>();
            for (int i = 0; i < billboards.Length; i++)
            {
                var billboard = billboards[i];
                if (billboard == null || billboard.GetComponent<ThunderBallRangeVisual>() != null) continue;
                billboard.rollDegrees = degrees;
            }
        }
    }
}
