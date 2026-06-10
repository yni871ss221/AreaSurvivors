using UnityEngine;

namespace AreaSurvivors
{
    public enum EnemyKind
    {
        Boar,
        EliteBoar,
        Orc,
        EliteOrc,
        OrcKing
    }

    [System.Serializable]
    public sealed class EnemyDefinition
    {
        public EnemyKind kind = EnemyKind.Boar;
        public string displayName = "イノシシ";
        public string spriteKey = "EnemyBoar";
        [Min(0.1f)]
        public float animationSpeedMultiplier = 1f;
        public float hpMultiplier = 1f;
        public float damageMultiplier = 1f;
        public float speedMultiplier = 1f;
        public float cellSize = 1f;
        public int xpValue = 1;
        public int tokenValue;
        public bool elite;
        public bool boss;
        public Color outlineColor = Color.black;
        public float outlineThickness = 0.018f;
    }

    [System.Serializable]
    public sealed class SpawnPhase
    {
        public float startSeconds;
        public EnemyKind enemyKind = EnemyKind.Boar;
        public float spawnInterval = 1.8f;
        public int baseBatchCount = 1;
        public int batchIncreasePerDirectionChange = 1;
        public int maxBatchCount = 12;
    }

    [System.Serializable]
    public sealed class TimedEnemySpawn
    {
        public float timeSeconds;
        public EnemyKind enemyKind;
        public int count = 1;
        public bool announce;
        public string announcement;
    }

    [CreateAssetMenu(menuName = "Area Survivors/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Player")]
        public float playerMoveSpeed = 2.1f;
        public int playerMaxHp = 40;
        public float playerReviveSeconds = 6f;
        public float enemyTerritorySlow = 0.35f;
        public int paintRadius = 1;
        public float playerVisualScale = 1f;
        public float moveSpeedPerUpgradeLevel = 0.18f;
        public int paintRadiusLevelsPerBonus = 2;
        public int maxHpPerUpgradeLevel = 5;
        public float reviveSecondsReductionPerUpgradeLevel = 0.35f;
        public float minReviveSeconds = 1f;
        public float runMoveSpeedMultiplier = 1.08f;
        public int runPaintRadiusBonus = 1;
        public int runMaxHpBonus = 8;

        [Header("Camera")]
        public float cameraOrthographicSize = 12.5f;
        public Vector3 cameraOffset = new Vector3(0f, -15.5f, -19f);
        public float cameraPitch = -45f;
        public float cameraZoomedInOrthographicSize = 3.9f;
        public Vector3 cameraZoomedInOffset = new Vector3(0f, -8.5f, -9f);
        public float cameraZoomedInPitch = -35f;
        [Range(0f, 1f)]
        public float cameraDefaultZoom = 0.5f;
        public float cameraZoomScrollSpeed = 0.16f;
        [Range(0f, 1f)]
        public float cameraPlayerWeight = 0.55f;

        [Header("Tower")]
        public int towerMaxHp = 160;
        public int towerMaxHpPerUpgradeLevel = 12;
        public float ballistaBuildSeconds = 2.2f;
        public float ballistaRange = 9.5f;
        public float ballistaCooldown = 1.15f;
        public int ballistaDamage = 5;
        public int ballistaMaxHp = 90;
        public float fenceBuildSeconds = 1.8f;
        public int fenceMaxHp = 70;
        public int startingBallistaStock = 4;
        public int startingFenceStock = 4;

        [Header("Resources")]
        public int startingWood = 100;
        public int startingStone = 100;
        public int startingWoodPerUpgradeLevel = 25;
        public int startingStonePerUpgradeLevel = 25;
        public int fenceWoodCost = 10;
        public int fenceStoneCost = 0;
        public int ballistaWoodCost = 50;
        public int ballistaStoneCost = 30;
        public float harvestIntervalSeconds = 1f;
        public int harvestAmountPerTick = 2;
        public int harvestAmount1Cell = 100;
        public int harvestAmount2Cell = 200;
        public int harvestAmount4Cell = 400;
        public int harvestAmount8Cell = 800;
        public float woodcuttingSpeedPerUpgradeLevel = 0.1f;
        public int woodcuttingGainPerUpgradeLevel = 1;
        public float miningSpeedPerUpgradeLevel = 0.1f;
        public int miningGainPerUpgradeLevel = 1;

        [Header("Combat")]
        public int baseAttackPower = 6;
        public float knightCooldown = 1.05f;
        public float archerCooldown = 0.75f;
        public float mageCooldown = 1.45f;
        public int attackPowerPerUpgradeLevel = 1;
        public float attackCooldownReductionPerUpgradeLevel = 0.06f;
        public float minAttackCooldownMultiplier = 0.45f;
        public int runAttackPowerBonus = 2;
        public float runAttackCooldownMultiplier = 0.92f;
        public int knightDamageBonus = 2;
        public float knightSlashRange = 1.05f;
        public float knightSlashOffset = 1.05f;
        public float mageExplosionRadius = 1.1f;
        [Header("Player Advanced Stats")]
        public float baseKnockback = 1f;
        public float knockbackForceUnit = 2.2f;
        public float knockbackDuration = 0.16f;
        public int baseDefense = 0;
        public float baseXpGainMultiplier = 1f;
        public int baseAutoRegen = 0;
        public float autoRegenIntervalSeconds = 2f;
        public float baseWorkSpeedMultiplier = 1f;
        public int baseResourceGainBonus = 0;
        public float knockbackPerUpgradeLevel = 1f;
        public int defensePerUpgradeLevel = 1;
        public float xpGainMultiplierPerUpgradeLevel = 0.1f;
        public int autoRegenPerUpgradeLevel = 1;
        public float workSpeedMultiplierPerUpgradeLevel = 0.1f;
        public int resourceGainPerUpgradeLevel = 1;
        public int runKnockbackBonus = 1;
        public int runDefenseBonus = 1;
        public float runXpGainMultiplierBonus = 0.1f;
        public int runAutoRegenBonus = 1;
        public float runWorkSpeedMultiplierBonus = 0.1f;
        public int runResourceGainBonus = 1;
        [Header("Permanent Skill Effects")]
        public float ballistaRangePerUpgradeLevel = 0.75f;
        public int initialTerritoryRadiusPerUpgradeLevel = 1;
        public int towerAutoRegenPerUpgradeLevel = 1;
        public float endTokenGainMultiplierPerUpgradeLevel = 0.1f;
        public float eliteSpawnRatePerUpgradeLevel = 0.1f;
        public float autoBuildSpeedPerUpgradeLevel = 0.1f;
        public float autoWoodcuttingSpeedPerUpgradeLevel = 0.1f;
        public float autoMiningSpeedPerUpgradeLevel = 0.1f;
        public float projectileSpeed = 11.5f;
        public float projectileLifetime = 4.2f;
        public float projectileVisualScale = 1.35f;

        [Header("Enemies")]
        public float enemyBaseSpeed = 0.9f;
        public float enemyVisualScale = 1f;
        public int enemyBaseHp = 14;
        public int enemyDamage = 3;
        public float spawnInterval = 1.8f;
        public float enemySpawnRadius = 28f;
        public float difficultyRampSeconds = 55f;
        public float playerTerritorySlow = 0.35f;
        public float spawnDirectionChangeSeconds = 30f;
        [Range(1f, 180f)]
        public float spawnDirectionArcDegrees = 60f;
        public int maxAliveEnemies = 160;
        public float bossTimeSeconds = 300f;
        public string bossAnnouncement = "オークキング出現！";
        public EnemyDefinition[] enemyDefinitions;
        public SpawnPhase[] spawnPhases;
        public TimedEnemySpawn[] timedEnemySpawns;

        [Header("Progression")]
        public int xpPerEnemy = 1;
        public int tokenKillsDivisor = 8;

        public void EnsureEnemySpawnDefaults()
        {
            if (enemyDefinitions == null || enemyDefinitions.Length == 0)
            {
                enemyDefinitions = new[]
                {
                    new EnemyDefinition
                    {
                        kind = EnemyKind.Boar,
                        displayName = "イノシシ",
                        spriteKey = "EnemyBoar",
                        hpMultiplier = 1f,
                        damageMultiplier = 1f,
                        speedMultiplier = 1f,
                        cellSize = 1f,
                        xpValue = Mathf.Max(1, xpPerEnemy),
                        tokenValue = 0,
                        outlineColor = Color.black,
                        outlineThickness = 0.018f
                    },
                    new EnemyDefinition
                    {
                        kind = EnemyKind.EliteBoar,
                        displayName = "エリートイノシシ",
                        spriteKey = "EnemyBoar",
                        hpMultiplier = 5f,
                        damageMultiplier = 2f,
                        speedMultiplier = 0.95f,
                        cellSize = 1.5f,
                        xpValue = Mathf.Max(5, xpPerEnemy * 6),
                        tokenValue = 1,
                        elite = true,
                        outlineColor = new Color(1f, 0.86f, 0.12f, 1f),
                        outlineThickness = 0.055f
                    },
                    new EnemyDefinition
                    {
                        kind = EnemyKind.Orc,
                        displayName = "オーク",
                        spriteKey = "EnemyOrc",
                        animationSpeedMultiplier = 0.5f,
                        hpMultiplier = 2f,
                        damageMultiplier = 2f,
                        speedMultiplier = 0.82f,
                        cellSize = 2f,
                        xpValue = Mathf.Max(3, xpPerEnemy * 3),
                        tokenValue = 0,
                        outlineColor = Color.black,
                        outlineThickness = 0.02f
                    },
                    new EnemyDefinition
                    {
                        kind = EnemyKind.EliteOrc,
                        displayName = "エリートオーク",
                        spriteKey = "EnemyOrc",
                        animationSpeedMultiplier = 0.5f,
                        hpMultiplier = 10f,
                        damageMultiplier = 4f,
                        speedMultiplier = 0.76f,
                        cellSize = 2.5f,
                        xpValue = Mathf.Max(12, xpPerEnemy * 12),
                        tokenValue = 1,
                        elite = true,
                        outlineColor = new Color(1f, 0.86f, 0.12f, 1f),
                        outlineThickness = 0.055f
                    },
                    new EnemyDefinition
                    {
                        kind = EnemyKind.OrcKing,
                        displayName = "オークキング",
                        spriteKey = "EnemyOrcKing",
                        animationSpeedMultiplier = 0.5f,
                        hpMultiplier = 40f,
                        damageMultiplier = 8f,
                        speedMultiplier = 0.62f,
                        cellSize = 4f,
                        xpValue = 50,
                        tokenValue = 3,
                        boss = true,
                        outlineColor = new Color(1f, 0.08f, 0.04f, 1f),
                        outlineThickness = 0.075f
                    }
                };
            }

            if (spawnPhases == null || spawnPhases.Length == 0)
            {
                spawnPhases = new[]
                {
                    new SpawnPhase { startSeconds = 0f, enemyKind = EnemyKind.Boar, spawnInterval = spawnInterval, baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 10 },
                    new SpawnPhase { startSeconds = 150f, enemyKind = EnemyKind.Orc, spawnInterval = Mathf.Max(0.5f, spawnInterval * 1.05f), baseBatchCount = 1, batchIncreasePerDirectionChange = 1, maxBatchCount = 14 }
                };
            }

            if (timedEnemySpawns == null || timedEnemySpawns.Length == 0)
            {
                timedEnemySpawns = new[]
                {
                    new TimedEnemySpawn { timeSeconds = 120f, enemyKind = EnemyKind.EliteBoar, count = 1, announce = true, announcement = "エリートイノシシ出現！" },
                    new TimedEnemySpawn { timeSeconds = 270f, enemyKind = EnemyKind.EliteOrc, count = 1, announce = true, announcement = "エリートオーク出現！" },
                    new TimedEnemySpawn { timeSeconds = 300f, enemyKind = EnemyKind.OrcKing, count = 1, announce = true, announcement = "オークキング出現！" }
                };
            }
        }

        public EnemyDefinition GetEnemyDefinition(EnemyKind kind)
        {
            EnsureEnemySpawnDefaults();
            foreach (var definition in enemyDefinitions)
            {
                if (definition != null && definition.kind == kind) return definition;
            }
            return enemyDefinitions != null && enemyDefinitions.Length > 0 ? enemyDefinitions[0] : null;
        }
    }
}
