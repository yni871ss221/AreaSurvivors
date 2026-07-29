using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed partial class GameManager
    {        void EndRun(bool clear)
        {
            EndRun(clear, clear ? currentStage : 0, 0, string.Empty);
        }

        void EndRun(bool clear, int clearedStage, int unlockedStage, string clearMessage)
        {
            if (!clear && gameEnding) return;
            gameEnding = true;
            Time.timeScale = 1f;
            StopGameplayActionAudio();
            int guaranteedTokens;
            int tokenBaseBeforeMultiplier;
            float endTokenMultiplier;
            int tokenEarned = EndTokenReward(out guaranteedTokens, out tokenBaseBeforeMultiplier, out endTokenMultiplier);
            int tokenBalanceBeforeEndReward = ProgressionStore.Data.tokens;
            RunResult.Last = new RunResult
            {
                kills = kills,
                damageDealt = damageDealt,
                level = level,
                tokensEarned = tokenEarned,
                reachedStage = currentStage,
                survivedSeconds = elapsed,
                gameClear = clear,
                clearedStage = clearedStage,
                unlockedStage = unlockedStage,
                allStagesDifficultyFiveCleared = clear && ProgressionStore.AreAllStagesClearedAtMaxDifficulty(),
                clearMessage = clearMessage,
                upgrades = new List<string>(runUpgrades),
                acquiredRelics = new List<string>(runRelics),
                acquiredRelicEntries = new List<RunRelicReportEntry>(runRelicEntries),
                damageReport = runDamageTracker.BuildReport()
            };
            ProgressionStore.AddRunTokens(kills, tokenEarned);
            int tokenBalanceAfterEndReward = ProgressionStore.Data.tokens;
            WriteTokenRunLog(
                clear,
                clearedStage,
                unlockedStage,
                tokenEarned,
                guaranteedTokens,
                tokenBaseBeforeMultiplier,
                endTokenMultiplier,
                tokenBalanceBeforeEndReward,
                tokenBalanceAfterEndReward);
            runtimeResourceDiagnostics.LogSnapshot(SceneNames.GameEnd);
            SceneManager.LoadScene(SceneNames.GameEnd);
        }

        void StopGameplayActionAudio()
        {
            var weapon = Player != null ? Player.weapon : null;
            if (weapon == null && Player != null) weapon = Player.GetComponentInChildren<WeaponController>();
            if (weapon != null) weapon.StopRuntimeWeapons();
            AudioManager.StopSfx();
        }

        void FreezeGameplayForEndingCutscene(Transform focusTarget, EnemyController visibleBoss)
        {
            Time.timeScale = 0f;
            StopGameplayActionAudio();
            spawner?.StopAndClearEnemies(visibleBoss);
        }

        public IEnumerator WaitForEndingCutsceneCamera(Transform focusTarget)
        {
            var cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null && focusTarget != null)
            {
                const float moveSeconds = 3.8f;
                yield return cameraFollow.MoveToCutsceneTarget(focusTarget, moveSeconds);
            }
        }

        int EndTokenReward()
        {
            return EndTokenReward(out _, out _, out _);
        }

        int EndTokenReward(out int guaranteedTokens, out int baseTokens, out float multiplier)
        {
            guaranteedTokens = config.roundEndTokenReward
                + ProgressionStore.GetLevel(UpgradeType.EndTokenGain) * config.roundEndTokenRewardPerUpgradeLevel;
            baseTokens = Mathf.Max(0, RunTokens) + Mathf.Max(0, guaranteedTokens);
            multiplier = RelicEffects.EndTokenMultiplier;
            return Mathf.Max(0, Mathf.RoundToInt(baseTokens * multiplier));
        }

        void WriteTokenRunLog(
            bool clear,
            int clearedStage,
            int unlockedStage,
            int tokenEarned,
            int guaranteedTokens,
            int tokenBaseBeforeMultiplier,
            float endTokenMultiplier,
            int tokenBalanceBeforeEndReward,
            int tokenBalanceAfterEndReward)
        {
            RecordDifficultyCheckpoint("run_end");
            var localNow = DateTime.Now;
            var utcNow = DateTime.UtcNow;
            var playerHealth = Player != null ? Player.GetComponent<Health>() : null;
            var playerStats = Player != null ? Player.Stats : default;
            var entry = new TokenRunLogEntry
            {
                sessionId = string.IsNullOrEmpty(runSessionId) ? Guid.NewGuid().ToString("N") : runSessionId,
                timestampLocal = localNow.ToString("yyyy-MM-dd HH:mm:ss"),
                timestampUtc = utcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                appVersion = Application.version,
                unityVersion = Application.unityVersion,
                gameClear = clear,
                startStage = runStartStage,
                startStageDifficulty = runStartStageDifficulty,
                reachedStage = currentStage,
                reachedStageDifficulty = ProgressionStore.GetStageDifficulty(currentStage),
                clearedStage = clearedStage,
                unlockedStage = unlockedStage,
                survivedSeconds = elapsed,
                survivedTime = FormatRunTime(elapsed),
                level = level,
                kills = kills,
                damageDealt = damageDealt,
                runTokensBeforeEndReward = RunTokens,
                killMilestoneTokens = tokenRuntime.KillMilestoneTokens,
                elapsedTimeTokens = tokenRuntime.ElapsedTimeTokens,
                tokenOrbTokens = tokenRuntime.TokenOrbTokens,
                paintAreaTokens = tokenRuntime.PaintAreaTokens,
                relicDuplicateTokens = tokenRuntime.RelicDuplicateTokens,
                guaranteedEndTokens = guaranteedTokens,
                endTokenGainLevel = ProgressionStore.GetLevel(UpgradeType.EndTokenGain),
                endTokenMultiplier = endTokenMultiplier,
                endTokenBaseBeforeMultiplier = tokenBaseBeforeMultiplier,
                finalEndRewardTokens = tokenEarned,
                tokenBalanceAtRunStart = tokenRuntime.TokenBalanceAtRunStart,
                tokenBalanceBeforeEndReward = tokenBalanceBeforeEndReward,
                tokenBalanceAfterEndReward = tokenBalanceAfterEndReward,
                totalTokenBalanceIncrease = tokenBalanceAfterEndReward - tokenRuntime.TokenBalanceAtRunStart,
                killTokenDivisor = config != null ? Mathf.Max(1, config.tokenKillsDivisor) : 1,
                killTokenRemainder = tokenRuntime.KillTokenProgress,
                elapsedTokenIntervalSeconds = TokenRuntimeService.ElapsedTokenRewardIntervalSeconds,
                nextElapsedTokenRewardSeconds = tokenRuntime.NextElapsedTokenRewardSeconds,
                paintAreaTokenThreshold = TokenRuntimeService.PaintAreaTokenThreshold,
                paintAreaTokenRemainder = tokenRuntime.PaintAreaTokenProgress,
                reachedStageSummary = StageLogSummary(),
                bossClearSummary = BossClearLogSummary(),
                reachedStages = new List<RunStageLogEntry>(runReachedStages),
                bossClears = new List<RunBossClearLogEntry>(runBossClears),
                upgrades = new List<string>(runUpgrades),
                acquiredRelics = new List<string>(runRelics),
                currentXp = xp,
                xpToNext = xpToNext,
                baseExperienceCollected = runDifficultyTelemetry.BaseExperienceCollected,
                appliedExperienceGained = runDifficultyTelemetry.AppliedExperienceGained,
                currentXpMultiplier = playerStats.xpGainMultiplier,
                playerCurrentHp = playerHealth != null ? playerHealth.currentHp : 0,
                playerMaxHp = playerHealth != null ? playerHealth.maxHp : 0,
                playerMinimumHp = runDifficultyTelemetry.PlayerMinimumHp,
                playerDamageTaken = runDifficultyTelemetry.PlayerDamageTaken,
                playerHitCount = runDifficultyTelemetry.PlayerHitCount,
                playerHealingReceived = runDifficultyTelemetry.PlayerHealingReceived,
                playerDeaths = runDifficultyTelemetry.PlayerDeaths,
                playerDefense = playerStats.defense,
                playerMoveSpeed = playerStats.moveSpeed,
                playerPaintRadius = playerStats.paintRadius,
                playerAutoRegen = playerStats.autoRegen,
                buildingDamageTaken = runDifficultyTelemetry.BuildingDamageTaken,
                buildingDestroyedCount = runDifficultyTelemetry.BuildingDestroyedCount,
                buildingRevivedCount = runDifficultyTelemetry.BuildingRevivedCount,
                enemySpawned = runDifficultyTelemetry.TotalEnemySpawned,
                enemyKilled = runDifficultyTelemetry.TotalEnemyKilled,
                peakAliveEnemies = runDifficultyTelemetry.PeakAliveEnemies,
                levelUps = runDifficultyTelemetry.BuildLevelUps(),
                upgradeHistory = runDifficultyTelemetry.BuildUpgradeHistory(),
                enemyStats = runDifficultyTelemetry.BuildEnemyStats(),
                buildingStats = runDifficultyTelemetry.BuildBuildingStats(),
                difficultyCheckpoints = runDifficultyTelemetry.BuildCheckpoints(),
                damageReport = runDamageTracker.BuildReport()
            };

            TokenRunLogger.Append(entry);
        }

        string StageLogSummary()
        {
            if (runReachedStages.Count == 0) return string.Empty;
            var parts = new List<string>();
            for (int i = 0; i < runReachedStages.Count; i++)
            {
                var entry = runReachedStages[i];
                parts.Add($"STAGE {entry.stage}(難易度{entry.difficulty}, {entry.reachedTime})");
            }

            return string.Join(" -> ", parts);
        }

        string BossClearLogSummary()
        {
            if (runBossClears.Count == 0) return string.Empty;
            var parts = new List<string>();
            for (int i = 0; i < runBossClears.Count; i++)
            {
                var entry = runBossClears[i];
                string unlock = entry.unlockedNextStage ? $", unlock:{entry.unlockedStage}" : string.Empty;
                parts.Add($"STAGE {entry.stage} {entry.bossName} {entry.clearedTime} first:{entry.firstClear}{unlock}");
            }

            return string.Join(" | ", parts);
        }

    }
}
