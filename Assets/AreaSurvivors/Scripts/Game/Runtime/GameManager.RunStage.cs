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
    {        public void GameOver()
        {
            EndRun(false);
        }

        public void BeginTowerCollapseCutscene(TowerController tower)
        {
            if (endingCutsceneActive || gameEnding) return;
            endingCutsceneActive = true;
            FreezeGameplayForEndingCutscene(tower != null ? tower.EnemyTarget : null, null);
        }

        public void BossSpawned(EnemyController boss)
        {
            if (boss == null) return;
            runDifficultyTelemetry.RecordBossSpawn(boss);
            bossActive = true;
            AudioManager.PlayBgm(BgmTrack.GameBoss);
            if (timerText != null) timerText.color = Color.red;
            gameHud?.ShowBoss(boss);
            ShowAnnouncement(spawner != null ? spawner.CurrentBossAnnouncement : config.bossAnnouncement);
            RecordDifficultyCheckpoint("boss_spawn");
        }

        public void BossDefeated(EnemyController boss)
        {
            if (gameEnding) return;
            bool firstClear = IsFirstBossDefeatForCurrentStage(boss);
            if (ShouldSpawnBossRelicChest(firstClear, currentStage)) DropBossRelicChest(boss);
            StartCoroutine(BossDefeatedRoutine(boss));
        }

        public static bool ShouldSpawnBossRelicChest(bool firstClear, int stage)
        {
            return !firstClear && stage >= 1 && stage < 4;
        }

        public static bool ShouldGrantRelicBeforeGameClear(bool firstClear, int stage)
        {
            return !firstClear && stage >= 4;
        }

        public bool IsFirstBossDefeatForCurrentStage(EnemyController boss)
        {
            return boss != null && boss.boss && !ProgressionStore.IsStageCleared(currentStage);
        }

        public void BeginBossDefeatCutscene(EnemyController boss)
        {
            if (boss == null || !IsFirstBossDefeatForCurrentStage(boss) || endingCutsceneActive || gameEnding) return;
            endingCutsceneActive = true;
            FreezeGameplayForEndingCutscene(boss.transform, boss);
        }

        void DropBossRelicChest(EnemyController boss)
        {
            if (boss == null) return;
            SpawnRelicChest(boss.transform.position);
        }

        IEnumerator BossDefeatedRoutine(EnemyController boss)
        {
            bool firstClear = IsFirstBossDefeatForCurrentStage(boss);
            int defeatedDifficulty = ProgressionStore.GetStageDifficulty(currentStage);
            int bossTokenReward = boss != null ? Mathf.Max(0, boss.tokenValue) : 0;
            bool unlockedNextStage = ProgressionStore.MarkStageCleared(currentStage, defeatedDifficulty);
            UnlockNextDifficultyForBossClear(currentStage, defeatedDifficulty);
            RecordBossClear(boss, firstClear, unlockedNextStage, unlockedNextStage ? currentStage + 1 : 0);
            ReviveBuildingsOnBossDefeat();
            RecordDifficultyCheckpoint("boss_clear");
            if (firstClear)
            {
                yield return FirstBossClearRewardRoutine(bossTokenReward);
                yield return FirstBossDefeatEndRoutine(currentStage, unlockedNextStage ? currentStage + 1 : 0);
                yield break;
            }

            if (currentStage < 4)
            {
                int nextStage = unlockedNextStage ? currentStage + 1 : Mathf.Min(currentStage + 1, 4);
                yield return StageTransitionRoutine(boss, nextStage);
            }
            else
            {
                yield return GameClearRoutine(
                    boss,
                    currentStage,
                    unlockedNextStage ? currentStage + 1 : 0,
                    string.Empty,
                    ShouldGrantRelicBeforeGameClear(firstClear, currentStage));
            }
        }

        static bool UnlockNextDifficultyForBossClear(int stage, int defeatedDifficulty)
        {
            int nextDifficulty = Mathf.Clamp(defeatedDifficulty + 1, ProgressionStore.MinStageDifficulty, ProgressionStore.MaxStageDifficulty);
            if (nextDifficulty <= defeatedDifficulty) return false;
            return ProgressionStore.UnlockStageDifficulty(stage, nextDifficulty);
        }

        void ReviveBuildingsOnBossDefeat()
        {
            if (!ProgressionStore.IsUnlocked(UpgradeType.ReviveBuildingsOnBossDefeat)) return;
            int revived = BuildingRevivalState.ReviveDestroyedBuildings(grid, 0.5f);
            runDifficultyTelemetry.RecordBuildingRevives(revived);
        }

        IEnumerator GameClearRoutine(
            EnemyController boss,
            int clearedStage,
            int unlockedStage,
            string clearMessage,
            bool grantRelicBeforeEnd)
        {
            gameEnding = true;
            StopGameplayActionAudio();
            spawner?.StopAndClearEnemies(boss);
            ShowAnnouncement("GAME CLEAR");
            if (grantRelicBeforeEnd)
            {
                yield return AcquireRelicRewardRoutine();
            }
            yield return new WaitForSeconds(1.8f);
            EndRun(true, clearedStage, unlockedStage, clearMessage);
        }

        IEnumerator FirstBossDefeatEndRoutine(int clearedStage, int unlockedStage)
        {
            gameEnding = true;
            ShowAnnouncement("STAGE CLEAR");
            yield return new WaitForSecondsRealtime(0.45f);
            EndRun(true, clearedStage, unlockedStage, string.Empty);
        }

        IEnumerator FirstBossClearRewardRoutine(int bossTokenReward)
        {
            if (bossTokenReward > 0)
            {
                AddRunTokens(bossTokenReward);
                yield return new WaitForSecondsRealtime(0.35f);
            }

            yield return AcquireRelicRewardRoutine();
            if (endingCutsceneActive && !gameEnding) Time.timeScale = 0f;
        }

        IEnumerator AcquireRelicRewardRoutine()
        {
            AudioManager.PlaySfx(SfxTrack.RelicChestPickup);
            if (!RelicCatalog.TryPickRandom(out var definition))
            {
                ShowAnnouncement(LocalizationService.Text("レリックが見つかりません", "No relic found"));
                yield break;
            }

            if (!RelicCatalog.TryAcquireReward(
                    definition,
                    out bool newlyUnlocked,
                    out int duplicateTokenReward))
            {
                ShowAnnouncement(LocalizationService.Text("レリックが見つかりません", "No relic found"));
                yield break;
            }

            if (newlyUnlocked)
            {
                Player?.StatsSource?.Refresh();
                Player?.ApplyCurrentStats(false);
            }
            bool closed = false;
            ShowRelicAcquisition(
                definition,
                duplicateTokenReward,
                () => closed = true);
            while (!closed) yield return null;
        }

        IEnumerator StageTransitionRoutine(EnemyController boss, int nextStage)
        {
            stageTransitionActive = true;
            gameEnding = true;
            spawner?.StopSpawning();
            yield return DefeatRemainingEnemiesForStageTransition(boss);
            yield return AttractRemainingStageRewards();
            ShowAnnouncement("ROUND " + nextStage);
            yield return new WaitForSeconds(1.2f);
            if (boss != null) Destroy(boss.gameObject);
            gameEnding = false;
            stageTransitionActive = false;
            BeginStage(nextStage, 0f, true);
        }

        IEnumerator DefeatRemainingEnemiesForStageTransition(EnemyController boss)
        {
            var remainingEnemies = new List<EnemyController>();
            foreach (var enemy in EnemyController.ActiveEnemies)
            {
                if (enemy == null || enemy == boss || enemy.boss) continue;
                enemy.SetActionLocked(true, enemy.FacingDirection);
                remainingEnemies.Add(enemy);
            }

            if (screenFade != null)
            {
                yield return screenFade.FlashWhite(
                    config != null ? config.stageTransitionFlashPeakAlpha : 0.92f,
                    config != null ? config.stageTransitionFlashInSeconds : 0.05f,
                    config != null ? config.stageTransitionFlashHoldSeconds : 0.06f,
                    config != null ? config.stageTransitionFlashOutSeconds : 0.2f);
            }

            float hitDelaySeconds = config != null ? config.stageTransitionEnemyHitDelaySeconds : 0.24f;
            foreach (var enemy in remainingEnemies)
            {
                if (enemy != null) enemy.BeginStageTransitionDefeat(hitDelaySeconds);
            }

            float timeoutSeconds = config != null ? config.stageTransitionEnemyDefeatTimeoutSeconds : 1.2f;
            float elapsedSeconds = 0f;
            while (elapsedSeconds < timeoutSeconds)
            {
                if (!HasRemainingStageTransitionEnemy(remainingEnemies)) yield break;
                elapsedSeconds += Time.unscaledDeltaTime;
                yield return null;
            }

            Debug.LogWarning(
                "Stage transition enemy defeat exceeded its normal timeout. " +
                "Remaining enemies will be defeated immediately so their rewards are preserved.");
            foreach (var enemy in remainingEnemies)
            {
                if (enemy != null) enemy.ForceStageTransitionDefeat();
            }

            while (HasRemainingStageTransitionEnemy(remainingEnemies))
            {
                yield return null;
            }
        }

        static bool HasRemainingStageTransitionEnemy(List<EnemyController> enemies)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null) return true;
            }
            return false;
        }

        IEnumerator AttractRemainingStageRewards()
        {
            if (Player == null) yield break;

            PickupAttractionRegistry.CopyActiveTo(stageTransitionPickups);
            float longestEstimatedTravelSeconds = 0f;

            for (int i = 0; i < stageTransitionPickups.Count; i++)
            {
                var pickup = stageTransitionPickups[i];
                if (pickup == null) continue;
                longestEstimatedTravelSeconds = Mathf.Max(
                    longestEstimatedTravelSeconds,
                    pickup.EstimateStageTransitionAttractionSeconds(Player));
                pickup.BeginStageTransitionAttraction(Player);
            }

            float timeoutSeconds =
                longestEstimatedTravelSeconds *
                StageTransitionPickupAttractionTimeoutMultiplier +
                StageTransitionPickupAttractionTimeoutPaddingSeconds;
            float elapsedSeconds = 0f;
            while (elapsedSeconds < timeoutSeconds &&
                   PickupAttractionRegistry.StageTransitionAttractionCount > 0)
            {
                elapsedSeconds += Time.unscaledDeltaTime;
                yield return null;
            }

            for (int i = 0; i < stageTransitionPickups.Count; i++)
            {
                if (stageTransitionPickups[i] != null)
                {
                    stageTransitionPickups[i].CompleteStageTransitionAttraction();
                }
            }
            stageTransitionPickups.Clear();
        }

    }
}
