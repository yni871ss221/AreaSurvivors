using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        sealed class SpawnPhase
        {
            public float startSeconds;
            public EnemyKind enemyKind = EnemyKind.Boar;
            public float spawnInterval = 1.8f;
            public int baseBatchCount = 1;
            public int batchIncreasePerDirectionChange = 1;
            public int maxBatchCount = 12;
        }

        sealed class TimedEnemySpawn
        {
            public float timeSeconds;
            public EnemyKind enemyKind;
            public int count = 1;
            public bool announce;
            public string announcement;
        }

        public GameObject enemyPrefab;
        public GameConfig config;
        public TileGrid grid;
        public Transform target;
        public GameObject xpOrbPrefab;
        public GameObject damagePopupPrefab;
        public float radius = 34f;
        [Header("Vertical Map Spawn")]
        public bool useUpperChunkSpawn;
        public int spawnChunkOffsetY = 3;
        public int spawnChunkCells = TileGrid.DefaultChunkCells;

        public float ElapsedSeconds => elapsed;
        public float StageElapsedSeconds => stageElapsed;
        public float CurrentDirectionDegrees => directionDegrees;
        public int DirectionChangeIndex => directionChangeIndex;
        public string CurrentBossAnnouncement => currentBossAnnouncement;

        float elapsed;
        float stageElapsed;
        float elapsedOffset;
        int currentStage = 1;
        int currentStageDifficulty = 1;
        float directionTimer;
        float directionDegrees;
        int directionChangeIndex;
        int lastDirectionSector = -1;
        Vector3Int currentSpawnCell;
        bool hasCurrentSpawnCell;
        bool hasNextBossTestSpawnDirection;
        Vector2 nextBossTestSpawnDirection;
        bool running;
        bool[] timedSpawned;
        string currentBossAnnouncement = "オークキング出現！";
        readonly List<EnemyController> activeEnemies = new List<EnemyController>();

        public void Begin(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget)
        {
            BeginStage(gameConfig, tileGrid, chaseTarget, 1, 0f);
        }

        public void BeginStage(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget, int stage, float displayElapsedOffset)
        {
            BeginStage(gameConfig, tileGrid, chaseTarget, stage, displayElapsedOffset, 0f);
        }

        public void BeginStage(GameConfig gameConfig, TileGrid tileGrid, Transform chaseTarget, int stage, float displayElapsedOffset, float startStageElapsedSeconds)
        {
            config = gameConfig;
            grid = tileGrid;
            target = chaseTarget;
            config.EnsureEnemySpawnDefaults();
            radius = Mathf.Max(10f, config.enemySpawnRadius);
            currentStage = Mathf.Max(1, stage);
            currentStageDifficulty = ProgressionStore.GetStageDifficulty(currentStage);
            elapsedOffset = Mathf.Max(0f, displayElapsedOffset);
            stageElapsed = Mathf.Max(0f, startStageElapsedSeconds);
            elapsed = elapsedOffset + stageElapsed;
            directionTimer = 0f;
            directionChangeIndex = 0;
            timedSpawned = new bool[TimedSpawnsForCurrentStage().Length];
            MarkPastTimedSpawnsAsHandled();
            currentBossAnnouncement = BossAnnouncementForStage(currentStage);
            ChooseNextDirection();
            running = true;
            StartCoroutine(SpawnLoop());
        }

        public void SetNextBossTestSpawnSide(BossTestSpawnSide side)
        {
            nextBossTestSpawnDirection = DirectionForBossTestSpawnSide(side);
            hasNextBossTestSpawnDirection = true;
        }

        void Update()
        {
            if (!running || Time.timeScale <= 0f) return;
            float delta = Time.deltaTime;
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

        void MarkPastTimedSpawnsAsHandled()
        {
            var timedSpawns = TimedSpawnsForCurrentStage();
            if (timedSpawns == null || timedSpawned == null) return;
            for (int i = 0; i < timedSpawns.Length && i < timedSpawned.Length; i++)
            {
                var timed = timedSpawns[i];
                timedSpawned[i] = timed != null && timed.timeSeconds < stageElapsed;
            }
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
                    SpawnBatch(phase.enemyKind, DifficultySpawnCount(batch));
                }
                float interval = phase != null ? phase.spawnInterval : config.spawnInterval;
                yield return new WaitForSeconds(Mathf.Max(0.18f, interval));
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
                SpawnBatch(timed.enemyKind, TimedSpawnCount(timed), true);
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
            int capacity = force ? count : Mathf.Max(0, MaxAliveEnemiesForDifficulty() - activeEnemies.Count);
            int spawnCount = Mathf.Min(count, capacity);
            for (int i = 0; i < spawnCount; i++) SpawnOne(kind);
        }

        void SpawnOne(EnemyKind kind)
        {
            var definition = config.GetEnemyDefinition(kind);
            if (definition == null) return;
            var spawnPosition = ResolveSpawnPosition(definition);
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
            enemy.Configure(config, grid, target, definition, hp, definition.speedMultiplier);
            activeEnemies.Add(enemy);
            if (definition.boss) GameManager.Instance?.BossSpawned(enemy);
        }

        public EnemyController SpawnSummonedEnemy(EnemyKind kind, Vector3 worldPosition)
        {
            if (enemyPrefab == null || config == null || grid == null || target == null) return null;
            var definition = config.GetEnemyDefinition(kind);
            if (definition == null) return null;
            var spawnPosition = ClampSpawnInsideGrid(worldPosition, definition.cellSize);
            var go = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
            go.name = definition.displayName;
            var enemy = go.GetComponent<EnemyController>();
            if (enemy == null)
            {
                Destroy(go);
                return null;
            }

            enemy.xpOrbPrefab = xpOrbPrefab;
            enemy.damagePopupPrefab = damagePopupPrefab;
            int hp = EnemyHp(definition);
            enemy.Configure(config, grid, target, definition, hp, definition.speedMultiplier);
            activeEnemies.Add(enemy);
            return enemy;
        }

        int TimedSpawnCount(TimedEnemySpawn timed)
        {
            int count = Mathf.Max(1, timed != null ? timed.count : 1);
            if (IsBossTimedSpawn(timed)) return count;
            if (!IsEliteTimedSpawn(timed)) return count;

            int skillLevel = Mathf.Clamp(
                ProgressionStore.GetLevel(UpgradeType.EliteSpawnCount),
                0,
                ProgressionStore.GetMaxLevel(UpgradeType.EliteSpawnCount));
            int countPerLevel = config != null ? Mathf.Max(0, config.eliteTimedSpawnCountPerUpgradeLevel) : 1;
            return DifficultySpawnCount(count + skillLevel * countPerLevel);
        }

        int DifficultySpawnCount(int count)
        {
            return Mathf.Max(1, count) * Mathf.Clamp(currentStageDifficulty, ProgressionStore.MinStageDifficulty, ProgressionStore.MaxStageDifficulty);
        }

        int MaxAliveEnemiesForDifficulty()
        {
            int difficulty = Mathf.Clamp(currentStageDifficulty, ProgressionStore.MinStageDifficulty, ProgressionStore.MaxStageDifficulty);
            return Mathf.Max(1, config.maxAliveEnemies) * difficulty;
        }

        int EnemyHp(EnemyDefinition definition)
        {
            int hp = Mathf.Max(1, Mathf.RoundToInt(config.enemyBaseHp * Mathf.Max(0.01f, definition.hpMultiplier)));
            if (definition != null && definition.boss)
            {
                hp *= Mathf.Clamp(currentStageDifficulty, ProgressionStore.MinStageDifficulty, ProgressionStore.MaxStageDifficulty);
            }

            return hp;
        }

        static bool IsBossTimedSpawn(TimedEnemySpawn timed)
        {
            if (timed == null) return false;
            return timed.enemyKind == EnemyKind.OrcKing ||
                timed.enemyKind == EnemyKind.GoblinLord ||
                timed.enemyKind == EnemyKind.Lich ||
                timed.enemyKind == EnemyKind.Dragon;
        }

        static bool IsEliteTimedSpawn(TimedEnemySpawn timed)
        {
            if (timed == null) return false;
            return timed.enemyKind == EnemyKind.EliteBoar ||
                timed.enemyKind == EnemyKind.EliteOrc ||
                timed.enemyKind == EnemyKind.EliteGoblin ||
                timed.enemyKind == EnemyKind.EliteOgre ||
                timed.enemyKind == EnemyKind.EliteSkeleton ||
                timed.enemyKind == EnemyKind.EliteSkeletonKnight ||
                timed.enemyKind == EnemyKind.EliteLizard ||
                timed.enemyKind == EnemyKind.EliteLizardman;
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
            if (useUpperChunkSpawn && TryChooseUpperChunkSpawnCell())
            {
                return;
            }

            const int sectors = 8;
            int sector = Random.Range(0, sectors);
            if (sector == lastDirectionSector) sector = (sector + Random.Range(1, sectors)) % sectors;
            lastDirectionSector = sector;
            directionDegrees = sector * (360f / sectors);
        }

        Vector3 ResolveSpawnPosition(EnemyDefinition definition)
        {
            if (definition != null && definition.boss && hasNextBossTestSpawnDirection)
            {
                hasNextBossTestSpawnDirection = false;
                var fixedDirection = nextBossTestSpawnDirection.sqrMagnitude > 0.001f
                    ? nextBossTestSpawnDirection.normalized
                    : Vector2.up;
                return ClampSpawnInsideGrid(target.position + (Vector3)(fixedDirection * radius), definition.cellSize);
            }

            if (useUpperChunkSpawn)
            {
                if (!hasCurrentSpawnCell) TryChooseUpperChunkSpawnCell();
                if (hasCurrentSpawnCell && grid != null)
                {
                    var basePosition = grid.groundTilemap.GetCellCenterWorld(currentSpawnCell);
                    var jitter = Random.insideUnitCircle * Mathf.Max(0.05f, grid.cellSize * 0.35f);
                    return ClampSpawnInsideGrid(basePosition + new Vector3(jitter.x, jitter.y, 0f), definition.cellSize);
                }
            }

            float halfArc = Mathf.Max(0.5f, config.spawnDirectionArcDegrees * 0.5f);
            float angle = directionDegrees + Random.Range(-halfArc, halfArc);
            var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            return ClampSpawnInsideGrid(target.position + (Vector3)(dir * radius), definition.cellSize);
        }

        static Vector2 DirectionForBossTestSpawnSide(BossTestSpawnSide side)
        {
            switch (side)
            {
                case BossTestSpawnSide.Down:
                    return Vector2.down;
                case BossTestSpawnSide.Left:
                    return Vector2.left;
                case BossTestSpawnSide.Right:
                    return Vector2.right;
                default:
                    return Vector2.up;
            }
        }

        bool TryChooseUpperChunkSpawnCell()
        {
            if (grid == null || grid.width <= 0 || grid.height <= 0) return false;
            int chunkCells = Mathf.Max(1, spawnChunkCells > 0 ? spawnChunkCells : grid.groundChunkCells);
            int columns = Mathf.Max(1, Mathf.CeilToInt(grid.width / (float)chunkCells));
            int rows = Mathf.Max(1, Mathf.CeilToInt(grid.height / (float)chunkCells));
            int centerColumn = columns / 2;
            int centerRow = rows / 2;
            int spawnRow = Mathf.Clamp(centerRow + Mathf.Max(1, spawnChunkOffsetY), 0, rows - 1);
            int startX = centerColumn * chunkCells;
            int endX = Mathf.Min(grid.width, startX + chunkCells);
            int startY = spawnRow * chunkCells;
            int endY = Mathf.Min(grid.height, startY + chunkCells);
            if (startX >= endX || startY >= endY) return false;

            var selected = grid.GridToCell(Random.Range(startX, endX), Random.Range(startY, endY));
            for (int i = 0; i < 20; i++)
            {
                var candidate = grid.GridToCell(Random.Range(startX, endX), Random.Range(startY, endY));
                if (grid.HasObject(candidate)) continue;
                selected = candidate;
                break;
            }

            currentSpawnCell = selected;
            hasCurrentSpawnCell = true;
            return true;
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
            if (currentStage == 4)
            {
                return new[]
                {
                    new SpawnPhase { startSeconds = 0f, enemyKind = EnemyKind.Lizard, spawnInterval = config.spawnInterval, baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 10 },
                    new SpawnPhase { startSeconds = 60f, enemyKind = EnemyKind.Lizardman, spawnInterval = Mathf.Max(0.5f, config.spawnInterval * 1.05f), baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 14 }
                };
            }

            if (currentStage == 3)
            {
                return new[]
                {
                    new SpawnPhase { startSeconds = 0f, enemyKind = EnemyKind.Skeleton, spawnInterval = config.spawnInterval, baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 10 },
                    new SpawnPhase { startSeconds = 60f, enemyKind = EnemyKind.SkeletonKnight, spawnInterval = Mathf.Max(0.5f, config.spawnInterval * 1.05f), baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 14 }
                };
            }

            if (currentStage == 2)
            {
                return new[]
                {
                    new SpawnPhase { startSeconds = 0f, enemyKind = EnemyKind.Goblin, spawnInterval = config.spawnInterval, baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 10 },
                    new SpawnPhase { startSeconds = 60f, enemyKind = EnemyKind.Ogre, spawnInterval = Mathf.Max(0.5f, config.spawnInterval * 1.05f), baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 14 }
                };
            }

            return new[]
            {
                new SpawnPhase { startSeconds = 0f, enemyKind = EnemyKind.Boar, spawnInterval = config.spawnInterval, baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 10 },
                new SpawnPhase { startSeconds = 60f, enemyKind = EnemyKind.Orc, spawnInterval = Mathf.Max(0.5f, config.spawnInterval * 1.05f), baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 14 }
            };
        }

        TimedEnemySpawn[] TimedSpawnsForCurrentStage()
        {
            if (currentStage == 4)
            {
                return new[]
                {
                    new TimedEnemySpawn { timeSeconds = 30f, enemyKind = EnemyKind.EliteLizard, count = 1, announce = true, announcement = "エリートリザード出現！" },
                    new TimedEnemySpawn { timeSeconds = 90f, enemyKind = EnemyKind.EliteLizardman, count = 1, announce = true, announcement = "エリートリザードマン出現！" },
                    new TimedEnemySpawn { timeSeconds = 120f, enemyKind = EnemyKind.Dragon, count = 1, announce = true, announcement = "ドラゴン出現！" }
                };
            }

            if (currentStage == 3)
            {
                return new[]
                {
                    new TimedEnemySpawn { timeSeconds = 30f, enemyKind = EnemyKind.EliteSkeleton, count = 1, announce = true, announcement = "エリートスケルトン出現！" },
                    new TimedEnemySpawn { timeSeconds = 90f, enemyKind = EnemyKind.EliteSkeletonKnight, count = 1, announce = true, announcement = "エリートスケルトンナイト出現！" },
                    new TimedEnemySpawn { timeSeconds = 120f, enemyKind = EnemyKind.Lich, count = 1, announce = true, announcement = "リッチ出現！" }
                };
            }

            if (currentStage == 2)
            {
                return new[]
                {
                    new TimedEnemySpawn { timeSeconds = 30f, enemyKind = EnemyKind.EliteGoblin, count = 1, announce = true, announcement = "エリートゴブリン出現！" },
                    new TimedEnemySpawn { timeSeconds = 90f, enemyKind = EnemyKind.EliteOgre, count = 1, announce = true, announcement = "エリートオーガ出現！" },
                    new TimedEnemySpawn { timeSeconds = 120f, enemyKind = EnemyKind.GoblinLord, count = 1, announce = true, announcement = "ゴブリンロード出現！" }
                };
            }

            return new[]
            {
                new TimedEnemySpawn { timeSeconds = 30f, enemyKind = EnemyKind.EliteBoar, count = 1, announce = true, announcement = "エリートイノシシ出現！" },
                new TimedEnemySpawn { timeSeconds = 90f, enemyKind = EnemyKind.EliteOrc, count = 1, announce = true, announcement = "エリートオーク出現！" },
                new TimedEnemySpawn { timeSeconds = 120f, enemyKind = EnemyKind.OrcKing, count = 1, announce = true, announcement = "オークキング出現！" }
            };
        }

        static string BossAnnouncementForStage(int stage)
        {
            if (stage == 4) return "ドラゴン出現！";
            if (stage == 3) return "リッチ出現！";
            return stage == 2 ? "ゴブリンロード出現！" : "オークキング出現！";
        }
    }
}
