using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(EnemyController), typeof(Health))]
    public sealed class DragonBreathAttackController : MonoBehaviour
    {
        public GameObject breathProjectilePrefab;
        public Sprite downMouthClosedFrame;
        public Sprite downMouthOpenFrame;
        public Sprite rightMouthClosedFrame;
        public Sprite rightMouthOpenFrame;
        public Sprite leftMouthClosedFrame;
        public Sprite leftMouthOpenFrame;
        public Sprite upMouthClosedFrame;
        public Sprite upMouthOpenFrame;

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
                enemy.enemyKind == EnemyKind.Dragon &&
                !attacking;
        }

        IEnumerator AttackRoutine()
        {
            attacking = true;
            cooldownTimer = CooldownSeconds;
            var target = CurrentTarget();
            var launchDirection = TargetDirection(target);
            var facingDirection = CardinalDirection(launchDirection);
            var originalScale = visual != null ? visual.transform.localScale : Vector3.one;
            var originalPosition = visual != null ? visual.transform.localPosition : Vector3.zero;
            float originalSpriteHeight = visual != null && visual.sprite != null ? visual.sprite.bounds.size.y : 0f;

            enemy.SetActionLocked(true, facingDirection);
            if (animator != null) animator.enabled = false;
            if (bounceAnimation != null) bounceAnimation.enabled = false;

            ApplyMouthFrame(facingDirection, false, originalScale, originalPosition, originalSpriteHeight);
            yield return WaitSeconds(MouthClosedSeconds);
            if (health == null || health.IsDead)
            {
                FinishAttack(facingDirection, originalScale, originalPosition);
                yield break;
            }

            ApplyMouthFrame(facingDirection, true, originalScale, originalPosition, originalSpriteHeight);
            yield return WaitSeconds(MouthOpenSeconds);
            if (health == null || health.IsDead)
            {
                FinishAttack(facingDirection, originalScale, originalPosition);
                yield break;
            }

            SpawnBreathProjectile(launchDirection);
            yield return WaitSeconds(RecoverSeconds);
            FinishAttack(facingDirection, originalScale, originalPosition);
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

        Vector2 TargetDirection(Transform target)
        {
            if (target != null)
            {
                var toTarget = (Vector2)(target.position - transform.position);
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

        void ApplyMouthFrame(Vector2 direction, bool open, Vector3 originalScale, Vector3 originalPosition, float originalSpriteHeight)
        {
            if (visual == null) return;
            var frame = FrameFor(direction, open);
            if (frame != null) visual.sprite = frame;
            visual.transform.localScale = new Vector3(Mathf.Abs(originalScale.x), originalScale.y, originalScale.z);
            if (frame != null && originalSpriteHeight > 0f)
            {
                float heightDifference = frame.bounds.size.y - originalSpriteHeight;
                visual.transform.localPosition = originalPosition + Vector3.up * (heightDifference * 0.5f * Mathf.Abs(originalScale.y));
            }
        }

        Sprite FrameFor(Vector2 direction, bool open)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                if (direction.x < 0f) return open ? leftMouthOpenFrame : leftMouthClosedFrame;
                return open ? rightMouthOpenFrame : rightMouthClosedFrame;
            }
            if (direction.y > 0f) return open ? upMouthOpenFrame : upMouthClosedFrame;
            return open ? downMouthOpenFrame : downMouthClosedFrame;
        }

        void SpawnBreathProjectile(Vector2 launchDirection)
        {
            if (breathProjectilePrefab == null)
            {
                if (!warnedMissingPrefab)
                {
                    warnedMissingPrefab = true;
                    Debug.LogWarning("DragonBreathAttackController needs a breath projectile prefab reference.");
                }
                return;
            }

            var direction = launchDirection.sqrMagnitude > 0.001f ? launchDirection.normalized : Vector2.down;
            float spawnDistance = Mathf.Max(CellSize * 1.9f, transform.lossyScale.x * 0.45f, 0.7f);
            var spawnPosition = transform.position + (Vector3)(direction * spawnDistance);
            var projectileObject = Instantiate(breathProjectilePrefab, spawnPosition, Quaternion.identity);
            var projectile = projectileObject.GetComponent<BossDragonBreathProjectile>();
            if (projectile == null) projectile = projectileObject.AddComponent<BossDragonBreathProjectile>();
            AudioManager.PlaySfx(SfxTrack.FireballCast);
            projectile.Configure(
                direction,
                BreathDamage,
                enemy != null ? enemy.damagePopupPrefab : null,
                ProjectileSpeed,
                BreathRangeWorld,
                BreathHitboxWorld,
                ExplosionRadiusWorld,
                ProjectileVisualScale,
                ExplosionDurationSeconds);
        }

        void LoadGeneratedFramesIfMissing()
        {
            if (downMouthClosedFrame == null) downMouthClosedFrame = GeneratedSpriteLoader.Load("Boss/Dragon/Down_MouthClosed");
            if (downMouthOpenFrame == null) downMouthOpenFrame = GeneratedSpriteLoader.Load("Boss/Dragon/Down_MouthOpen");
            if (rightMouthClosedFrame == null) rightMouthClosedFrame = GeneratedSpriteLoader.Load("Boss/Dragon/Right_MouthClosed");
            if (rightMouthOpenFrame == null) rightMouthOpenFrame = GeneratedSpriteLoader.Load("Boss/Dragon/Right_MouthOpen");
            if (leftMouthClosedFrame == null) leftMouthClosedFrame = GeneratedSpriteLoader.Load("Boss/Dragon/Left_MouthClosed");
            if (leftMouthOpenFrame == null) leftMouthOpenFrame = GeneratedSpriteLoader.Load("Boss/Dragon/Left_MouthOpen");
            if (upMouthClosedFrame == null) upMouthClosedFrame = GeneratedSpriteLoader.Load("Boss/Dragon/Up_MouthClosed");
            if (upMouthOpenFrame == null) upMouthOpenFrame = GeneratedSpriteLoader.Load("Boss/Dragon/Up_MouthOpen");
        }

        GameConfig Config => enemy != null ? enemy.config : null;
        float CooldownSeconds => Config != null ? Mathf.Max(0.05f, Config.dragonBreathCooldownSeconds) : 4.5f;
        float MouthClosedSeconds => Config != null ? Mathf.Max(0f, Config.dragonBreathMouthClosedSeconds) : 0.55f;
        float MouthOpenSeconds => Config != null ? Mathf.Max(0f, Config.dragonBreathMouthOpenSeconds) : 0.32f;
        float RecoverSeconds => Config != null ? Mathf.Max(0f, Config.bossSpecialAttackRecoverSeconds) : 0.15f;
        float ProjectileSpeed => Config != null ? Mathf.Max(0.05f, Config.dragonBreathProjectileSpeed) : 4.2f;
        float CellSize => enemy != null && enemy.grid != null ? Mathf.Max(0.01f, enemy.grid.cellSize) : TileGrid.DefaultCellSize;
        float BreathRangeWorld => (Config != null ? Mathf.Max(0.1f, Config.dragonBreathRangeCells) : 15f) * CellSize;
        Vector2 BreathHitboxWorld
        {
            get
            {
                var sizeCells = Config != null ? Config.dragonBreathHitboxSizeCells : new Vector2(3f, 3f);
                Vector2 cellWorld = enemy != null && enemy.grid != null ? enemy.grid.WorldCellSize() : Vector2.one * CellSize;
                return new Vector2(
                    Mathf.Max(0.1f, sizeCells.x) * Mathf.Max(0.01f, cellWorld.x),
                    Mathf.Max(0.1f, sizeCells.y) * Mathf.Max(0.01f, cellWorld.y));
            }
        }
        float ExplosionRadiusWorld => (Config != null ? Mathf.Max(0.1f, Config.dragonBreathExplosionRadiusCells) : 3f) * CellSize;
        float DamageMultiplier => Config != null ? Mathf.Max(0f, Config.dragonBreathDamageMultiplier) : 0.5f;
        int BreathDamage => enemy != null ? Mathf.Max(1, Mathf.CeilToInt(enemy.attackDamage * DamageMultiplier)) : 0;
        float ProjectileVisualScale => Config != null ? Mathf.Max(0.05f, Config.dragonBreathProjectileVisualScale) : 1f;
        float ExplosionDurationSeconds => Config != null ? Mathf.Max(0.04f, Config.dragonBreathExplosionDurationSeconds) : 0.28f;
    }
}
