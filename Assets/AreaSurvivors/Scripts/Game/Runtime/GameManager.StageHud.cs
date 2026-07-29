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
    {        void UpdateHud()
        {
            if (timerText != null)
            {
                var span = TimeSpan.FromSeconds(hudElapsed);
                timerText.text = $"{span.Minutes:00}:{span.Seconds:00}";
                if (!bossActive) timerText.color = Color.white;
            }
            if (killText != null) killText.text = kills.ToString();
            if (levelText != null) levelText.text = $"Lv {level}";
            if (xpBar != null) xpBar.value = xpToNext <= 0 ? 0f : (float)xp / xpToNext;
            gameHud?.SetStage(currentStage);
        }

        void BeginStage(int stage)
        {
            BeginStage(stage, 0f);
        }

        void BeginStage(int stage, float startStageElapsedSeconds)
        {
            BeginStage(stage, startStageElapsedSeconds, false);
        }

        void BeginStage(int stage, float startStageElapsedSeconds, bool preserveRunElapsed)
        {
            currentStage = Mathf.Max(1, stage);
            float displayElapsedOffset = preserveRunElapsed ? elapsed : StageStartDisplaySeconds();
            elapsed = displayElapsedOffset + Mathf.Max(0f, startStageElapsedSeconds);
            hudElapsed = elapsed;
            tokenRuntime.SetElapsedTokenRewardSchedule(elapsed);
            bossActive = false;
            RecordStageReached(currentStage, runReachedStages.Count == 0);
            AudioManager.PlayBgm(BgmTrack.GameNormal);
            if (timerText != null) timerText.color = Color.white;
            if (spawner != null)
            {
                spawner.useUpperChunkSpawn = false;
                spawner.BeginStage(
                    config,
                    grid,
                    Tower.EnemyTarget,
                    currentStage,
                    displayElapsedOffset,
                    startStageElapsedSeconds,
                    Player != null ? Player.transform : null);
            }
            gameHud?.SetStage(currentStage);
            RecordDifficultyCheckpoint("stage_start");
        }

        void RecordStageReached(int stage, bool startStage)
        {
            runReachedStages.Add(new RunStageLogEntry
            {
                stage = Mathf.Max(1, stage),
                difficulty = ProgressionStore.GetStageDifficulty(stage),
                reachedSeconds = elapsed,
                reachedTime = FormatRunTime(elapsed),
                startStage = startStage
            });
        }

        void RecordBossClear(EnemyController boss, bool firstClear, bool unlockedNextStage, int unlockedStage)
        {
            var entry = new RunBossClearLogEntry
            {
                stage = currentStage,
                difficulty = ProgressionStore.GetStageDifficulty(currentStage),
                bossName = !string.IsNullOrWhiteSpace(boss != null ? boss.displayName : null) ? boss.displayName : "Boss",
                enemyKind = boss != null ? boss.enemyKind.ToString() : string.Empty,
                firstClear = firstClear,
                unlockedNextStage = unlockedNextStage,
                unlockedStage = unlockedStage,
                clearedSeconds = elapsed,
                clearedTime = FormatRunTime(elapsed),
                kills = kills,
                level = level,
                runTokens = RunTokens
            };
            runDifficultyTelemetry.ApplyBossClearMetrics(entry);
            runBossClears.Add(entry);
        }

        void RecordDifficultyCheckpoint(string eventType)
        {
            runDifficultyTelemetry.RecordCheckpoint(
                eventType,
                currentStage,
                ProgressionStore.GetStageDifficulty(currentStage),
                elapsed,
                level,
                xp,
                xpToNext,
                kills);
        }

        static string FormatRunTime(float seconds)
        {
            return TimeSpan.FromSeconds(Mathf.Max(0f, seconds)).ToString(@"mm\:ss");
        }

        float StageStartDisplaySeconds()
        {
            return Mathf.Max(0, currentStage - 1) * config.bossTimeSeconds;
        }

    }
}
