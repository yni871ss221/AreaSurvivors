using System;
using UnityEngine;

namespace AreaSurvivors.Testing
{
    public enum GameplayTestAssertionType
    {
        NoMonitoredObjectStalled,
        AllRequiredObjectsReachTarget,
        EnemyCountAtLeast,
        EnemyCountAtMost,
        ObjectNameExists,
        ObjectNameMissing,
        ObjectHealthAtLeast,
        ObjectHealthAtMost,
        WoodAtLeast,
        StoneAtLeast,
        TokensAtLeast,
        ConfigFloatApproximately,
        AllMonitoredObjectsInsideGrid,
        CameraViewportInsideGrid,
        GameStageEquals,
        WeaponSlashLevelAtLeast,
        WeaponArrowLevelAtLeast,
        WeaponFireballLevelAtLeast
    }

    public enum GameplayConfigValueType
    {
        Float,
        Integer,
        Boolean
    }

    public enum GameplayTestActionType
    {
        MoveObjectToCell,
        DamageObject,
        HealObject,
        SetObjectActive,
        DestroyObject,
        LevelUpSlashWeapon,
        LevelUpArrowWeapon,
        LevelUpFireballWeapon
    }

    [CreateAssetMenu(menuName = "Area Survivors/Testing/Gameplay Test Scenario")]
    public sealed class GameplayTestScenario : ScriptableObject
    {
        [Serializable]
        public sealed class SystemSettings
        {
            public bool buildGrid = true;
            public bool enableGameManager;
            public bool enableEnemySpawner;
            public bool enableNaturalLandmarkSpawner;
            public bool enableBuildPlacement;
            public bool enableScenePlayer;
            public bool enableSceneTower;
            public bool clearExistingEnemies = true;
            public bool clearExistingNaturalLandmarks = true;
        }

        [Serializable]
        public sealed class EnemyPlacement
        {
            public EnemyKind kind = EnemyKind.Boar;
            public Vector2Int cellOffset = new Vector2Int(-8, 0);
            [Min(1)]
            public int count = 1;
            public Vector2Int spacing = Vector2Int.right;
            public bool monitorForStall = true;
            public bool requireReachTarget = true;
        }

        [Serializable]
        public sealed class LandmarkPlacement
        {
            public string landmarkName = "Rock1";
            public Vector2Int cellOffset = new Vector2Int(-4, 0);
            [Min(1)]
            public int count = 1;
            public Vector2Int spacing = Vector2Int.right;
        }

        [Serializable]
        public sealed class PrefabPlacement
        {
            public GameObject prefab;
            public string instanceName;
            public Vector2Int cellOffset;
            public Vector3 worldOffset;
            public Vector3 eulerAngles;
            public Vector3 scale = Vector3.one;
            public bool monitorForStall;
            public bool requireReachTarget;
        }

        [Serializable]
        public sealed class ConfigOverride
        {
            public string fieldName;
            public GameplayConfigValueType valueType;
            public float floatValue;
            public int integerValue;
            public bool booleanValue;
        }

        [Serializable]
        public sealed class ScheduledAction
        {
            [Min(0f)]
            public float atSeconds;
            public GameplayTestActionType type;
            public string objectName;
            public Vector2Int cellOffset;
            public Vector3 worldOffset;
            public int amount;
            public bool active = true;
        }

        [Serializable]
        public sealed class Assertion
        {
            public GameplayTestAssertionType type = GameplayTestAssertionType.NoMonitoredObjectStalled;
            public int expectedCount;
            public float expectedValue;
            [Min(0f)]
            public float tolerance = 0.001f;
            public string objectName;
            public string fieldName;
        }

        [Header("Systems")]
        public SystemSettings systems = new SystemSettings();
        public ConfigOverride[] configOverrides = Array.Empty<ConfigOverride>();

        [Header("Placement")]
        public Vector2Int targetCellOffset;
        public EnemyPlacement[] enemies = Array.Empty<EnemyPlacement>();
        public LandmarkPlacement[] landmarks = Array.Empty<LandmarkPlacement>();
        public PrefabPlacement[] prefabs = Array.Empty<PrefabPlacement>();

        [Header("Timeline")]
        public ScheduledAction[] scheduledActions = Array.Empty<ScheduledAction>();

        [Header("Execution")]
        public bool useFixedRandomSeed = true;
        public int randomSeed = 12345;
        public bool focusCameraOnSetup = true;
        public Vector2Int cameraFocusCellOffset;
        [Min(0.1f)]
        public float simulationTimeScale = 4f;
        [Min(0.1f)]
        public float testDurationSeconds = 8f;
        [Min(0.1f)]
        public float reachDistance = 1.25f;
        [Min(0.1f)]
        public float stallSeconds = 2.5f;
        [Min(0.001f)]
        public float stallMovementThreshold = 0.08f;
        public bool pauseOnComplete;
        public bool autoExitPlayModeOnComplete = true;

        [Header("Assertions")]
        public Assertion[] assertions =
        {
            new Assertion { type = GameplayTestAssertionType.NoMonitoredObjectStalled }
        };
    }
}
