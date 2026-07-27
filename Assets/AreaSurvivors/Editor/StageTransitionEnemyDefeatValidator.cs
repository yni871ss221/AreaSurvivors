using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class StageTransitionEnemyDefeatValidator
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string GameManagerSourcePath = "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.cs";
        const string EnemySpawnerSourcePath = "Assets/AreaSurvivors/Scripts/Game/Characters/EnemySpawner.cs";
        const string PlayerControllerSourcePath = "Assets/AreaSurvivors/Scripts/Game/Characters/PlayerController.cs";
        const string AttractablePickupSourcePath = "Assets/AreaSurvivors/Scripts/Game/Pickups/AttractablePickup.cs";
        const string ExperienceOrbSourcePath = "Assets/AreaSurvivors/Scripts/Game/Pickups/ExperienceOrb.cs";
        const string TokenOrbSourcePath = "Assets/AreaSurvivors/Scripts/Game/Pickups/TokenOrb.cs";
        const string SuccessMarkerPath = "TokenReports/Validation/stage-transition-enemy-defeat-validator.success";

        [MenuItem("Area Survivors/Validate/Stage Transition Enemy Defeat")]
        public static void ValidateFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Stage transition enemy defeat validation requires Edit Mode because it may open 05_Game.unity additively.");
            }

            DeleteSuccessMarker();
            Scene scene = OpenGameScene(out bool openedAdditive);
            try
            {
                var failures = new List<string>();
                GameManager manager = FindSingleInScene<GameManager>(scene, failures);
                if (manager != null) ValidateManager(manager, failures);
                ValidateBossRelicRewardPolicy(failures);
                ValidateStageTransitionRewardFlow(failures);
                ValidateStageTransitionAttractionContracts(failures);
                ValidateLevelUpQueueContract(failures);
                ValidateGameClearEnemyCleanupSafety(failures);

                if (failures.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Stage transition enemy defeat validation failed:\n- " + string.Join("\n- ", failures));
                }

                WriteSuccessMarker();
                Debug.Log(
                    "Stage transition enemy defeat validation passed. " +
                    "Screen flash, reward-preserving enemy defeat, arrival-time pickup rewards, queued multi-level choices, " +
                    "repeat Dragon relic acquisition, and mutation-safe final enemy cleanup are configured.");
            }
            finally
            {
                if (openedAdditive && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ValidateBossRelicRewardPolicy(List<string> failures)
        {
            for (int stage = 1; stage <= 3; stage++)
            {
                if (!GameManager.ShouldSpawnBossRelicChest(false, stage))
                {
                    failures.Add($"Repeat clear on Stage {stage} must continue spawning the field relic chest.");
                }
                if (GameManager.ShouldGrantRelicBeforeGameClear(false, stage))
                {
                    failures.Add($"Stage {stage} must not use the final-stage direct relic acquisition path.");
                }
            }

            if (GameManager.ShouldSpawnBossRelicChest(false, 4))
            {
                failures.Add("Repeat Dragon clear must not leave a field relic chest behind when the run ends.");
            }
            if (!GameManager.ShouldGrantRelicBeforeGameClear(false, 4))
            {
                failures.Add("Repeat Dragon clear must acquire a relic before the game-clear result ends the run.");
            }
            if (GameManager.ShouldSpawnBossRelicChest(true, 4) ||
                GameManager.ShouldGrantRelicBeforeGameClear(true, 4))
            {
                failures.Add("First Dragon clear must keep using the existing first-clear relic reward path exactly once.");
            }
        }

        static void ValidateStageTransitionRewardFlow(List<string> failures)
        {
            string source = File.ReadAllText(GameManagerSourcePath);
            int transitionIndex = source.IndexOf(
                "IEnumerator StageTransitionRoutine",
                StringComparison.Ordinal);
            int defeatIndex = source.IndexOf(
                "yield return DefeatRemainingEnemiesForStageTransition",
                transitionIndex,
                StringComparison.Ordinal);
            int attractionIndex = source.IndexOf(
                "yield return AttractRemainingStageRewards",
                transitionIndex,
                StringComparison.Ordinal);
            int announcementIndex = source.IndexOf(
                "ShowAnnouncement(\"ROUND \" + nextStage)",
                transitionIndex,
                StringComparison.Ordinal);
            if (transitionIndex < 0 ||
                defeatIndex < transitionIndex ||
                attractionIndex < defeatIndex ||
                announcementIndex < attractionIndex)
            {
                failures.Add(
                    "Stage transition order must be remaining-enemy defeat, all reward attraction, then next-round announcement.");
            }

            int defeatRoutineIndex = source.IndexOf(
                "IEnumerator DefeatRemainingEnemiesForStageTransition",
                StringComparison.Ordinal);
            int attractionRoutineIndex = source.IndexOf(
                "IEnumerator AttractRemainingStageRewards",
                defeatRoutineIndex,
                StringComparison.Ordinal);
            if (defeatRoutineIndex < 0 || attractionRoutineIndex <= defeatRoutineIndex)
            {
                failures.Add("Stage-transition enemy defeat and reward-attraction routines were not found.");
                return;
            }

            string defeatRoutine = source.Substring(
                defeatRoutineIndex,
                attractionRoutineIndex - defeatRoutineIndex);
            int flashIndex = defeatRoutine.IndexOf("screenFade.FlashWhite", StringComparison.Ordinal);
            int beginDefeatIndex = defeatRoutine.IndexOf("enemy.BeginStageTransitionDefeat", StringComparison.Ordinal);
            int forceDefeatIndex = defeatRoutine.IndexOf("enemy.ForceStageTransitionDefeat", StringComparison.Ordinal);
            if (flashIndex < 0 || beginDefeatIndex < flashIndex)
            {
                failures.Add("The white flash must finish before remaining enemies begin their defeat sequence.");
            }
            if (forceDefeatIndex < beginDefeatIndex)
            {
                failures.Add("The timeout fallback must force a reward-producing defeat instead of clearing enemies.");
            }
            if (defeatRoutine.Contains("StopAndClearEnemies"))
            {
                failures.Add("Stage-transition enemy cleanup must not discard XP or token rewards.");
            }

            int gameClearIndex = source.IndexOf("IEnumerator GameClearRoutine", StringComparison.Ordinal);
            int firstClearRewardIndex = source.IndexOf(
                "IEnumerator FirstBossDefeatEndRoutine",
                gameClearIndex,
                StringComparison.Ordinal);
            if (gameClearIndex < 0 || firstClearRewardIndex <= gameClearIndex)
            {
                failures.Add("Game-clear routine boundaries were not found.");
                return;
            }
            string gameClearRoutine = source.Substring(
                gameClearIndex,
                firstClearRewardIndex - gameClearIndex);
            int relicIndex = gameClearRoutine.IndexOf(
                "yield return AcquireRelicRewardRoutine",
                StringComparison.Ordinal);
            int endRunIndex = gameClearRoutine.IndexOf("EndRun(", StringComparison.Ordinal);
            if (relicIndex < 0 || endRunIndex < relicIndex)
            {
                failures.Add("Repeat Dragon relic acquisition must complete before EndRun.");
            }
        }

        static void ValidateGameClearEnemyCleanupSafety(List<string> failures)
        {
            string source = File.ReadAllText(EnemySpawnerSourcePath);
            int cleanupIndex = source.IndexOf(
                "public void StopAndClearEnemies",
                StringComparison.Ordinal);
            int stopSpawningIndex = source.IndexOf(
                "public void StopSpawning",
                cleanupIndex,
                StringComparison.Ordinal);
            if (cleanupIndex < 0 || stopSpawningIndex <= cleanupIndex)
            {
                failures.Add("Final enemy cleanup method boundaries were not found.");
                return;
            }

            string cleanupMethod = source.Substring(
                cleanupIndex,
                stopSpawningIndex - cleanupIndex);
            if (!cleanupMethod.Contains(
                    "new List<EnemyController>(EnemyController.ActiveEnemies)"))
            {
                failures.Add(
                    "Final enemy cleanup must snapshot the active-enemy registry before destroying enemies.");
            }
            if (cleanupMethod.Contains("foreach (var enemy in EnemyController.ActiveEnemies)"))
            {
                failures.Add(
                    "Final enemy cleanup must not enumerate the live registry while Destroy removes entries from it.");
            }
        }

        static void ValidateStageTransitionAttractionContracts(List<string> failures)
        {
            ValidatePickupAttractionSourceContract(failures);
            if (!Mathf.Approximately(
                    PickupAttractionMotion.ResolveSpeed(6f, 2.4f),
                    6f))
            {
                failures.Add(
                    "Pickup attraction must retain its configured minimum speed for normal player speeds.");
            }
            if (!Mathf.Approximately(
                    PickupAttractionRegistry.ScanIntervalSeconds,
                    0.1f))
            {
                failures.Add(
                    "Player-owned pickup proximity scans must run at the configured 0.1-second interval.");
            }

            var playerObject = new GameObject("Stage Transition Attraction Player Probe");
            var experienceObject = new GameObject("Stage Transition Experience Probe");
            var tokenObject = new GameObject("Stage Transition Token Probe");
            try
            {
                var player = playerObject.AddComponent<PlayerController>();
                var experience = experienceObject.AddComponent<ExperienceOrb>();
                experience.value = 7;
                var token = tokenObject.AddComponent<TokenOrb>();
                token.value = 5;
                PickupAttractionRegistry.RegisterForValidation(experience);
                PickupAttractionRegistry.RegisterForValidation(token);

                experienceObject.transform.position = Vector3.right * 2f;
                experience.attractRange = 0.25f;
                tokenObject.transform.position = Vector3.right * 10f;
                var registeredPickups = new List<AttractablePickup>();
                PickupAttractionRegistry.CopyActiveTo(registeredPickups);
                int proximityAttractions = PickupAttractionRegistry.BeginNearbyAttraction(
                    player,
                    playerObject.transform.position,
                    playerObject.transform.position + Vector3.right * 4f);
                if (proximityAttractions != 1 ||
                    !experience.IsAttracting ||
                    token.IsAttracting)
                {
                    failures.Add(
                        "Player-owned pickup scan must attract only pickups inside their configured range. " +
                        $"registered={registeredPickups.Count}, started={proximityAttractions}, " +
                        $"experienceAttracting={experience.IsAttracting}, tokenAttracting={token.IsAttracting}.");
                }

                experienceObject.transform.position = Vector3.right * 10f;
                tokenObject.transform.position = Vector3.right * 10f;
                experience.speed = 6f;
                token.speed = 6f;
                var playerMoveSpeedField = typeof(PlayerController).GetField(
                    "moveSpeed",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                if (playerMoveSpeedField == null)
                {
                    failures.Add("PlayerController must expose the current movement speed to pickup attraction.");
                    return;
                }
                playerMoveSpeedField.SetValue(player, 12f);
                float expectedAttractionSpeed =
                    12f + PickupAttractionMotion.MinimumSpeedLeadOverPlayer;
                float experienceAttractionSpeed = experience.ResolveAttractionSpeed(player);
                float tokenAttractionSpeed = token.ResolveAttractionSpeed(player);
                if (!Mathf.Approximately(experienceAttractionSpeed, expectedAttractionSpeed) ||
                    !Mathf.Approximately(tokenAttractionSpeed, expectedAttractionSpeed) ||
                    experienceAttractionSpeed <= player.CurrentMoveSpeed ||
                    tokenAttractionSpeed <= player.CurrentMoveSpeed)
                {
                    failures.Add(
                        "XP and token attraction must remain faster than the player's current movement speed.");
                }

                int reservedExperience = experience.BeginStageTransitionAttraction(player);
                int reservedTokens = token.BeginStageTransitionAttraction(player);
                if (reservedExperience != 7 ||
                    reservedTokens != 5 ||
                    experience.value != 0 ||
                    token.value != 0)
                {
                    failures.Add("Stage-transition attraction must reserve every XP and token value exactly once.");
                }
                if (!experience.IsStageTransitionAttracting ||
                    !token.IsStageTransitionAttracting ||
                    experience.BeginStageTransitionAttraction(player) != 0 ||
                    token.BeginStageTransitionAttraction(player) != 0)
                {
                    failures.Add("A reserved pickup must attract once and must not be awarded twice.");
                }

                Vector3 xpStep = ExperienceOrb.MoveTowardsTarget(
                    experienceObject.transform.position,
                    playerObject.transform.position,
                    experienceAttractionSpeed,
                    0.1f);
                Vector3 tokenStep = TokenOrb.MoveTowardsTarget(
                    tokenObject.transform.position,
                    playerObject.transform.position,
                    tokenAttractionSpeed,
                    0.1f);
                if (!Mathf.Approximately(xpStep.x, 8.6f) ||
                    !Mathf.Approximately(tokenStep.x, 8.6f) ||
                    xpStep == playerObject.transform.position ||
                    tokenStep == playerObject.transform.position)
                {
                    failures.Add(
                        "Stage-transition pickups must use the same speed-based MoveTowards motion instead of snapping or fixed-duration interpolation.");
                }

                float expectedTravelSeconds =
                    10f / PickupAttractionMotion.MinimumSpeedLeadOverPlayer;
                if (!Mathf.Approximately(
                        experience.EstimateStageTransitionAttractionSeconds(player),
                        expectedTravelSeconds) ||
                    !Mathf.Approximately(
                        token.EstimateStageTransitionAttractionSeconds(player),
                        expectedTravelSeconds))
                {
                    failures.Add(
                        "Stage-transition pickup timeout must account for the worst-case relative catch-up speed.");
                }
            }
            finally
            {
                var token = tokenObject.GetComponent<TokenOrb>();
                var experience = experienceObject.GetComponent<ExperienceOrb>();
                PickupAttractionRegistry.UnregisterForValidation(token);
                PickupAttractionRegistry.UnregisterForValidation(experience);
                UnityEngine.Object.DestroyImmediate(tokenObject);
                UnityEngine.Object.DestroyImmediate(experienceObject);
                UnityEngine.Object.DestroyImmediate(playerObject);
            }
        }

        static void ValidatePickupAttractionSourceContract(List<string> failures)
        {
            string[] requiredPaths =
            {
                AttractablePickupSourcePath,
                PlayerControllerSourcePath,
                ExperienceOrbSourcePath,
                TokenOrbSourcePath,
                GameManagerSourcePath
            };
            for (int i = 0; i < requiredPaths.Length; i++)
            {
                if (!File.Exists(requiredPaths[i]))
                {
                    failures.Add($"Pickup attraction source is missing: {requiredPaths[i]}");
                    return;
                }
            }

            string pickupSource = File.ReadAllText(AttractablePickupSourcePath);
            string playerSource = File.ReadAllText(PlayerControllerSourcePath);
            string experienceSource = File.ReadAllText(ExperienceOrbSourcePath);
            string tokenSource = File.ReadAllText(TokenOrbSourcePath);
            string managerSource = File.ReadAllText(GameManagerSourcePath);
            if (!pickupSource.Contains("PickupAttractionRegistry") ||
                !pickupSource.Contains("ResolveAttractionSpeed(player)") ||
                !pickupSource.Contains("Time.unscaledDeltaTime") &&
                !playerSource.Contains("Time.unscaledDeltaTime"))
            {
                failures.Add(
                    "Shared pickup attraction must use player-relative speed for normal and stage-transition movement.");
            }
            if (!playerSource.Contains("ProcessPickupAttractions()") ||
                !playerSource.Contains("PickupAttractionRegistry.BeginNearbyAttraction"))
            {
                failures.Add(
                    "PlayerController must own the periodic proximity scan and active pickup movement.");
            }
            if (experienceSource.Contains("void Update()") ||
                tokenSource.Contains("void Update()") ||
                pickupSource.Contains("void Update()"))
            {
                failures.Add(
                    "XP and token pickups must not run an individual Update loop while waiting on the field.");
            }
            if (pickupSource.Contains("OnTriggerEnter2D") ||
                tokenSource.Contains("AddComponent<CircleCollider2D>"))
            {
                failures.Add(
                    "Player-owned pickup collection must not recreate per-pickup Physics trigger callbacks.");
            }
            if (managerSource.Contains("FindObjectsOfType<ExperienceOrb>") ||
                managerSource.Contains("FindObjectsOfType<TokenOrb>") ||
                !managerSource.Contains("PickupAttractionRegistry.CopyActiveTo"))
            {
                failures.Add(
                    "Stage-transition reward attraction must use the shared pickup registry instead of a scene-wide object search.");
            }
            int completeIndex = pickupSource.IndexOf(
                "public void CompleteStageTransitionAttraction",
                StringComparison.Ordinal);
            int proximityIndex = pickupSource.IndexOf(
                "internal bool CanBeginProximityAttraction",
                completeIndex,
                StringComparison.Ordinal);
            if (completeIndex < 0 || proximityIndex <= completeIndex)
            {
                failures.Add("Stage-transition pickup completion method boundaries were not found.");
            }
            else
            {
                string completionMethod = pickupSource.Substring(
                    completeIndex,
                    proximityIndex - completeIndex);
                int awardIndex = completionMethod.IndexOf(
                    "AwardReward(rewardValue)",
                    StringComparison.Ordinal);
                int destroyIndex = completionMethod.IndexOf(
                    "Destroy(gameObject)",
                    StringComparison.Ordinal);
                if (!pickupSource.Contains("int stageTransitionRewardValue;") ||
                    awardIndex < 0 ||
                    destroyIndex < awardIndex)
                {
                    failures.Add(
                        "A stage-transition pickup must award its reserved value when it reaches the player, before it is destroyed.");
                }
            }
            if (managerSource.Contains("int totalExperience = 0;") ||
                managerSource.Contains("int totalTokens = 0;") ||
                managerSource.Contains("AddExperience(totalExperience)") ||
                managerSource.Contains("AddRunTokens(totalTokens)"))
            {
                failures.Add(
                    "Stage-transition XP and tokens must not be deferred and awarded as one aggregate after all pickups finish.");
            }
        }

        static void ValidateLevelUpQueueContract(List<string> failures)
        {
            string source = File.ReadAllText(GameManagerSourcePath);
            int addExperienceIndex = source.IndexOf(
                "public void AddExperience",
                StringComparison.Ordinal);
            int queueMethodIndex = source.IndexOf(
                "void QueueRunLevelUps",
                addExperienceIndex,
                StringComparison.Ordinal);
            if (addExperienceIndex < 0 || queueMethodIndex <= addExperienceIndex)
            {
                failures.Add("AddExperience method boundaries were not found.");
                return;
            }

            string addExperienceMethod = source.Substring(
                addExperienceIndex,
                queueMethodIndex - addExperienceIndex);
            if (!source.Contains("int pendingRunLevelUps;") ||
                !addExperienceMethod.Contains("gainedLevels++;") ||
                !addExperienceMethod.Contains("QueueRunLevelUps(gainedLevels);") ||
                !source.Contains("pendingRunLevelUps += count;") ||
                !source.Contains("pendingRunLevelUps--;") ||
                !source.Contains("if (TryShowNextRunLevelUp()) return;"))
            {
                failures.Add(
                    "Every gained level must enqueue one run-upgrade choice and the next choice must open after the current panel completes.");
            }
            if (addExperienceMethod.Contains("ShowLevelUp();"))
            {
                failures.Add(
                    "AddExperience must not overwrite the same level-up panel repeatedly when multiple levels are gained in one award.");
            }
        }

        static void ValidateManager(GameManager manager, List<string> failures)
        {
            if (manager.config == null)
            {
                failures.Add("GameManager.config is missing.");
            }
            else
            {
                GameConfig config = manager.config;
                if (config.stageTransitionFlashPeakAlpha <= 0f) failures.Add("Stage transition flash alpha must be positive.");
                if (config.stageTransitionFlashInSeconds <= 0f) failures.Add("Stage transition flash-in duration must be positive.");
                if (config.stageTransitionFlashOutSeconds <= 0f) failures.Add("Stage transition flash-out duration must be positive.");
                if (config.stageTransitionEnemyHitDelaySeconds + 0.0001f < EnemyHitFlash.FlashSeconds)
                {
                    failures.Add("Enemy hit delay must allow the hit flash to finish before the death animation starts.");
                }

                float requiredTimeout = config.stageTransitionEnemyHitDelaySeconds + EnemyController.NormalDeathDurationSeconds + 0.1f;
                if (config.stageTransitionEnemyDefeatTimeoutSeconds + 0.0001f < requiredTimeout)
                {
                    failures.Add("Enemy defeat timeout is shorter than hit delay plus the normal death animation.");
                }
            }

            if (manager.screenFade == null)
            {
                failures.Add("GameManager.screenFade is missing.");
            }
            else
            {
                if (manager.screenFade.GetComponent<CanvasGroup>() == null)
                {
                    failures.Add("ScreenFadeOverlay requires a CanvasGroup on the same GameObject.");
                }

                if (manager.screenFade.GetComponent<Graphic>() == null)
                {
                    failures.Add("ScreenFadeOverlay requires a Graphic on the same GameObject for the white flash.");
                }

                if (manager.screenFade.GetComponentInParent<Canvas>(true) == null)
                {
                    failures.Add("ScreenFadeOverlay requires a parent Canvas that can be enabled for the white flash.");
                }
            }

            if (manager.spawner == null)
            {
                failures.Add("GameManager.spawner is missing.");
            }
            else if (manager.spawner.xpOrbPrefab == null)
            {
                failures.Add("EnemySpawner.xpOrbPrefab is missing.");
            }
            else if (manager.spawner.xpOrbPrefab.GetComponent<ExperienceOrb>() == null)
            {
                failures.Add("EnemySpawner.xpOrbPrefab does not contain ExperienceOrb.");
            }
            else if (manager.spawner.xpOrbPrefab.GetComponentInChildren<Collider2D>(true) != null)
            {
                failures.Add(
                    "EnemySpawner.xpOrbPrefab must not keep a Physics collider after player-owned attraction migration.");
            }
            if (manager.levelUpPanel == null)
            {
                failures.Add("GameManager.levelUpPanel is missing.");
            }
        }

        static T FindSingleInScene<T>(Scene scene, List<string> failures) where T : Component
        {
            T found = null;
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(true))
                {
                    count++;
                    if (found == null) found = component;
                }
            }

            if (count != 1) failures.Add($"05_Game must contain exactly one {typeof(T).Name}; found {count}.");
            return found;
        }

        static Scene OpenGameScene(out bool openedAdditive)
        {
            Scene loaded = SceneManager.GetSceneByPath(GameScenePath);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                openedAdditive = false;
                return loaded;
            }

            openedAdditive = true;
            return EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        }

        static void DeleteSuccessMarker()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
        }

        static void WriteSuccessMarker()
        {
            string directory = Path.GetDirectoryName(SuccessMarkerPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("O"));
        }
    }
}
