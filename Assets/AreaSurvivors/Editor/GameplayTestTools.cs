using AreaSurvivors.Testing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class GameplayTestTools
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string TestScenePath = "Assets/AreaSurvivors/Scenes/90_GameplayTest.unity";
        const string TestFolder = "Assets/AreaSurvivors/Testing";
        const string DefaultScenarioPath = TestFolder + "/Gameplay_Navigation_Default.asset";
        const string PrefabSmokeScenarioPath = TestFolder + "/Gameplay_Prefab_Smoke.asset";
        const string ActionSmokeScenarioPath = TestFolder + "/Gameplay_Action_Smoke.asset";
        const string EnemyVisualsScenarioPath = TestFolder + "/Gameplay_Enemy_Visuals.asset";
        const string MapPerimeterScenarioPath = TestFolder + "/Gameplay_Map_Perimeter.asset";

        [MenuItem("Area Survivors/Test Scenarios/Build Gameplay Test Scene")]
        public static void BuildGameplayTestScene()
        {
            EnsureFolder();
            var scenario = EnsureDefaultScenario();
            EnsurePrefabSmokeScenario();
            EnsureActionSmokeScenario();
            EnsureEnemyVisualsScenario();
            EnsureMapPerimeterScenario();

            if (!AssetDatabase.CopyAsset(GameScenePath, TestScenePath) && AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null)
            {
                Debug.LogError("Gameplay test scene could not be copied.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(TestScenePath);
            var runnerObject = GameObject.Find("Gameplay Test Runner") ?? GameObject.Find("Navigation Test Runner");
            if (runnerObject == null) runnerObject = new GameObject("Gameplay Test Runner");
            runnerObject.name = "Gameplay Test Runner";

            var runner = runnerObject.GetComponent<GameplayTestRunner>();
            if (runner == null) runner = runnerObject.AddComponent<GameplayTestRunner>();
            AssignReferences(runner, scenario);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = scenario;
            Debug.Log($"Gameplay test scene built: {TestScenePath}");
        }

        [MenuItem("Area Survivors/Test Scenarios/Open Gameplay Test")]
        public static void OpenGameplayTest()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath) == null) BuildGameplayTestScene();
            else EditorSceneManager.OpenScene(TestScenePath);
        }

        [MenuItem("Area Survivors/Test Scenarios/Use Selected Scenario")]
        public static void UseSelectedScenario()
        {
            var scenario = Selection.activeObject as GameplayTestScenario;
            if (scenario == null)
            {
                Debug.LogWarning("GameplayTestScenario assetを選択してください。");
                return;
            }

            OpenGameplayTest();
            var runner = Object.FindObjectOfType<GameplayTestRunner>();
            if (runner == null)
            {
                BuildGameplayTestScene();
                runner = Object.FindObjectOfType<GameplayTestRunner>();
            }

            AssignReferences(runner, scenario);
            EditorUtility.SetDirty(runner);
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
            EditorSceneManager.SaveScene(runner.gameObject.scene);
            Debug.Log($"Gameplay test scenario selected: {scenario.name}");
        }

        [MenuItem("Area Survivors/Test Scenarios/Run Selected Scenario")]
        public static void RunSelectedScenario()
        {
            UseSelectedScenario();
            if (Object.FindObjectOfType<GameplayTestRunner>() == null) return;
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        [MenuItem("Area Survivors/Test Scenarios/Run Current Gameplay Test")]
        public static void RunCurrentGameplayTest()
        {
            OpenGameplayTest();
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        [MenuItem("Area Survivors/Test Scenarios/Samples/Use Navigation Default")]
        public static void UseNavigationDefault()
        {
            UseScenarioAsset(EnsureDefaultScenario());
        }

        [MenuItem("Area Survivors/Test Scenarios/Samples/Use Prefab Smoke")]
        public static void UsePrefabSmoke()
        {
            UseScenarioAsset(EnsurePrefabSmokeScenario());
        }

        [MenuItem("Area Survivors/Test Scenarios/Samples/Use Action Smoke")]
        public static void UseActionSmoke()
        {
            UseScenarioAsset(EnsureActionSmokeScenario());
        }

        [MenuItem("Area Survivors/Test Scenarios/Samples/Use Enemy Visuals")]
        public static void UseEnemyVisuals()
        {
            UseScenarioAsset(EnsureEnemyVisualsScenario());
        }

        [MenuItem("Area Survivors/Test Scenarios/Samples/Use Map Perimeter")]
        public static void UseMapPerimeter()
        {
            UseScenarioAsset(EnsureMapPerimeterScenario());
        }

        [MenuItem("Area Survivors/Test Scenarios/Create New Gameplay Scenario")]
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

        static void AssignReferences(GameplayTestRunner runner, GameplayTestScenario scenario)
        {
            runner.scenario = scenario;
            runner.config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/AreaSurvivors/Resources/Config/GameConfig.asset");
            runner.grid = Object.FindObjectOfType<TileGrid>();
            runner.landmarkSpawner = Object.FindObjectOfType<NaturalLandmarkSpawner>();
            runner.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/Enemy.prefab");
            runner.xpOrbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/ExperienceOrb.prefab");
            runner.damagePopupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/DamagePopup.prefab");
        }

        public static void UseScenarioAsset(GameplayTestScenario scenario)
        {
            OpenGameplayTest();
            var runner = Object.FindObjectOfType<GameplayTestRunner>();
            if (runner == null)
            {
                BuildGameplayTestScene();
                runner = Object.FindObjectOfType<GameplayTestRunner>();
            }
            AssignReferences(runner, scenario);
            EditorUtility.SetDirty(runner);
            EditorSceneManager.MarkSceneDirty(runner.gameObject.scene);
            EditorSceneManager.SaveScene(runner.gameObject.scene);
            Selection.activeObject = scenario;
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
            scenario.landmarks = new[]
            {
                new GameplayTestScenario.LandmarkPlacement { landmarkName = "Rock4", cellOffset = new Vector2Int(-7, 0) },
                new GameplayTestScenario.LandmarkPlacement { landmarkName = "Rock1", cellOffset = new Vector2Int(-3, 3) },
                new GameplayTestScenario.LandmarkPlacement { landmarkName = "Tree1", cellOffset = new Vector2Int(-3, -3) }
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
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/ExperienceOrb.prefab"),
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
            scenario.targetCellOffset = new Vector2Int(60, 0);
            scenario.enemies = new[]
            {
                new GameplayTestScenario.EnemyPlacement
                {
                    kind = EnemyKind.Boar,
                    cellOffset = new Vector2Int(44, 0),
                    monitorForStall = true,
                    requireReachTarget = false,
                    suppressStuckRecovery = true
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
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/ExperienceOrb.prefab"),
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

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(TestFolder)) AssetDatabase.CreateFolder("Assets/AreaSurvivors", "Testing");
        }
    }
}
