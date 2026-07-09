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
        public bool applyLaunchScale = true;
        public float knockback;
        public float knockbackDuration = 0.16f;
        public Sprite impactSprite;
        public Color impactColor = Color.white;
        public float impactVisualScale = 1f;
        public bool playImpactFlash = true;
        public GameObject impactFlashPrefab;
        public GameObject explosionHitboxPrefab;
        public bool paintsTerritory = true;
        [Tooltip("短い間隔でPixelBurstを生成するため、負荷調査後はデフォルト無効。必要な弾PrefabだけONにしてください。")]
        public bool playTrailFlecks;
        int damage;
        bool explosive;
        bool resolved;
        RunDamageSource damageSource;
        float explosionRadius = 1.1f;
        float trailTimer;
        float trailPaintTimer;
        const float ArrowVisualScaleMultiplier = 0.5f;
        const int ArrowPaintRadius = 1;
        const float FireballTrailPaintInterval = 0.06f;
        const int FireballTrailPaintRadius = 1;

        void Awake()
        {
            EnsureVisibleProjectile();
        }

        void Update()
        {
            PaintFireballTrailIfNeeded();
            if (!playTrailFlecks) return;
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
            trailTimer = 0f;
            trailPaintTimer = 0f;
            ApplyWeaponSortingOrder(WeaponSortingOrders.Projectile);
            if (applyLaunchScale && visualScale > 0f) transform.localScale = Vector3.one * (visualScale * (isExplosive ? 1f : ArrowVisualScaleMultiplier));
            var normalizedDirection = direction.normalized;
            if (normalizedDirection.sqrMagnitude < 0.001f) normalizedDirection = Vector2.right;
            GetComponent<Rigidbody2D>().velocity = normalizedDirection * speed;
            float zDegrees = Mathf.Atan2(normalizedDirection.y, normalizedDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, zDegrees);
            var billboard = GetComponentInChildren<PaperBillboard>();
            if (billboard != null)
            {
                billboard.rollDegrees = zDegrees;
            }
            CancelInvoke(nameof(Expire));
            Invoke(nameof(Expire), lifetime);
        }

        public void SetDamageSource(RunDamageSource source)
        {
            damageSource = source;
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
                var health = other.GetComponent<Health>();
                int creditedDamage = health != null && !health.IsDead ? health.DamageAmount(damage) : 0;
                if (health != null) health.Damage(damage, other.ClosestPoint(transform.position));
                if (paintsTerritory) PaintPlayerTerritory(enemy.transform.position, ArrowPaintRadius);
                ApplyKnockback(enemy, GetComponent<Rigidbody2D>() != null ? GetComponent<Rigidbody2D>().velocity.normalized : transform.right);
                RegisterDamage(creditedDamage);
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
            AudioManager.PlaySfx(SfxTrack.ExplosionHit);
            ImpactFlash();
            if (paintsTerritory) PaintPlayerTerritory(transform.position, Mathf.CeilToInt(explosionRadius));
            ProjectileExplosionHitbox.Spawn(explosionHitboxPrefab, transform.position, explosionRadius, damage, knockback, knockbackDuration, paintsTerritory, damageSource);
            Destroy(gameObject);
        }

        void RegisterDamage(int amount)
        {
            if (damageSource.IsAssigned) GameManager.Instance?.RegisterDamageDealt(damageSource, amount);
            else GameManager.Instance?.RegisterDamageDealt(amount);
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
            if (!playImpactFlash) return;
            var source = GetComponentInChildren<PaperMeshVisual>();
            var flashSprite = impactSprite != null ? impactSprite : source != null ? source.sprite : null;
            if (flashSprite == null) return;
            if (impactFlashPrefab == null)
            {
                Debug.LogError("Projectile impact prefab is missing. Assign Projectile.impactFlashPrefab on projectile prefabs.");
                return;
            }

            var go = Instantiate(impactFlashPrefab, transform.position, Quaternion.identity);
            go.name = explosive ? "Fireball Impact" : "Arrow Impact";
            var flash = go.GetComponent<ProjectileImpactFlash>();
            if (flash == null)
            {
                Debug.LogError("Projectile impact prefab is missing ProjectileImpactFlash.");
                Destroy(go);
                return;
            }

            var color = impactSprite != null ? impactColor : explosive ? new Color(1f, 0.62f, 0.22f, 0.9f) : new Color(1f, 0.92f, 0.45f, 0.78f);
            flash.Play(flashSprite, color, ImpactScale(), impactSprite != null ? 0.26f : explosive ? 0.18f : 0.12f);
        }

        float ImpactScale()
        {
            if (impactSprite != null) return Mathf.Max(0.05f, impactVisualScale);
            if (explosive) return Mathf.Max(0.05f, explosionRadius);
            return 0.72f;
        }

        void TrailFleck()
        {
            var source = GetComponentInChildren<PaperMeshVisual>();
            if (source == null || source.sprite == null) return;
            var color = explosive ? new Color(1f, 0.45f, 0.16f, 0.36f) : new Color(1f, 0.88f, 0.42f, 0.24f);
            PixelBurstEffect.Spawn(source.sprite, transform.position - transform.right * 0.18f, color, 1, explosive ? 0.28f : 0.16f, explosive ? 0.16f : 0.11f, WeaponSortingOrders.ProjectileTrail);
        }

        void PaintFireballTrailIfNeeded()
        {
            if (!explosive || !paintsTerritory) return;
            trailPaintTimer -= Time.deltaTime;
            if (trailPaintTimer > 0f) return;

            trailPaintTimer = FireballTrailPaintInterval;
            PaintPlayerTerritory(transform.position, FireballTrailPaintRadius);
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
                Debug.LogError("Projectile visual is missing. Add a PaperMeshVisual child to the projectile prefab.");
                return;
            }

            if (visual.sprite == null)
            {
                var sprite = fallbackSprite;
                if (sprite != null) visual.Configure(sprite, fallbackColor, WeaponSortingOrders.Projectile);
                else Debug.LogError("Projectile sprite is missing. Assign PaperMeshVisual.sourceSprite on the projectile prefab.");
            }
            else
            {
                visual.Configure(visual.sprite, visual.color, WeaponSortingOrders.Projectile);
            }

            visual.visible = true;
            visual.order = WeaponSortingOrders.Projectile;
        }
    }
}
