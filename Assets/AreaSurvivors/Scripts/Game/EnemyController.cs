using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class EnemyController : MonoBehaviour
    {
        public GameConfig config;
        public TileGrid grid;
        public Transform target;
        public GameObject xpOrbPrefab;
        public GameObject damagePopupPrefab;
        public DirectionalSpriteAnimator directionalAnimator;
        public float obstacleAvoidanceRadius = 1.45f;
        public float obstacleAvoidanceWeight = 1.65f;
        public int xpValue = 1;
        public int tokenValue;
        public int attackDamage = 3;
        public EnemyKind enemyKind = EnemyKind.Boar;
        public string displayName = "イノシシ";
        public bool elite;
        public bool boss;
        [Header("Stuck Recovery")]
        public float stuckDetectionSeconds = 1.5f;
        public float stuckMinimumProgress = 0.08f;
        public float stuckPositionThreshold = 0.14f;
        public float stuckRecoverySeconds = 2f;
        public float stuckRecoveryMinimumSeconds = 0.65f;
        public float stuckRecoveryAbsoluteLimitSeconds = 8f;
        public float stuckRecoveryCooldownSeconds = 2.5f;
        public float stuckMinimumTargetDistance = 2.5f;
        public bool SuppressStuckRecovery { get; set; }

        Rigidbody2D body;
        Health health;
        KnockbackReceiver knockback;
        Collider2D[] colliders;
        GridObjectVisual gridVisual;
        PaperMeshVisual visual;
        RuntimeSpriteOutline outline;
        float contactTimer;
        float speedMultiplier = 1f;
        float enemyCellSize = 1f;
        bool ignoresNaturalObstacles;
        bool expectsToMove;
        bool recoveringFromStuck;
        float stuckTimer;
        float stuckRecoveryTimer;
        float stuckRecoveryElapsed;
        float stuckRecoveryCooldown;
        float lastTargetDistance = -1f;
        Vector2 lastStuckSamplePosition;
        bool hasStuckSample;
        bool dying;
        Color desiredOutlineColor = Color.black;
        float desiredOutlineThickness = 0.018f;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            knockback = GetComponent<KnockbackReceiver>();
            if (knockback == null) knockback = gameObject.AddComponent<KnockbackReceiver>();
            colliders = GetComponents<Collider2D>();
            gridVisual = GetComponent<GridObjectVisual>();
            if (gridVisual == null) gridVisual = gameObject.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureCharacter(1f);
            visual = GetComponentInChildren<PaperMeshVisual>();
            outline = visual != null ? visual.GetComponent<RuntimeSpriteOutline>() : GetComponentInChildren<RuntimeSpriteOutline>();
            if (outline == null && visual != null) outline = visual.gameObject.AddComponent<RuntimeSpriteOutline>();
            var reveal = GetComponent<CharacterOcclusionReveal>();
            if (reveal == null) reveal = gameObject.AddComponent<CharacterOcclusionReveal>();
            reveal.silhouetteColor = new Color(1f, 0.52f, 0.28f, 0.56f);
            reveal.outlineColor = elite ? Color.yellow : boss ? Color.red : Color.white;
            ApplyOutlineStyle();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        public void Configure(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget, int hp, float speedScale)
        {
            Configure(gameConfig, tileGrid, chaseTarget, gameConfig != null ? gameConfig.GetEnemyDefinition(EnemyKind.Boar) : null, hp, speedScale);
        }

        public void Configure(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget, EnemyDefinition definition, int hp, float speedScale)
        {
            config = gameConfig;
            grid = tileGrid;
            target = chaseTarget;
            if (definition == null && config != null) definition = config.GetEnemyDefinition(EnemyKind.Boar);
            ApplyDefinition(definition);
            health.SetMax(hp);
            body.drag = 0f;
            speedMultiplier = Mathf.Max(0.05f, speedScale);
        }

        void ApplyDefinition(EnemyDefinition definition)
        {
            if (definition == null)
            {
                xpValue = config != null ? config.xpPerEnemy : 1;
                attackDamage = config != null ? config.enemyDamage : 3;
                transform.localScale = Vector3.one * (config != null ? Mathf.Max(0.1f, config.enemyVisualScale) : 1f);
                return;
            }

            enemyKind = definition.kind;
            displayName = string.IsNullOrEmpty(definition.displayName) ? definition.kind.ToString() : definition.displayName;
            if (directionalAnimator != null)
            {
                directionalAnimator.SetFramesFromResources(definition.spriteKey);
                directionalAnimator.SetPlaybackSpeedMultiplier(definition.animationSpeedMultiplier);
            }
            xpValue = Mathf.Max(0, definition.xpValue);
            tokenValue = Mathf.Max(0, definition.tokenValue);
            elite = definition.elite;
            boss = definition.boss;
            attackDamage = Mathf.Max(0, Mathf.RoundToInt((config != null ? config.enemyDamage : 3) * Mathf.Max(0f, definition.damageMultiplier)));
            float visualScale = config != null ? Mathf.Max(0.1f, config.enemyVisualScale) : 1f;
            float cellScale = Mathf.Max(0.1f, definition.cellSize);
            enemyCellSize = cellScale;
            ignoresNaturalObstacles = definition.elite || definition.boss;
            transform.localScale = Vector3.one * visualScale * cellScale;
            speedMultiplier = Mathf.Max(0.05f, definition.speedMultiplier);
            obstacleAvoidanceRadius = Mathf.Max(0.45f, 0.8f + cellScale * 0.8f);
            obstacleAvoidanceWeight = Mathf.Max(1.65f, 1.45f + cellScale * 0.45f);
            ConfigureFootCollider(cellScale);

            desiredOutlineColor = definition.outlineColor;
            desiredOutlineThickness = Mathf.Max(0.004f, definition.outlineThickness);
            if (definition.elite) desiredOutlineThickness = Mathf.Max(desiredOutlineThickness, 0.055f);
            if (definition.boss) desiredOutlineThickness = Mathf.Max(desiredOutlineThickness, 0.075f);
            ApplyOutlineStyle();
            if (ignoresNaturalObstacles) IgnoreNaturalObstacleCollisions();
        }

        void ConfigureFootCollider(float cellScale)
        {
            if (gridVisual == null) return;
            gridVisual.ConfigureCharacter(Mathf.Max(1f, cellScale));
            foreach (var box in GetComponents<BoxCollider2D>())
            {
                if (box != null) Destroy(box);
            }
            var circle = GetComponent<CircleCollider2D>();
            gridVisual.ConfigureCharacterCircle(circle);
            colliders = GetComponents<Collider2D>();
        }

        void LateUpdate()
        {
            ApplyOutlineStyle();
        }

        void ApplyOutlineStyle()
        {
            if (outline == null && visual != null) outline = visual.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) return;
            outline.outlineColor = desiredOutlineColor;
            outline.thickness = desiredOutlineThickness;
            outline.blink = false;
            var reveal = GetComponent<CharacterOcclusionReveal>();
            if (reveal != null) reveal.outlineColor = boss ? Color.red : elite ? Color.yellow : Color.white;
        }

        void Update()
        {
            expectsToMove = false;
            if (dying || target == null) return;
            if (knockback != null && knockback.Active)
            {
                if (directionalAnimator != null) directionalAnimator.Tick(Vector2.down, true);
                ResetStuckTracking();
                return;
            }
            expectsToMove = true;
            var targetDirection = ((Vector2)(target.position - transform.position)).normalized;
            var direction = AvoidObstacles(targetDirection);
            float slow = grid.GetMoveMultiplier(transform.position, TileOwner.Enemy, config.playerTerritorySlow);
            body.velocity = direction * config.enemyBaseSpeed * slow * speedMultiplier;
            if (directionalAnimator != null) directionalAnimator.Tick(direction, body.velocity.sqrMagnitude > 0.01f);
            grid.Paint(transform.position, TileOwner.Enemy, 1);
            UpdateStuckRecovery();
        }

        Vector2 AvoidObstacles(Vector2 targetDirection)
        {
            if (ignoresNaturalObstacles || recoveringFromStuck) return targetDirection;

            var position = (Vector2)transform.position;
            var avoidance = Vector2.zero;
            var nearby = Physics2D.OverlapCircleAll(position, obstacleAvoidanceRadius);
            foreach (var hit in nearby)
            {
                if (hit == null || hit.attachedRigidbody == body) continue;
                if (!IsNaturalObstacle(hit)) continue;

                var closest = hit.ClosestPoint(position);
                var away = position - closest;
                if (away.sqrMagnitude < 0.001f) away = position - (Vector2)hit.transform.position;
                if (away.sqrMagnitude < 0.001f) away = Vector2.Perpendicular(targetDirection);

                float distance = Mathf.Max(0.05f, away.magnitude);
                float strength = Mathf.Clamp01((obstacleAvoidanceRadius - distance) / obstacleAvoidanceRadius);
                avoidance += away.normalized * strength;
            }

            float castRadius = Mathf.Max(0.28f, 0.18f + enemyCellSize * 0.2f);
            float lookAhead = Mathf.Max(0.75f, 0.65f + enemyCellSize * 0.85f);
            var frontHits = Physics2D.CircleCastAll(position, castRadius, targetDirection, lookAhead);
            foreach (var frontHit in frontHits)
            {
                if (!IsNaturalObstacle(frontHit.collider)) continue;
                var side = Vector2.Perpendicular(targetDirection);
                if (Vector2.Dot(side, position - (Vector2)frontHit.collider.transform.position) < 0f) side = -side;
                float proximity = 1f - Mathf.Clamp01(frontHit.distance / lookAhead);
                avoidance += side * (0.9f + enemyCellSize * 0.35f) * Mathf.Lerp(0.55f, 1f, proximity);
            }

            var steered = targetDirection + avoidance * obstacleAvoidanceWeight;
            return steered.sqrMagnitude > 0.01f ? steered.normalized : targetDirection;
        }

        void IgnoreNaturalObstacleCollisions()
        {
            SetNaturalObstacleCollisionIgnored(true);
        }

        void SetNaturalObstacleCollisionIgnored(bool ignored)
        {
            if (colliders == null || colliders.Length == 0) return;
            var obstacles = FindObjectsOfType<Obstacle>();
            foreach (var obstacle in obstacles)
            {
                if (obstacle == null) continue;
                var obstacleColliders = obstacle.GetComponents<Collider2D>();
                foreach (var own in colliders)
                {
                    if (own == null) continue;
                    foreach (var obstacleCollider in obstacleColliders)
                    {
                        if (obstacleCollider != null) Physics2D.IgnoreCollision(own, obstacleCollider, ignored);
                    }
                }
            }
        }

        void UpdateStuckRecovery()
        {
            if (ignoresNaturalObstacles || dying || !expectsToMove || SuppressStuckRecovery || target == null)
            {
                ResetStuckTracking();
                return;
            }

            stuckRecoveryCooldown = Mathf.Max(0f, stuckRecoveryCooldown - Time.deltaTime);
            float targetDistance = Vector2.Distance(transform.position, target.position);
            if (recoveringFromStuck)
            {
                stuckRecoveryElapsed += Time.deltaTime;
                stuckRecoveryTimer -= Time.deltaTime;
                bool minimumRecoveryElapsed = stuckRecoveryElapsed >= Mathf.Max(0f, stuckRecoveryMinimumSeconds);
                bool safelyClear = minimumRecoveryElapsed && !IsNearNaturalObstacle(true) && !OverlapsNaturalObstacle();
                bool absoluteLimitReached = stuckRecoveryElapsed >= Mathf.Max(stuckRecoverySeconds, stuckRecoveryAbsoluteLimitSeconds);
                if (safelyClear || absoluteLimitReached)
                {
                    SetNaturalObstacleCollisionIgnored(false);
                    recoveringFromStuck = false;
                    stuckRecoveryElapsed = 0f;
                    stuckRecoveryCooldown = Mathf.Max(0f, stuckRecoveryCooldownSeconds);
                    ResetStuckTracking();
                }
                return;
            }

            if (targetDistance <= Mathf.Max(0f, stuckMinimumTargetDistance))
            {
                ResetStuckTracking();
                return;
            }

            if (lastTargetDistance < 0f)
            {
                lastTargetDistance = targetDistance;
                return;
            }

            if (!hasStuckSample)
            {
                stuckTimer = 0f;
                lastStuckSamplePosition = transform.position;
                lastTargetDistance = targetDistance;
                hasStuckSample = true;
                return;
            }

            stuckTimer += Time.deltaTime;
            if (stuckTimer < Mathf.Max(0.1f, stuckDetectionSeconds)) return;

            float positionProgress = Vector2.Distance(lastStuckSamplePosition, transform.position);
            float targetProgress = lastTargetDistance - targetDistance;
            bool positionUnchanged = positionProgress <= Mathf.Max(0.01f, stuckPositionThreshold);
            bool didNotApproachTarget = targetProgress < Mathf.Max(0.001f, stuckMinimumProgress);
            lastStuckSamplePosition = transform.position;
            lastTargetDistance = targetDistance;
            stuckTimer = 0f;
            if (!positionUnchanged || !didNotApproachTarget || stuckRecoveryCooldown > 0f || !IsNearNaturalObstacle(false)) return;

            SetNaturalObstacleCollisionIgnored(true);
            recoveringFromStuck = true;
            stuckRecoveryElapsed = 0f;
            stuckRecoveryTimer = Mathf.Max(0.1f, stuckRecoverySeconds);
        }

        bool IsNearNaturalObstacle(bool requireClearance)
        {
            float recoveryRadius = Mathf.Max(0.45f, obstacleAvoidanceRadius);
            float radius = requireClearance
                ? recoveryRadius + Mathf.Max(0.35f, enemyCellSize * 0.35f)
                : recoveryRadius;
            var nearby = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (var hit in nearby)
            {
                if (IsNaturalObstacle(hit)) return true;
            }
            return false;
        }

        bool OverlapsNaturalObstacle()
        {
            if (colliders == null) return false;
            var obstacles = FindObjectsOfType<Obstacle>();
            foreach (var own in colliders)
            {
                if (own == null || !own.enabled) continue;
                foreach (var obstacle in obstacles)
                {
                    if (obstacle == null) continue;
                    foreach (var obstacleCollider in obstacle.GetComponents<Collider2D>())
                    {
                        if (obstacleCollider == null || !obstacleCollider.enabled) continue;
                        if (own.Distance(obstacleCollider).isOverlapped) return true;
                    }
                }
            }

            return false;
        }

        void ResetStuckTracking()
        {
            stuckTimer = 0f;
            lastTargetDistance = target != null ? Vector2.Distance(transform.position, target.position) : -1f;
            lastStuckSamplePosition = transform.position;
            hasStuckSample = false;
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (dying) return;
            contactTimer -= Time.deltaTime;
            if (contactTimer > 0f) return;
            var otherHealth = collision.collider.GetComponentInParent<Health>();
            if (otherHealth == null) return;
            var barrier = collision.collider.GetComponentInParent<WoodenBarrier>();
            var ballista = collision.collider.GetComponentInParent<BallistaTower>();
            var carpenterHut = collision.collider.GetComponentInParent<CarpenterHut>();
            var workerHut = collision.collider.GetComponentInParent<WorkerHut>();
            var watchTower = collision.collider.GetComponentInParent<WatchTower>();
            if (collision.collider.GetComponentInParent<PlayerController>() == null &&
                collision.collider.GetComponentInParent<TowerController>() == null &&
                (barrier == null || !barrier.IsBuilt) &&
                (ballista == null || !ballista.IsBuilt) &&
                (carpenterHut == null || !carpenterHut.IsBuilt) &&
                (workerHut == null || !workerHut.IsBuilt) &&
                (watchTower == null || !watchTower.IsBuilt)) return;
            Vector3 hitPoint = collision.contactCount > 0
                ? collision.GetContact(0).point
                : collision.collider.ClosestPoint(transform.position);
            int dealt = otherHealth.Damage(attackDamage, hitPoint);
            DamagePopup.Show(damagePopupPrefab, hitPoint + Vector3.up * 0.18f, dealt, Color.red);
            contactTimer = 0.75f;
        }

        static bool IsNaturalObstacle(Collider2D collider)
        {
            if (collider == null || collider.GetComponentInParent<Obstacle>() == null) return false;
            return collider.GetComponentInParent<WoodenBarrier>() == null &&
                   collider.GetComponentInParent<BallistaTower>() == null &&
                   collider.GetComponentInParent<CarpenterHut>() == null &&
                   collider.GetComponentInParent<WorkerHut>() == null &&
                   collider.GetComponentInParent<WatchTower>() == null;
        }

        void OnDamaged(Health damagedHealth, int amount)
        {
            DamagePopup.Show(damagePopupPrefab, damagedHealth.LastDamagePoint + Vector3.up * 0.18f, amount, Color.white);
        }

        void OnDied(Health _)
        {
            if (dying) return;
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            dying = true;
            body.velocity = Vector2.zero;
            foreach (var col in colliders) col.enabled = false;
            if (directionalAnimator != null) directionalAnimator.enabled = false;

            var startScale = transform.localScale;
            float direction = transform.position.x < 0f ? -1f : 1f;
            var billboard = visual != null ? visual.GetComponent<PaperBillboard>() : null;
            float elapsed = 0f;
            const float duration = 0.48f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                if (billboard != null) billboard.rollDegrees = Mathf.Lerp(0f, 82f * direction, t);
                transform.localScale = new Vector3(startScale.x * Mathf.Lerp(1f, 1.08f, t), startScale.y * Mathf.Lerp(1f, 0.36f, t), startScale.z);
                if (visual != null)
                {
                    var color = visual.color;
                    color.a = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, t));
                    visual.color = color;
                }
                yield return null;
            }

            DropRewards();
            GameManager.Instance?.RegisterKill();
            if (boss) GameManager.Instance?.BossDefeated(this);
            Destroy(gameObject);
        }

        void DropRewards()
        {
            if (xpOrbPrefab != null && xpValue > 0)
            {
                var orb = Instantiate(xpOrbPrefab, transform.position, Quaternion.identity);
                var experience = orb.GetComponent<ExperienceOrb>();
                if (experience != null) experience.value = xpValue;
            }

            if (tokenValue > 0)
            {
                var token = TokenOrb.Spawn(transform.position + Vector3.right * 0.22f, tokenValue);
                if (boss && token != null)
                {
                    token.attractRange = 999f;
                    token.speed = 10f;
                }
            }
        }
    }
}
