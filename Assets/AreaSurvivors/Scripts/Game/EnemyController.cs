using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class EnemyController : MonoBehaviour
    {
        public GameConfig config;
        public TileGrid grid;
        public Transform target;
        public Slider hpBar;
        public GameObject xpOrbPrefab;
        public GameObject damagePopupPrefab;
        public DirectionalSpriteAnimator directionalAnimator;
        public int xpValue = 1;

        Rigidbody2D body;
        Health health;
        float contactTimer;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
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
            if (target == null) return;
            var direction = ((Vector2)(target.position - transform.position)).normalized;
            float slow = grid.GetOwner(transform.position) == TileOwner.Player ? config.playerTerritorySlow : 1f;
            body.velocity = direction * config.enemyBaseSpeed * slow * Mathf.Max(0.7f, transform.localScale.x);
            if (directionalAnimator != null) directionalAnimator.Tick(direction, body.velocity.sqrMagnitude > 0.01f);
            grid.Paint(transform.position, TileOwner.Enemy, 1);
            if (hpBar != null) hpBar.value = health.Normalized;
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            contactTimer -= Time.deltaTime;
            if (contactTimer > 0f) return;
            var otherHealth = collision.collider.GetComponent<Health>();
            if (otherHealth == null) return;
            if (collision.collider.GetComponent<PlayerController>() == null && collision.collider.GetComponent<TowerController>() == null) return;
            otherHealth.Damage(config.enemyDamage);
            DamagePopup.Show(damagePopupPrefab, collision.transform.position + Vector3.up * 0.45f, config.enemyDamage, Color.red);
            contactTimer = 0.75f;
        }

        void OnDamaged(Health _, int amount)
        {
            DamagePopup.Show(damagePopupPrefab, transform.position + Vector3.up * 0.4f, amount, Color.white);
        }

        void OnDied(Health _)
        {
            if (xpOrbPrefab != null) Instantiate(xpOrbPrefab, transform.position, Quaternion.identity);
            GameManager.Instance?.RegisterKill();
            Destroy(gameObject);
        }
    }
}
