using System.Collections;
using System.Collections.Generic;
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
        public float radius = 34f;

        public float ElapsedSeconds => elapsed;
        public float StageElapsedSeconds => stageElapsed;
        public float CurrentDirectionDegrees => directionDegrees;
        public int DirectionChangeIndex => directionChangeIndex;
        public string CurrentBossAnnouncement => currentBossAnnouncement;

        float elapsed;
        float stageElapsed;
        float elapsedOffset;
        float stageTimeMultiplier = 1f;
        float enemyMoveMultiplier = 1f;
        int currentStage = 1;
        float directionTimer;
        float directionDegrees;
        int directionChangeIndex;
        int lastDirectionSector = -1;
        bool running;
        bool[] timedSpawned;
        string currentBossAnnouncement = "オークキング出現！";
        readonly List<EnemyController> activeEnemies = new List<EnemyController>();

        public void Begin(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget)
        {
            BeginStage(gameConfig, tileGrid, chaseTarget, 1, 0f, 1f);
        }

        public void BeginStage(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget, int stage, float displayElapsedOffset, float speedMultiplier)
        {
            config = gameConfig;
            grid = tileGrid;
            target = chaseTarget;
            config.EnsureEnemySpawnDefaults();
            radius = Mathf.Max(10f, config.enemySpawnRadius);
            currentStage = Mathf.Max(1, stage);
            elapsedOffset = Mathf.Max(0f, displayElapsedOffset);
            elapsed = elapsedOffset;
            stageElapsed = 0f;
            stageTimeMultiplier = Mathf.Max(0.1f, speedMultiplier);
            enemyMoveMultiplier = Mathf.Max(0.1f, speedMultiplier);
            directionTimer = 0f;
            directionChangeIndex = 0;
            timedSpawned = new bool[TimedSpawnsForCurrentStage().Length];
            currentBossAnnouncement = BossAnnouncementForStage(currentStage);
            ChooseNextDirection();
            running = true;
            StartCoroutine(SpawnLoop());
        }

        void Update()
        {
            if (!running || Time.timeScale <= 0f) return;
            float delta = Time.deltaTime * stageTimeMultiplier;
            stageElapsed += delta;
            elapsed = elapsedOffset + stageElapsed;
            directionTimer += delta;
            if (directionTimer >= Mathf.Max(1f, config.spawnDirectionChangeSeconds))
            {
                directionTimer -= Mathf.Max(1f, config.spawnDirectionChangeSeconds);
                directionChangeIndex++;
                ChooseNextDirection();
            }
            ProcessTimedSpawns();
            activeEnemies.RemoveAll(enemy => enemy == null);
        }

        IEnumerator SpawnLoop()
        {
            while (running)
            {
                var phase = CurrentPhase();
                if (phase != null)
                {
                    int batch = Mathf.Clamp(
                        phase.baseBatchCount + directionChangeIndex * phase.batchIncreasePerDirectionChange,
                        1,
                        Mathf.Max(1, phase.maxBatchCount));
                    SpawnBatch(phase.enemyKind, batch);
                }
                float interval = phase != null ? phase.spawnInterval : config.spawnInterval;
                yield return new WaitForSeconds(Mathf.Max(0.18f, interval / Mathf.Max(0.1f, stageTimeMultiplier)));
            }
        }

        void ProcessTimedSpawns()
        {
            var timedSpawns = TimedSpawnsForCurrentStage();
            if (timedSpawns == null) return;
            for (int i = 0; i < timedSpawns.Length; i++)
            {
                if (timedSpawned[i]) continue;
                var timed = timedSpawns[i];
                if (timed == null || stageElapsed < timed.timeSeconds) continue;
                timedSpawned[i] = true;
                SpawnBatch(timed.enemyKind, Mathf.Max(1, timed.count), true);
                if (timed.announce && !string.IsNullOrEmpty(timed.announcement))
                {
                    GameManager.Instance?.ShowAnnouncement(timed.announcement);
                }
            }
        }

        SpawnPhase CurrentPhase()
        {
            var phases = SpawnPhasesForCurrentStage();
            if (phases == null || phases.Length == 0) return null;
            SpawnPhase result = phases[0];
            foreach (var phase in phases)
            {
                if (phase != null && phase.startSeconds <= stageElapsed && phase.startSeconds >= result.startSeconds) result = phase;
            }
            return result;
        }

        void SpawnBatch(EnemyKind kind, int count, bool force = false)
        {
            if (enemyPrefab == null || target == null) return;
            int capacity = force ? count : Mathf.Max(0, config.maxAliveEnemies - activeEnemies.Count);
            int spawnCount = Mathf.Min(count, capacity);
            for (int i = 0; i < spawnCount; i++) SpawnOne(kind);
        }

        void SpawnOne(EnemyKind kind)
        {
            kind = ApplyEliteSpawnChance(kind);
            var definition = config.GetEnemyDefinition(kind);
            if (definition == null) return;
            float halfArc = Mathf.Max(0.5f, config.spawnDirectionArcDegrees * 0.5f);
            float angle = directionDegrees + Random.Range(-halfArc, halfArc);
            var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var spawnPosition = ClampSpawnInsideGrid(target.position + (Vector3)(dir * radius), definition.cellSize);
            var go = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            go.name = definition.displayName;
            var enemy = go.GetComponent<EnemyController>();
            if (enemy == null)
            {
                Destroy(go);
                return;
            }

            enemy.xpOrbPrefab = xpOrbPrefab;
            enemy.damagePopupPrefab = damagePopupPrefab;
            int hp = Mathf.Max(1, Mathf.RoundToInt(config.enemyBaseHp * Mathf.Max(0.01f, definition.hpMultiplier)));
            enemy.Configure(config, grid, target, definition, hp, definition.speedMultiplier * enemyMoveMultiplier);
            activeEnemies.Add(enemy);
            if (definition.boss) GameManager.Instance?.BossSpawned(enemy);
        }

        EnemyKind ApplyEliteSpawnChance(EnemyKind kind)
        {
            int level = ProgressionStore.GetLevel(UpgradeType.EliteSpawnRate);
            if (level <= 0 || config == null) return kind;
            float chance = Mathf.Clamp01(level * config.eliteSpawnRatePerUpgradeLevel);
            if (Random.value >= chance) return kind;
            if (kind == EnemyKind.Boar) return EnemyKind.EliteBoar;
            if (kind == EnemyKind.Orc) return EnemyKind.EliteOrc;
            if (kind == EnemyKind.Goblin) return EnemyKind.EliteGoblin;
            if (kind == EnemyKind.Ogre) return EnemyKind.EliteOgre;
            return kind;
        }

        Vector3 ClampSpawnInsideGrid(Vector3 candidate, float enemyCellSize)
        {
            if (grid == null || grid.groundTilemap == null || grid.width <= 0 || grid.height <= 0) return candidate;

            Vector3 minCenter = grid.GridToWorld(0, 0);
            Vector3 maxCenter = grid.GridToWorld(grid.width - 1, grid.height - 1);
            Vector3 rightStep = grid.width > 1 ? grid.GridToWorld(1, 0) - minCenter : new Vector3(grid.cellSize, 0f, 0f);
            Vector3 upStep = grid.height > 1 ? grid.GridToWorld(0, 1) - minCenter : new Vector3(0f, grid.cellSize, 0f);
            float insetX = Mathf.Abs(rightStep.x) * (Mathf.Max(1f, enemyCellSize) * 0.5f + 0.5f);
            float insetY = Mathf.Abs(upStep.y) * (Mathf.Max(1f, enemyCellSize) * 0.5f + 0.5f);
            float minX = Mathf.Min(minCenter.x, maxCenter.x) - Mathf.Abs(rightStep.x) * 0.5f + insetX;
            float maxX = Mathf.Max(minCenter.x, maxCenter.x) + Mathf.Abs(rightStep.x) * 0.5f - insetX;
            float minY = Mathf.Min(minCenter.y, maxCenter.y) - Mathf.Abs(upStep.y) * 0.5f + insetY;
            float maxY = Mathf.Max(minCenter.y, maxCenter.y) + Mathf.Abs(upStep.y) * 0.5f - insetY;
            candidate.x = Mathf.Clamp(candidate.x, minX, maxX);
            candidate.y = Mathf.Clamp(candidate.y, minY, maxY);
            return candidate;
        }

        void ChooseNextDirection()
        {
            const int sectors = 8;
            int sector = Random.Range(0, sectors);
            if (sector == lastDirectionSector) sector = (sector + Random.Range(1, sectors)) % sectors;
            lastDirectionSector = sector;
            directionDegrees = sector * (360f / sectors);
        }

        public void StopAndClearEnemies(EnemyController except = null)
        {
            running = false;
            StopAllCoroutines();
            foreach (var enemy in FindObjectsOfType<EnemyController>())
            {
                if (enemy != null && enemy != except) Destroy(enemy.gameObject);
            }
            activeEnemies.Clear();
        }

        SpawnPhase[] SpawnPhasesForCurrentStage()
        {
            if (currentStage == 2)
            {
                return new[]
                {
                    new SpawnPhase { startSeconds = 0f, enemyKind = EnemyKind.Goblin, spawnInterval = config.spawnInterval, baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 10 },
                    new SpawnPhase { startSeconds = 150f, enemyKind = EnemyKind.Ogre, spawnInterval = Mathf.Max(0.5f, config.spawnInterval * 1.05f), baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 14 }
                };
            }

            return config.spawnPhases;
        }

        TimedEnemySpawn[] TimedSpawnsForCurrentStage()
        {
            if (currentStage == 2)
            {
                return new[]
                {
                    new TimedEnemySpawn { timeSeconds = 120f, enemyKind = EnemyKind.EliteGoblin, count = 1, announce = true, announcement = "エリートゴブリン出現！" },
                    new TimedEnemySpawn { timeSeconds = 270f, enemyKind = EnemyKind.EliteOgre, count = 1, announce = true, announcement = "エリートオーガ出現！" },
                    new TimedEnemySpawn { timeSeconds = 300f, enemyKind = EnemyKind.GoblinLord, count = 1, announce = true, announcement = "ゴブリンロード出現！" }
                };
            }

            return config.timedEnemySpawns ?? new TimedEnemySpawn[0];
        }

        static string BossAnnouncementForStage(int stage)
        {
            return stage == 2 ? "ゴブリンロード出現！" : "オークキング出現！";
        }
    }
}
