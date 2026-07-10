using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(EnemyController), typeof(Health))]
    public sealed class BossSpecialAttackController : MonoBehaviour
    {
        public EnemyKind bossKind = EnemyKind.OrcKing;
        public GameObject shockwavePrefab;
        public Sprite[] downAttackFrames;
        public Sprite[] rightAttackFrames;
        public Sprite[] upAttackFrames;

        EnemyController enemy;
        Health health;
        DirectionalSpriteAnimator animator;
        PaperMeshVisual visual;
        EnemyBounceAnimation bounceAnimation;
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
                enemy.enemyKind == bossKind &&
                enemy.target != null &&
                !attacking;
        }

        IEnumerator AttackRoutine()
        {
            attacking = true;
            cooldownTimer = CooldownSeconds;
            var shockwaveDirection = AttackDirection();
            var direction = CardinalDirection(shockwaveDirection);
            var originalScale = visual != null ? visual.transform.localScale : Vector3.one;
            var originalPosition = visual != null ? visual.transform.localPosition : Vector3.zero;
            float originalSpriteHeight = visual != null && visual.sprite != null ? visual.sprite.bounds.size.y : 0f;

            enemy.SetActionLocked(true, direction);
            if (animator != null) animator.enabled = false;
            if (bounceAnimation != null) bounceAnimation.enabled = false;

            ApplyAttackFrame(direction, 0, originalScale, originalPosition, originalSpriteHeight);
            yield return WaitSeconds(RaiseSeconds);

            ApplyAttackFrame(direction, 1, originalScale, originalPosition, originalSpriteHeight);
            yield return WaitSeconds(SlamSeconds);
            yield return SpawnShockwaveSequence(shockwaveDirection);
            yield return WaitSeconds(RecoverSeconds);

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

        Vector2 AttackDirection()
        {
            var target = CurrentTarget();
            if (target != null)
            {
                var toTarget = (Vector2)(target.position - transform.position);
                if (toTarget.sqrMagnitude > 0.001f) return toTarget.normalized;
            }
            return enemy != null ? enemy.FacingDirection : Vector2.down;
        }

        Transform CurrentTarget()
        {
            Transform player = GameManager.Instance != null && GameManager.Instance.Player != null
                ? GameManager.Instance.Player.transform
                : null;
            Transform tower = GameManager.Instance != null && GameManager.Instance.Tower != null
                ? GameManager.Instance.Tower.transform
                : enemy != null ? enemy.target : null;

            if (player == null) return tower;
            if (tower == null) return player;
            float playerDistance = ((Vector2)(player.position - transform.position)).sqrMagnitude;
            float towerDistance = ((Vector2)(tower.position - transform.position)).sqrMagnitude;
            return playerDistance <= towerDistance ? player : tower;
        }

        static Vector2 CardinalDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return Vector2.down;
            return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x), 0f)
                : new Vector2(0f, Mathf.Sign(direction.y));
        }

        void ApplyAttackFrame(Vector2 direction, int frameIndex, Vector3 originalScale, Vector3 originalPosition, float originalSpriteHeight)
        {
            if (visual == null) return;
            bool mirrorX;
            var frame = FrameFor(direction, frameIndex, out mirrorX);
            if (frame != null) visual.sprite = frame;
            visual.transform.localScale = new Vector3(
                Mathf.Abs(originalScale.x) * (mirrorX ? -1f : 1f),
                originalScale.y,
                originalScale.z);
            if (frame != null && originalSpriteHeight > 0f)
            {
                float heightDifference = frame.bounds.size.y - originalSpriteHeight;
                visual.transform.localPosition = originalPosition + Vector3.up * (heightDifference * 0.5f * Mathf.Abs(originalScale.y));
            }
        }

        Sprite FrameFor(Vector2 direction, int frameIndex, out bool mirrorX)
        {
            mirrorX = false;
            Sprite[] frames;
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                frames = rightAttackFrames;
                mirrorX = direction.x < 0f;
            }
            else
            {
                frames = direction.y > 0f ? upAttackFrames : downAttackFrames;
            }

            if (frames != null && frames.Length > 0)
            {
                int index = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
                if (frames[index] != null) return frames[index];
                for (int i = 0; i < frames.Length; i++)
                {
                    if (frames[i] != null) return frames[i];
                }
            }

            return visual != null ? visual.sprite : null;
        }

        IEnumerator SpawnShockwaveSequence(Vector2 direction)
        {
            if (shockwavePrefab == null)
            {
                if (!warnedMissingPrefab)
                {
                    warnedMissingPrefab = true;
                    Debug.LogWarning("BossSpecialAttackController needs a shockwave prefab reference.");
                }
                yield break;
            }

            float cellSize = enemy != null && enemy.grid != null ? enemy.grid.cellSize : TileGrid.DefaultCellSize;
            var worldSize = new Vector2(
                Mathf.Max(0.1f, ShockwaveSizeCells.x) * cellSize,
                Mathf.Max(0.1f, ShockwaveSizeCells.y) * cellSize);
            int segmentCount = Mathf.Max(1, ShockwaveSegmentCount);
            float stepCells = Mathf.Max(0.1f, ShockwaveRangeCells) / segmentCount;
            int damage = Mathf.Max(0, enemy != null ? enemy.attackDamage : 0) * Mathf.Max(1, DamageMultiplier);
            for (int i = 0; i < segmentCount; i++)
            {
                float centerDistance = (i + 0.5f) * stepCells * cellSize;
                var position = transform.position + (Vector3)(direction.normalized * centerDistance);
                PaintShockwaveArea(position, worldSize);
                var projectileObject = Instantiate(shockwavePrefab, position, Quaternion.identity);
                var projectile = projectileObject.GetComponent<BossShockwaveProjectile>();
                if (projectile == null) projectile = projectileObject.AddComponent<BossShockwaveProjectile>();
                projectile.Configure(
                    worldSize,
                    damage,
                    enemy != null ? enemy.damagePopupPrefab : null,
                    ShockwaveDisplaySeconds);
                AudioManager.PlaySfx(SfxTrack.BossShockwaveHit);
                if (i < segmentCount - 1) yield return WaitSeconds(ShockwaveStepIntervalSeconds);
            }
        }

        void PaintShockwaveArea(Vector3 position, Vector2 worldSize)
        {
            var grid = enemy != null ? enemy.grid : null;
            if (grid == null && GameManager.Instance != null) grid = GameManager.Instance.grid;
            if (grid == null) return;

            float cellSize = Mathf.Max(0.01f, grid.cellSize);
            grid.PaintEllipseOverlappingCells(
                position,
                TileOwner.Enemy,
                Mathf.Max(0.1f, worldSize.x / cellSize * 0.5f),
                Mathf.Max(0.1f, worldSize.y / cellSize * 0.5f));
        }

        void LoadGeneratedFramesIfMissing()
        {
            if (downAttackFrames == null || downAttackFrames.Length == 0 || downAttackFrames[0] == null)
            {
                downAttackFrames = new[]
                {
                    GeneratedSpriteLoader.Load("Boss/OrcKing/Down_Raise"),
                    GeneratedSpriteLoader.Load("Boss/OrcKing/Down_Slam")
                };
            }
            if (rightAttackFrames == null || rightAttackFrames.Length == 0 || rightAttackFrames[0] == null)
            {
                rightAttackFrames = new[]
                {
                    GeneratedSpriteLoader.Load("Boss/OrcKing/Right_Raise"),
                    GeneratedSpriteLoader.Load("Boss/OrcKing/Right_Slam")
                };
            }
            if (upAttackFrames == null || upAttackFrames.Length == 0 || upAttackFrames[0] == null)
            {
                upAttackFrames = new[]
                {
                    GeneratedSpriteLoader.Load("Boss/OrcKing/Up_Raise"),
                    GeneratedSpriteLoader.Load("Boss/OrcKing/Up_Slam")
                };
            }
        }

        GameConfig Config => enemy != null ? enemy.config : null;
        float CooldownSeconds => Config != null ? Mathf.Max(0.05f, Config.bossSpecialAttackCooldownSeconds) : 5f;
        float RaiseSeconds => Config != null ? Mathf.Max(0f, Config.bossSpecialAttackRaiseSeconds) : 0.5f;
        float SlamSeconds => Config != null ? Mathf.Max(0f, Config.bossSpecialAttackSlamSeconds) : 0.35f;
        float RecoverSeconds => Config != null ? Mathf.Max(0f, Config.bossSpecialAttackRecoverSeconds) : 0.15f;
        float ShockwaveRangeCells => Config != null ? Mathf.Max(0.1f, Config.bossShockwaveRangeCells) : 10f;
        int ShockwaveSegmentCount => Config != null ? Mathf.Max(1, Config.bossShockwaveSegmentCount) : 5;
        float ShockwaveStepIntervalSeconds => Config != null ? Mathf.Max(0f, Config.bossShockwaveStepIntervalSeconds) : 0.12f;
        float ShockwaveDisplaySeconds => Config != null ? Mathf.Max(0.05f, Config.bossShockwaveDisplaySeconds) : 1f;
        Vector2 ShockwaveSizeCells => Config != null ? Config.bossShockwaveSizeCells : new Vector2(2f, 2f);
        int DamageMultiplier => Config != null ? Mathf.Max(1, Config.bossShockwaveDamageMultiplier) : 1;
    }
}
