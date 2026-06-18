using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public enum EnemyKind
    {
        Boar,
        EliteBoar,
        Orc,
        EliteOrc,
        OrcKing,
        Goblin,
        EliteGoblin,
        Ogre,
        EliteOgre,
        GoblinLord
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

    [System.Serializable]
    public sealed class WeaponLevelDefinition
    {
        [Range(1, GameConfig.MaxWeaponLevel)]
        public int level = 1;
        public int attackPower = 6;
        public float cooldownSeconds = 1f;
        public float projectileSpeed = 11.5f;
        public float range = 1f;
        public float knockback = 1f;
    }

    [CreateAssetMenu(menuName = "Area Survivors/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        public const int MaxWeaponLevel = 10;

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
        public float cameraPitch = -35f;
        public float cameraZoomedInOrthographicSize = 3.9f;
        public Vector3 cameraZoomedInOffset = new Vector3(0f, -8.5f, -9f);
        public float cameraZoomedInPitch = -45f;
        [Range(0f, 1f)]
        public float cameraDefaultZoom = 0.5f;
        public float cameraZoomScrollSpeed = 0.16f;
        [Range(0f, 1f)]
        public float cameraPlayerWeight = 1f;
        public bool cameraUseGridBounds = true;
        public Vector2 cameraMinimumPosition = new Vector2(-25f, -25f);
        public Vector2 cameraMaximumPosition = new Vector2(25f, 25f);
        [Min(0f)]
        public float cameraBoundsPadding = 0.25f;

        [Header("Tower")]
        public int towerMaxHp = 160;
        public int towerMaxHpPerUpgradeLevel = 12;
        public float ballistaBuildSeconds = 2.2f;
        public float ballistaRange = 9.5f;
        public float ballistaCooldown = 1.15f;
        public int ballistaDamage = 5;
        public int ballistaMaxHp = 90;
        public float towerCannonRange = 10f;
        public float towerCannonCooldown = 3f;
        public int towerCannonDamage = 8;
        public float towerCannonExplosionRadius = 1.25f;
        public float towerCannonProjectileSpeed = 9.5f;
        public float towerCannonProjectileLifetime = 4.2f;
        public float towerCannonProjectileVisualScale = 0.32f;
        public float towerCannonKnockback = 2.2f;
        public int towerUpgradeWoodCost = 300;
        public int towerUpgradeStoneCost = 300;
        public float towerUpgradeBuildSeconds = 5f;
        public int upgradedTowerMaxHp = 450;
        public int upgradedTowerRegenBonus = 3;
        public int upgradedTowerCannonDamageBonus = 10;
        public float upgradedTowerCannonExplosionRadiusMultiplier = 2f;
        public int upgradedTowerImmediatePaintRadiusCells = 15;
        public float woodenWallBuildSeconds = 1.8f;
        public int woodenWallMaxHp = 70;
        public float carpenterHutBuildSeconds = 2.4f;
        public int carpenterHutMaxHp = 50;
        public float carpenterHutAutoBuildSpeedMultiplier = 0.1f;
        public float workerHutBuildSeconds = 2.4f;
        public int workerHutMaxHp = 50;
        public float watchTowerBuildSeconds = 3.2f;
        public int watchTowerMaxHp = 100;
        public float watchTowerAutoPaintIntervalSeconds = 2f;
        public int watchTowerAutoPaintRadiusCells = 10;
        public int startingBallistaStock = 4;
        public int startingWallStock = 4;

        [Header("Resources")]
        public int startingWood = 100;
        public int startingStone = 100;
        public int startingWoodPerUpgradeLevel = 25;
        public int startingStonePerUpgradeLevel = 25;
        public int woodenWallWoodCost = 10;
        public int woodenWallStoneCost = 0;
        public int woodenGateWoodCost = 20;
        public int woodenGateStoneCost = 0;
        public int ballistaWoodCost = 50;
        public int ballistaStoneCost = 30;
        public int carpenterHutWoodCost = 30;
        public int carpenterHutStoneCost = 20;
        public int workerHutWoodCost = 30;
        public int workerHutStoneCost = 20;
        public int watchTowerWoodCost = 50;
        public int watchTowerStoneCost = 50;
        public float harvestIntervalSeconds = 1f;
        public int harvestAmountPerTick = 2;
        public float workerHutAutoGatherBaseIntervalSeconds = 5f;
        public float workerHutAutoGatherIntervalReductionPerLevel = 1f;
        public int workerHutAutoGatherBaseAmount = 1;
        public int workerHutAutoGatherAmountPerLevel = 1;
        public int harvestAmount1Cell = 100;
        public int harvestAmount2Cell = 200;
        public int harvestAmount4Cell = 400;
        public int harvestAmount8Cell = 800;

        [Header("Combat")]
        public int baseAttackPower = 6;
        public float knightCooldown = 1.05f;
        public float archerCooldown = 0.75f;
        public float mageCooldown = 1.45f;
        public float minAttackCooldownMultiplier = 0.45f;
        public int runAttackPowerBonus = 2;
        public float runAttackCooldownMultiplier = 0.92f;
        public int knightDamageBonus = 2;
        public float knightSlashRange = 1.05f;
        public float knightSlashOffset = 1.05f;
        public float mageExplosionRadius = 1.1f;
        [Header("Weapon Levels")]
        public WeaponLevelDefinition[] knightWeaponLevels;
        public WeaponLevelDefinition[] archerWeaponLevels;
        public WeaponLevelDefinition[] mageWeaponLevels;
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
        public int towerAutoRegenPerUpgradeLevel = 1;
        public float endTokenGainMultiplierPerUpgradeLevel = 0.1f;
        public float eliteSpawnRatePerUpgradeLevel = 0.1f;
        public float autoBuildSpeedPerUpgradeLevel = 0.1f;
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

        public void EnsureWeaponLevelDefaults()
        {
            knightWeaponLevels = EnsureWeaponLevels(
                knightWeaponLevels,
                knightCooldown,
                0f,
                knightSlashRange,
                baseKnockback,
                false);
            archerWeaponLevels = EnsureWeaponLevels(
                archerWeaponLevels,
                archerCooldown,
                projectileSpeed,
                projectileSpeed * projectileLifetime,
                baseKnockback,
                true);
            mageWeaponLevels = EnsureWeaponLevels(
                mageWeaponLevels,
                mageCooldown,
                projectileSpeed,
                mageExplosionRadius,
                baseKnockback,
                true);
        }

        public WeaponStatBlock GetWeaponStats(CharacterType type, int level)
        {
            EnsureWeaponLevelDefaults();
            var source = type == CharacterType.Archer
                ? archerWeaponLevels
                : type == CharacterType.Mage
                    ? mageWeaponLevels
                    : knightWeaponLevels;
            int index = Mathf.Clamp(level, 1, MaxWeaponLevel) - 1;
            var definition = source[index];
            return new WeaponStatBlock
            {
                level = definition.level,
                attackPower = definition.attackPower,
                cooldownSeconds = Mathf.Max(0.05f, definition.cooldownSeconds),
                projectileSpeed = Mathf.Max(0f, definition.projectileSpeed),
                range = Mathf.Max(0f, definition.range),
                knockback = Mathf.Max(0f, definition.knockback)
            };
        }

        WeaponLevelDefinition[] EnsureWeaponLevels(
            WeaponLevelDefinition[] source,
            float baseCooldown,
            float baseProjectileSpeed,
            float baseRange,
            float baseKnockbackValue,
            bool usesProjectile)
        {
            var result = new WeaponLevelDefinition[MaxWeaponLevel];
            for (int i = 0; i < MaxWeaponLevel; i++)
            {
                var existing = FindWeaponLevel(source, i + 1);
                result[i] = existing ?? CreateDefaultWeaponLevel(
                    i + 1,
                    baseCooldown,
                    baseProjectileSpeed,
                    baseRange,
                    baseKnockbackValue,
                    usesProjectile);
                result[i].level = i + 1;
            }

            return result;
        }

        static WeaponLevelDefinition FindWeaponLevel(WeaponLevelDefinition[] source, int level)
        {
            if (source == null) return null;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null && source[i].level == level) return source[i];
            }
            return null;
        }

        WeaponLevelDefinition CreateDefaultWeaponLevel(
            int level,
            float baseCooldown,
            float baseProjectileSpeed,
            float baseRange,
            float baseKnockbackValue,
            bool usesProjectile)
        {
            int bonusLevel = Mathf.Max(0, level - 1);
            return new WeaponLevelDefinition
            {
                level = level,
                attackPower = baseAttackPower + bonusLevel,
                cooldownSeconds = Mathf.Max(0.05f, baseCooldown * Mathf.Max(minAttackCooldownMultiplier, 1f - bonusLevel * 0.06f)),
                projectileSpeed = usesProjectile ? baseProjectileSpeed + bonusLevel * 0.25f : 0f,
                range = baseRange + bonusLevel * (usesProjectile ? 0.75f : 0.08f),
                knockback = baseKnockbackValue + bonusLevel
            };
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            EnsureWeaponLevelDefaults();
        }
#endif

        public void EnsureEnemySpawnDefaults()
        {
            var defaultDefinitions = DefaultEnemyDefinitions();
            if (enemyDefinitions == null || enemyDefinitions.Length == 0)
            {
                enemyDefinitions = defaultDefinitions;
            }
            else
            {
                var merged = new List<EnemyDefinition>(enemyDefinitions);
                foreach (var defaultDefinition in defaultDefinitions)
                {
                    bool exists = false;
                    foreach (var definition in merged)
                    {
                        if (definition != null && definition.kind == defaultDefinition.kind)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists) merged.Add(defaultDefinition);
                }

                enemyDefinitions = merged.ToArray();
            }

            ApplyStageTwoAnimationSpeedDefaults(defaultDefinitions);

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
            foreach (var definition in DefaultEnemyDefinitions())
            {
                if (definition != null && definition.kind == kind) return definition;
            }
            return enemyDefinitions != null && enemyDefinitions.Length > 0 ? enemyDefinitions[0] : null;
        }

        EnemyDefinition[] DefaultEnemyDefinitions()
        {
            return new[]
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
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.Goblin,
                    displayName = "ゴブリン",
                    spriteKey = "EnemyGoblin",
                    animationSpeedMultiplier = 0.35f,
                    hpMultiplier = 2f,
                    damageMultiplier = 2f,
                    speedMultiplier = 1f,
                    cellSize = 1f,
                    xpValue = Mathf.Max(2, xpPerEnemy * 2),
                    tokenValue = 0,
                    outlineColor = Color.black,
                    outlineThickness = 0.018f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.EliteGoblin,
                    displayName = "エリートゴブリン",
                    spriteKey = "EnemyGoblin",
                    animationSpeedMultiplier = 0.35f,
                    hpMultiplier = 10f,
                    damageMultiplier = 4f,
                    speedMultiplier = 0.95f,
                    cellSize = 1.5f,
                    xpValue = Mathf.Max(10, xpPerEnemy * 10),
                    tokenValue = 1,
                    elite = true,
                    outlineColor = new Color(1f, 0.86f, 0.12f, 1f),
                    outlineThickness = 0.055f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.Ogre,
                    displayName = "オーガ",
                    spriteKey = "EnemyOgre",
                    animationSpeedMultiplier = 0.35f,
                    hpMultiplier = 4f,
                    damageMultiplier = 4f,
                    speedMultiplier = 0.82f,
                    cellSize = 2f,
                    xpValue = Mathf.Max(6, xpPerEnemy * 6),
                    tokenValue = 0,
                    outlineColor = Color.black,
                    outlineThickness = 0.02f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.EliteOgre,
                    displayName = "エリートオーガ",
                    spriteKey = "EnemyOgre",
                    animationSpeedMultiplier = 0.35f,
                    hpMultiplier = 20f,
                    damageMultiplier = 8f,
                    speedMultiplier = 0.76f,
                    cellSize = 2.5f,
                    xpValue = Mathf.Max(24, xpPerEnemy * 24),
                    tokenValue = 1,
                    elite = true,
                    outlineColor = new Color(1f, 0.86f, 0.12f, 1f),
                    outlineThickness = 0.055f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.GoblinLord,
                    displayName = "ゴブリンロード",
                    spriteKey = "EnemyGoblinLord",
                    animationSpeedMultiplier = 0.35f,
                    hpMultiplier = 80f,
                    damageMultiplier = 16f,
                    speedMultiplier = 0.62f,
                    cellSize = 4f,
                    xpValue = 100,
                    tokenValue = 5,
                    boss = true,
                    outlineColor = new Color(1f, 0.08f, 0.04f, 1f),
                    outlineThickness = 0.075f
                }
            };
        }

        void ApplyStageTwoAnimationSpeedDefaults(EnemyDefinition[] defaults)
        {
            if (enemyDefinitions == null || defaults == null) return;
            foreach (var definition in enemyDefinitions)
            {
                if (definition == null || !IsStageTwoEnemy(definition.kind)) continue;
                foreach (var defaultDefinition in defaults)
                {
                    if (defaultDefinition != null && defaultDefinition.kind == definition.kind)
                    {
                        definition.animationSpeedMultiplier = defaultDefinition.animationSpeedMultiplier;
                        break;
                    }
                }
            }
        }

        static bool IsStageTwoEnemy(EnemyKind kind)
        {
            return kind == EnemyKind.Goblin ||
                kind == EnemyKind.EliteGoblin ||
                kind == EnemyKind.Ogre ||
                kind == EnemyKind.EliteOgre ||
                kind == EnemyKind.GoblinLord;
        }
    }
}
