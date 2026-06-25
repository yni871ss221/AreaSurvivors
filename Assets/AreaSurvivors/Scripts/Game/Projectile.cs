using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour
    {
        public Sprite fallbackSprite;
        public Color fallbackColor = Color.white;
        public float lifetime = 3f;
        public float visualScale = 1f;
        public float knockback;
        public float knockbackDuration = 0.16f;
        public Sprite impactSprite;
        public Color impactColor = Color.white;
        public float impactVisualScale = 1f;
        public bool paintsTerritory = true;
        int damage;
        bool explosive;
        bool resolved;
        float explosionRadius = 1.1f;
        float trailTimer;
        const float ArrowVisualScaleMultiplier = 0.5f;
        const int ArrowPaintRadius = 1;

        void Awake()
        {
            EnsureVisibleProjectile();
        }

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
            EnsureVisibleProjectile();
            resolved = false;
            damage = amount;
            explosive = isExplosive;
            explosionRadius = Mathf.Max(0.05f, radius);
            lifetime = Mathf.Max(0.05f, seconds);
            visualScale = Mathf.Max(0.05f, scale);
            ApplyWeaponSortingOrder(WeaponSortingOrders.Projectile);
            if (visualScale > 0f) transform.localScale = Vector3.one * (visualScale * (isExplosive ? 1f : ArrowVisualScaleMultiplier));
            var normalizedDirection = direction.normalized;
            if (normalizedDirection.sqrMagnitude < 0.001f) normalizedDirection = Vector2.right;
            GetComponent<Rigidbody2D>().velocity = normalizedDirection * speed;
            transform.right = normalizedDirection;
            var billboard = GetComponentInChildren<PaperBillboard>();
            if (billboard != null)
            {
                billboard.rollDegrees = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            }
            CancelInvoke(nameof(Expire));
            Invoke(nameof(Expire), lifetime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved) return;
            var enemy = other.GetComponent<EnemyController>();
            if (enemy == null) return;
            if (explosive)
            {
                Detonate();
            }
            else
            {
                ImpactFlash();
                var dealt = other.GetComponent<Health>()?.Damage(damage, other.ClosestPoint(transform.position)) ?? 0;
                if (paintsTerritory) PaintPlayerTerritory(enemy.transform.position, ArrowPaintRadius);
                ApplyKnockback(enemy, GetComponent<Rigidbody2D>() != null ? GetComponent<Rigidbody2D>().velocity.normalized : transform.right);
                GameManager.Instance?.RegisterDamageDealt(dealt);
                resolved = true;
                Destroy(gameObject);
            }
        }

        void Expire()
        {
            if (resolved) return;
            if (explosive)
            {
                Detonate();
                return;
            }

            resolved = true;
            Destroy(gameObject);
        }

        void Detonate()
        {
            if (resolved) return;
            resolved = true;
            ImpactFlash();
            if (paintsTerritory) PaintPlayerTerritory(transform.position, Mathf.CeilToInt(explosionRadius));
            ProjectileExplosionHitbox.Spawn(transform.position, explosionRadius, damage, knockback, knockbackDuration, paintsTerritory);
            Destroy(gameObject);
        }

        static void PaintPlayerTerritory(Vector3 position, int radius)
        {
            var grid = GameManager.Instance != null ? GameManager.Instance.grid : null;
            if (grid != null) grid.Paint(position, TileOwner.Player, Mathf.Max(1, radius));
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
            var flashSprite = impactSprite != null ? impactSprite : source != null ? source.sprite : null;
            if (flashSprite == null) return;

            var go = new GameObject(explosive ? "Fireball Impact" : "Arrow Impact");
            go.transform.position = transform.position;
            go.transform.localScale = Vector3.one * ImpactScale();
            go.AddComponent<PaperBillboard>();
            var visual = go.AddComponent<PaperMeshVisual>();
            var color = impactSprite != null ? impactColor : explosive ? new Color(1f, 0.62f, 0.22f, 0.9f) : new Color(1f, 0.92f, 0.45f, 0.78f);
            visual.Configure(flashSprite, color, WeaponSortingOrders.Impact);
            go.AddComponent<ProjectileImpactFlash>().Configure(visual, impactSprite != null ? 0.26f : explosive ? 0.18f : 0.12f);
        }

        float ImpactScale()
        {
            if (impactSprite != null) return Mathf.Max(0.05f, impactVisualScale);
            if (explosive) return Mathf.Max(0.05f, explosionRadius);
            return 0.72f;
        }

        void TrailFleck()
        {
            if (explosive && paintsTerritory) PaintPlayerTerritory(transform.position, 1);
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

        void EnsureVisibleProjectile()
        {
            var visual = GetComponentInChildren<PaperMeshVisual>(true);
            if (visual == null)
            {
                var child = new GameObject("Paper Visual");
                child.transform.SetParent(transform, false);
                child.AddComponent<PaperBillboard>();
                visual = child.AddComponent<PaperMeshVisual>();
            }

            if (visual.sprite == null)
            {
                var sprite = fallbackSprite;
                if (sprite == null) sprite = GeneratedSpriteLoader.Load(name.Contains("Fireball") ? "Fireball" : "Arrow");
                if (sprite != null) visual.Configure(sprite, fallbackColor, WeaponSortingOrders.Projectile);
            }
            else
            {
                visual.Configure(visual.sprite, visual.color, WeaponSortingOrders.Projectile);
            }

            visual.visible = true;
            visual.order = WeaponSortingOrders.Projectile;
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

    sealed class ProjectileExplosionHitbox : MonoBehaviour
    {
        readonly List<Collider2D> hits = new List<Collider2D>(24);
        readonly HashSet<EnemyController> damaged = new HashSet<EnemyController>();
        CircleCollider2D hitbox;
        Vector3 origin;
        int damage;
        float knockback;
        float knockbackDuration;
        bool paintsTerritory;

        public static void Spawn(Vector3 position, float radius, int damage, float knockback, float knockbackDuration, bool paintsTerritory = true)
        {
            var go = new GameObject("Projectile Explosion Hitbox");
            go.transform.position = position;

            var hitbox = go.AddComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = Mathf.Max(0.05f, radius);

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;

            go.AddComponent<ProjectileExplosionHitbox>().Configure(hitbox, position, damage, knockback, knockbackDuration, paintsTerritory);
        }

        void Configure(CircleCollider2D source, Vector3 hitOrigin, int hitDamage, float knockbackStrength, float knockbackSeconds, bool shouldPaintTerritory)
        {
            hitbox = source;
            origin = hitOrigin;
            damage = hitDamage;
            knockback = knockbackStrength;
            knockbackDuration = knockbackSeconds;
            paintsTerritory = shouldPaintTerritory;
            StartCoroutine(DamageAfterPhysicsSync());
        }

        IEnumerator DamageAfterPhysicsSync()
        {
            yield return new WaitForFixedUpdate();
            DamageOverlaps();
            Destroy(gameObject);
        }

        void DamageOverlaps()
        {
            if (hitbox == null) return;
            if (paintsTerritory) PaintPlayerTerritory(origin, Mathf.CeilToInt(hitbox.radius));
            Physics2D.SyncTransforms();
            hits.Clear();
            damaged.Clear();
            var filter = new ContactFilter2D();
            filter.NoFilter();
            hitbox.OverlapCollider(filter, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                var enemy = hits[i] != null ? hits[i].GetComponent<EnemyController>() : null;
                if (enemy == null || damaged.Contains(enemy)) continue;
                damaged.Add(enemy);
                var health = enemy.GetComponent<Health>();
                var dealt = health != null ? health.Damage(damage, hits[i].ClosestPoint(origin)) : 0;
                ApplyKnockback(enemy);
                GameManager.Instance?.RegisterDamageDealt(dealt);
            }
        }

        void ApplyKnockback(EnemyController enemy)
        {
            if (enemy == null || knockback <= 0f) return;
            var receiver = enemy.GetComponent<KnockbackReceiver>();
            if (receiver == null) return;
            var direction = ((Vector2)enemy.transform.position - (Vector2)origin).normalized;
            receiver.Apply(direction, knockback, knockbackDuration);
        }

        static void PaintPlayerTerritory(Vector3 position, int radius)
        {
            var grid = GameManager.Instance != null ? GameManager.Instance.grid : null;
            if (grid != null) grid.Paint(position, TileOwner.Player, Mathf.Max(1, radius));
        }
    }
}
