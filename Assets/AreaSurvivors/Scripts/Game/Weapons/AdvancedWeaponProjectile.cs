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
        float thunderBallVerticalRadiusMultiplier = 1f;
        Transform orbitTarget;
        float orbitAngleDegrees;
        float orbitRadius;
        float orbitSpeedDegrees = 180f;
        Vector2 launchOrigin;
        ExcaliburSectorVisual excaliburSectorVisual;
        float excaliburArcDegrees;
        float excaliburInitialLength;
        float excaliburCurrentLength;
        float excaliburBandWidth;
        float excaliburRevealFraction;
        bool consumed;

        public void Configure(WeaponType weaponType, Vector2 launchDirection, WeaponStatBlock weaponStats, GameConfig gameConfig)
        {
            type = weaponType;
            stats = weaponStats;
            config = gameConfig;
            grid = FindObjectOfType<TileGrid>();
            thunderBallVerticalRadiusMultiplier = 1f;
            direction = launchDirection.sqrMagnitude > 0.01f ? launchDirection.normalized : Vector2.down;
            launchOrigin = transform.position;
            spawnTime = Time.time;
            float speed = Mathf.Max(0.1f, stats.projectileSpeed);
            float distance = Mathf.Max(stats.distance, stats.range);
            lifetime = type == WeaponType.ThunderBall || type == WeaponType.ThunderStorm
                ? Mathf.Max(0.1f, stats.durationSeconds)
                : Mathf.Max(0.1f, distance / speed);
            if (type == WeaponType.Excalibur)
            {
                float speedCells = config != null ? Mathf.Max(0.1f, config.excaliburTravelSpeedCellsPerSecond) : 5f;
                stats.projectileSpeed = Mathf.Max(0.1f, speedCells * TileGrid.DefaultCellSize);
                lifetime = Mathf.Max(0.1f, stats.distance / stats.projectileSpeed);
            }
            if (type == WeaponType.BoomerangSword || type == WeaponType.Banana) SetupBoomerangMotion(speed, distance, type == WeaponType.Banana);
            else outboundSeconds = lifetime;
            ApplyDirectionRoll(direction);
            ApplyVisualScale();
            if (type == WeaponType.ThunderBall || type == WeaponType.ThunderStorm)
            {
                thunderBallVerticalRadiusMultiplier = GridCellAspectY();
                var rangeVisual = GetComponentInChildren<ThunderBallRangeVisual>();
                if (rangeVisual != null) rangeVisual.Configure(stats.range, thunderBallVerticalRadiusMultiplier);
            }
            PaintAttackTrail();
        }

        public void ConfigureOrbit(Transform target, int index, int count, float radius)
        {
            orbitTarget = target;
            orbitAngleDegrees = 360f * Mathf.Max(0, index) / Mathf.Max(1, count);
            orbitRadius = Mathf.Max(0.05f, radius);
            orbitSpeedDegrees = Mathf.Max(90f, stats.rotationSpeed > 0f ? stats.rotationSpeed : 180f);
            if (orbitTarget != null) TickOrbit(0f);
        }

        void Update()
        {
            if (Time.time - spawnTime >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (orbitTarget != null)
            {
                TickOrbit(Time.deltaTime);
                return;
            }

            if (type == WeaponType.ThunderBall || type == WeaponType.ThunderStorm)
            {
                TickThunderBall();
                return;
            }

            if (type == WeaponType.BoomerangSword || type == WeaponType.Banana)
            {
                TickBoomerang();
                return;
            }

            if (type == WeaponType.Excalibur)
            {
                TickExcalibur();
                return;
            }

            var moveDirection = direction;
            transform.position += (Vector3)(moveDirection * Mathf.Max(0.1f, stats.projectileSpeed) * Time.deltaTime);
            PaintAttackTrail();
        }

        void TickOrbit(float deltaTime)
        {
            if (orbitTarget == null) return;
            orbitAngleDegrees += orbitSpeedDegrees * deltaTime;
            float radians = orbitAngleDegrees * Mathf.Deg2Rad;
            transform.position = orbitTarget.position + new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f) * orbitRadius;
            ApplyRoll(orbitAngleDegrees);
            TickThunderBallDamageOnly();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other);
        }

        void SetupBoomerangMotion(float speed, float distance, bool preserveLaunchDirection)
        {
            var initialDirection = preserveLaunchDirection ? direction : Random.insideUnitCircle;
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

        void TickExcalibur()
        {
            float elapsed = Mathf.Max(0f, Time.time - spawnTime);
            excaliburCurrentLength = CalculateExcaliburLength(
                elapsed,
                stats.projectileSpeed,
                excaliburInitialLength,
                stats.distance);
            excaliburRevealFraction = CalculateExcaliburRevealFraction(
                elapsed,
                stats.projectileSpeed,
                excaliburBandWidth);
            UpdateExcaliburShape();
            PaintAttackTrail();
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
            TickThunderBallDamageOnly();
        }

        void TickThunderBallDamageOnly()
        {
            float radiusX = Mathf.Max(0.05f, stats.range);
            float radiusY = Mathf.Max(0.05f, stats.range * thunderBallVerticalRadiusMultiplier);
            var colliders = Physics2D.OverlapCircleAll(transform.position, Mathf.Max(radiusX, radiusY));
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!ContainsThunderBallPoint(colliders[i].ClosestPoint(transform.position), radiusX, radiusY)) continue;
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
            if (consumed) return;
            var enemy = other != null ? other.GetComponentInParent<EnemyController>() : null;
            if (enemy == null) return;
            var health = enemy.GetComponent<Health>();
            if (health == null || health.IsDead) return;
            if ((type == WeaponType.Gun || type == WeaponType.MachineGun) && piercedTargets.Contains(health)) return;
            if (!CanHit(health)) return;

            int damage = Mathf.Max(0, stats.attackPower);
            int creditedDamage = health.DamageAmount(damage);
            int dealt = health.Damage(damage, enemy.transform.position);
            if (dealt <= 0) return;
            GameManager.Instance?.RegisterWeaponDamage(WeaponCatalog.BaseWeaponOf(type), creditedDamage);
            if (type == WeaponType.Gun || type == WeaponType.MachineGun) piercedTargets.Add(health);
            ApplyKnockback(enemy);
            if (type == WeaponType.MachineGun)
            {
                consumed = true;
                Destroy(gameObject);
            }
        }

        bool CanHit(Health health)
        {
            if (health == null) return false;
            float interval = type == WeaponType.ThunderBall || type == WeaponType.ThunderStorm
                ? Mathf.Max(0.05f, stats.damageIntervalSeconds)
                : type == WeaponType.Gun || type == WeaponType.MachineGun ? lifetime + 1f
                : type == WeaponType.Excalibur ? Mathf.Max(0.05f, stats.damageIntervalSeconds) : 0.25f;
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
            if (type != WeaponType.BoomerangSword && type != WeaponType.Banana && type != WeaponType.AuraSword && type != WeaponType.Excalibur) return;
            if (grid == null) grid = FindObjectOfType<TileGrid>();
            if (grid == null) return;
            if (type == WeaponType.Excalibur)
            {
                float fullInnerRadius = ExcaliburSectorVisual.CalculateInnerRadius(excaliburCurrentLength, excaliburBandWidth);
                float visibleInnerRadius = ExcaliburSectorVisual.CalculateVisibleInnerRadius(
                    fullInnerRadius,
                    excaliburCurrentLength,
                    excaliburRevealFraction);
                grid.PaintSector(
                    launchOrigin,
                    direction,
                    excaliburCurrentLength,
                    visibleInnerRadius,
                    excaliburArcDegrees * 0.5f,
                    TileOwner.Player);
                return;
            }
            float cellSize = Mathf.Max(0.01f, grid.cellSize);
            float radiusWorld = Mathf.Max(0.05f, stats.range * 0.5f);
            int radiusCells = Mathf.Max(1, Mathf.CeilToInt(radiusWorld / cellSize));
            grid.Paint(transform.position, TileOwner.Player, radiusCells);
        }

        float GridCellAspectY()
        {
            if (grid == null) grid = FindObjectOfType<TileGrid>();
            if (grid == null) return 1f;
            Vector2 cellSize = grid.WorldCellSize();
            return Mathf.Clamp(cellSize.y / Mathf.Max(0.01f, cellSize.x), 0.2f, 1f);
        }

        bool ContainsThunderBallPoint(Vector2 point, float radiusX, float radiusY)
        {
            Vector2 local = point - (Vector2)transform.position;
            float normalized = (local.x * local.x) / (radiusX * radiusX) + (local.y * local.y) / (radiusY * radiusY);
            return normalized <= 1f;
        }

        void ApplyVisualScale()
        {
            if (type == WeaponType.Gun || type == WeaponType.MachineGun) return;
            if (type == WeaponType.Excalibur)
            {
                InitializeExcaliburGrowth();
                UpdateExcaliburShape();
                return;
            }
            float baseScale = 0.42f;
            if (type == WeaponType.AuraSword) baseScale = Mathf.Clamp(stats.range * 0.35f, 0.5f, 1.6f);
            if (type == WeaponType.BoomerangSword) baseScale = Mathf.Clamp(stats.range * 0.42f, 0.36f, 1.2f);
            if (type == WeaponType.Banana) baseScale = Mathf.Max(0.05f, stats.range);
            if (type == WeaponType.ThunderBall || type == WeaponType.ThunderStorm) baseScale = 0.42f;
            transform.localScale = Vector3.one * baseScale;
        }

        void InitializeExcaliburGrowth()
        {
            float baseArcDegrees = config != null ? config.excaliburBaseArcDegrees : 30f;
            float maxArcDegrees = config != null ? config.excaliburMaxArcDegrees : 150f;
            float baseRange = stats.range;
            if (config != null)
            {
                baseRange = Mathf.Max(0.05f, config.GetWeaponStats(WeaponType.AuraSword, 1).range);
            }
            excaliburArcDegrees = CalculateExcaliburArcDegrees(stats.range, baseRange, baseArcDegrees, maxArcDegrees);
            excaliburInitialLength = TileGrid.DefaultCellSize *
                (config != null ? Mathf.Max(0.05f, config.excaliburInitialRadiusCells) : 0.25f);
            float bandWidthCellsPerAttack = config != null ? config.excaliburBandWidthCells : 3f;
            excaliburBandWidth = CalculateExcaliburBandWidth(bandWidthCellsPerAttack, stats.projectileCount);
            // The first visible sector stays close to the player.  Its small launch length is
            // intentionally independent from the finished strike thickness, so the reveal,
            // forward movement, and widening begin on the same frame instead of waiting for a
            // full-width sector to finish revealing at a fixed position.
            excaliburInitialLength = Mathf.Min(
                Mathf.Max(0.05f, stats.distance),
                excaliburInitialLength);
            excaliburCurrentLength = excaliburInitialLength;
            excaliburRevealFraction = 0f;
            excaliburSectorVisual = GetComponent<ExcaliburSectorVisual>();
            transform.localScale = Vector3.one;
        }

        void UpdateExcaliburShape()
        {
            if (excaliburSectorVisual == null) return;
            excaliburSectorVisual.Configure(
                excaliburCurrentLength,
                excaliburArcDegrees,
                excaliburBandWidth,
                excaliburRevealFraction);
        }

        public static float CalculateExcaliburArcDegrees(float currentRange, float baseRange, float baseArcDegrees, float maxArcDegrees)
        {
            float rangeRatio = Mathf.Max(0.05f, currentRange) / Mathf.Max(0.05f, baseRange);
            return Mathf.Clamp(baseArcDegrees * rangeRatio, 1f, maxArcDegrees);
        }

        public static float CalculateExcaliburLength(float elapsedSeconds, float speed, float initialLength, float maxLength)
        {
            float grownLength = Mathf.Max(0.05f, initialLength) +
                Mathf.Max(0f, elapsedSeconds) * Mathf.Max(0.1f, speed);
            return Mathf.Min(Mathf.Max(0.05f, maxLength), Mathf.Max(Mathf.Max(0.05f, initialLength), grownLength));
        }

        public static float CalculateExcaliburBandWidth(float cellsPerAttack, int attackCount)
        {
            return TileGrid.DefaultCellSize * Mathf.Max(0.05f, cellsPerAttack) * Mathf.Max(1, attackCount);
        }

        public static float CalculateExcaliburRevealFraction(float elapsedSeconds, float speed, float bandWidth)
        {
            float revealSeconds = Mathf.Max(0.01f, Mathf.Max(0.05f, bandWidth) / Mathf.Max(0.1f, speed));
            return Mathf.Clamp01(Mathf.Max(0f, elapsedSeconds) / revealSeconds);
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
