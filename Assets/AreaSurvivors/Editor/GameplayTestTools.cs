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
        const string SelectedScenarioEditorPref = "AreaSurvivors.GameplayTestScenarioPath";
        static bool playModeQueued;

        [MenuItem("Area Survivors/Test Scenarios/Build Gameplay Test Scene")]
        public static void BuildGameplayTestScene()
        {
            EnsureFolder();
            var scenario = EnsureDefaultScenario();
            EnsurePrefabSmokeScenario();
            EnsureActionSmokeScenario();
            EnsureEnemyVisualsScenario();
            EnsureMapPerimeterScenario();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrapObject = new GameObject("Gameplay Test Bootstrap");
            var bootstrap = bootstrapObject.AddComponent<GameplayTestBootstrap>();
            AssignReferences(bootstrap, scenario);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TestScenePath);
            Selection.activeObject = scenario;
            Debug.Log($"Lightweight gameplay test scene built: {TestScenePath}");
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

            UseScenarioAsset(scenario);
        }

        [MenuItem("Area Survivors/Test Scenarios/Run Selected Scenario")]
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

        [MenuItem("Area Survivors/Test Scenarios/Run Current Gameplay Test")]
        public static void RunCurrentGameplayTest()
        {
            QueuePlayMode();
        }

        [MenuItem("Area Survivors/Test Scenarios/Run Samples/Prefab Smoke")]
        public static void RunPrefabSmoke() => RunScenarioAsset(EnsurePrefabSmokeScenario());

        [MenuItem("Area Survivors/Test Scenarios/Run Samples/Navigation Default")]
        public static void RunNavigationDefault() => RunScenarioAsset(EnsureDefaultScenario());

        [MenuItem("Area Survivors/Test Scenarios/Run Samples/Map Perimeter")]
        public static void RunMapPerimeter() => RunScenarioAsset(EnsureMapPerimeterScenario());

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

        static void AssignReferences(GameplayTestBootstrap bootstrap, GameplayTestScenario scenario)
        {
            bootstrap.scenario = scenario;
            bootstrap.config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/AreaSurvivors/Resources/Config/GameConfig.asset");
            bootstrap.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/Enemy.prefab");
            bootstrap.xpOrbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/ExperienceOrb.prefab");
            bootstrap.damagePopupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AreaSurvivors/Prefabs/DamagePopup.prefab");
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
