using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class ProjectileExplosionHitbox : MonoBehaviour
    {
        [SerializeField] CircleCollider2D hitbox;
        [SerializeField] Rigidbody2D body;

        readonly List<Collider2D> hits = new List<Collider2D>(24);
        readonly HashSet<EnemyController> damaged = new HashSet<EnemyController>();
        Vector3 origin;
        int damage;
        float knockback;
        float knockbackDuration;
        bool paintsTerritory;
        RunDamageSource damageSource;

        public static void Spawn(GameObject prefab, Vector3 position, float radius, int damage, float knockback, float knockbackDuration, bool paintsTerritory = true, RunDamageSource source = default)
        {
            if (prefab == null)
            {
                Debug.LogError("Projectile explosion hitbox prefab is missing. Assign Projectile.explosionHitboxPrefab on projectile prefabs.");
                return;
            }

            var go = Instantiate(prefab, position, Quaternion.identity);
            go.name = "Projectile Explosion Hitbox";
            var explosion = go.GetComponent<ProjectileExplosionHitbox>();
            if (explosion == null)
            {
                Debug.LogError("Projectile explosion hitbox prefab is missing ProjectileExplosionHitbox.");
                Destroy(go);
                return;
            }

            explosion.Configure(position, radius, damage, knockback, knockbackDuration, paintsTerritory, source);
        }

        void Configure(Vector3 hitOrigin, float radius, int hitDamage, float knockbackStrength, float knockbackSeconds, bool shouldPaintTerritory, RunDamageSource runDamageSource)
        {
            if (hitbox == null) hitbox = GetComponent<CircleCollider2D>();
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (hitbox == null)
            {
                Debug.LogError("Projectile explosion hitbox prefab has no CircleCollider2D.");
                Destroy(gameObject);
                return;
            }

            hitbox.isTrigger = true;
            hitbox.radius = Mathf.Max(0.05f, radius);
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.simulated = true;
            }

            origin = hitOrigin;
            damage = hitDamage;
            knockback = knockbackStrength;
            knockbackDuration = knockbackSeconds;
            paintsTerritory = shouldPaintTerritory;
            damageSource = runDamageSource;
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
            if (paintsTerritory) PaintPlayerTerritory(origin, hitbox.radius);
            Physics2D.SyncTransforms();
            hits.Clear();
            damaged.Clear();
            var filter = new ContactFilter2D();
            filter.NoFilter();
            hitbox.OverlapCollider(filter, hits);
            CombatPerformanceDiagnostics.RecordAreaOverlapQuery(hits.Count);
            for (int i = 0; i < hits.Count; i++)
            {
                var enemy = hits[i] != null ? hits[i].GetComponent<EnemyController>() : null;
                if (enemy == null || damaged.Contains(enemy)) continue;
                damaged.Add(enemy);
                var health = enemy.GetComponent<Health>();
                int creditedDamage = health != null && !health.IsDead ? health.DamageAmount(damage) : 0;
                if (health != null)
                {
                    CombatPerformanceDiagnostics.RecordAreaDamageAttempt();
                    int dealt = health.Damage(damage, hits[i].ClosestPoint(origin));
                    if (dealt > 0) CombatPerformanceDiagnostics.RecordAreaDamageHit();
                }
                ApplyKnockback(enemy);
                RegisterDamage(creditedDamage);
            }
        }

        void RegisterDamage(int amount)
        {
            if (damageSource.IsAssigned) GameManager.Instance?.RegisterDamageDealt(damageSource, amount);
            else GameManager.Instance?.RegisterDamageDealt(amount);
        }

        void ApplyKnockback(EnemyController enemy)
        {
            if (enemy == null || knockback <= 0f) return;
            var receiver = enemy.GetComponent<KnockbackReceiver>();
            if (receiver == null) return;
            var direction = ((Vector2)enemy.transform.position - (Vector2)origin).normalized;
            receiver.Apply(direction, knockback, knockbackDuration);
        }

        static void PaintPlayerTerritory(Vector3 position, float radiusWorld)
        {
            var grid = GameManager.Instance != null ? GameManager.Instance.grid : null;
            if (grid == null) return;

            Vector2 cellSize = grid.WorldCellSize();
            float radiusX = Mathf.Max(0.1f, radiusWorld / Mathf.Max(0.01f, cellSize.x));
            float radiusY = Mathf.Max(0.1f, radiusWorld / Mathf.Max(0.01f, cellSize.y));
            grid.PaintEllipseOverlappingCells(position, TileOwner.Player, radiusX, radiusY);
        }
    }
}
