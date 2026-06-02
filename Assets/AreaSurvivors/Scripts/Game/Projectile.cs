using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Projectile : MonoBehaviour
    {
        public float lifetime = 3f;
        int damage;
        bool explosive;

        public void Launch(Vector2 direction, int amount, float speed, bool isExplosive)
        {
            damage = amount;
            explosive = isExplosive;
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
            if (explosive)
            {
                var hits = Physics2D.OverlapCircleAll(transform.position, 1.1f);
                foreach (var hit in hits)
                {
                    if (hit.GetComponent<EnemyController>() != null)
                    {
                        var dealt = hit.GetComponent<Health>()?.Damage(damage) ?? 0;
                        GameManager.Instance?.RegisterDamageDealt(dealt);
                    }
                }
            }
            else
            {
                var dealt = other.GetComponent<Health>()?.Damage(damage) ?? 0;
                GameManager.Instance?.RegisterDamageDealt(dealt);
            }
            Destroy(gameObject);
        }
    }
}
