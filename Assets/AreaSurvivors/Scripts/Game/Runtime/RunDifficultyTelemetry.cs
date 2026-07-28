using System;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    sealed class RunDifficultyTelemetry
    {
        sealed class BuildingTracker
        {
            public string type;
            public Health health;
            public int damageTaken;
            public int destroyedCount;
        }

        readonly List<RunLevelLogEntry> levelUps = new List<RunLevelLogEntry>();
        readonly List<RunUpgradeLogEntry> upgradeHistory = new List<RunUpgradeLogEntry>();
        readonly List<RunDifficultyCheckpoint> checkpoints = new List<RunDifficultyCheckpoint>();
        readonly Dictionary<string, RunEnemyKindLogEntry> enemyStats =
            new Dictionary<string, RunEnemyKindLogEntry>();
        readonly Dictionary<Health, BuildingTracker> buildings =
            new Dictionary<Health, BuildingTracker>();

        Health playerHealth;
        int baseExperienceCollected;
        int appliedExperienceGained;
        int playerDamageTaken;
        int playerHitCount;
        int playerHealingReceived;
        int playerDeaths;
        int playerMinimumHp;
        int buildingRevivedCount;
        int totalEnemySpawned;
        int totalEnemyKilled;
        int peakAliveEnemies;
        string currentBossName = string.Empty;
        int currentBossMaxHp;
        float currentBossStartedAt = -1f;
        float currentBossFightSeconds;
        int bossPlayerDamageBaseline;
        int bossBuildingDamageBaseline;
        bool bossTimingActive;

        public int BaseExperienceCollected => baseExperienceCollected;
        public int AppliedExperienceGained => appliedExperienceGained;
        public int PlayerDamageTaken => playerDamageTaken;
        public int PlayerHitCount => playerHitCount;
        public int PlayerHealingReceived => playerHealingReceived;
        public int PlayerDeaths => playerDeaths;
        public int PlayerMinimumHp => playerMinimumHp;
        public int BuildingRevivedCount => buildingRevivedCount;
        public int TotalEnemySpawned => totalEnemySpawned;
        public int TotalEnemyKilled => totalEnemyKilled;
        public int PeakAliveEnemies => peakAliveEnemies;
        public int BuildingDamageTaken
        {
            get
            {
                int total = 0;
                foreach (var pair in buildings) total += pair.Value.damageTaken;
                return total;
            }
        }
        public int BuildingDestroyedCount
        {
            get
            {
                int total = 0;
                foreach (var pair in buildings) total += pair.Value.destroyedCount;
                return total;
            }
        }

        public void Reset()
        {
            Dispose();
            levelUps.Clear();
            upgradeHistory.Clear();
            checkpoints.Clear();
            enemyStats.Clear();
            buildings.Clear();
            baseExperienceCollected = 0;
            appliedExperienceGained = 0;
            playerDamageTaken = 0;
            playerHitCount = 0;
            playerHealingReceived = 0;
            playerDeaths = 0;
            playerMinimumHp = 0;
            buildingRevivedCount = 0;
            totalEnemySpawned = 0;
            totalEnemyKilled = 0;
            peakAliveEnemies = 0;
            currentBossName = string.Empty;
            currentBossMaxHp = 0;
            currentBossStartedAt = -1f;
            currentBossFightSeconds = 0f;
            bossPlayerDamageBaseline = 0;
            bossBuildingDamageBaseline = 0;
            bossTimingActive = false;
        }

        public void Bind(PlayerController player)
        {
            playerHealth = player != null ? player.GetComponent<Health>() : null;
            if (playerHealth != null)
            {
                playerMinimumHp = playerHealth.currentHp;
                playerHealth.Damaged += OnPlayerDamaged;
                playerHealth.Healed += OnPlayerHealed;
                playerHealth.Died += OnPlayerDied;
            }

            var healthComponents = UnityEngine.Object.FindObjectsOfType<Health>(true);
            for (int i = 0; i < healthComponents.Length; i++)
            {
                var health = healthComponents[i];
                if (health == null || health == playerHealth) continue;
                string buildingType = BuildingType(health);
                if (string.IsNullOrEmpty(buildingType)) continue;

                var tracker = new BuildingTracker
                {
                    type = buildingType,
                    health = health
                };
                buildings.Add(health, tracker);
                health.Damaged += OnBuildingDamaged;
                health.Died += OnBuildingDied;
            }
        }

        public void Dispose()
        {
            if (playerHealth != null)
            {
                playerHealth.Damaged -= OnPlayerDamaged;
                playerHealth.Healed -= OnPlayerHealed;
                playerHealth.Died -= OnPlayerDied;
                playerHealth = null;
            }

            foreach (var pair in buildings)
            {
                var health = pair.Key;
                if (health == null) continue;
                health.Damaged -= OnBuildingDamaged;
                health.Died -= OnBuildingDied;
            }
        }

        public void RecordExperience(int baseAmount, int appliedAmount)
        {
            baseExperienceCollected += Mathf.Max(0, baseAmount);
            appliedExperienceGained += Mathf.Max(0, appliedAmount);
        }

        public void RecordLevelUp(
            int level,
            int stage,
            float elapsedSeconds,
            int kills,
            int currentXp,
            int xpToNext,
            float xpMultiplier,
            string source)
        {
            levelUps.Add(new RunLevelLogEntry
            {
                level = level,
                stage = stage,
                reachedSeconds = elapsedSeconds,
                reachedTime = FormatTime(elapsedSeconds),
                kills = kills,
                currentXp = currentXp,
                xpToNext = xpToNext,
                baseExperienceCollected = baseExperienceCollected,
                appliedExperienceGained = appliedExperienceGained,
                xpMultiplier = xpMultiplier,
                source = source
            });
        }

        public void RecordUpgrade(int level, int stage, float elapsedSeconds, string label)
        {
            upgradeHistory.Add(new RunUpgradeLogEntry
            {
                level = level,
                stage = stage,
                acquiredSeconds = elapsedSeconds,
                acquiredTime = FormatTime(elapsedSeconds),
                label = label
            });
        }

        public void RecordEnemySpawn(EnemyController enemy, int stage)
        {
            if (enemy == null) return;
            var entry = GetOrCreateEnemyEntry(enemy, stage);
            entry.spawned++;
            totalEnemySpawned++;
            UpdatePeakAliveEnemies(EnemyController.ActiveEnemyCount);
        }

        public void RecordEnemyKill(EnemyController enemy, int stage)
        {
            if (enemy == null) return;
            var entry = GetOrCreateEnemyEntry(enemy, stage);
            entry.killed++;
            totalEnemyKilled++;
        }

        public void UpdatePeakAliveEnemies(int aliveEnemies)
        {
            peakAliveEnemies = Mathf.Max(peakAliveEnemies, Mathf.Max(0, aliveEnemies));
        }

        public void RecordBuildingRevives(int count)
        {
            buildingRevivedCount += Mathf.Max(0, count);
        }

        public void RecordBossSpawn(EnemyController boss)
        {
            if (boss == null) return;
            currentBossName = string.IsNullOrWhiteSpace(boss.displayName) ? boss.enemyKind.ToString() : boss.displayName;
            var health = boss.GetComponent<Health>();
            currentBossMaxHp = health != null ? health.maxHp : 0;
            currentBossStartedAt = Time.time;
            currentBossFightSeconds = 0f;
            bossPlayerDamageBaseline = playerDamageTaken;
            bossBuildingDamageBaseline = BuildingDamageTaken;
            bossTimingActive = true;
        }

        public void ApplyBossClearMetrics(RunBossClearLogEntry entry)
        {
            if (entry == null) return;
            currentBossFightSeconds = CurrentBossFightSeconds();
            bossTimingActive = false;
            entry.bossMaxHp = currentBossMaxHp;
            entry.bossFightSeconds = currentBossFightSeconds;
            entry.playerDamageTakenDuringFight = Mathf.Max(0, playerDamageTaken - bossPlayerDamageBaseline);
            entry.buildingDamageTakenDuringFight = Mathf.Max(0, BuildingDamageTaken - bossBuildingDamageBaseline);
        }

        public void RecordCheckpoint(
            string eventType,
            int stage,
            int difficulty,
            float elapsedSeconds,
            int level,
            int currentXp,
            int xpToNext,
            int kills)
        {
            bool includeBoss = !string.Equals(eventType, "stage_start", StringComparison.Ordinal);
            checkpoints.Add(new RunDifficultyCheckpoint
            {
                eventType = eventType,
                stage = stage,
                difficulty = difficulty,
                elapsedSeconds = elapsedSeconds,
                elapsedTime = FormatTime(elapsedSeconds),
                level = level,
                currentXp = currentXp,
                xpToNext = xpToNext,
                kills = kills,
                baseExperienceCollected = baseExperienceCollected,
                appliedExperienceGained = appliedExperienceGained,
                playerCurrentHp = playerHealth != null ? playerHealth.currentHp : 0,
                playerDamageTaken = playerDamageTaken,
                buildingDamageTaken = BuildingDamageTaken,
                buildingDestroyedCount = BuildingDestroyedCount,
                enemySpawned = totalEnemySpawned,
                enemyKilled = totalEnemyKilled,
                peakAliveEnemies = peakAliveEnemies,
                bossName = includeBoss ? currentBossName : string.Empty,
                bossMaxHp = includeBoss ? currentBossMaxHp : 0,
                bossFightSeconds = includeBoss ? CurrentBossFightSeconds() : 0f
            });
        }

        public List<RunLevelLogEntry> BuildLevelUps()
        {
            return new List<RunLevelLogEntry>(levelUps);
        }

        public List<RunUpgradeLogEntry> BuildUpgradeHistory()
        {
            return new List<RunUpgradeLogEntry>(upgradeHistory);
        }

        public List<RunDifficultyCheckpoint> BuildCheckpoints()
        {
            return new List<RunDifficultyCheckpoint>(checkpoints);
        }

        public List<RunEnemyKindLogEntry> BuildEnemyStats()
        {
            var result = new List<RunEnemyKindLogEntry>(enemyStats.Count);
            foreach (var pair in enemyStats)
            {
                var source = pair.Value;
                result.Add(new RunEnemyKindLogEntry
                {
                    stage = source.stage,
                    difficulty = source.difficulty,
                    enemyKind = source.enemyKind,
                    displayName = source.displayName,
                    elite = source.elite,
                    boss = source.boss,
                    maxHp = source.maxHp,
                    attackDamage = source.attackDamage,
                    xpValue = source.xpValue,
                    spawned = source.spawned,
                    killed = source.killed
                });
            }
            result.Sort((left, right) =>
            {
                int stageCompare = left.stage.CompareTo(right.stage);
                return stageCompare != 0
                    ? stageCompare
                    : string.CompareOrdinal(left.enemyKind, right.enemyKind);
            });
            return result;
        }

        public List<RunBuildingLogEntry> BuildBuildingStats()
        {
            var grouped = new Dictionary<string, RunBuildingLogEntry>();
            foreach (var pair in buildings)
            {
                var tracker = pair.Value;
                if (!grouped.TryGetValue(tracker.type, out var entry))
                {
                    entry = new RunBuildingLogEntry { buildingType = tracker.type };
                    grouped.Add(tracker.type, entry);
                }

                entry.instanceCount++;
                entry.damageTaken += tracker.damageTaken;
                entry.destroyedCount += tracker.destroyedCount;
                var health = tracker.health;
                if (health == null) continue;
                entry.totalMaxHp += health.maxHp;
                entry.totalCurrentHp += Mathf.Max(0, health.currentHp);
                var revival = health.GetComponent<BuildingRevivalState>();
                if (health.IsDead || (revival != null && revival.IsDestroyed)) entry.currentDestroyedCount++;
            }

            var result = new List<RunBuildingLogEntry>(grouped.Values);
            result.Sort((left, right) => string.CompareOrdinal(left.buildingType, right.buildingType));
            return result;
        }

        RunEnemyKindLogEntry GetOrCreateEnemyEntry(EnemyController enemy, int stage)
        {
            int safeStage = Mathf.Max(1, stage);
            string key = safeStage + ":" + enemy.enemyKind;
            if (enemyStats.TryGetValue(key, out var entry)) return entry;

            var health = enemy.GetComponent<Health>();
            entry = new RunEnemyKindLogEntry
            {
                stage = safeStage,
                difficulty = ProgressionStore.GetStageDifficulty(safeStage),
                enemyKind = enemy.enemyKind.ToString(),
                displayName = enemy.displayName,
                elite = enemy.elite,
                boss = enemy.boss,
                maxHp = health != null ? health.maxHp : 0,
                attackDamage = enemy.attackDamage,
                xpValue = enemy.xpValue
            };
            enemyStats.Add(key, entry);
            return entry;
        }

        void OnPlayerDamaged(Health health, int amount)
        {
            int dealt = health != null ? Mathf.Max(0, health.LastDamageDealt) : Mathf.Max(0, amount);
            if (dealt <= 0) return;
            playerDamageTaken += dealt;
            playerHitCount++;
            playerMinimumHp = Mathf.Min(playerMinimumHp, health.currentHp);
        }

        void OnPlayerHealed(Health health, int amount)
        {
            playerHealingReceived += Mathf.Max(0, amount);
        }

        void OnPlayerDied(Health health)
        {
            playerDeaths++;
            playerMinimumHp = 0;
        }

        void OnBuildingDamaged(Health health, int amount)
        {
            if (health == null || !buildings.TryGetValue(health, out var tracker)) return;
            tracker.damageTaken += Mathf.Max(0, health.LastDamageDealt);
        }

        void OnBuildingDied(Health health)
        {
            if (health == null || !buildings.TryGetValue(health, out var tracker)) return;
            tracker.destroyedCount++;
        }

        float CurrentBossFightSeconds()
        {
            if (!bossTimingActive || currentBossStartedAt < 0f) return currentBossFightSeconds;
            return Mathf.Max(0f, Time.time - currentBossStartedAt);
        }

        static string BuildingType(Health health)
        {
            if (health.GetComponentInParent<TowerController>() != null) return "CenterTower";
            if (health.GetComponentInParent<BallistaTower>() != null) return "Ballista";
            if (health.GetComponentInParent<WoodenBarrier>() != null) return "WoodenBarrier";
            if (health.GetComponentInParent<WatchTower>() != null) return "WatchTower";
            return string.Empty;
        }

        static string FormatTime(float seconds)
        {
            return TimeSpan.FromSeconds(Mathf.Max(0f, seconds)).ToString(@"mm\:ss");
        }
    }
}
