using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(EnemyController), typeof(Health))]
    public sealed class LichSummonAttackController : MonoBehaviour
    {
        public GameObject summonCirclePrefab;
        public Sprite downCastFrame;
        public Sprite rightCastFrame;
        public Sprite leftCastFrame;
        public Sprite upCastFrame;

        EnemyController enemy;
        Health health;
        DirectionalSpriteAnimator animator;
        PaperMeshVisual visual;
        EnemyBounceAnimation bounceAnimation;
        EnemySpawner spawner;
        float cooldownTimer;
        bool attacking;
        bool warnedMissingPrefab;

        void Awake()
        {
            enemy = GetComponent<EnemyController>();
            health = GetComponent<Health>();
            animator = enemy != null ? enemy.directionalAnimator : GetComponent<DirectionalSpriteAnimator>();
            visual = GetComponentInChildren<PaperMeshVisual>(true);
            bounceAnimation = visual != null ? visual.GetComponent<EnemyBounceAnimation>() : null;
            LoadGeneratedFramesIfMissing();
        }

        void OnEnable()
        {
            cooldownTimer = CooldownSeconds;
        }

        void Update()
        {
            if (!CanAttack()) return;
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer > 0f) return;
            StartCoroutine(AttackRoutine());
        }

        bool CanAttack()
        {
            return enemy != null &&
                health != null &&
                !health.IsDead &&
                enemy.boss &&
                enemy.enemyKind == EnemyKind.Lich &&
                !attacking;
        }

        IEnumerator AttackRoutine()
        {
            attacking = true;
            cooldownTimer = CooldownSeconds;
            var direction = CardinalDirection(TargetDirection());
            var travelDirection = enemy != null ? enemy.FacingDirection : direction;
            var summonPosition = transform.position + (Vector3)CalculateSummonOffset(
                travelDirection,
                Random.Range(-90f, 90f),
                GridWorldCellSize(),
                SummonDistanceCells);
            var originalScale = visual != null ? visual.transform.localScale : Vector3.one;
            var originalPosition = visual != null ? visual.transform.localPosition : Vector3.zero;
            float originalSpriteHeight = visual != null && visual.sprite != null ? visual.sprite.bounds.size.y : 0f;

            enemy.SetActionLocked(true, direction);
            if (animator != null) animator.enabled = false;
            if (bounceAnimation != null) bounceAnimation.enabled = false;

            ApplyCastFrame(direction, originalScale, originalPosition, originalSpriteHeight);
            AudioManager.PlaySfx(SfxTrack.LichSummonMagic);
            yield return WaitSeconds(CastSeconds);
            if (health == null || health.IsDead)
            {
                FinishAttack(direction, originalScale, originalPosition);
                yield break;
            }

            SpawnSummonCircle(transform.position);
            SpawnSummonCircle(summonPosition);
            SummonEnemies(summonPosition);
            yield return WaitSeconds(RecoverSeconds);
            FinishAttack(direction, originalScale, originalPosition);
        }

        void FinishAttack(Vector2 direction, Vector3 originalScale, Vector3 originalPosition)
        {
            if (visual != null)
            {
                visual.transform.localScale = originalScale;
                visual.transform.localPosition = originalPosition;
            }
            if (animator != null)
            {
                animator.enabled = true;
                animator.Tick(direction, false);
            }
            if (bounceAnimation != null) bounceAnimation.enabled = true;
            enemy.SetActionLocked(false, direction);
            attacking = false;
        }

        IEnumerator WaitSeconds(float seconds)
        {
            float elapsed = 0f;
            seconds = Mathf.Max(0f, seconds);
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        Vector2 TargetDirection()
        {
            if (enemy != null && enemy.target != null)
            {
                var toTarget = (Vector2)(enemy.target.position - transform.position);
                if (toTarget.sqrMagnitude > 0.001f) return toTarget.normalized;
            }
            return enemy != null ? enemy.FacingDirection : Vector2.down;
        }

        static Vector2 CardinalDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return Vector2.down;
            return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x), 0f)
                : new Vector2(0f, Mathf.Sign(direction.y));
        }

        void ApplyCastFrame(Vector2 direction, Vector3 originalScale, Vector3 originalPosition, float originalSpriteHeight)
        {
            if (visual == null) return;
            var frame = FrameFor(direction);
            if (frame != null) visual.sprite = frame;
            visual.transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
            if (frame != null && originalSpriteHeight > 0f)
            {
                float heightDifference = frame.bounds.size.y - originalSpriteHeight;
                visual.transform.localPosition = originalPosition + Vector3.up * (heightDifference * 0.5f * Mathf.Abs(originalScale.y));
            }
        }

        Sprite FrameFor(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                return direction.x < 0f ? leftCastFrame : rightCastFrame;
            }
            return direction.y > 0f ? upCastFrame : downCastFrame;
        }

        void SpawnSummonCircle(Vector3 position)
        {
            if (summonCirclePrefab == null)
            {
                if (!warnedMissingPrefab)
                {
                    warnedMissingPrefab = true;
                    Debug.LogWarning("LichSummonAttackController needs a summon circle prefab reference.");
                }
                return;
            }

            var circleObject = Instantiate(summonCirclePrefab, position, Quaternion.identity);
            var effect = circleObject.GetComponent<LichSummonCircleEffect>();
            if (effect == null) effect = circleObject.AddComponent<LichSummonCircleEffect>();
            effect.Configure(SummonRadius, GridCellAspectY(), SummonCircleDurationSeconds);
        }

        void SummonEnemies(Vector3 summonPosition)
        {
            var enemySpawner = Spawner;
            if (enemySpawner == null) return;

            int skeletonCount = SkeletonCount;
            int skeletonKnightCount = SkeletonKnightCount;
            int requestedCount = skeletonCount + skeletonKnightCount;
            int availableCount = Mathf.Min(
                requestedCount,
                enemySpawner.RemainingAliveEnemyCapacity);
            if (availableCount <= 0)
            {
                CombatPerformanceDiagnostics.RecordSummonedEnemyCapBlocked(
                    requestedCount);
                return;
            }

            int allowedSkeletons = Mathf.Min(
                skeletonCount,
                Mathf.CeilToInt(
                    availableCount *
                    (skeletonCount / Mathf.Max(1f, requestedCount))));
            int allowedSkeletonKnights = Mathf.Min(
                skeletonKnightCount,
                availableCount - allowedSkeletons);
            int remainingSlots =
                availableCount - allowedSkeletons - allowedSkeletonKnights;
            if (remainingSlots > 0)
            {
                int additionalSkeletons = Mathf.Min(
                    skeletonCount - allowedSkeletons,
                    remainingSlots);
                allowedSkeletons += additionalSkeletons;
                remainingSlots -= additionalSkeletons;
                allowedSkeletonKnights += Mathf.Min(
                    skeletonKnightCount - allowedSkeletonKnights,
                    remainingSlots);
            }

            CombatPerformanceDiagnostics.RecordSummonedEnemyCapBlocked(
                requestedCount - allowedSkeletons - allowedSkeletonKnights);
            SummonEnemyBatch(EnemyKind.Skeleton, allowedSkeletons, summonPosition);
            SummonEnemyBatch(EnemyKind.SkeletonKnight, allowedSkeletonKnights, summonPosition);
        }

        void SummonEnemyBatch(EnemyKind kind, int count, Vector3 summonPosition)
        {
            var enemySpawner = Spawner;
            if (enemySpawner == null) return;
            int safeCount = Mathf.Max(0, count);
            for (int i = 0; i < safeCount; i++)
            {
                if (enemySpawner.SpawnSummonedEnemy(
                        kind,
                        RandomPointInSummonArea(summonPosition, i, safeCount)) == null)
                {
                    break;
                }
            }
        }

        Vector3 RandomPointInSummonArea(Vector3 summonPosition, int index, int count)
        {
            float radiusX = SummonRadius;
            float radiusY = SummonRadius * GridCellAspectY();
            float angle = (index / Mathf.Max(1f, count)) * Mathf.PI * 2f + Random.Range(-0.28f, 0.28f);
            float distance = Mathf.Sqrt(Random.Range(0.12f, 1f));
            var local = new Vector2(Mathf.Cos(angle) * radiusX * distance, Mathf.Sin(angle) * radiusY * distance);
            return summonPosition + (Vector3)local;
        }

        public static Vector2 CalculateSummonOffset(
            Vector2 forwardDirection,
            float angleDegrees,
            Vector2 worldCellSize,
            float distanceCells)
        {
            worldCellSize.x = Mathf.Max(0.01f, Mathf.Abs(worldCellSize.x));
            worldCellSize.y = Mathf.Max(0.01f, Mathf.Abs(worldCellSize.y));
            var cellForward = new Vector2(
                forwardDirection.x / worldCellSize.x,
                forwardDirection.y / worldCellSize.y);
            if (cellForward.sqrMagnitude < 0.001f) cellForward = Vector2.down;
            cellForward.Normalize();

            float angle = Mathf.Clamp(angleDegrees, -90f, 90f) * Mathf.Deg2Rad;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);
            var cellDirection = new Vector2(
                cellForward.x * cos - cellForward.y * sin,
                cellForward.x * sin + cellForward.y * cos);
            float distance = Mathf.Max(0f, distanceCells);
            return new Vector2(
                cellDirection.x * worldCellSize.x * distance,
                cellDirection.y * worldCellSize.y * distance);
        }

        float GridCellAspectY()
        {
            Vector2 cellSize = GridWorldCellSize();
            return Mathf.Clamp(cellSize.y / cellSize.x, 0.2f, 1f);
        }

        Vector2 GridWorldCellSize()
        {
            var grid = enemy != null ? enemy.grid : null;
            if (grid == null) grid = FindObjectOfType<TileGrid>();
            if (grid == null) return Vector2.one;
            Vector2 cellSize = grid.WorldCellSize();
            return new Vector2(
                Mathf.Max(0.01f, Mathf.Abs(cellSize.x)),
                Mathf.Max(0.01f, Mathf.Abs(cellSize.y)));
        }

        void LoadGeneratedFramesIfMissing()
        {
            if (downCastFrame == null) downCastFrame = GeneratedSpriteLoader.Load("Boss/Lich/Down_Cast");
            if (rightCastFrame == null) rightCastFrame = GeneratedSpriteLoader.Load("Boss/Lich/Right_Cast");
            if (leftCastFrame == null) leftCastFrame = GeneratedSpriteLoader.Load("Boss/Lich/Left_Cast");
            if (upCastFrame == null) upCastFrame = GeneratedSpriteLoader.Load("Boss/Lich/Up_Cast");
        }

        EnemySpawner Spawner
        {
            get
            {
                if (spawner == null && GameManager.Instance != null) spawner = GameManager.Instance.spawner;
                if (spawner == null) spawner = FindObjectOfType<EnemySpawner>();
                return spawner;
            }
        }

        GameConfig Config => enemy != null ? enemy.config : null;
        float CooldownSeconds => Config != null ? Mathf.Max(0.05f, Config.bossSpecialAttackCooldownSeconds) : 5f;
        float CastSeconds => Config != null ? Mathf.Max(0f, Config.bossSpecialAttackRaiseSeconds) : 0.5f;
        float RecoverSeconds => Config != null ? Mathf.Max(0f, Config.bossSpecialAttackRecoverSeconds) : 0.15f;
        float SummonRadius => Config != null ? Mathf.Max(0.2f, Config.lichSummonRadius) : 4f;
        float SummonDistanceCells => Config != null ? Mathf.Max(0f, Config.lichSummonDistanceCells) : 10f;
        float SummonCircleDurationSeconds => Config != null ? Mathf.Max(0.1f, Config.lichSummonCircleDurationSeconds) : 2.2f;
        int SkeletonCount => Config != null ? Mathf.Max(0, Config.lichSummonSkeletonCount) : 10;
        int SkeletonKnightCount => Config != null ? Mathf.Max(0, Config.lichSummonSkeletonKnightCount) : 10;
    }
}
