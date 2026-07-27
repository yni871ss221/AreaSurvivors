using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class AdvancedWeaponProjectile : MonoBehaviour
    {
        readonly Dictionary<Health, float> hitTimers = new Dictionary<Health, float>();
        readonly Dictionary<Collider2D, EnemyController> colliderEnemyCache = new Dictionary<Collider2D, EnemyController>();
        readonly Dictionary<EnemyController, Health> enemyHealthCache = new Dictionary<EnemyController, Health>();
        readonly HashSet<Health> piercedTargets = new HashSet<Health>();
        readonly HashSet<EnemyController> queriedEnemies = new HashSet<EnemyController>();
        Collider2D[] overlapBuffer = new Collider2D[128];
        Collider2D projectileCollider;
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
        float excaliburLastScanInnerRadius;
        float excaliburLastScanOuterRadius;
        float nextAreaDamageAt;
        float nextBananaDamageScanAt;
        float nextExcaliburScanAt;
        float nextThunderTargetScanAt;
        EnemyController cachedThunderTarget;
        Vector2 lastBananaDamageScanPosition;
        float bananaDamageRadius;
        bool consumed;
        const float MinimumDamageScanIntervalSeconds = 0.05f;
        const float ThunderTargetScanIntervalSeconds = 0.1f;
        public const float BananaDamageScanIntervalSeconds = 0.25f;
        const int MaximumOverlapBufferSize = 4096;

        public void Configure(WeaponType weaponType, Vector2 launchDirection, WeaponStatBlock weaponStats, GameConfig gameConfig)
        {
            type = weaponType;
            stats = weaponStats;
            config = gameConfig;
            grid = FindObjectOfType<TileGrid>();
            projectileCollider = GetComponent<Collider2D>();
            thunderBallVerticalRadiusMultiplier = 1f;
            direction = launchDirection.sqrMagnitude > 0.01f ? launchDirection.normalized : Vector2.down;
            launchOrigin = transform.position;
            spawnTime = Time.time;
            nextAreaDamageAt = Time.time;
            nextBananaDamageScanAt = Time.time;
            nextExcaliburScanAt = Time.time;
            nextThunderTargetScanAt = Time.time;
            cachedThunderTarget = null;
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
            ConfigureDamageDetection();
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
            if (type == WeaponType.Excalibur || type == WeaponType.Banana) return;
            CombatPerformanceDiagnostics.RecordProjectileTriggerCallback();
            TryDamage(other);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (type == WeaponType.Excalibur || type == WeaponType.Banana) return;
            CombatPerformanceDiagnostics.RecordProjectileTriggerCallback();
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
            if (type == WeaponType.Banana) TickBananaDamage();
        }

        void ConfigureDamageDetection()
        {
            if (type != WeaponType.Banana || projectileCollider == null) return;
            bananaDamageRadius = ResolveColliderQueryRadius(projectileCollider);
            lastBananaDamageScanPosition = transform.position;
            nextBananaDamageScanAt = Time.time;
            projectileCollider.enabled = false;
        }

        void TickBananaDamage()
        {
            if (Time.time < nextBananaDamageScanAt) return;
            nextBananaDamageScanAt =
                Time.time + BananaDamageScanIntervalSeconds;

            Vector2 currentPosition = transform.position;
            int colliderCount = QueryOverlapCapsule(
                lastBananaDamageScanPosition,
                currentPosition,
                bananaDamageRadius);
            lastBananaDamageScanPosition = currentPosition;
            CombatPerformanceDiagnostics.RecordProjectileOverlapQuery(colliderCount);
            CombatPerformanceDiagnostics.RecordBananaOverlapQuery(colliderCount);

            queriedEnemies.Clear();
            for (int i = 0; i < colliderCount; i++)
            {
                var enemy = ResolveEnemy(overlapBuffer[i]);
                if (enemy == null || !queriedEnemies.Add(enemy)) continue;
                var health = ResolveHealth(enemy);
                if (health == null || health.IsDead || IsHitCoolingDown(health))
                    continue;
                TryDamageEnemy(enemy, health);
            }
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
            TickExcaliburDamage();
        }

        void TickThunderBall()
        {
            if (Time.time >= nextThunderTargetScanAt)
            {
                nextThunderTargetScanAt = Time.time + ThunderTargetScanIntervalSeconds;
                cachedThunderTarget = FindNearestEnemy();
            }
            if (cachedThunderTarget != null)
            {
                var desired = ((Vector2)(cachedThunderTarget.AttackTargetPosition - transform.position)).normalized;
                direction = Vector2.Lerp(direction, desired, Time.deltaTime * 1.8f).normalized;
            }

            transform.position += (Vector3)(direction * Mathf.Max(0.1f, stats.projectileSpeed) * Time.deltaTime);
            ApplyDirectionRoll(direction);
            TickThunderBallDamageOnly();
        }

        void TickThunderBallDamageOnly()
        {
            float damageInterval = Mathf.Max(0.05f, stats.damageIntervalSeconds);
            if (Time.time < nextAreaDamageAt) return;
            nextAreaDamageAt = Time.time + damageInterval;

            float radiusX = Mathf.Max(0.05f, stats.range);
            float radiusY = Mathf.Max(0.05f, stats.range * thunderBallVerticalRadiusMultiplier);
            int colliderCount = QueryOverlapCircle(transform.position, Mathf.Max(radiusX, radiusY));
            CombatPerformanceDiagnostics.RecordProjectileOverlapQuery(colliderCount);
            queriedEnemies.Clear();
            for (int i = 0; i < colliderCount; i++)
            {
                var collider = overlapBuffer[i];
                if (collider == null) continue;
                if (!ContainsThunderBallPoint(collider.ClosestPoint(transform.position), radiusX, radiusY)) continue;
                var enemy = collider.GetComponentInParent<EnemyController>();
                if (enemy == null || !queriedEnemies.Add(enemy)) continue;
                TryDamageEnemy(enemy);
            }
        }

        void TickExcaliburDamage()
        {
            float damageInterval = Mathf.Max(0.05f, stats.damageIntervalSeconds);
            if (Time.time < nextExcaliburScanAt) return;
            nextExcaliburScanAt = Time.time + CalculateExcaliburScanInterval(damageInterval);

            float outerRadius = Mathf.Max(0.05f, excaliburCurrentLength);
            float fullInnerRadius = ExcaliburSectorVisual.CalculateInnerRadius(outerRadius, excaliburBandWidth);
            float innerRadius = ExcaliburSectorVisual.CalculateVisibleInnerRadius(
                fullInnerRadius,
                outerRadius,
                excaliburRevealFraction);
            // The visible annular sector travels between damage ticks. Query the full radial
            // sweep since the previous tick so enemies crossed between samples are not missed.
            float sweptInnerRadius = Mathf.Min(excaliburLastScanInnerRadius, innerRadius);
            float sweptOuterRadius = Mathf.Max(excaliburLastScanOuterRadius, outerRadius);
            int colliderCount = QueryOverlapCircle(launchOrigin, sweptOuterRadius);
            CombatPerformanceDiagnostics.RecordProjectileOverlapQuery(colliderCount);
            queriedEnemies.Clear();
            for (int i = 0; i < colliderCount; i++)
            {
                var collider = overlapBuffer[i];
                var enemy = ResolveEnemy(collider);
                if (enemy == null || queriedEnemies.Contains(enemy)) continue;
                var health = ResolveHealth(enemy);
                if (health == null || health.IsDead || IsHitCoolingDown(health)) continue;
                if (!ColliderIntersectsExcaliburSector(
                    collider,
                    launchOrigin,
                    direction,
                    sweptInnerRadius,
                    sweptOuterRadius,
                    excaliburArcDegrees * 0.5f)) continue;
                queriedEnemies.Add(enemy);
                TryDamageEnemy(enemy, health);
            }
            excaliburLastScanInnerRadius = innerRadius;
            excaliburLastScanOuterRadius = outerRadius;
        }

        int QueryOverlapCircle(Vector2 center, float radius)
        {
            while (true)
            {
                int count = Physics2D.OverlapCircleNonAlloc(center, radius, overlapBuffer);
                if (count < overlapBuffer.Length || overlapBuffer.Length >= MaximumOverlapBufferSize) return count;
                int nextSize = Mathf.Min(MaximumOverlapBufferSize, overlapBuffer.Length * 2);
                overlapBuffer = new Collider2D[nextSize];
            }
        }

        int QueryOverlapCapsule(
            Vector2 start,
            Vector2 end,
            float radius)
        {
            float safeRadius = Mathf.Max(0.05f, radius);
            Vector2 movement = end - start;
            float distance = movement.magnitude;
            if (distance <= 0.0001f)
            {
                return QueryOverlapCircle(end, safeRadius);
            }

            Vector2 center = (start + end) * 0.5f;
            Vector2 size = new Vector2(
                distance + safeRadius * 2f,
                safeRadius * 2f);
            float angle =
                Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
            while (true)
            {
                int count = Physics2D.OverlapCapsuleNonAlloc(
                    center,
                    size,
                    CapsuleDirection2D.Horizontal,
                    angle,
                    overlapBuffer);
                if (count < overlapBuffer.Length ||
                    overlapBuffer.Length >= MaximumOverlapBufferSize)
                {
                    return count;
                }
                int nextSize = Mathf.Min(
                    MaximumOverlapBufferSize,
                    overlapBuffer.Length * 2);
                overlapBuffer = new Collider2D[nextSize];
            }
        }

        static float ResolveColliderQueryRadius(Collider2D collider)
        {
            if (collider == null) return 0.05f;
            var scale = collider.transform.lossyScale;
            float maximumScale = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y));
            if (collider is CircleCollider2D circle)
            {
                return Mathf.Max(
                    0.05f,
                    circle.radius * maximumScale);
            }

            var extents = collider.bounds.extents;
            return Mathf.Max(
                0.05f,
                Mathf.Max(extents.x, extents.y));
        }

        static bool ColliderIntersectsExcaliburSector(
            Collider2D collider,
            Vector2 origin,
            Vector2 forward,
            float innerRadius,
            float outerRadius,
            float halfArcDegrees)
        {
            if (collider == null) return false;
            var bounds = collider.bounds;
            Vector2 boundsCenter = bounds.center;
            Vector2 centerOffset = boundsCenter - origin;
            float centerDistance = centerOffset.magnitude;
            float boundsRadius = new Vector2(bounds.extents.x, bounds.extents.y).magnitude;
            float clampedInnerRadius = Mathf.Max(0f, innerRadius);
            float clampedOuterRadius = Mathf.Max(clampedInnerRadius, outerRadius);
            if (centerDistance - boundsRadius > clampedOuterRadius ||
                centerDistance + boundsRadius < clampedInnerRadius)
            {
                return false;
            }

            Vector2 normalizedForward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.right;
            if (centerDistance > boundsRadius + 0.001f)
            {
                float angularAllowance = Mathf.Asin(Mathf.Clamp01(boundsRadius / centerDistance)) * Mathf.Rad2Deg;
                if (Vector2.Angle(normalizedForward, centerOffset) > halfArcDegrees + angularAllowance)
                    return false;
            }

            if (ContainsExcaliburPoint(boundsCenter, origin, forward, innerRadius, outerRadius, halfArcDegrees)) return true;

            float middleRadius = (Mathf.Max(0f, innerRadius) + Mathf.Max(0f, outerRadius)) * 0.5f;
            Vector2 middlePoint = origin + normalizedForward * middleRadius;
            Vector2 closestToMiddle = collider.ClosestPoint(middlePoint);
            if (ContainsExcaliburPoint(closestToMiddle, origin, forward, innerRadius, outerRadius, halfArcDegrees)) return true;

            Vector2 closestToOrigin = collider.ClosestPoint(origin);
            return ContainsExcaliburPoint(closestToOrigin, origin, forward, innerRadius, outerRadius, halfArcDegrees);
        }

        EnemyController FindNearestEnemy()
        {
            var enemies = EnemyController.ActiveEnemies;
            CombatPerformanceDiagnostics.RecordProjectileTargetScan(enemies.Count);
            EnemyController best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < enemies.Count; i++)
            {
                var enemy = enemies[i];
                if (enemy == null || !enemy.IsAlive) continue;
                var offset = (Vector2)(enemy.AttackTargetPosition - transform.position);
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
            var enemy = ResolveEnemy(other);
            TryDamageEnemy(enemy);
        }

        void TryDamageEnemy(EnemyController enemy)
        {
            if (consumed) return;
            if (enemy == null) return;
            var health = ResolveHealth(enemy);
            TryDamageEnemy(enemy, health);
        }

        void TryDamageEnemy(EnemyController enemy, Health health)
        {
            if (consumed) return;
            if (enemy == null) return;
            if (health == null || health.IsDead) return;
            if ((type == WeaponType.Gun || type == WeaponType.MachineGun) && piercedTargets.Contains(health)) return;
            if (!CanHit(health)) return;

            CombatPerformanceDiagnostics.RecordProjectileDamageAttempt();
            int damage = Mathf.Max(0, stats.attackPower);
            int creditedDamage = health.DamageAmount(damage);
            int dealt = health.Damage(damage, enemy.transform.position);
            if (dealt <= 0) return;
            CombatPerformanceDiagnostics.RecordProjectileDamageHit(type);
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
                : type == WeaponType.Excalibur ? Mathf.Max(0.05f, stats.damageIntervalSeconds)
                : type == WeaponType.Banana ? BananaDamageScanIntervalSeconds
                : 0.25f;
            if (hitTimers.TryGetValue(health, out var next) && Time.time < next) return false;
            hitTimers[health] = Time.time + interval;
            return true;
        }

        bool IsHitCoolingDown(Health health)
        {
            return health != null &&
                hitTimers.TryGetValue(health, out var next) &&
                Time.time < next;
        }

        EnemyController ResolveEnemy(Collider2D collider)
        {
            if (collider == null) return null;
            if (colliderEnemyCache.TryGetValue(collider, out var cachedEnemy))
                return cachedEnemy;

            var enemy = collider.GetComponentInParent<EnemyController>();
            colliderEnemyCache[collider] = enemy;
            return enemy;
        }

        Health ResolveHealth(EnemyController enemy)
        {
            if (enemy == null) return null;
            if (enemyHealthCache.TryGetValue(enemy, out var cachedHealth))
                return cachedHealth;

            var health = enemy.GetComponent<Health>();
            enemyHealthCache[enemy] = health;
            return health;
        }

        public static float CalculateExcaliburScanInterval(float damageIntervalSeconds)
        {
            return Mathf.Max(MinimumDamageScanIntervalSeconds, damageIntervalSeconds);
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
                CombatPerformanceDiagnostics.RecordAttackPaint(type);
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
            CombatPerformanceDiagnostics.RecordAttackPaint(type);
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
            float initialFullInnerRadius = ExcaliburSectorVisual.CalculateInnerRadius(
                excaliburCurrentLength,
                excaliburBandWidth);
            excaliburLastScanInnerRadius = ExcaliburSectorVisual.CalculateVisibleInnerRadius(
                initialFullInnerRadius,
                excaliburCurrentLength,
                excaliburRevealFraction);
            excaliburLastScanOuterRadius = excaliburCurrentLength;
            excaliburSectorVisual = GetComponent<ExcaliburSectorVisual>();
            if (excaliburSectorVisual != null) excaliburSectorVisual.SetRuntimeCombatColliderEnabled(false);
            transform.localScale = Vector3.one;
        }

        bool UpdateExcaliburShape()
        {
            if (excaliburSectorVisual == null) return true;
            return excaliburSectorVisual.ConfigureIfChanged(
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

        public static bool ContainsExcaliburPoint(
            Vector2 point,
            Vector2 origin,
            Vector2 forward,
            float innerRadius,
            float outerRadius,
            float halfArcDegrees)
        {
            Vector2 offset = point - origin;
            float distanceSquared = offset.sqrMagnitude;
            float safeInnerRadius = Mathf.Max(0f, innerRadius);
            float safeOuterRadius = Mathf.Max(safeInnerRadius, outerRadius);
            if (distanceSquared < safeInnerRadius * safeInnerRadius ||
                distanceSquared > safeOuterRadius * safeOuterRadius) return false;
            if (distanceSquared <= 0.000001f) return safeInnerRadius <= 0f;

            Vector2 safeForward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector2.right;
            Vector2 normalizedOffset = offset / Mathf.Sqrt(distanceSquared);
            float dot = Mathf.Clamp(Vector2.Dot(safeForward, normalizedOffset), -1f, 1f);
            float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;
            return angle <= Mathf.Clamp(halfArcDegrees, 0f, 180f);
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
