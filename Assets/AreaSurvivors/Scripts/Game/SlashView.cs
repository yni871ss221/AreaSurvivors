using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class SlashView : MonoBehaviour
    {
        static Sprite[] frames;

        const float HitboxWidthMultiplier = 0.5f;
        const float HitboxForwardCenterMultiplier = 0.56f;

        public static void Flash(Vector3 position, Vector2 direction, float range, float baseRange, int damage, float knockback, float knockbackDuration)
        {
            var go = new GameObject("Knight Slash");
            var dir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.down;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float rangeScale = Mathf.Max(0.05f, range) / Mathf.Max(0.05f, baseRange);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var hitbox = go.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(Mathf.Max(0.05f, range), Mathf.Max(0.05f, range * HitboxWidthMultiplier));
            hitbox.offset = new Vector2(range * HitboxForwardCenterMultiplier, 0f);

            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;

            var visualObject = new GameObject("Visual");
            visualObject.transform.SetParent(go.transform, false);
            visualObject.transform.localPosition = new Vector3(hitbox.offset.x, hitbox.offset.y, 0f);
            visualObject.transform.localScale = Vector3.one * (0.78f * rangeScale);
            var billboard = visualObject.AddComponent<PaperBillboard>();
            billboard.rollDegrees = angle;
            EnsureFrames();
            var visual = visualObject.AddComponent<PaperMeshVisual>();
            visual.Configure(frames.Length > 0 ? frames[0] : Resources.Load<Sprite>("Slash"), new Color(1f, 0.92f, 0.42f, 0.82f), WeaponSortingOrders.Slash);
            PixelBurstEffect.Spawn(visual.sprite, visualObject.transform.position, new Color(1f, 0.94f, 0.45f, 0.46f), 3, 0.22f * rangeScale, 0.16f * rangeScale, WeaponSortingOrders.SlashBurst);
            go.AddComponent<KnightSlashHitbox>().Configure(hitbox, position, dir, damage, knockback, knockbackDuration);
            go.AddComponent<SlashView>().StartCoroutine(go.GetComponent<SlashView>().Life(visual, dir));
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

        IEnumerator Life(PaperMeshVisual visual, Vector2 direction)
        {
            EnsureFrames();
            float frameSeconds = 0.055f;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null) visual.sprite = frames[i];
                visual.color = new Color(1f, 0.92f, 0.42f, Mathf.Lerp(0.82f, 0.32f, i / Mathf.Max(1f, frames.Length - 1f)));
                transform.position += (Vector3)direction * 0.035f;
                yield return new WaitForSeconds(frameSeconds);
            }
            Destroy(gameObject);
        }
    }

    sealed class KnightSlashHitbox : MonoBehaviour
    {
        readonly List<Collider2D> hits = new List<Collider2D>(16);
        readonly HashSet<EnemyController> damaged = new HashSet<EnemyController>();
        BoxCollider2D hitbox;
        Vector3 origin;
        Vector2 direction;
        int damage;
        float knockback;
        float knockbackDuration;
        const int SlashPaintRadius = 1;

        public void Configure(BoxCollider2D source, Vector3 attackOrigin, Vector2 attackDirection, int attackDamage, float knockbackStrength, float knockbackSeconds)
        {
            hitbox = source;
            origin = attackOrigin;
            direction = attackDirection.sqrMagnitude > 0.01f ? attackDirection.normalized : Vector2.down;
            damage = attackDamage;
            knockback = knockbackStrength;
            knockbackDuration = knockbackSeconds;
            StartCoroutine(DamageAfterPhysicsSync());
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
                var dealt = health != null ? health.Damage(damage, hits[i].ClosestPoint(origin)) : 0;
                PaintPlayerTerritory(enemy.transform.position, SlashPaintRadius);
                ApplyKnockback(enemy);
                GameManager.Instance?.RegisterDamageDealt(dealt);
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
            receiver.Apply(direction, knockback, knockbackDuration);
        }
    }
}
