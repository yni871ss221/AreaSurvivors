using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AreaSurvivors.Testing
{
    public sealed class GameplayTestRunner : MonoBehaviour
    {
        sealed class Observation
        {
            public GameObject instance;
            public bool monitorForStall;
            public bool requireReachTarget;
            public Vector2 samplePosition;
            public float stationarySeconds;
            public bool reached;
            public bool stalled;
        }

        public GameplayTestScenario scenario;
        public GameConfig config;
        public TileGrid grid;
        public NaturalLandmarkSpawner landmarkSpawner;
        public GameObject enemyPrefab;
        public GameObject xpOrbPrefab;
        public GameObject damagePopupPrefab;
        public bool runOnStart = true;

        readonly List<Observation> observations = new List<Observation>();
        readonly HashSet<int> executedActions = new HashSet<int>();
        GameConfig runtimeConfig;
        Transform target;
        Vector3Int centerCell;
        float elapsed;
        bool completed;

        public bool Completed => completed;
        public bool Passed { get; private set; }
        public string ResultSummary { get; private set; }
        public int SpawnedObjectCount { get; private set; }
        public GameConfig RuntimeConfig => runtimeConfig;

        void Awake()
        {
            ApplySystemSettings();
        }

        void Start()
        {
            if (runOnStart) Begin();
        }

        public void Begin()
        {
            if (scenario == null || config == null || grid == null)
            {
                Complete(false, "必要な参照が不足しています。");
                return;
            }

            completed = false;
            Passed = false;
            ResultSummary = string.Empty;
            SpawnedObjectCount = 0;
            elapsed = 0f;
            observations.Clear();
            executedActions.Clear();
            Time.timeScale = Mathf.Max(0.1f, scenario.simulationTimeScale);
            if (scenario.useFixedRandomSeed) UnityEngine.Random.InitState(scenario.randomSeed);

            runtimeConfig = Instantiate(config);
            runtimeConfig.name = $"{config.name} (Gameplay Test)";
            ApplyConfigOverrides();
            if (scenario.systems.buildGrid)
            {
                grid.ApplySquareChunkMapLayout();
                grid.Build();
            }
            ClearExistingObjects();

            centerCell = grid.GridToCell(grid.width / 2, grid.height / 2);
            CreateTarget(centerCell);
            SpawnLandmarks(centerCell);
            SpawnPrefabs(centerCell);
            SpawnEnemies(centerCell);
            FocusCamera(centerCell);

            Debug.Log($"[GameplayTest] START {scenario.name}: objects={SpawnedObjectCount}, observations={observations.Count}");
        }

        void Update()
        {
            if (completed || scenario == null) return;
            elapsed += Time.deltaTime;
            ExecuteScheduledActions();
            UpdateObservations();

            foreach (var assertion in scenario.assertions ?? Array.Empty<GameplayTestScenario.Assertion>())
            {
                if (assertion == null) continue;
                if (TryGetImmediateFailure(assertion, out var failure))
                {
                    Complete(false, failure);
                    return;
                }
            }

            if (elapsed < scenario.testDurationSeconds) return;
            foreach (var assertion in scenario.assertions ?? Array.Empty<GameplayTestScenario.Assertion>())
            {
                if (assertion == null) continue;
                if (!EvaluateAssertion(assertion, out var failure))
                {
                    Complete(false, failure);
                    return;
                }
            }

            Complete(true, BuildSummary());
        }

        void UpdateObservations()
        {
            foreach (var observation in observations)
            {
                if (observation.instance == null)
                {
                    observation.reached = true;
                    continue;
                }

                if (observation.requireReachTarget &&
                    Vector2.Distance(observation.instance.transform.position, target.position) <= scenario.reachDistance)
                {
                    observation.reached = true;
                }

                if (!observation.monitorForStall || observation.reached) continue;
                float movement = Vector2.Distance(observation.samplePosition, observation.instance.transform.position);
                if (movement <= scenario.stallMovementThreshold)
                {
                    observation.stationarySeconds += Time.deltaTime;
                    if (observation.stationarySeconds >= scenario.stallSeconds) observation.stalled = true;
                }
                else
                {
                    observation.samplePosition = observation.instance.transform.position;
                    observation.stationarySeconds = 0f;
                    observation.stalled = false;
                }
            }
        }

        void ExecuteScheduledActions()
        {
            var actions = scenario.scheduledActions ?? Array.Empty<GameplayTestScenario.ScheduledAction>();
            for (int i = 0; i < actions.Length; i++)
            {
                if (executedActions.Contains(i) || actions[i] == null || elapsed < actions[i].atSeconds) continue;
                executedActions.Add(i);
                ExecuteScheduledAction(actions[i]);
            }
        }

        void ExecuteScheduledAction(GameplayTestScenario.ScheduledAction action)
        {
            var targetObject = FindObjectByName(action.objectName);
            if (targetObject == null)
            {
                Debug.LogWarning($"[GameplayTest] Scheduled action target not found: {action.objectName}");
                return;
            }

            switch (action.type)
            {
                case GameplayTestActionType.MoveObjectToCell:
                    targetObject.transform.position = CellOffsetToWorld(centerCell, action.cellOffset) + action.worldOffset;
                    break;
                case GameplayTestActionType.DamageObject:
                    targetObject.GetComponentInChildren<Health>(true)?.Damage(action.amount);
                    break;
                case GameplayTestActionType.HealObject:
                    targetObject.GetComponentInChildren<Health>(true)?.Heal(action.amount);
                    break;
                case GameplayTestActionType.SetObjectActive:
                    targetObject.SetActive(action.active);
                    break;
                case GameplayTestActionType.DestroyObject:
                    Destroy(targetObject);
                    break;
            }
        }

        bool TryGetImmediateFailure(GameplayTestScenario.Assertion assertion, out string failure)
        {
            failure = null;
            if (assertion.type != GameplayTestAssertionType.NoMonitoredObjectStalled) return false;
            int stalled = observations.FindAll(item => item.monitorForStall && item.stalled).Count;
            if (stalled <= 0) return false;
            failure = $"{stalled}個の監視対象が{scenario.stallSeconds:0.0}秒以上停止しました。";
            return true;
        }

        bool EvaluateAssertion(GameplayTestScenario.Assertion assertion, out string failure)
        {
            failure = null;
            int enemyCount = FindObjectsOfType<EnemyController>().Length;
            switch (assertion.type)
            {
                case GameplayTestAssertionType.NoMonitoredObjectStalled:
                    int stalled = observations.FindAll(item => item.monitorForStall && item.stalled).Count;
                    if (stalled == 0) return true;
                    failure = $"停止対象={stalled}";
                    return false;
                case GameplayTestAssertionType.AllRequiredObjectsReachTarget:
                    int required = observations.FindAll(item => item.requireReachTarget).Count;
                    int reached = observations.FindAll(item => item.requireReachTarget && item.reached).Count;
                    if (required == reached) return true;
                    failure = $"目標到達={reached}/{required}";
                    return false;
                case GameplayTestAssertionType.EnemyCountAtLeast:
                    if (enemyCount >= assertion.expectedCount) return true;
                    failure = $"敵数={enemyCount}, 必要最小数={assertion.expectedCount}";
                    return false;
                case GameplayTestAssertionType.EnemyCountAtMost:
                    if (enemyCount <= assertion.expectedCount) return true;
                    failure = $"敵数={enemyCount}, 許容最大数={assertion.expectedCount}";
                    return false;
                case GameplayTestAssertionType.ObjectNameExists:
                    if (FindObjectByName(assertion.objectName) != null) return true;
                    failure = $"オブジェクトが存在しません: {assertion.objectName}";
                    return false;
                case GameplayTestAssertionType.ObjectNameMissing:
                    if (FindObjectByName(assertion.objectName) == null) return true;
                    failure = $"オブジェクトが残っています: {assertion.objectName}";
                    return false;
                case GameplayTestAssertionType.ObjectHealthAtLeast:
                    return EvaluateObjectHealth(assertion, true, out failure);
                case GameplayTestAssertionType.ObjectHealthAtMost:
                    return EvaluateObjectHealth(assertion, false, out failure);
                case GameplayTestAssertionType.WoodAtLeast:
                    return EvaluateResourceCount("Wood", FindGameManager()?.Wood, assertion.expectedCount, out failure);
                case GameplayTestAssertionType.StoneAtLeast:
                    return EvaluateResourceCount("Stone", FindGameManager()?.Stone, assertion.expectedCount, out failure);
                case GameplayTestAssertionType.TokensAtLeast:
                    return EvaluateResourceCount("Tokens", FindGameManager()?.RunTokens, assertion.expectedCount, out failure);
                case GameplayTestAssertionType.ConfigFloatApproximately:
                    return EvaluateConfigFloat(assertion, out failure);
                case GameplayTestAssertionType.AllMonitoredObjectsInsideGrid:
                    return EvaluateMonitoredObjectsInsideGrid(out failure);
                case GameplayTestAssertionType.CameraViewportInsideGrid:
                    return EvaluateCameraViewportInsideGrid(out failure);
                default:
                    return true;
            }
        }

        bool EvaluateCameraViewportInsideGrid(out string failure)
        {
            failure = null;
            var camera = Camera.main;
            if (camera == null || grid == null)
            {
                failure = "Camera or TileGrid was not found.";
                return false;
            }

            Bounds bounds = grid.GetWorldBounds();
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    Ray ray = camera.ViewportPointToRay(new Vector3(x, y, 0f));
                    if (Mathf.Abs(ray.direction.z) <= 0.0001f) continue;
                    Vector3 ground = ray.GetPoint(-ray.origin.z / ray.direction.z);
                    if (ground.x < bounds.min.x - 0.01f || ground.x > bounds.max.x + 0.01f ||
                        ground.y < bounds.min.y - 0.01f || ground.y > bounds.max.y + 0.01f)
                    {
                        failure = $"Camera viewport left the grid at {ground}.";
                        return false;
                    }
                }
            }
            return true;
        }

        bool EvaluateMonitoredObjectsInsideGrid(out string failure)
        {
            failure = null;
            if (grid == null)
            {
                failure = "TileGrid was not found.";
                return false;
            }

            foreach (var observation in observations)
            {
                if (observation.instance == null) continue;
                if (grid.TryWorldToGrid(observation.instance.transform.position, out _, out _)) continue;
                failure = $"{observation.instance.name} left the map at {observation.instance.transform.position}.";
                return false;
            }

            return true;
        }

        bool EvaluateObjectHealth(GameplayTestScenario.Assertion assertion, bool atLeast, out string failure)
        {
            failure = null;
            var targetObject = FindObjectByName(assertion.objectName);
            var health = targetObject != null ? targetObject.GetComponentInChildren<Health>(true) : null;
            if (health == null)
            {
                failure = $"Healthが見つかりません: {assertion.objectName}";
                return false;
            }

            bool passed = atLeast ? health.currentHp >= assertion.expectedCount : health.currentHp <= assertion.expectedCount;
            if (passed) return true;
            failure = $"{assertion.objectName} HP={health.currentHp}, expected {(atLeast ? ">=" : "<=")} {assertion.expectedCount}";
            return false;
        }

        static bool EvaluateResourceCount(string resourceName, int? actual, int expected, out string failure)
        {
            failure = null;
            if (!actual.HasValue)
            {
                failure = "GameManagerが見つかりません";
                return false;
            }
            if (actual.Value >= expected) return true;
            failure = $"{resourceName}={actual.Value}, expected >= {expected}";
            return false;
        }

        bool EvaluateConfigFloat(GameplayTestScenario.Assertion assertion, out string failure)
        {
            failure = null;
            if (runtimeConfig == null)
            {
                failure = "Runtime GameConfigが見つかりません";
                return false;
            }

            var field = typeof(GameConfig).GetField(assertion.fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (field == null || field.FieldType != typeof(float))
            {
                failure = $"float GameConfig fieldが見つかりません: {assertion.fieldName}";
                return false;
            }

            float actual = (float)field.GetValue(runtimeConfig);
            if (Mathf.Abs(actual - assertion.expectedValue) <= assertion.tolerance) return true;
            failure = $"{assertion.fieldName}={actual}, expected {assertion.expectedValue} +/- {assertion.tolerance}";
            return false;
        }

        string BuildSummary()
        {
            int monitored = observations.FindAll(item => item.monitorForStall).Count;
            int stalled = observations.FindAll(item => item.monitorForStall && item.stalled).Count;
            int required = observations.FindAll(item => item.requireReachTarget).Count;
            int reached = observations.FindAll(item => item.requireReachTarget && item.reached).Count;
            return $"時間終了: objects={SpawnedObjectCount}, 到達={reached}/{required}, 停止={stalled}/{monitored}";
        }

        void CreateTarget(Vector3Int center)
        {
            var targetObject = new GameObject("Gameplay Test Target");
            targetObject.transform.SetParent(transform, false);
            targetObject.transform.position = CellOffsetToWorld(center, scenario.targetCellOffset);
            target = targetObject.transform;
        }

        void SpawnLandmarks(Vector3Int center)
        {
            if (landmarkSpawner == null || scenario.landmarks == null) return;
            foreach (var placement in scenario.landmarks)
            {
                if (placement == null) continue;
                for (int i = 0; i < Mathf.Max(1, placement.count); i++)
                {
                    var offset = placement.cellOffset + placement.spacing * i;
                    if (landmarkSpawner.CreateTestLandmark(grid, placement.landmarkName, OffsetCell(center, offset))) SpawnedObjectCount++;
                    else Debug.LogWarning($"[GameplayTest] Landmark could not be placed: {placement.landmarkName} at {offset}");
                }
            }
        }

        void SpawnPrefabs(Vector3Int center)
        {
            foreach (var placement in scenario.prefabs ?? Array.Empty<GameplayTestScenario.PrefabPlacement>())
            {
                if (placement == null || placement.prefab == null) continue;
                var instance = Instantiate(placement.prefab, CellOffsetToWorld(center, placement.cellOffset) + placement.worldOffset, Quaternion.Euler(placement.eulerAngles));
                if (!string.IsNullOrWhiteSpace(placement.instanceName)) instance.name = placement.instanceName;
                instance.transform.localScale = placement.scale;
                RegisterObservation(instance, placement.monitorForStall, placement.requireReachTarget);
                SpawnedObjectCount++;
            }
        }

        void SpawnEnemies(Vector3Int center)
        {
            if (enemyPrefab == null) return;
            foreach (var placement in scenario.enemies ?? Array.Empty<GameplayTestScenario.EnemyPlacement>())
            {
                if (placement == null) continue;
                var definition = runtimeConfig.GetEnemyDefinition(placement.kind);
                if (definition == null) continue;
                for (int i = 0; i < Mathf.Max(1, placement.count); i++)
                {
                    var offset = placement.cellOffset + placement.spacing * i;
                    var instance = Instantiate(enemyPrefab, CellOffsetToWorld(center, offset), Quaternion.identity);
                    instance.name = $"Test {definition.displayName} {i + 1}";
                    var enemy = instance.GetComponent<EnemyController>();
                    if (enemy == null)
                    {
                        Destroy(instance);
                        continue;
                    }

                    enemy.xpOrbPrefab = xpOrbPrefab;
                    enemy.damagePopupPrefab = damagePopupPrefab;
                    int hp = Mathf.Max(1, Mathf.RoundToInt(runtimeConfig.enemyBaseHp * Mathf.Max(0.01f, definition.hpMultiplier)));
                    enemy.Configure(runtimeConfig, grid, target, definition, hp, definition.speedMultiplier);
                    RegisterObservation(instance, placement.monitorForStall, placement.requireReachTarget);
                    SpawnedObjectCount++;
                }
            }
        }

        void RegisterObservation(GameObject instance, bool monitorForStall, bool requireReachTarget)
        {
            if (!monitorForStall && !requireReachTarget) return;
            observations.Add(new Observation
            {
                instance = instance,
                monitorForStall = monitorForStall,
                requireReachTarget = requireReachTarget,
                samplePosition = instance.transform.position
            });
        }

        void ApplyConfigOverrides()
        {
            foreach (var entry in scenario.configOverrides ?? Array.Empty<GameplayTestScenario.ConfigOverride>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.fieldName)) continue;
                var field = typeof(GameConfig).GetField(entry.fieldName, BindingFlags.Instance | BindingFlags.Public);
                if (field == null)
                {
                    Debug.LogWarning($"[GameplayTest] GameConfig field not found: {entry.fieldName}");
                    continue;
                }

                try
                {
                    object value = entry.valueType == GameplayConfigValueType.Integer ? entry.integerValue :
                        entry.valueType == GameplayConfigValueType.Boolean ? entry.booleanValue : entry.floatValue;
                    field.SetValue(runtimeConfig, value);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[GameplayTest] Config override failed: {entry.fieldName}: {exception.Message}");
                }
            }
        }

        void ApplySystemSettings()
        {
            if (scenario == null) return;
            SetEnabled(FindObjectsOfType<GameManager>(true), scenario.systems.enableGameManager);
            SetEnabled(FindObjectsOfType<EnemySpawner>(true), scenario.systems.enableEnemySpawner);
            SetEnabled(FindObjectsOfType<NaturalLandmarkSpawner>(true), scenario.systems.enableNaturalLandmarkSpawner);
            SetEnabled(FindObjectsOfType<BuildPlacementController>(true), scenario.systems.enableBuildPlacement);
            SetActive(FindObjectsOfType<PlayerController>(true), scenario.systems.enableScenePlayer);
            SetActive(FindObjectsOfType<TowerController>(true), scenario.systems.enableSceneTower);
        }

        void ClearExistingObjects()
        {
            if (scenario.systems.clearExistingEnemies)
            {
                foreach (var enemy in FindObjectsOfType<EnemyController>(true)) Destroy(enemy.gameObject);
            }

            if (scenario.systems.clearExistingNaturalLandmarks)
            {
                foreach (var obstacle in FindObjectsOfType<Obstacle>(true)) Destroy(obstacle.gameObject);
            }
        }

        Vector3 CellOffsetToWorld(Vector3Int center, Vector2Int offset)
        {
            return grid.groundTilemap.GetCellCenterWorld(OffsetCell(center, offset));
        }

        static Vector3Int OffsetCell(Vector3Int center, Vector2Int offset)
        {
            return center + new Vector3Int(offset.x, offset.y, 0);
        }

        static GameObject FindObjectByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) return null;
            foreach (var target in FindObjectsOfType<GameObject>(true))
            {
                if (target.name == objectName) return target;
            }
            return null;
        }

        static GameManager FindGameManager()
        {
            if (GameManager.Instance != null) return GameManager.Instance;
            var managers = FindObjectsOfType<GameManager>(true);
            return managers.Length > 0 ? managers[0] : null;
        }

        static void SetEnabled<T>(T[] components, bool enabled) where T : Behaviour
        {
            foreach (var component in components)
            {
                if (component != null) component.enabled = enabled;
            }
        }

        static void SetActive<T>(T[] components, bool active) where T : Component
        {
            foreach (var component in components)
            {
                if (component != null) component.gameObject.SetActive(active);
            }
        }

        void FocusCamera(Vector3Int center)
        {
            if (!scenario.focusCameraOnSetup) return;
            var camera = Camera.main;
            if (camera == null) return;

            var follow = camera.GetComponent<CameraFollow>();
            if (follow != null) follow.enabled = false;
            var focus = CellOffsetToWorld(center, scenario.cameraFocusCellOffset);
            var offset = runtimeConfig != null ? runtimeConfig.cameraOffset : new Vector3(0f, -15.5f, -19f);
            camera.transform.position = focus + offset;
            camera.transform.rotation = Quaternion.Euler(runtimeConfig != null ? runtimeConfig.cameraPitch : -45f, 0f, 0f);
            if (runtimeConfig != null) camera.orthographicSize = runtimeConfig.cameraOrthographicSize;
        }

        void Complete(bool passed, string details)
        {
            if (completed) return;
            completed = true;
            Passed = passed;
            ResultSummary = details;
            Debug.Log($"[GameplayTest] {(passed ? "PASS" : "FAIL")} {scenario?.name}: {details}");
            if (scenario != null && scenario.pauseOnComplete) Debug.Break();
            else
            {
                Time.timeScale = 1f;
#if UNITY_EDITOR
                if (scenario != null && scenario.autoExitPlayModeOnComplete) StartCoroutine(ExitPlayMode());
#endif
            }
        }

#if UNITY_EDITOR
        System.Collections.IEnumerator ExitPlayMode()
        {
            yield return new WaitForSecondsRealtime(0.15f);
            UnityEditor.EditorApplication.isPlaying = false;
        }
#endif

        void OnDrawGizmos()
        {
            if (target == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(target.position, scenario != null ? scenario.reachDistance : 1f);
        }
    }
}
