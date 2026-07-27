using System;
using System.IO;
using AreaSurvivors.Testing;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class CombatPerformanceProbeCommands
    {
        const float ProbeDurationSeconds = 10f;
        const float EnemyCrowdProbeWarmupSeconds = 2f;
        const string MenuRoot = "Area Survivors/Diagnostics/Combat Performance Probe/";
        const string SustainedScenarioPath = "Assets/AreaSurvivors/Testing/Gameplay_Combat_Performance_Excalibur_Sustained.asset";
        const string KillBurstScenarioPath = "Assets/AreaSurvivors/Testing/Gameplay_Combat_Performance_Excalibur_KillBurst.asset";
        const string FrostSustainedScenarioPath = "Assets/AreaSurvivors/Testing/Gameplay_Combat_Performance_Frost_Sustained.asset";
        const string EnemyCrowdScenarioPath = "Assets/AreaSurvivors/Testing/Gameplay_Combat_Performance_Enemy_Crowd.asset";
        const int SustainedEnemyBaseHp = 5000;
        const int KillBurstEnemyBaseHp = 1;
        const int EnemiesPerCluster = 8;
        static readonly Vector2Int[] LichCrowdClusterOffsets =
        {
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1),
            new Vector2Int(0, -1)
        };

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Baseline")]
        static void PrepareExcaliburSustainedBaseline()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.Baseline, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Without Damage Popups")]
        static void PrepareExcaliburSustainedWithoutDamagePopups()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.DisableDamagePopups, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Without Hit Flash")]
        static void PrepareExcaliburSustainedWithoutHitFlash()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.DisableHitFlash, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Without Damage Feedback")]
        static void PrepareExcaliburSustainedWithoutDamageFeedback()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.DisableDamageFeedback, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Without Enemy Controller")]
        static void PrepareExcaliburSustainedWithoutEnemyController()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.DisableEnemyController, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Without Enemy Contact Check")]
        static void PrepareExcaliburSustainedWithoutEnemyContactCheck()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.DisableEnemyContactCheck, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Without Enemy Move Multiplier")]
        static void PrepareExcaliburSustainedWithoutEnemyMoveMultiplier()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.DisableEnemyMoveMultiplier, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Without Enemy Paint")]
        static void PrepareExcaliburSustainedWithoutEnemyPaint()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.DisableEnemyPaint, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Without Enemy Animation")]
        static void PrepareExcaliburSustainedWithoutEnemyAnimation()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.DisableEnemyAnimation, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Without Enemy Y Sort")]
        static void PrepareExcaliburSustainedWithoutEnemyYSort()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.DisableEnemyYSort, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Sustained Without Enemy-Enemy Collision")]
        static void PrepareExcaliburSustainedWithoutEnemyEnemyCollision()
        {
            PrepareExcaliburScenario(SustainedScenarioPath, RuntimePerformanceProbeMode.DisableEnemyEnemyCollision, true);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Kill Burst Baseline")]
        static void PrepareExcaliburKillBurstBaseline()
        {
            PrepareExcaliburScenario(KillBurstScenarioPath, RuntimePerformanceProbeMode.Baseline, false);
        }

        [MenuItem(MenuRoot + "Prepare Excalibur Kill Burst Without Damage Feedback")]
        static void PrepareExcaliburKillBurstWithoutDamageFeedback()
        {
            PrepareExcaliburScenario(KillBurstScenarioPath, RuntimePerformanceProbeMode.DisableDamageFeedback, false);
        }

        [MenuItem(MenuRoot + "Prepare Frost Sustained Without Damage Feedback")]
        static void PrepareFrostSustainedWithoutDamageFeedback()
        {
            PrepareExcaliburScenario(FrostSustainedScenarioPath, RuntimePerformanceProbeMode.DisableDamageFeedback, true);
            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(FrostSustainedScenarioPath);
            if (scenario == null) return;
            scenario.startingWeapon = WeaponType.Frost;
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssets();
            RunState.SetNextWeaponTest(WeaponType.Frost);
            GameplayTestTools.UseScenarioAsset(scenario);
        }

        [MenuItem(MenuRoot + "Prepare Enemy Crowd Baseline")]
        static void PrepareEnemyCrowdBaseline()
        {
            PrepareEnemyCrowdScenario(RuntimePerformanceProbeMode.Baseline);
        }

        [MenuItem(MenuRoot + "Prepare Enemy Crowd Without Enemy-Enemy Collision")]
        static void PrepareEnemyCrowdWithoutEnemyEnemyCollision()
        {
            PrepareEnemyCrowdScenario(RuntimePerformanceProbeMode.DisableEnemyEnemyCollision);
        }

        [MenuItem(MenuRoot + "Prepare Enemy Crowd Without Occlusion")]
        static void PrepareEnemyCrowdWithoutOcclusion()
        {
            PrepareEnemyCrowdScenario(RuntimePerformanceProbeMode.DisableOcclusion);
        }

        [MenuItem(MenuRoot + "Prepare Enemy Crowd Without Outline")]
        static void PrepareEnemyCrowdWithoutOutline()
        {
            PrepareEnemyCrowdScenario(RuntimePerformanceProbeMode.DisableOutline);
        }

        [MenuItem(MenuRoot + "Prepare Enemy Crowd Without Occlusion And Outline")]
        static void PrepareEnemyCrowdWithoutOcclusionAndOutline()
        {
            PrepareEnemyCrowdScenario(RuntimePerformanceProbeMode.DisableOcclusionAndOutline);
        }

        [MenuItem(MenuRoot + "Prepare Enemy Crowd Without Enemy Controller")]
        static void PrepareEnemyCrowdWithoutEnemyController()
        {
            PrepareEnemyCrowdScenario(RuntimePerformanceProbeMode.DisableEnemyController);
        }

        [MenuItem(MenuRoot + "Prepare Enemy Crowd With Physics Multithreading")]
        static void PrepareEnemyCrowdWithPhysicsMultithreading()
        {
            PrepareEnemyCrowdScenario(RuntimePerformanceProbeMode.EnablePhysicsMultithreading);
        }

        [MenuItem(MenuRoot + "Start Baseline (10s)")]
        static void StartBaseline()
        {
            Start(RuntimePerformanceProbeMode.Baseline);
        }

        [MenuItem(MenuRoot + "Start Without Damage Popups (10s)")]
        static void StartWithoutDamagePopups()
        {
            Start(RuntimePerformanceProbeMode.DisableDamagePopups);
        }

        [MenuItem(MenuRoot + "Start Without Hit Flash (10s)")]
        static void StartWithoutHitFlash()
        {
            Start(RuntimePerformanceProbeMode.DisableHitFlash);
        }

        [MenuItem(MenuRoot + "Start Without Damage Feedback (10s)")]
        static void StartWithoutDamageFeedback()
        {
            Start(RuntimePerformanceProbeMode.DisableDamageFeedback);
        }

        [MenuItem(MenuRoot + "Log Last Result")]
        static void LogLastResult()
        {
            string path = RuntimePerformanceProbe.LastResultFilePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"Combat performance probe result does not exist yet: {path}");
                return;
            }

            Debug.Log($"[CombatPerformanceProbeResult] {File.ReadAllText(path)}");
        }

        [MenuItem(MenuRoot + "Start Baseline (10s)", true)]
        static bool CanStartBaseline()
        {
            return CanStart();
        }

        [MenuItem(MenuRoot + "Start Without Damage Popups (10s)", true)]
        static bool CanStartWithoutDamagePopups()
        {
            return CanStart();
        }

        [MenuItem(MenuRoot + "Start Without Hit Flash (10s)", true)]
        static bool CanStartWithoutHitFlash()
        {
            return CanStart();
        }

        [MenuItem(MenuRoot + "Start Without Damage Feedback (10s)", true)]
        static bool CanStartWithoutDamageFeedback()
        {
            return CanStart();
        }

        static bool CanStart()
        {
            return EditorApplication.isPlaying && !RuntimePerformanceProbe.IsRunning;
        }

        static void Start(RuntimePerformanceProbeMode mode)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("Combat performance probe can only start while Play Mode is running.");
                return;
            }

            RuntimePerformanceProbe.Begin(mode, ProbeDurationSeconds);
            Debug.Log($"[CombatPerformanceProbe] Started mode={mode}, duration={ProbeDurationSeconds:0}s.");
        }

        static void PrepareExcaliburScenario(string scenarioPath, RuntimePerformanceProbeMode mode, bool sustained)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before preparing the deterministic combat performance scenario.");
                return;
            }

            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(scenarioPath);
            if (scenario == null)
            {
                scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
                scenario.name = Path.GetFileNameWithoutExtension(scenarioPath);
                AssetDatabase.CreateAsset(scenario, scenarioPath);
            }

            scenario.systems = new GameplayTestScenario.SystemSettings
            {
                buildGrid = true,
                enableGameManager = true,
                enableEnemySpawner = false,
                enableScenePlayer = true,
                enableSceneTower = false,
                clearExistingEnemies = true,
            };
            scenario.configOverrides = sustained
                ? new[]
                {
                    IntegerOverride("enemyBaseHp", SustainedEnemyBaseHp),
                    IntegerOverride("enemyDamage", 0)
                }
                : new[]
                {
                    IntegerOverride("enemyBaseHp", KillBurstEnemyBaseHp),
                    IntegerOverride("enemyDamage", 0)
                };
            scenario.targetCellOffset = Vector2Int.zero;
            scenario.enemies = BuildLichCrowdPlacements();
            scenario.prefabs = Array.Empty<GameplayTestScenario.PrefabPlacement>();
            scenario.scheduledActions = new[]
            {
                new GameplayTestScenario.ScheduledAction
                {
                    atSeconds = 0f,
                    type = GameplayTestActionType.MoveObjectToCell,
                    objectName = "Player",
                    cellOffset = Vector2Int.zero,
                    active = true
                }
            };
            scenario.useFixedRandomSeed = true;
            scenario.randomSeed = 20260726;
            scenario.focusCameraOnSetup = true;
            scenario.cameraFocusCellOffset = Vector2Int.zero;
            scenario.simulationTimeScale = 1f;
            scenario.testDurationSeconds = 90f;
            scenario.pauseOnComplete = false;
            scenario.autoExitPlayModeOnComplete = false;
            scenario.runPerformanceProbe = true;
            scenario.performanceProbeMode = mode;
            scenario.performanceProbeWarmupSeconds = 0f;
            scenario.performanceProbeDurationSeconds = ProbeDurationSeconds;
            scenario.overrideStartingWeapon = true;
            scenario.startingWeapon = WeaponType.Excalibur;
            scenario.assertions = Array.Empty<GameplayTestScenario.Assertion>();

            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssets();
            RunState.SelectedCharacter = CharacterType.Knight;
            RunState.SetNextWeaponTest(WeaponType.Excalibur);
            GameplayTestTools.UseScenarioAsset(scenario);
            GameplayTestTools.OpenGameplayTest();
            Debug.Log(
                $"[CombatPerformanceProbe] Prepared {scenario.name}: mode={mode}, enemies={LichCrowdClusterOffsets.Length * EnemiesPerCluster}, sustained={sustained}.");
        }

        static void PrepareEnemyCrowdScenario(RuntimePerformanceProbeMode mode)
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Exit Play Mode before preparing the deterministic enemy crowd performance scenario.");
                return;
            }

            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(EnemyCrowdScenarioPath);
            if (scenario == null)
            {
                scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
                scenario.name = Path.GetFileNameWithoutExtension(EnemyCrowdScenarioPath);
                AssetDatabase.CreateAsset(scenario, EnemyCrowdScenarioPath);
            }

            scenario.systems = new GameplayTestScenario.SystemSettings
            {
                buildGrid = true,
                enableGameManager = true,
                enableEnemySpawner = false,
                enableScenePlayer = true,
                enableSceneTower = false,
                clearExistingEnemies = true,
            };
            scenario.configOverrides = new[]
            {
                IntegerOverride("enemyBaseHp", SustainedEnemyBaseHp),
                IntegerOverride("enemyDamage", 0),
                FloatOverride("enemyBaseSpeed", 0f)
            };
            scenario.targetCellOffset = Vector2Int.zero;
            scenario.enemies = BuildLichCrowdPlacements();
            scenario.prefabs = Array.Empty<GameplayTestScenario.PrefabPlacement>();
            scenario.scheduledActions = new[]
            {
                new GameplayTestScenario.ScheduledAction
                {
                    atSeconds = 0f,
                    type = GameplayTestActionType.MoveObjectToCell,
                    objectName = "Player",
                    cellOffset = new Vector2Int(0, 20),
                    active = true
                }
            };
            scenario.useFixedRandomSeed = true;
            scenario.randomSeed = 20260726;
            scenario.focusCameraOnSetup = true;
            scenario.cameraFocusCellOffset = Vector2Int.zero;
            scenario.simulationTimeScale = 1f;
            scenario.testDurationSeconds = 90f;
            scenario.pauseOnComplete = false;
            scenario.autoExitPlayModeOnComplete = false;
            scenario.runPerformanceProbe = true;
            scenario.performanceProbeMode = mode;
            scenario.performanceProbeWarmupSeconds = EnemyCrowdProbeWarmupSeconds;
            scenario.performanceProbeDurationSeconds = ProbeDurationSeconds;
            scenario.overrideStartingWeapon = true;
            scenario.startingWeapon = WeaponType.Slash;
            scenario.assertions = Array.Empty<GameplayTestScenario.Assertion>();

            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssets();
            RunState.SelectedCharacter = CharacterType.Knight;
            RunState.SetNextWeaponTest(WeaponType.Slash);
            GameplayTestTools.UseScenarioAsset(scenario);
            GameplayTestTools.OpenGameplayTest();
            Debug.Log(
                $"[CombatPerformanceProbe] Prepared {scenario.name}: mode={mode}, enemies={LichCrowdClusterOffsets.Length * EnemiesPerCluster}, frozenCrowd=true.");
        }

        static GameplayTestScenario.EnemyPlacement[] BuildLichCrowdPlacements()
        {
            var placements = new GameplayTestScenario.EnemyPlacement[LichCrowdClusterOffsets.Length];
            for (int i = 0; i < placements.Length; i++)
            {
                placements[i] = new GameplayTestScenario.EnemyPlacement
                {
                    kind = (i & 1) == 0 ? EnemyKind.Skeleton : EnemyKind.SkeletonKnight,
                    cellOffset = LichCrowdClusterOffsets[i],
                    count = EnemiesPerCluster,
                    spacing = Vector2Int.zero,
                    monitorForStall = false,
                    requireReachTarget = false
                };
            }

            return placements;
        }

        static GameplayTestScenario.ConfigOverride IntegerOverride(string fieldName, int value)
        {
            return new GameplayTestScenario.ConfigOverride
            {
                fieldName = fieldName,
                valueType = GameplayConfigValueType.Integer,
                integerValue = value
            };
        }

        static GameplayTestScenario.ConfigOverride FloatOverride(string fieldName, float value)
        {
            return new GameplayTestScenario.ConfigOverride
            {
                fieldName = fieldName,
                valueType = GameplayConfigValueType.Float,
                floatValue = value
            };
        }
    }
}
