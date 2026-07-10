using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public sealed class BossShockwaveProjectile : MonoBehaviour
    {
        public Sprite shockwaveSprite;
        public GameObject damagePopupPrefab;
        public float displaySeconds = 1f;
        public Vector2 hitboxSize = new Vector2(1.4f, 1.4f);
        public int damage = 6;

        readonly HashSet<Health> damagedTargets = new HashSet<Health>();
        Rigidbody2D body;
        BoxCollider2D hitbox;
        PaperMeshVisual visual;
        float age;

        void Awake()
        {
            EnsureComponents();
        }

        void Update()
        {
            age += Time.deltaTime;
            if (age >= displaySeconds) Destroy(gameObject);
        }

        public void Configure(Vector2 worldHitboxSize, int hitDamage, GameObject popupPrefab, float seconds)
        {
            EnsureComponents();
            hitboxSize = new Vector2(Mathf.Max(0.05f, worldHitboxSize.x), Mathf.Max(0.05f, worldHitboxSize.y));
            damage = Mathf.Max(0, hitDamage);
            damagePopupPrefab = popupPrefab;
            displaySeconds = Mathf.Max(0.05f, seconds);
            age = 0f;
            damagedTargets.Clear();

            hitbox.size = hitboxSize;
            body.velocity = Vector2.zero;
            ApplyVisualSize();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            TryDamage(other);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            TryDamage(other);
        }

        void TryDamage(Collider2D other)
        {
            if (other == null || damage <= 0) return;
            var health = other.GetComponentInParent<Health>();
            if (health == null || health.IsDead || damagedTargets.Contains(health)) return;
            if (!IsValidTarget(other)) return;

            var hitPoint = other.ClosestPoint(transform.position);
            damagedTargets.Add(health);
            int amount = health.Damage(damage, hitPoint);
            DamagePopup.Show(
                damagePopupPrefab,
                hitPoint + Vector2.up * 0.18f,
                Mathf.Max(0, amount),
                Color.red);
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

            if (hitbox == null) hitbox = GetComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = hitboxSize;

            if (visual == null) visual = GetComponentInChildren<PaperMeshVisual>(true);
            if (visual == null)
            {
                var child = new GameObject("Paper Visual");
                child.transform.SetParent(transform, false);
                child.AddComponent<PaperBillboard>();
                visual = child.AddComponent<PaperMeshVisual>();
            }

            if (shockwaveSprite == null) shockwaveSprite = GeneratedSpriteLoader.Load("Boss/OrcKing/Shockwave");
            if (shockwaveSprite != null) visual.Configure(shockwaveSprite, Color.white, WeaponSortingOrders.Impact);
            visual.visible = true;
        }

        void ApplyVisualSize()
        {
            if (visual == null || visual.sprite == null) return;
            var bounds = visual.sprite.bounds.size;
            if (bounds.x <= 0.001f || bounds.y <= 0.001f) return;
            visual.transform.localScale = new Vector3(hitboxSize.x / bounds.x, hitboxSize.y / bounds.y, 1f);
        }
    }
}
