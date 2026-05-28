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
            GetComponent<Rigidbody2D>().velocity = direction.normalized * speed;
            transform.right = direction;
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
                    if (hit.GetComponent<EnemyController>() != null) hit.GetComponent<Health>()?.Damage(damage);
                }
            }
            else
            {
                other.GetComponent<Health>()?.Damage(damage);
            }
            Destroy(gameObject);
        }
    }
}
