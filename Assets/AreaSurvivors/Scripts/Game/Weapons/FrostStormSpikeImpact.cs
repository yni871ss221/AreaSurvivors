using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class FrostStormSpikeImpact : MonoBehaviour
    {
        [SerializeField] SpriteRenderer spikeRenderer;
        [SerializeField, Min(0.05f)] float visualDurationSeconds = 0.35f;

        WeaponType sourceWeaponType = WeaponType.Frost;
        int damage;
        float activateAt;
        float hideAt;
        bool applied;

        public static int ResolveDamage(int attackPower)
        {
            return Mathf.Max(0, attackPower) * 2;
        }

        public static Vector2 ResolveHitboxSize(float cellSize)
        {
            float size = Mathf.Max(0.01f, cellSize);
            return new Vector2(size, size);
        }

        public void Configure(int attackPower, float activationDelaySeconds, WeaponType weaponType)
        {
            damage = ResolveDamage(attackPower);
            sourceWeaponType = weaponType;
            activateAt = Time.time + Mathf.Max(0f, activationDelaySeconds);
            hideAt = activateAt + Mathf.Max(0.05f, visualDurationSeconds);
            applied = false;
            if (spikeRenderer != null) spikeRenderer.enabled = true;
            if (activationDelaySeconds <= 0f) ApplyImpact();
        }

        void Update()
        {
            if (!applied && Time.time >= activateAt) ApplyImpact();
            if (spikeRenderer != null && spikeRenderer.enabled && Time.time >= hideAt)
            {
                spikeRenderer.enabled = false;
            }
        }

        void ApplyImpact()
        {
            if (applied) return;
            applied = true;
            if (damage <= 0) return;

            Vector2 hitboxSize = ResolveHitboxSize(TileGrid.DefaultCellSize);
            var colliders = Physics2D.OverlapBoxAll(transform.position, hitboxSize, 0f);
            var damaged = new HashSet<Health>();
            for (int i = 0; i < colliders.Length; i++)
            {
                var enemy = colliders[i] != null ? colliders[i].GetComponentInParent<EnemyController>() : null;
                var health = enemy != null ? enemy.GetComponent<Health>() : null;
                if (health == null || health.IsDead || !damaged.Add(health)) continue;
                int dealt = health.Damage(damage, transform.position);
                if (dealt > 0) GameManager.Instance?.RegisterWeaponDamage(sourceWeaponType, dealt);
            }
        }
    }
}
