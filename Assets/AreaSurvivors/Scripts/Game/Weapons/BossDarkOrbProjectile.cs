using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class BossDarkOrbProjectile : MonoBehaviour
    {
        public Sprite orbSprite;
        public GameObject damagePopupPrefab;
        public int damage = 4;
        public float speed = 2.4f;
        public float lifetimeSeconds = 8f;
        public float damageRadius = 1.25f;
        public float damageIntervalSeconds = 0.45f;
        public float visualScale = 1f;

        readonly Dictionary<Health, float> nextHitTimes = new Dictionary<Health, float>();
        Rigidbody2D body;
        CircleCollider2D hitbox;
        PaperMeshVisual visual;
        ThunderBallRangeVisual rangeVisual;
        TileGrid grid;
        Transform playerTarget;
        Transform towerTarget;
        float age;
        Vector2 direction = Vector2.down;
        float damageVerticalRadiusMultiplier = 1f;

        void Awake()
        {
            EnsureComponents();
        }

        public void Configure(
            Transform player,
            Transform tower,
            int hitDamage,
            GameObject popupPrefab,
            float projectileSpeed,
            float projectileLifetime,
            float radius,
            float interval,
            float scale)
        {
            EnsureComponents();
            playerTarget = player;
            towerTarget = tower;
            damage = Mathf.Max(0, hitDamage);
            damagePopupPrefab = popupPrefab;
            speed = Mathf.Max(0.05f, projectileSpeed);
            lifetimeSeconds = Mathf.Max(0.1f, projectileLifetime);
            damageRadius = Mathf.Max(0.05f, radius);
            damageIntervalSeconds = Mathf.Max(0.05f, interval);
            visualScale = Mathf.Max(0.05f, scale);
            damageVerticalRadiusMultiplier = GridCellAspectY();
            age = 0f;
            nextHitTimes.Clear();
            hitbox.radius = SearchRadius;
            ApplyVisualScale();
            ApplyRangeVisual();

            var target = CurrentTarget();
            if (target != null)
            {
                var toTarget = (Vector2)(target.position - transform.position);
                if (toTarget.sqrMagnitude > 0.001f) direction = toTarget.normalized;
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetimeSeconds)
            {
                Destroy(gameObject);
                return;
            }

            var target = CurrentTarget();
            if (target != null)
            {
                var desired = (Vector2)(target.position - transform.position);
                if (desired.sqrMagnitude > 0.001f)
                {
                    direction = Vector2.Lerp(direction, desired.normalized, Time.deltaTime * 1.8f).normalized;
                }
            }

            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            ApplyDirectionRoll(direction);
            TickAreaDamage();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other, true);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other, true);
        }

        Transform CurrentTarget()
        {
            var playerHealth = playerTarget != null ? playerTarget.GetComponent<Health>() : null;
            var towerHealth = towerTarget != null ? towerTarget.GetComponent<Health>() : null;
            bool playerValid = playerTarget != null && (playerHealth == null || !playerHealth.IsDead);
            bool towerValid = towerTarget != null && (towerHealth == null || !towerHealth.IsDead);
            if (!playerValid) return towerValid ? towerTarget : null;
            if (!towerValid) return playerTarget;

            float playerDistance = ((Vector2)(playerTarget.position - transform.position)).sqrMagnitude;
            float towerDistance = ((Vector2)(towerTarget.position - transform.position)).sqrMagnitude;
            return playerDistance <= towerDistance ? playerTarget : towerTarget;
        }

        void TickAreaDamage()
        {
            var colliders = Physics2D.OverlapCircleAll(transform.position, SearchRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                TryDamage(colliders[i], true);
            }
        }

        void TryDamage(Collider2D other, bool requireDamageArea)
        {
            if (other == null || damage <= 0) return;
            var health = other.GetComponentInParent<Health>();
            if (health == null || health.IsDead || !IsValidTarget(other)) return;
            var hitPoint = other.ClosestPoint(transform.position);
            if (requireDamageArea && !ContainsDamagePoint(hitPoint)) return;
            if (nextHitTimes.TryGetValue(health, out var next) && Time.time < next) return;

            nextHitTimes[health] = Time.time + damageIntervalSeconds;
            int amount = health.Damage(damage, hitPoint);
            if (amount <= 0) return;
            DamagePopup.Show(
                damagePopupPrefab,
                hitPoint + Vector2.up * 0.18f,
                amount,
                new Color(0.55f, 0.12f, 0.95f));
        }

        static bool IsValidTarget(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null) return true;
            if (other.GetComponentInParent<TowerController>() != null) return true;

            var buildable = other.GetComponentInParent<IBuildableConstruction>();
            return buildable != null && buildable.IsBuilt;
        }

        void EnsureComponents()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.simulated = true;

            if (hitbox == null) hitbox = GetComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = SearchRadius;

            if (visual == null) visual = GetComponentInChildren<PaperMeshVisual>(true);
            if (visual == null)
            {
                var child = new GameObject("Paper Visual");
                child.transform.SetParent(transform, false);
                child.AddComponent<PaperBillboard>();
                visual = child.AddComponent<PaperMeshVisual>();
            }

            if (orbSprite == null) orbSprite = GeneratedSpriteLoader.Load("Boss/GoblinLord/DarkOrb");
            if (orbSprite != null) visual.Configure(orbSprite, Color.white, WeaponSortingOrders.Projectile);
            visual.visible = true;

            if (rangeVisual == null) rangeVisual = GetComponentInChildren<ThunderBallRangeVisual>(true);
        }

        void ApplyVisualScale()
        {
            if (visual != null) visual.transform.localScale = Vector3.one * visualScale;
        }

        void ApplyRangeVisual()
        {
            if (rangeVisual != null) rangeVisual.Configure(damageRadius, damageVerticalRadiusMultiplier);
        }

        float GridCellAspectY()
        {
            if (grid == null) grid = FindObjectOfType<TileGrid>();
            if (grid == null) return 1f;
            Vector2 cellSize = grid.WorldCellSize();
            return Mathf.Clamp(cellSize.y / Mathf.Max(0.01f, cellSize.x), 0.2f, 1f);
        }

        bool ContainsDamagePoint(Vector2 point)
        {
            float radiusX = Mathf.Max(0.05f, damageRadius);
            float radiusY = Mathf.Max(0.05f, damageRadius * damageVerticalRadiusMultiplier);
            Vector2 local = point - (Vector2)transform.position;
            float normalized = (local.x * local.x) / (radiusX * radiusX) + (local.y * local.y) / (radiusY * radiusY);
            return normalized <= 1f;
        }

        float SearchRadius => Mathf.Max(damageRadius, damageRadius * damageVerticalRadiusMultiplier);

        void ApplyDirectionRoll(Vector2 visualDirection)
        {
            if (visualDirection.sqrMagnitude < 0.001f) return;
            var degrees = Mathf.Atan2(visualDirection.y, visualDirection.x) * Mathf.Rad2Deg;
            var billboard = visual != null ? visual.GetComponent<PaperBillboard>() : null;
            if (billboard != null) billboard.rollDegrees = degrees;
        }
    }
}
