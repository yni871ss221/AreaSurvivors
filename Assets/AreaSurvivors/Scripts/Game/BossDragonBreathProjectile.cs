using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class BossDragonBreathProjectile : MonoBehaviour
    {
        public Sprite fireballSprite;
        public Sprite explosionSprite;
        public GameObject damagePopupPrefab;
        public int damage = 1;
        public float speed = 4.2f;
        public float rangeWorld = 10.5f;
        public Vector2 hitboxSizeWorld = new Vector2(2.1f, 2.1f);
        public float explosionRadiusWorld = 2.1f;
        public float projectileVisualScale = 1f;
        public float explosionDurationSeconds = 0.28f;

        readonly HashSet<Health> damagedTargets = new HashSet<Health>();
        Rigidbody2D body;
        BoxCollider2D hitbox;
        PaperMeshVisual visual;
        TileGrid grid;
        Vector2 direction = Vector2.down;
        float traveled;
        bool resolved;

        void Awake()
        {
            EnsureComponents();
        }

        public void Configure(
            Vector2 launchDirection,
            int hitDamage,
            GameObject popupPrefab,
            float projectileSpeed,
            float projectileRangeWorld,
            Vector2 projectileHitboxSizeWorld,
            float explosionRadius,
            float visualScale,
            float explosionDuration)
        {
            EnsureComponents();
            direction = launchDirection.sqrMagnitude > 0.001f ? launchDirection.normalized : Vector2.down;
            damage = Mathf.Max(0, hitDamage);
            damagePopupPrefab = popupPrefab;
            speed = Mathf.Max(0.05f, projectileSpeed);
            rangeWorld = Mathf.Max(0.05f, projectileRangeWorld);
            hitboxSizeWorld = new Vector2(
                Mathf.Max(0.05f, projectileHitboxSizeWorld.x),
                Mathf.Max(0.05f, projectileHitboxSizeWorld.y));
            explosionRadiusWorld = Mathf.Max(0.05f, explosionRadius);
            projectileVisualScale = Mathf.Max(0.05f, visualScale);
            explosionDurationSeconds = Mathf.Max(0.04f, explosionDuration);
            grid = GameManager.Instance != null ? GameManager.Instance.grid : null;
            traveled = 0f;
            resolved = false;
            damagedTargets.Clear();
            hitbox.size = hitboxSizeWorld;
            ApplyVisualScale();
            PaintBreathArea();
        }

        void Update()
        {
            if (resolved) return;
            float step = speed * Time.deltaTime;
            transform.position += (Vector3)(direction * step);
            traveled += step;
            PaintBreathArea();
            if (traveled >= rangeWorld) Detonate();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (resolved || !IsValidTarget(other)) return;
            Detonate();
        }

        void Detonate()
        {
            if (resolved) return;
            resolved = true;
            AudioManager.PlaySfx(SfxTrack.ExplosionHit);
            PaintExplosionArea();
            SpawnExplosionVisual();
            DamageExplosion();
            Destroy(gameObject);
        }

        void DamageExplosion()
        {
            Physics2D.SyncTransforms();
            damagedTargets.Clear();
            var colliders = Physics2D.OverlapCircleAll(transform.position, explosionRadiusWorld);
            for (int i = 0; i < colliders.Length; i++)
            {
                TryDamage(colliders[i]);
            }
        }

        void TryDamage(Collider2D other)
        {
            if (other == null || damage <= 0 || !IsValidTarget(other)) return;
            var health = other.GetComponentInParent<Health>();
            if (health == null || health.IsDead || damagedTargets.Contains(health)) return;
            Vector2 hitPoint = other.ClosestPoint(transform.position);
            if (((Vector2)transform.position - hitPoint).sqrMagnitude > explosionRadiusWorld * explosionRadiusWorld) return;

            damagedTargets.Add(health);
            int amount = health.Damage(damage, hitPoint);
            if (amount <= 0) return;
            DamagePopup.Show(
                damagePopupPrefab,
                hitPoint + Vector2.up * 0.18f,
                amount,
                new Color(1f, 0.32f, 0.08f));
        }

        static bool IsValidTarget(Collider2D other)
        {
            if (other == null) return false;
            if (other.GetComponentInParent<PlayerController>() != null) return true;
            if (other.GetComponentInParent<TowerController>() != null) return true;

            var buildable = other.GetComponentInParent<IBuildableConstruction>();
            return buildable != null && buildable.IsBuilt;
        }

        void SpawnExplosionVisual()
        {
            if (explosionSprite == null) explosionSprite = GeneratedSpriteLoader.Load("Boss/Dragon/BreathExplosion");
            if (explosionSprite == null) return;

            var go = new GameObject("Dragon Breath Explosion");
            go.transform.position = transform.position;
            go.AddComponent<PaperBillboard>();
            var explosionVisual = go.AddComponent<PaperMeshVisual>();
            explosionVisual.Configure(explosionSprite, Color.white, WeaponSortingOrders.Impact);
            explosionVisual.visible = true;
            go.AddComponent<DragonBreathExplosionEffect>().Configure(explosionVisual, explosionRadiusWorld, explosionDurationSeconds);
        }

        void PaintBreathArea()
        {
            var activeGrid = ResolveGrid();
            if (activeGrid == null) return;

            Vector2 cellSize = activeGrid.WorldCellSize();
            activeGrid.PaintEllipseOverlappingCells(
                transform.position,
                TileOwner.Enemy,
                Mathf.Max(0.1f, hitboxSizeWorld.x * 0.5f / Mathf.Max(0.01f, cellSize.x)),
                Mathf.Max(0.1f, hitboxSizeWorld.y * 0.5f / Mathf.Max(0.01f, cellSize.y)));
        }

        void PaintExplosionArea()
        {
            var activeGrid = ResolveGrid();
            if (activeGrid == null) return;

            Vector2 cellSize = activeGrid.WorldCellSize();
            activeGrid.PaintEllipseOverlappingCells(
                transform.position,
                TileOwner.Enemy,
                Mathf.Max(0.1f, explosionRadiusWorld / Mathf.Max(0.01f, cellSize.x)),
                Mathf.Max(0.1f, explosionRadiusWorld / Mathf.Max(0.01f, cellSize.y)));
        }

        TileGrid ResolveGrid()
        {
            if (grid == null && GameManager.Instance != null) grid = GameManager.Instance.grid;
            return grid;
        }

        void EnsureComponents()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.simulated = true;

            if (hitbox == null) hitbox = GetComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = hitboxSizeWorld;

            if (visual == null) visual = GetComponentInChildren<PaperMeshVisual>(true);
            if (visual == null)
            {
                var child = new GameObject("Paper Visual");
                child.transform.SetParent(transform, false);
                child.AddComponent<PaperBillboard>();
                visual = child.AddComponent<PaperMeshVisual>();
            }

            if (fireballSprite == null) fireballSprite = GeneratedSpriteLoader.Load("Boss/Dragon/BreathFireball");
            if (fireballSprite != null) visual.Configure(fireballSprite, Color.white, WeaponSortingOrders.Projectile);
            visual.visible = true;
        }

        void ApplyVisualScale()
        {
            if (visual == null || visual.sprite == null) return;
            float desiredWidth = Mathf.Max(hitboxSizeWorld.x, hitboxSizeWorld.y) * projectileVisualScale;
            float spriteWidth = Mathf.Max(0.01f, visual.sprite.bounds.size.x);
            visual.transform.localScale = Vector3.one * (desiredWidth / spriteWidth);
        }
    }

    public sealed class DragonBreathExplosionEffect : MonoBehaviour
    {
        PaperMeshVisual visual;
        float duration = 0.28f;
        float age;
        Vector3 baseScale = Vector3.one;

        public void Configure(PaperMeshVisual target, float radius, float seconds)
        {
            visual = target;
            duration = Mathf.Max(0.04f, seconds);
            float diameter = Mathf.Max(0.05f, radius * 2f);
            float spriteWidth = visual != null && visual.sprite != null ? Mathf.Max(0.01f, visual.sprite.bounds.size.x) : 1f;
            baseScale = Vector3.one * (diameter / spriteWidth);
            transform.localScale = baseScale * 0.75f;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            transform.localScale = baseScale * Mathf.Lerp(0.75f, 1.35f, t);
            if (visual != null)
            {
                var color = visual.color;
                color.a = Mathf.Lerp(1f, 0f, t);
                visual.color = color;
            }
            if (age >= duration) Destroy(gameObject);
        }
    }
}
