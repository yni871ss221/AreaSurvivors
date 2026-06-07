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

        public void Configure(GameConfig gameConfig, PlayerController owner)
        {
            config = gameConfig;
            player = owner;
            attackPower = config.baseAttackPower + ProgressionStore.GetLevel(UpgradeType.AttackPower);
            cooldownMultiplier = Mathf.Max(0.45f, 1f - ProgressionStore.GetLevel(UpgradeType.AttackCooldown) * 0.06f);
            StopAllCoroutines();
            StartCoroutine(AttackLoop());
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
            var center = transform.position + (Vector3)(direction * 1.05f);
            var hits = Physics2D.OverlapCircleAll(center, 1.05f);
            for (int i = 0; i < hits.Length; i++)
            {
                var enemy = hits[i].GetComponent<EnemyController>();
                if (enemy == null) continue;
                var dealt = hits[i].GetComponent<Health>()?.Damage(attackPower + 2) ?? 0;
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
            go.GetComponent<Projectile>().Launch(dir.normalized, attackPower, config.projectileSpeed, explosive);
        }
    }
}
