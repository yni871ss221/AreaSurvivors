using AreaSurvivors.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class GameplayTestTools
    {
        const string TestScenePath = "Assets/AreaSurvivors/Scenes/90_GameplayTest.unity";
        const string TestFolder = "Assets/AreaSurvivors/Testing";
        const string DefaultScenarioPath = TestFolder + "/Gameplay_Navigation_Default.asset";
        const string PrefabSmokeScenarioPath = TestFolder + "/Gameplay_Prefab_Smoke.asset";
        const string ActionSmokeScenarioPath = TestFolder + "/Gameplay_Action_Smoke.asset";
        const string EnemyVisualsScenarioPath = TestFolder + "/Gameplay_Enemy_Visuals.asset";
        const string MapPerimeterScenarioPath = TestFolder + "/Gameplay_Map_Perimeter.asset";
        const string RebootWeaponsScenarioPath = TestFolder + "/Gameplay_Reboot_Weapons.asset";
        const string StageProgressionScenarioPath = TestFolder + "/Gameplay_Stage_Progression.asset";
        const string EnemyLoad200ScenarioPath = TestFolder + "/Gameplay_Enemy_Load_200.asset";
        const string EnemyLoad400ScenarioPath = TestFolder + "/Gameplay_Enemy_Load_400.asset";
        const string EnemyLoad800ScenarioPath = TestFolder + "/Gameplay_Enemy_Load_800.asset";
        const string EnemyLoad1200ScenarioPath = TestFolder + "/Gameplay_Enemy_Load_1200.asset";
        const string PerformanceLoadMarkerRelativePath =
            "Library/AreaSafeUnity/performance-load-scenarios.ok";
        const string SelectedScenarioEditorPref = "AreaSurvivors.GameplayTestScenarioPath";
        static bool playModeQueued;
        static readonly RuntimePerformanceProbeMode[] PerformanceLoadProbeModes =
        {
            RuntimePerformanceProbeMode.Baseline,
            RuntimePerformanceProbeMode.DisableEnemyContactCheck,
            RuntimePerformanceProbeMode.DisableEnemyMoveMultiplier,
            RuntimePerformanceProbeMode.DisableEnemyPaint,
            RuntimePerformanceProbeMode.DisableEnemyAnimation,
            RuntimePerformanceProbeMode.DisableEnemyYSort,
            RuntimePerformanceProbeMode.DisableOcclusion,
            RuntimePerformanceProbeMode.DisableOutline,
            RuntimePerformanceProbeMode.DisableEnemyEnemyCollision,
            RuntimePerformanceProbeMode.EnablePhysicsMultithreading,
            RuntimePerformanceProbeMode.DisableEnemyController,
            RuntimePerformanceProbeMode.Baseline
        };

        public static void BuildGameplayTestScene()
        {
            EnsureFolder();
            var scenario = EnsureDefaultScenario();
            EnsurePrefabSmokeScenario();
            EnsureActionSmokeScenario();
            EnsureEnemyVisualsScenario();
            EnsureMapPerimeterScenario();
            EnsureRebootWeaponsScenario();
            EnsureStageProgressionScenario();
            EnsureEnemyLoadScenario(EnemyLoad200ScenarioPath, 200, 20, true);
            EnsureEnemyLoadScenario(EnemyLoad400ScenarioPath, 400, 40, true);
            EnsureEnemyLoadScenario(EnemyLoad800ScenarioPath, 800, 80, true);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrapObject = new GameObject("Gameplay Test Bootstrap");
            var bootstrap = bootstrapObject.AddComponent<GameplayTestBootstrap>();
            AssignReferences(bootstrap, scenario);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TestScenePath);
            Selection.activeObject = scenario;
            Debug.Log($"Lightweight gameplay test scene built: {TestScenePath}");
        }

        public static void OpenGameplayTest()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null) BuildGameplayTestScene();
            else EditorSceneManager.OpenScene(TestScenePath);
        }

        public static void UseSelectedScenario()
        {
            var scenario = Selection.activeObject as GameplayTestScenario;
            if (scenario == null)
            {
                Debug.LogWarning("GameplayTestScenario assetを選択してください。");
                return;
            }

            UseScenarioAsset(scenario);
        }

        public static void RunSelectedScenario()
        {
            var scenario = Selection.activeObject as GameplayTestScenario;
            if (scenario == null)
            {
                Debug.LogWarning("Select a GameplayTestScenario asset.");
                return;
            }
            RunScenarioAsset(scenario);
        }

        public static void RunCurrentGameplayTest()
        {
            QueuePlayMode();
        }

        public static void RunPrefabSmoke() => RunScenarioAsset(EnsurePrefabSmokeScenario());

        public static void RunNavigationDefault() => RunScenarioAsset(EnsureDefaultScenario());

        public static void RunMapPerimeter() => RunScenarioAsset(EnsureMapPerimeterScenario());

        public static void RunRebootWeapons() => RunScenarioAsset(EnsureRebootWeaponsScenario());

        public static void RunStageProgression() => RunScenarioAsset(EnsureStageProgressionScenario());

        public static void UseNavigationDefault()
        {
            UseScenarioAsset(EnsureDefaultScenario());
        }

        public static void UsePrefabSmoke()
        {
            UseScenarioAsset(EnsurePrefabSmokeScenario());
        }

        public static void UseActionSmoke()
        {
            UseScenarioAsset(EnsureActionSmokeScenario());
        }

        public static void UseEnemyVisuals()
        {
            UseScenarioAsset(EnsureEnemyVisualsScenario());
        }

        public static void UseMapPerimeter()
        {
            UseScenarioAsset(EnsureMapPerimeterScenario());
        }

        public static void UseRebootWeapons()
        {
            UseScenarioAsset(EnsureRebootWeaponsScenario());
        }

        public static void UseStageProgression()
        {
            UseScenarioAsset(EnsureStageProgressionScenario());
        }

        [MenuItem("Area Survivors/Testing/Performance Load/Rebuild 200-400-800 Matrix")]
        public static void RebuildPerformanceLoadScenarios()
        {
            EnsureFolder();
            EnsureEnemyLoadScenario(EnemyLoad200ScenarioPath, 200, 20, true);
            EnsureEnemyLoadScenario(EnemyLoad400ScenarioPath, 400, 40, true);
            EnsureEnemyLoadScenario(EnemyLoad800ScenarioPath, 800, 80, true);

            string projectRoot =
                System.IO.Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string markerPath = System.IO.Path.Combine(
                projectRoot,
                PerformanceLoadMarkerRelativePath.Replace(
                    '/',
                    System.IO.Path.DirectorySeparatorChar));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(markerPath));
            System.IO.File.WriteAllText(markerPath, System.DateTime.UtcNow.ToString("O"));
            Debug.Log(
                "Performance load scenarios rebuilt: 200, 400, 800 enemies with deterministic A/B matrix.");
        }

        [MenuItem("Area Survivors/Testing/Performance Load/Prepare 200")]
        public static void PrepareEnemyLoad200()
        {
            UseScenarioAsset(EnsureEnemyLoadScenario(EnemyLoad200ScenarioPath, 200, 20, true));
            OpenGameplayTest();
        }

        [MenuItem("Area Survivors/Testing/Performance Load/Prepare 400")]
        public static void PrepareEnemyLoad400()
        {
            UseScenarioAsset(EnsureEnemyLoadScenario(EnemyLoad400ScenarioPath, 400, 40, true));
            OpenGameplayTest();
        }

        [MenuItem("Area Survivors/Testing/Performance Load/Prepare 800")]
        public static void PrepareEnemyLoad800()
        {
            UseScenarioAsset(EnsureEnemyLoadScenario(EnemyLoad800ScenarioPath, 800, 80, true));
            OpenGameplayTest();
        }

        [MenuItem("Area Survivors/Testing/Performance Load/Prepare Legacy 1200 Baseline")]
        public static void PrepareEnemyLoad1200()
        {
            UseScenarioAsset(EnsureEnemyLoadScenario(EnemyLoad1200ScenarioPath, 1200, 0, false));
            OpenGameplayTest();
        }

        public static RuntimePerformanceProbeMode[] GetPerformanceLoadProbeModes()
        {
            return (RuntimePerformanceProbeMode[])PerformanceLoadProbeModes.Clone();
        }

        public static void CreateNewGameplayScenario()
        {
            EnsureFolder();
            var scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
            scenario.name = "Gameplay_NewScenario";
            string path = AssetDatabase.GenerateUniqueAssetPath(TestFolder + "/Gameplay_NewScenario.asset");
            AssetDatabase.CreateAsset(scenario, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = scenario;
            EditorGUIUtility.PingObject(scenario);
        }

        static void AssignReferences(GameplayTestBootstrap bootstrap, GameplayTestScenario scenario)
        {
            bootstrap.scenario = scenario;
            bootstrap.config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/AreaSurvivors/Resources/Config/GameConfig.asset");
            bootstrap.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/Characters/Enemy.prefab");
            bootstrap.xpOrbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/Pickups/ExperienceOrb.prefab");
            bootstrap.damagePopupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/UI/DamagePopup.prefab");
        }

        public static void UseScenarioAsset(GameplayTestScenario scenario)
        {
            if (scenario == null) return;
            EditorPrefs.SetString(SelectedScenarioEditorPref, AssetDatabase.GetAssetPath(scenario));
            Selection.activeObject = scenario;
            Debug.Log($"Gameplay test scenario selected: {scenario.name}");
        }

        static void RunScenarioAsset(GameplayTestScenario scenario)
        {
            UseScenarioAsset(scenario);
            QueuePlayMode();
        }

        static void QueuePlayMode()
        {
            OpenGameplayTest();
            if (playModeQueued) return;
            playModeQueued = true;
            EditorApplication.update += EnterPlayModeWhenReady;
        }

        static void EnterPlayModeWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorSceneManager.GetActiveScene().path != TestScenePath) return;
            EditorApplication.update -= EnterPlayModeWhenReady;
            playModeQueued = false;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = true;
        }

        static GameplayTestScenario EnsureDefaultScenario()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(DefaultScenarioPath);
            if (scenario != null) return scenario;

            scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
            scenario.name = "Gameplay_Navigation_Default";
            scenario.targetCellOffset = Vector2Int.zero;
            scenario.enemies = new[]
            {
                new GameplayTestScenario.EnemyPlacement { kind = EnemyKind.Orc, cellOffset = new Vector2Int(-12, 0) },
                new GameplayTestScenario.EnemyPlacement { kind = EnemyKind.Orc, cellOffset = new Vector2Int(-12, 1) }
            };
            scenario.simulationTimeScale = 4f;
            scenario.testDurationSeconds = 12f;
            scenario.stallSeconds = 3f;
            scenario.pauseOnComplete = false;
            AssetDatabase.CreateAsset(scenario, DefaultScenarioPath);
            AssetDatabase.SaveAssets();
            return scenario;
        }

        static GameplayTestScenario EnsurePrefabSmokeScenario()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(PrefabSmokeScenarioPath);
            if (scenario != null) return scenario;

            scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
            scenario.name = "Gameplay_Prefab_Smoke";
            scenario.prefabs = new[]
            {
                new GameplayTestScenario.PrefabPlacement
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/Pickups/ExperienceOrb.prefab"),
                    instanceName = "Test Experience Orb",
                    cellOffset = new Vector2Int(2, 0),
                    scale = Vector3.one
                }
            };
            scenario.configOverrides = new[]
            {
                new GameplayTestScenario.ConfigOverride
                {
                    fieldName = "enemyBaseSpeed",
                    valueType = GameplayConfigValueType.Float,
                    floatValue = 1.25f
                }
            };
            scenario.testDurationSeconds = 1f;
            scenario.simulationTimeScale = 4f;
            scenario.assertions = new[]
            {
                new GameplayTestScenario.Assertion
                {
                    type = GameplayTestAssertionType.ObjectNameExists,
                    objectName = "Test Experience Orb"
                }
            };
            AssetDatabase.CreateAsset(scenario, PrefabSmokeScenarioPath);
            AssetDatabase.SaveAssets();
            return scenario;
        }

        static GameplayTestScenario EnsureMapPerimeterScenario()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(MapPerimeterScenarioPath);
            if (scenario != null) return scenario;

            scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
            scenario.name = "Gameplay_Map_Perimeter";
            scenario.targetCellOffset = Vector2Int.zero;
            scenario.enemies = new[]
            {
                new GameplayTestScenario.EnemyPlacement
                {
                    kind = EnemyKind.Boar,
                    cellOffset = new Vector2Int(34, 0),
                    monitorForStall = true,
                    requireReachTarget = false
                }
            };
            scenario.simulationTimeScale = 4f;
            scenario.testDurationSeconds = 4f;
            scenario.stallSeconds = 1f;
            scenario.assertions = new[]
            {
                new GameplayTestScenario.Assertion
                {
                    type = GameplayTestAssertionType.AllMonitoredObjectsInsideGrid
                }
            };
            AssetDatabase.CreateAsset(scenario, MapPerimeterScenarioPath);
            AssetDatabase.SaveAssets();
            return scenario;
        }

        static GameplayTestScenario EnsureActionSmokeScenario()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(ActionSmokeScenarioPath);
            if (scenario != null) return scenario;

            scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
            scenario.name = "Gameplay_Action_Smoke";
            scenario.prefabs = new[]
            {
                new GameplayTestScenario.PrefabPlacement
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/Pickups/ExperienceOrb.prefab"),
                    instanceName = "Scheduled Action Target",
                    cellOffset = new Vector2Int(2, 0),
                    scale = Vector3.one
                }
            };
            scenario.scheduledActions = new[]
            {
                new GameplayTestScenario.ScheduledAction
                {
                    atSeconds = 0.25f,
                    type = GameplayTestActionType.DestroyObject,
                    objectName = "Scheduled Action Target"
                }
            };
            scenario.testDurationSeconds = 1f;
            scenario.simulationTimeScale = 4f;
            scenario.assertions = new[]
            {
                new GameplayTestScenario.Assertion
                {
                    type = GameplayTestAssertionType.ObjectNameMissing,
                    objectName = "Scheduled Action Target"
                }
            };
            AssetDatabase.CreateAsset(scenario, ActionSmokeScenarioPath);
            AssetDatabase.SaveAssets();
            return scenario;
        }

        static GameplayTestScenario EnsureEnemyVisualsScenario()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(EnemyVisualsScenarioPath);
            if (scenario != null) return scenario;

            scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
            scenario.name = "Gameplay_Enemy_Visuals";
            scenario.targetCellOffset = new Vector2Int(0, -12);
            scenario.enemies = new[]
            {
                new GameplayTestScenario.EnemyPlacement
                {
                    kind = EnemyKind.Orc,
                    cellOffset = new Vector2Int(-5, 4),
                    monitorForStall = false,
                    requireReachTarget = false
                },
                new GameplayTestScenario.EnemyPlacement
                {
                    kind = EnemyKind.OrcKing,
                    cellOffset = new Vector2Int(5, 4),
                    monitorForStall = false,
                    requireReachTarget = false
                }
            };
            scenario.simulationTimeScale = 1f;
            scenario.testDurationSeconds = 8f;
            scenario.assertions = new[]
            {
                new GameplayTestScenario.Assertion
                {
                    type = GameplayTestAssertionType.EnemyCountAtLeast,
                    expectedCount = 2
                }
            };
            AssetDatabase.CreateAsset(scenario, EnemyVisualsScenarioPath);
            AssetDatabase.SaveAssets();
            return scenario;
        }

        static GameplayTestScenario EnsureRebootWeaponsScenario()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(RebootWeaponsScenarioPath);
            if (scenario != null) return scenario;

            scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
            scenario.name = "Gameplay_Reboot_Weapons";
            scenario.systems.enableGameManager = true;
            scenario.systems.enableEnemySpawner = false;
            scenario.systems.enableScenePlayer = false;
            scenario.systems.enableSceneTower = true;
            scenario.systems.clearExistingEnemies = true;
            scenario.targetCellOffset = Vector2Int.zero;
            scenario.scheduledActions = new[]
            {
                new GameplayTestScenario.ScheduledAction
                {
                    atSeconds = 0.2f,
                    type = GameplayTestActionType.LevelUpArrowWeapon
                },
                new GameplayTestScenario.ScheduledAction
                {
                    atSeconds = 0.4f,
                    type = GameplayTestActionType.LevelUpFireballWeapon
                }
            };
            scenario.simulationTimeScale = 4f;
            scenario.testDurationSeconds = 1f;
            scenario.assertions = new[]
            {
                new GameplayTestScenario.Assertion
                {
                    type = GameplayTestAssertionType.GameStageEquals,
                    expectedCount = 1
                },
                new GameplayTestScenario.Assertion
                {
                    type = GameplayTestAssertionType.WeaponSlashLevelAtLeast,
                    expectedCount = 1
                },
                new GameplayTestScenario.Assertion
                {
                    type = GameplayTestAssertionType.WeaponArrowLevelAtLeast,
                    expectedCount = 1
                },
                new GameplayTestScenario.Assertion
                {
                    type = GameplayTestAssertionType.WeaponFireballLevelAtLeast,
                    expectedCount = 1
                }
            };
            AssetDatabase.CreateAsset(scenario, RebootWeaponsScenarioPath);
            AssetDatabase.SaveAssets();
            return scenario;
        }

        static GameplayTestScenario EnsureStageProgressionScenario()
        {
            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(StageProgressionScenarioPath);
            if (scenario != null) return scenario;

            scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
            scenario.name = "Gameplay_Stage_Progression";
            scenario.systems.enableGameManager = true;
            scenario.systems.enableEnemySpawner = false;
            scenario.systems.enableScenePlayer = false;
            scenario.systems.enableSceneTower = true;
            scenario.systems.clearExistingEnemies = true;
            scenario.targetCellOffset = Vector2Int.zero;
            scenario.scheduledActions = new[]
            {
                new GameplayTestScenario.ScheduledAction
                {
                    atSeconds = 1f,
                    type = GameplayTestActionType.SimulateBossDefeat
                }
            };
            scenario.simulationTimeScale = 4f;
            scenario.testDurationSeconds = 4f;
            scenario.assertions = new[]
            {
                new GameplayTestScenario.Assertion
                {
                    type = GameplayTestAssertionType.GameStageEquals,
                    expectedCount = 2
                }
            };
            AssetDatabase.CreateAsset(scenario, StageProgressionScenarioPath);
            AssetDatabase.SaveAssets();
            return scenario;
        }

        static GameplayTestScenario EnsureEnemyLoadScenario(
            string path,
            int enemyCount,
            int clusteredEnemyCount,
            bool usePerformanceMatrix)
        {
            var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(path);
            if (scenario == null)
            {
                scenario = ScriptableObject.CreateInstance<GameplayTestScenario>();
                scenario.name = System.IO.Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(scenario, path);
            }

            scenario.systems = new GameplayTestScenario.SystemSettings
            {
                buildGrid = true,
                enableEnemySpawner = false,
                enableGameManager = false,
                enableScenePlayer = false,
                enableSceneTower = false,
                clearExistingEnemies = true
            };
            scenario.configOverrides = System.Array.Empty<GameplayTestScenario.ConfigOverride>();
            scenario.targetCellOffset = Vector2Int.zero;
            scenario.enemies = BuildEnemyLoadPlacements(enemyCount, clusteredEnemyCount);
            scenario.prefabs = System.Array.Empty<GameplayTestScenario.PrefabPlacement>();
            scenario.scheduledActions = System.Array.Empty<GameplayTestScenario.ScheduledAction>();
            scenario.useFixedRandomSeed = true;
            scenario.randomSeed = 20260727;
            scenario.focusCameraOnSetup = true;
            scenario.cameraFocusCellOffset = Vector2Int.zero;
            scenario.simulationTimeScale = 1f;
            scenario.testDurationSeconds = usePerformanceMatrix ? 110f : 30f;
            scenario.pauseOnComplete = false;
            scenario.autoExitPlayModeOnComplete = false;
            scenario.runPerformanceProbe = !usePerformanceMatrix;
            scenario.performanceProbeMode = RuntimePerformanceProbeMode.Baseline;
            scenario.runPerformanceProbeMatrix = usePerformanceMatrix;
            scenario.performanceProbeMatrixModes = usePerformanceMatrix
                ? GetPerformanceLoadProbeModes()
                : System.Array.Empty<RuntimePerformanceProbeMode>();
            scenario.performanceProbeWarmupSeconds = usePerformanceMatrix ? 1f : 0f;
            scenario.performanceProbeDurationSeconds = 6f;
            scenario.performanceProbeTransitionSeconds = 0.5f;
            scenario.overrideStartingWeapon = false;
            scenario.assertions = new[]
            {
                new GameplayTestScenario.Assertion
                {
                    type = GameplayTestAssertionType.EnemyCountAtLeast,
                    expectedCount = enemyCount
                }
            };
            EditorUtility.SetDirty(scenario);
            AssetDatabase.SaveAssets();
            return scenario;
        }

        static GameplayTestScenario.EnemyPlacement[] BuildEnemyLoadPlacements(int enemyCount, int clusteredEnemyCount)
        {
            int total = Mathf.Max(1, enemyCount);
            int clustered = Mathf.Clamp(clusteredEnemyCount, 0, total);
            int distributed = total - clustered;
            const int columns = 40;
            int rows = Mathf.CeilToInt(distributed / (float)columns);
            var placements = new System.Collections.Generic.List<GameplayTestScenario.EnemyPlacement>(rows + 1);
            if (clustered > 0)
            {
                placements.Add(new GameplayTestScenario.EnemyPlacement
                {
                    kind = EnemyKind.Orc,
                    cellOffset = new Vector2Int(0, 18),
                    count = clustered,
                    spacing = Vector2Int.zero,
                    monitorForStall = false,
                    requireReachTarget = false
                });
            }

            int remaining = distributed;
            for (int row = 0; row < rows && remaining > 0; row++)
            {
                int rowCount = Mathf.Min(columns, remaining);
                placements.Add(new GameplayTestScenario.EnemyPlacement
                {
                    kind = EnemyKind.Orc,
                    cellOffset = new Vector2Int(-columns / 2, row - rows / 2),
                    count = rowCount,
                    spacing = Vector2Int.right,
                    monitorForStall = false,
                    requireReachTarget = false
                });
                remaining -= rowCount;
            }

            return placements.ToArray();
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder)) AssetDatabase.CreateFolder("Assets/AreaSurvivors", "Testing");
        }
    }
}
