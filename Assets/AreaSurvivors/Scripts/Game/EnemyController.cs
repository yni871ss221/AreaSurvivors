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

        Rigidbody2D body;
        Health health;
        Collider2D[] colliders;
        PaperMeshVisual visual;
        float contactTimer;
        bool dying;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            colliders = GetComponents<Collider2D>();
            visual = GetComponentInChildren<PaperMeshVisual>();
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        public void Configure(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget, int hp, float speedScale)
        {
            config = gameConfig;
            grid = tileGrid;
            target = chaseTarget;
            health.SetMax(hp);
            body.drag = 0f;
            xpValue = config.xpPerEnemy;
            transform.localScale = Vector3.one * Mathf.Clamp(speedScale, 1f, 1.8f);
        }

        void Update()
        {
            if (dying || target == null) return;
            var targetDirection = ((Vector2)(target.position - transform.position)).normalized;
            var direction = AvoidObstacles(targetDirection);
            float slow = grid.GetOwner(transform.position) == TileOwner.Player ? config.playerTerritorySlow : 1f;
            body.velocity = direction * config.enemyBaseSpeed * slow * Mathf.Max(0.7f, transform.localScale.x);
            if (directionalAnimator != null) directionalAnimator.Tick(direction, body.velocity.sqrMagnitude > 0.01f);
            grid.Paint(transform.position, TileOwner.Enemy, 1);
        }

        Vector2 AvoidObstacles(Vector2 targetDirection)
        {
            var position = (Vector2)transform.position;
            var avoidance = Vector2.zero;
            var colliders = Physics2D.OverlapCircleAll(position, obstacleAvoidanceRadius);
            foreach (var hit in colliders)
            {
                if (hit == null || hit.attachedRigidbody == body) continue;
                if (hit.GetComponent<Obstacle>() == null) continue;

                var closest = hit.ClosestPoint(position);
                var away = position - closest;
                if (away.sqrMagnitude < 0.001f) away = position - (Vector2)hit.transform.position;
                if (away.sqrMagnitude < 0.001f) away = Vector2.Perpendicular(targetDirection);

                float distance = Mathf.Max(0.05f, away.magnitude);
                float strength = Mathf.Clamp01((obstacleAvoidanceRadius - distance) / obstacleAvoidanceRadius);
                avoidance += away.normalized * strength;
            }

            var frontHit = Physics2D.CircleCast(position, 0.28f, targetDirection, 0.75f);
            if (frontHit.collider != null && frontHit.collider.GetComponent<Obstacle>() != null)
            {
                var side = Vector2.Perpendicular(targetDirection);
                if (Vector2.Dot(side, position - (Vector2)frontHit.collider.transform.position) < 0f) side = -side;
                avoidance += side * 0.9f;
            }

            var steered = targetDirection + avoidance * obstacleAvoidanceWeight;
            return steered.sqrMagnitude > 0.01f ? steered.normalized : targetDirection;
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (dying) return;
            contactTimer -= Time.deltaTime;
            if (contactTimer > 0f) return;
            var otherHealth = collision.collider.GetComponent<Health>();
            if (otherHealth == null) return;
            var fence = collision.collider.GetComponent<DefensiveFence>();
            if (collision.collider.GetComponent<PlayerController>() == null &&
                collision.collider.GetComponent<TowerController>() == null &&
                (fence == null || !fence.IsBuilt)) return;
            int dealt = otherHealth.Damage(config.enemyDamage);
            float height = collision.collider.GetComponent<TowerController>() != null ? 1.05f : fence != null ? 0.82f : 0.58f;
            DamagePopup.Show(damagePopupPrefab, collision.transform.position + Vector3.up * height, dealt, Color.red);
            contactTimer = 0.75f;
        }

        void OnDamaged(Health _, int amount)
        {
            DamagePopup.Show(damagePopupPrefab, transform.position + Vector3.up * 0.55f, amount, Color.white);
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

            if (xpOrbPrefab != null) Instantiate(xpOrbPrefab, transform.position, Quaternion.identity);
            GameManager.Instance?.RegisterKill();
            Destroy(gameObject);
        }
    }
}
