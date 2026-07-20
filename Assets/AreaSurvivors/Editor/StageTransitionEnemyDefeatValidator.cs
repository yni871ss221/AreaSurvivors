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

                if (failures.Count > 0)
                {
                    throw new InvalidOperationException(
                        "Stage transition enemy defeat validation failed:\n- " + string.Join("\n- ", failures));
                }

                WriteSuccessMarker();
                Debug.Log("Stage transition enemy defeat validation passed. Screen flash, enemy defeat timing, and XP drop references are configured.");
            }
            finally
            {
                if (openedAdditive && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
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
