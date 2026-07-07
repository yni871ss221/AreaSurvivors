using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(BoxCollider2D), typeof(Rigidbody2D))]
    public sealed class SlashView : MonoBehaviour
    {
        static Sprite[] frames;

        [SerializeField] BoxCollider2D hitbox;
        [SerializeField] Rigidbody2D body;
        [SerializeField] Transform visualRoot;
        [SerializeField] PaperMeshVisual visual;
        [SerializeField] PaperBillboard billboard;
        [SerializeField] float hitboxWidthMultiplier = 0.5f;
        [SerializeField] float hitboxForwardCenterMultiplier = 0.56f;
        [SerializeField] float visualScaleMultiplier = 0.78f;
        [SerializeField] float frameSeconds = 0.055f;
        [SerializeField] Color slashColor = new Color(1f, 0.92f, 0.42f, 0.82f);
        [SerializeField] int slashSortingOrder = WeaponSortingOrders.Slash;

        readonly List<Collider2D> hits = new List<Collider2D>(16);
        readonly HashSet<EnemyController> damaged = new HashSet<EnemyController>();
        Vector3 origin;
        Vector2 attackDirection;
        int damage;
        float knockback;
        float knockbackDuration;
        const int SlashPaintRadius = 1;

        public static void Flash(GameObject prefab, Vector3 position, Vector2 direction, float range, float baseRange, int damage, float knockback, float knockbackDuration)
        {
            if (prefab == null)
            {
                Debug.LogError("Slash prefab is missing. Assign WeaponController.slashPrefab on the Player prefab.");
                return;
            }

            var go = Instantiate(prefab, position, Quaternion.identity);
            var slash = go.GetComponent<SlashView>();
            if (slash == null)
            {
                Debug.LogError("Slash prefab is missing SlashView.");
                Destroy(go);
                return;
            }

            slash.Play(position, direction, range, baseRange, damage, knockback, knockbackDuration);
        }

        public static void Flash(Vector3 position, Vector2 direction, float range, float baseRange, int damage, float knockback, float knockbackDuration)
        {
            Debug.LogError("Slash prefab overload should be used for slash attacks.");
        }

        public void Play(Vector3 position, Vector2 direction, float range, float baseRange, int damage, float knockback, float knockbackDuration)
        {
            EnsureReferences();
            var dir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.down;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float rangeScale = Mathf.Max(0.05f, range) / Mathf.Max(0.05f, baseRange);
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.simulated = true;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            if (hitbox != null)
            {
                hitbox.isTrigger = true;
                hitbox.size = new Vector2(Mathf.Max(0.05f, range), Mathf.Max(0.05f, range * hitboxWidthMultiplier));
                hitbox.offset = new Vector2(range * hitboxForwardCenterMultiplier, 0f);
            }

            if (visualRoot != null)
            {
                var offset = hitbox != null ? hitbox.offset : Vector2.zero;
                visualRoot.localPosition = new Vector3(offset.x, offset.y, 0f);
                visualRoot.localScale = Vector3.one * (visualScaleMultiplier * rangeScale);
            }

            if (billboard != null) billboard.rollDegrees = angle;
            EnsureFrames();
            if (visual != null)
            {
                var sprite = frames.Length > 0 && frames[0] != null ? frames[0] : visual.sprite;
                visual.Configure(sprite, slashColor, slashSortingOrder);
            }

            origin = position;
            attackDirection = dir;
            this.damage = damage;
            this.knockback = knockback;
            this.knockbackDuration = knockbackDuration;

            StartCoroutine(DamageAfterPhysicsSync());
            StartCoroutine(Life(dir));
        }

        static void EnsureFrames()
        {
            if (frames != null) return;
            frames = new[]
            {
                GeneratedSpriteLoader.Load("Slash_0"),
                GeneratedSpriteLoader.Load("Slash_1"),
                GeneratedSpriteLoader.Load("Slash_2")
            };
        }

        IEnumerator Life(Vector2 direction)
        {
            EnsureFrames();
            for (int i = 0; i < frames.Length; i++)
            {
                if (visual != null)
                {
                    if (frames[i] != null) visual.sprite = frames[i];
                    visual.color = new Color(slashColor.r, slashColor.g, slashColor.b, Mathf.Lerp(slashColor.a, 0.32f, i / Mathf.Max(1f, frames.Length - 1f)));
                }
                transform.position += (Vector3)direction * 0.035f;
                yield return new WaitForSeconds(frameSeconds);
            }
            Destroy(gameObject);
        }

        IEnumerator DamageAfterPhysicsSync()
        {
            yield return new WaitForFixedUpdate();
            DamageOverlaps();
        }

        void DamageOverlaps()
        {
            if (hitbox == null) return;
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
                int creditedDamage = health != null && !health.IsDead ? health.DamageAmount(damage) : 0;
                if (health != null) health.Damage(damage, hits[i].ClosestPoint(origin));
                PaintPlayerTerritory(enemy.transform.position, SlashPaintRadius);
                ApplyKnockback(enemy);
                GameManager.Instance?.RegisterWeaponDamage(WeaponType.Slash, creditedDamage);
            }
        }

        static void PaintPlayerTerritory(Vector3 position, int radius)
        {
            var grid = GameManager.Instance != null ? GameManager.Instance.grid : null;
            if (grid != null) grid.Paint(position, TileOwner.Player, Mathf.Max(1, radius));
        }

        void ApplyKnockback(EnemyController enemy)
        {
            if (enemy == null || knockback <= 0f) return;
            var receiver = enemy.GetComponent<KnockbackReceiver>();
            if (receiver == null) return;
            receiver.Apply(attackDirection, knockback, knockbackDuration);
        }

        void EnsureReferences()
        {
            if (hitbox == null) hitbox = GetComponent<BoxCollider2D>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (visual == null) visual = GetComponentInChildren<PaperMeshVisual>(true);
            if (visualRoot == null && visual != null) visualRoot = visual.transform;
            if (billboard == null && visualRoot != null) billboard = visualRoot.GetComponent<PaperBillboard>();
        }
    }
}
