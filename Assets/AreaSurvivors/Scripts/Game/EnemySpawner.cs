using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        public GameObject enemyPrefab;
        public GameConfig config;
        public TileGrid grid;
        public Transform target;
        public GameObject xpOrbPrefab;
        public GameObject damagePopupPrefab;
        public float radius = 20f;
        float elapsed;

        public void Begin(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget)
        {
            config = gameConfig;
            grid = tileGrid;
            target = chaseTarget;
            StartCoroutine(SpawnLoop());
        }

        void Update()
        {
            elapsed += Time.deltaTime;
        }

        IEnumerator SpawnLoop()
        {
            while (true)
            {
                float ramp = 1f + elapsed / Mathf.Max(1f, config.difficultyRampSeconds);
                int batch = Mathf.Clamp(Mathf.FloorToInt(ramp), 1, 8);
                for (int i = 0; i < batch; i++) SpawnOne(ramp);
                yield return new WaitForSeconds(Mathf.Max(0.18f, config.spawnInterval / ramp));
            }
        }

        void SpawnOne(float ramp)
        {
            if (enemyPrefab == null || target == null) return;
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;
            var go = Instantiate(enemyPrefab, target.position + (Vector3)(dir * radius), Quaternion.identity);
            var enemy = go.GetComponent<EnemyController>();
            enemy.xpOrbPrefab = xpOrbPrefab;
            enemy.damagePopupPrefab = damagePopupPrefab;
            enemy.Configure(config, grid, target, Mathf.RoundToInt(config.enemyBaseHp * ramp), Mathf.Min(1.65f, 1f + ramp * 0.06f));
        }
    }
}
