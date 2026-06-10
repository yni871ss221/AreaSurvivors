using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour
    {
        public float lifetime = 3f;
        public float visualScale = 1f;
        public float knockback;
        public float knockbackDuration = 0.16f;
        int damage;
        bool explosive;
        float explosionRadius = 1.1f;
        float trailTimer;

        void Update()
        {
            trailTimer -= Time.deltaTime;
            if (trailTimer > 0f) return;
            trailTimer = explosive ? 0.045f : 0.065f;
            TrailFleck();
        }

        public void Launch(Vector2 direction, int amount, float speed, bool isExplosive)
        {
            Launch(direction, amount, speed, isExplosive, explosionRadius, lifetime, visualScale);
        }

        public void Launch(Vector2 direction, int amount, float speed, bool isExplosive, float radius, float seconds, float scale)
        {
            damage = amount;
            explosive = isExplosive;
            explosionRadius = Mathf.Max(0.05f, radius);
            lifetime = Mathf.Max(0.05f, seconds);
            visualScale = Mathf.Max(0.05f, scale);
            ApplyWeaponSortingOrder(WeaponSortingOrders.Projectile);
            if (visualScale > 0f) transform.localScale = Vector3.one * visualScale;
            var normalizedDirection = direction.normalized;
            GetComponent<Rigidbody2D>().velocity = normalizedDirection * speed;
            transform.right = normalizedDirection;
            var billboard = GetComponentInChildren<PaperBillboard>();
            if (billboard != null)
            {
                billboard.rollDegrees = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            }
            Destroy(gameObject, lifetime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var enemy = other.GetComponent<EnemyController>();
            if (enemy == null) return;
            ImpactFlash();
            if (explosive)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
                foreach (var hit in hits)
                {
                    var hitEnemy = hit.GetComponent<EnemyController>();
                    if (hitEnemy != null)
                    {
                        var dealt = hit.GetComponent<Health>()?.Damage(damage) ?? 0;
                        ApplyKnockback(hitEnemy, ((Vector2)hit.transform.position - (Vector2)transform.position).normalized);
                        GameManager.Instance?.RegisterDamageDealt(dealt);
                    }
                }
            }
            else
            {
                var dealt = other.GetComponent<Health>()?.Damage(damage) ?? 0;
                ApplyKnockback(enemy, GetComponent<Rigidbody2D>() != null ? GetComponent<Rigidbody2D>().velocity.normalized : transform.right);
                GameManager.Instance?.RegisterDamageDealt(dealt);
            }
            Destroy(gameObject);
        }

        void ApplyKnockback(EnemyController enemy, Vector2 direction)
        {
            if (enemy == null || knockback <= 0f) return;
            var receiver = enemy.GetComponent<KnockbackReceiver>();
            if (receiver == null) return;
            receiver.Apply(direction, knockback, knockbackDuration);
        }

        void ImpactFlash()
        {
            var source = GetComponentInChildren<PaperMeshVisual>();
            if (source == null || source.sprite == null) return;

            var go = new GameObject(explosive ? "Fireball Impact" : "Arrow Impact");
            go.transform.position = transform.position;
            go.transform.localScale = Vector3.one * (explosive ? 1.15f : 0.72f);
            go.AddComponent<PaperBillboard>();
            var visual = go.AddComponent<PaperMeshVisual>();
            var color = explosive ? new Color(1f, 0.62f, 0.22f, 0.9f) : new Color(1f, 0.92f, 0.45f, 0.78f);
            visual.Configure(source.sprite, color, WeaponSortingOrders.Impact);
            go.AddComponent<ProjectileImpactFlash>().Configure(visual, explosive ? 0.18f : 0.12f);
            PixelBurstEffect.Spawn(source.sprite, transform.position, color, explosive ? 8 : 4, explosive ? 0.42f : 0.24f, explosive ? 0.26f : 0.18f, WeaponSortingOrders.ImpactBurst);
        }

        void TrailFleck()
        {
            var source = GetComponentInChildren<PaperMeshVisual>();
            if (source == null || source.sprite == null) return;
            var color = explosive ? new Color(1f, 0.45f, 0.16f, 0.36f) : new Color(1f, 0.88f, 0.42f, 0.24f);
            PixelBurstEffect.Spawn(source.sprite, transform.position - transform.right * 0.18f, color, 1, explosive ? 0.28f : 0.16f, explosive ? 0.16f : 0.11f, WeaponSortingOrders.ProjectileTrail);
        }

        void ApplyWeaponSortingOrder(int sortingOrder)
        {
            var visuals = GetComponentsInChildren<PaperMeshVisual>(true);
            foreach (var visual in visuals)
            {
                if (visual != null) visual.order = sortingOrder;
            }
        }
    }

    sealed class ProjectileImpactFlash : MonoBehaviour
    {
        PaperMeshVisual visual;
        float lifetime = 0.12f;
        float age;
        Vector3 startScale;

        public void Configure(PaperMeshVisual target, float seconds)
        {
            visual = target;
            lifetime = Mathf.Max(0.04f, seconds);
            startScale = transform.localScale;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.localScale = startScale * Mathf.Lerp(0.75f, 1.85f, t);
            if (visual != null)
            {
                var color = visual.color;
                color.a = Mathf.Lerp(color.a, 0f, t);
                visual.color = color;
            }
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
