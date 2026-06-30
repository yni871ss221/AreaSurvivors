using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class ShieldOrbitShield : MonoBehaviour
    {
        readonly Dictionary<EnemyController, float> nextHitTimes = new Dictionary<EnemyController, float>();
        WeaponController owner;
        int damage;
        float knockback;
        float knockbackDuration;
        float hitCooldown = 0.35f;

        public void Configure(WeaponController source, int hitDamage, float knockbackStrength, float knockbackSeconds, float cooldownSeconds)
        {
            owner = source;
            damage = Mathf.Max(0, hitDamage);
            knockback = Mathf.Max(0f, knockbackStrength);
            knockbackDuration = Mathf.Max(0f, knockbackSeconds);
            hitCooldown = Mathf.Max(0.05f, cooldownSeconds);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            TryHit(other);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            TryHit(other);
        }

        void TryHit(Collider2D other)
        {
            var enemy = other.GetComponent<EnemyController>();
            if (enemy == null) enemy = other.GetComponentInParent<EnemyController>();
            if (enemy == null) return;

            float now = Time.time;
            if (nextHitTimes.TryGetValue(enemy, out var nextTime) && now < nextTime) return;

            var health = enemy.GetComponent<Health>();
            if (health == null || health.IsDead) return;

            nextHitTimes[enemy] = now + hitCooldown;
            var hitPoint = other.ClosestPoint(transform.position);
            int dealt = health.Damage(damage, hitPoint);
            ApplyKnockback(enemy);
            if (dealt > 0)
            {
                AudioManager.PlaySfx(SfxTrack.ShieldHit);
                GameManager.Instance?.RegisterDamageDealt(dealt);
            }
        }

        void ApplyKnockback(EnemyController enemy)
        {
            if (enemy == null || knockback <= 0f || knockbackDuration <= 0f) return;
            var receiver = enemy.GetComponent<KnockbackReceiver>();
            if (receiver == null) return;
            var origin = owner != null ? owner.transform.position : transform.position;
            var direction = (Vector2)(enemy.transform.position - origin);
            receiver.Apply(direction, knockback, knockbackDuration);
        }
    }
}
