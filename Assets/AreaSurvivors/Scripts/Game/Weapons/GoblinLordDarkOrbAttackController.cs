using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(EnemyController), typeof(Health))]
    public sealed class GoblinLordDarkOrbAttackController : MonoBehaviour
    {
        public GameObject darkOrbPrefab;
        public Sprite downCastFrame;
        public Sprite rightCastFrame;
        public Sprite leftCastFrame;
        public Sprite upCastFrame;

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
                enemy.enemyKind == EnemyKind.GoblinLord &&
                !attacking;
        }

        IEnumerator AttackRoutine()
        {
            attacking = true;
            cooldownTimer = CooldownSeconds;
            var target = CurrentTarget();
            var direction = CardinalDirection(TargetDirection(target));
            var originalScale = visual != null ? visual.transform.localScale : Vector3.one;
            var originalPosition = visual != null ? visual.transform.localPosition : Vector3.zero;
            float originalSpriteHeight = visual != null && visual.sprite != null ? visual.sprite.bounds.size.y : 0f;

            enemy.SetActionLocked(true, direction);
            if (animator != null) animator.enabled = false;
            if (bounceAnimation != null) bounceAnimation.enabled = false;

            ApplyCastFrame(direction, originalScale, originalPosition, originalSpriteHeight);
            yield return WaitSeconds(CastSeconds);
            SpawnDarkOrb(target);
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

        void SpawnDarkOrb(Transform initialTarget)
        {
            if (darkOrbPrefab == null)
            {
                if (!warnedMissingPrefab)
                {
                    warnedMissingPrefab = true;
                    Debug.LogWarning("GoblinLordDarkOrbAttackController needs a dark orb prefab reference.");
                }
                return;
            }

            var direction = TargetDirection(initialTarget);
            float spawnDistance = Mathf.Max(0.35f, enemy != null && enemy.grid != null ? enemy.grid.cellSize * 1.2f : 0.84f);
            var spawnPosition = transform.position + (Vector3)(direction.normalized * spawnDistance);
            var projectileObject = Instantiate(darkOrbPrefab, spawnPosition, Quaternion.identity);
            var projectile = projectileObject.GetComponent<BossDarkOrbProjectile>();
            if (projectile == null) projectile = projectileObject.AddComponent<BossDarkOrbProjectile>();
            AudioManager.PlaySfx(SfxTrack.GoblinLordDarkMagic);
            projectile.Configure(
                GameManager.Instance != null && GameManager.Instance.Player != null ? GameManager.Instance.Player.transform : null,
                GameManager.Instance != null && GameManager.Instance.Tower != null ? GameManager.Instance.Tower.transform : enemy != null ? enemy.target : null,
                DarkOrbDamage,
                enemy != null ? enemy.damagePopupPrefab : null,
                DarkOrbSpeed,
                DarkOrbLifetimeSeconds,
                DarkOrbDamageRadius,
                DarkOrbDamageIntervalSeconds,
                DarkOrbVisualScale);
        }

        void LoadGeneratedFramesIfMissing()
        {
            if (downCastFrame == null) downCastFrame = GeneratedSpriteLoader.Load("Boss/GoblinLord/Down_Cast");
            if (rightCastFrame == null) rightCastFrame = GeneratedSpriteLoader.Load("Boss/GoblinLord/Right_Cast");
            if (leftCastFrame == null) leftCastFrame = GeneratedSpriteLoader.Load("Boss/GoblinLord/Left_Cast");
            if (upCastFrame == null) upCastFrame = GeneratedSpriteLoader.Load("Boss/GoblinLord/Up_Cast");
        }

        GameConfig Config => enemy != null ? enemy.config : null;
        float CooldownSeconds => Config != null ? Mathf.Max(0.05f, Config.bossSpecialAttackCooldownSeconds) : 5f;
        float CastSeconds => Config != null ? Mathf.Max(0f, Config.bossSpecialAttackRaiseSeconds) : 0.5f;
        float RecoverSeconds => Config != null ? Mathf.Max(0f, Config.bossSpecialAttackRecoverSeconds) : 0.15f;
        float DarkOrbSpeed => Config != null ? Mathf.Max(0.05f, Config.goblinLordDarkOrbSpeed) : 2.4f;
        float DarkOrbLifetimeSeconds => Config != null ? Mathf.Max(0.1f, Config.goblinLordDarkOrbLifetimeSeconds) : 8f;
        float DarkOrbDamageRadius => Config != null ? Mathf.Max(0.05f, Config.goblinLordDarkOrbDamageRadius) : 1.25f;
        float DarkOrbDamageIntervalSeconds => Config != null ? Mathf.Max(0.05f, Config.goblinLordDarkOrbDamageIntervalSeconds) : 0.45f;
        float DarkOrbDamageMultiplier => Config != null ? Mathf.Max(0f, Config.goblinLordDarkOrbDamageMultiplier) : 0.5f;
        int DarkOrbDamage => enemy != null ? Mathf.Max(1, Mathf.CeilToInt(enemy.attackDamage * DarkOrbDamageMultiplier)) : 0;
        float DarkOrbVisualScale => Config != null ? Mathf.Max(0.05f, Config.goblinLordDarkOrbVisualScale) : 1f;
    }
}
