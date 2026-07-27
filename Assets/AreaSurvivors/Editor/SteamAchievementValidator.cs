using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.Editor
{
    public static class SteamAchievementValidator
    {
        public const string SuccessMarkerPath = "Library/AreaSurvivors/steam-achievement-validation.success";
        const string UpgradeScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";

        [MenuItem("Area Survivors/Validate/Steam Achievements")]
        public static void Validate()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
            ValidateDefinitions();
            ValidateFixedContentTargets();
            ValidateSkillTreeTargets();
            ValidateTitleSceneRuntime();
            ValidateServiceBehavior();
            Directory.CreateDirectory(Path.GetDirectoryName(SuccessMarkerPath));
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log("Steam achievement validation passed.");
        }

        static void ValidateDefinitions()
        {
            if (SteamAchievementCatalog.Definitions.Count != 13)
            {
                throw new InvalidOperationException("Steam achievement definition count must be 13.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in SteamAchievementCatalog.Definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.ApiName))
                {
                    throw new InvalidOperationException("Steam achievement API names must not be empty.");
                }

                if (!ids.Add(definition.ApiName))
                {
                    throw new InvalidOperationException("Duplicate Steam achievement API name: " + definition.ApiName);
                }
            }
        }

        static void ValidateFixedContentTargets()
        {
            var relicTargets = new HashSet<RelicType>(SteamAchievementCatalog.RequiredRelics);
            foreach (RelicType type in Enum.GetValues(typeof(RelicType)))
            {
                if (type != RelicType.None && !relicTargets.Contains(type))
                {
                    throw new InvalidOperationException("Current relic is missing from the fixed achievement target: " + type);
                }
            }

            var evolutionTargets = new HashSet<WeaponType>(SteamAchievementCatalog.RequiredEvolutions);
            foreach (WeaponType type in Enum.GetValues(typeof(WeaponType)))
            {
                if (WeaponCatalog.IsEvolution(type) && !evolutionTargets.Contains(type))
                {
                    throw new InvalidOperationException("Current evolution is missing from the fixed achievement target: " + type);
                }
            }

            if (SteamAchievementCatalog.RequiredDifficulty5Stages.Length != ProgressionStore.ImplementedStageCount)
            {
                throw new InvalidOperationException("Difficulty 5 stage target count does not match implemented stages.");
            }

            for (int stage = 1; stage <= ProgressionStore.ImplementedStageCount; stage++)
            {
                if (Array.IndexOf(SteamAchievementCatalog.RequiredDifficulty5Stages, stage) < 0)
                {
                    throw new InvalidOperationException("Difficulty 5 achievement is missing stage " + stage);
                }
            }
        }

        static void ValidateSkillTreeTargets()
        {
            Scene scene = OpenSceneIfNeeded(UpgradeScenePath, out bool openedHere);
            try
            {
                var sceneTypes = new HashSet<UpgradeType>();
                foreach (var node in FindInScene<SkillNodeView>(scene))
                {
                    if (node != null && node.implemented) sceneTypes.Add(node.type);
                }

                var catalogTypes = new HashSet<UpgradeType>(SteamAchievementCatalog.RequiredMaxedUpgrades);
                if (!sceneTypes.SetEquals(catalogTypes))
                {
                    var missing = new List<string>();
                    var extra = new List<string>();
                    foreach (var type in sceneTypes) if (!catalogTypes.Contains(type)) missing.Add(type.ToString());
                    foreach (var type in catalogTypes) if (!sceneTypes.Contains(type)) extra.Add(type.ToString());
                    throw new InvalidOperationException(
                        "Steam max-skill targets do not match implemented skill nodes. missing=" +
                        string.Join(",", missing) + " extra=" + string.Join(",", extra));
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ValidateTitleSceneRuntime()
        {
            Scene scene = OpenSceneIfNeeded(SteamAchievementSceneMigration.TitleScenePath, out bool openedHere);
            try
            {
                int count = FindInScene<SteamAchievementRuntime>(scene).Length;
                if (count != 1)
                {
                    throw new InvalidOperationException("Title scene must contain exactly one SteamAchievementRuntime. count=" + count);
                }
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ValidateServiceBehavior()
        {
            var backend = new MockBackend();
            var service = new SteamAchievementService(backend);
            if (!service.InitializeFromSteam() || !service.IsReady)
            {
                throw new InvalidOperationException("Steam achievement service did not become ready after all achievement states were read.");
            }
            var data = new SaveData { playCount = 1, totalKills = 100 };
            int unlocked = service.EvaluateAndStore(data);
            if (unlocked != 2 || backend.StoreCalls != 1 ||
                !backend.Unlocked.Contains(SteamAchievementCatalog.FirstSortie) ||
                !backend.Unlocked.Contains(SteamAchievementCatalog.Kill100))
            {
                throw new InvalidOperationException("Steam achievement service did not unlock and batch-store the expected milestones.");
            }

            unlocked = service.EvaluateAndStore(data);
            if (unlocked != 0 || backend.StoreCalls != 1)
            {
                throw new InvalidOperationException("Steam achievement service is not idempotent.");
            }
        }

        static Scene OpenSceneIfNeeded(string path, out bool openedHere)
        {
            openedHere = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == path) return loaded;
            }

            openedHere = true;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        static T[] FindInScene<T>(Scene scene) where T : Component
        {
            var results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results.ToArray();
        }

        sealed class MockBackend : ISteamAchievementBackend
        {
            public readonly HashSet<string> Unlocked = new HashSet<string>();
            public int StoreCalls { get; private set; }

            public bool TryGetAchievement(string apiName, out bool unlocked)
            {
                unlocked = Unlocked.Contains(apiName);
                return true;
            }

            public bool SetAchievement(string apiName)
            {
                Unlocked.Add(apiName);
                return true;
            }

            public bool StoreStats()
            {
                StoreCalls++;
                return true;
            }
        }
    }
}
