using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class WeaponController : MonoBehaviour
    {
        public GameObject arrowPrefab;
        public GameObject fireballPrefab;
        public Transform slashOrigin;
        GameConfig config;
        PlayerController player;
        int attackPower;
        float cooldownMultiplier;
        public int AttackPower => attackPower;
        public float CurrentCooldown => config == null || player == null ? 0f : GetCooldown();
        public float ProjectileSpeed => config != null ? config.projectileSpeed : 0f;
        public float WeaponRange
        {
            get
            {
                if (config == null || player == null) return 0f;
                if (player.characterType == CharacterType.Knight) return config.knightSlashRange;
                if (player.characterType == CharacterType.Mage) return config.mageExplosionRadius;
                return config.projectileSpeed * config.projectileLifetime;
            }
        }

        public void Configure(GameConfig gameConfig, PlayerController owner)
        {
            config = gameConfig;
            player = owner;
            RefreshFromStats();
            StopAllCoroutines();
            StartCoroutine(AttackLoop());
        }

        public void RefreshFromStats()
        {
            if (config == null || player == null) return;
            var stats = player.Stats;
            attackPower = stats.attackPower;
            cooldownMultiplier = Mathf.Max(config.minAttackCooldownMultiplier, stats.attackCooldownMultiplier);
        }

        IEnumerator AttackLoop()
        {
            while (true)
            {
                if (player != null && !player.IsReviving)
                {
                    if (player.characterType == CharacterType.Knight) KnightSlash();
                    if (player.characterType == CharacterType.Archer) ShootAtNearest(arrowPrefab, false);
                    if (player.characterType == CharacterType.Mage) ShootAtNearest(fireballPrefab, true);
                }

                yield return new WaitForSeconds(GetCooldown());
            }
        }

        float GetCooldown()
        {
            if (player.characterType == CharacterType.Knight) return config.knightCooldown * cooldownMultiplier;
            if (player.characterType == CharacterType.Archer) return config.archerCooldown * cooldownMultiplier;
            return config.mageCooldown * cooldownMultiplier;
        }

        void KnightSlash()
        {
            var direction = player.Facing.sqrMagnitude > 0.01f ? player.Facing.normalized : Vector2.down;
            var center = transform.position + (Vector3)(direction * config.knightSlashOffset);
            var hits = Physics2D.OverlapCircleAll(center, config.knightSlashRange);
            for (int i = 0; i < hits.Length; i++)
            {
                var enemy = hits[i].GetComponent<EnemyController>();
                if (enemy == null) continue;
                var dealt = hits[i].GetComponent<Health>()?.Damage(attackPower + config.knightDamageBonus) ?? 0;
                ApplyKnockback(enemy, direction);
                GameManager.Instance?.RegisterDamageDealt(dealt);
            }
            SlashView.Flash(transform.position, direction);
        }

        void ShootAtNearest(GameObject prefab, bool explosive)
        {
            if (prefab == null) return;
            var enemies = FindObjectsOfType<EnemyController>();
            EnemyController nearest = null;
            float best = float.MaxValue;
            foreach (var enemy in enemies)
            {
                float d = (enemy.transform.position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    nearest = enemy;
                }
            }

            var dir = nearest == null ? player.Facing : (Vector2)(nearest.transform.position - transform.position);
            var go = Instantiate(prefab, transform.position, Quaternion.identity);
            go.GetComponent<Projectile>().Launch(dir.normalized, attackPower, config.projectileSpeed, explosive, config.mageExplosionRadius, config.projectileLifetime, config.projectileVisualScale);
            var projectile = go.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.knockback = player.Stats.knockback * config.knockbackForceUnit;
                projectile.knockbackDuration = config.knockbackDuration;
            }
        }

        void ApplyKnockback(EnemyController enemy, Vector2 direction)
        {
            if (enemy == null || config == null || player == null) return;
            var receiver = enemy.GetComponent<KnockbackReceiver>();
            if (receiver == null) return;
            receiver.Apply(direction, player.Stats.knockback * config.knockbackForceUnit, config.knockbackDuration);
        }
    }
}
