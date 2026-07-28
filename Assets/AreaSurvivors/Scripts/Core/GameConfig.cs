using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        GoblinLord,
        Skeleton,
        EliteSkeleton,
        SkeletonKnight,
        EliteSkeletonKnight,
        Lich,
        Lizard,
        EliteLizard,
        Lizardman,
        EliteLizardman,
        Dragon
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
    public sealed class WeaponLevelDefinition
    {
        [Range(1, GameConfig.MaxWeaponLevel)]
        public int level = 1;
        public int attackPower = 6;
        public float cooldownSeconds = 1f;
        public float projectileSpeed = 11.5f;
        public float range = 1f;
        public float knockback = 1f;
        public int projectileCount = 1;
        public float explosionRadius;
        public float rotationSpeed;
        public float durationSeconds;
        public float slowAmount;
        public float damageIntervalSeconds;
        public float distance;
    }

    [System.Serializable]
    public sealed class CharacterBaseStatsDefinition
    {
        public int maxHp = 40;
        public float moveSpeed = 2.1f;
        public int paintRadius = 1;
        public float reviveSeconds = 6f;
        public int defense;
        public float xpGainMultiplier = 1f;
        public int autoRegen;

        public static CharacterBaseStatsDefinition Create(
            int maxHp,
            float moveSpeed,
            int paintRadius,
            float reviveSeconds,
            int defense,
            float xpGainMultiplier,
            int autoRegen)
        {
            return new CharacterBaseStatsDefinition
            {
                maxHp = maxHp,
                moveSpeed = moveSpeed,
                paintRadius = paintRadius,
                reviveSeconds = reviveSeconds,
                defense = defense,
                xpGainMultiplier = xpGainMultiplier,
                autoRegen = autoRegen
            };
        }
    }

    [CreateAssetMenu(menuName = "Area Survivors/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        public const int MaxWeaponLevel = 10;

        [Header("Player")]
        public CharacterBaseStatsDefinition knightBaseStats = CharacterBaseStatsDefinition.Create(40, 2.1f, 1, 6f, 3, 1.1f, 0);
        public CharacterBaseStatsDefinition archerBaseStats = CharacterBaseStatsDefinition.Create(30, 2.4f, 1, 6f, 1, 1f, 0);
        public CharacterBaseStatsDefinition mageBaseStats = CharacterBaseStatsDefinition.Create(20, 1.8f, 2, 6f, 0, 1.3f, 1);
        public float playerReviveInvincibleSeconds = 2f;
        public float enemyTerritorySlow = 0.35f;
        public float playerVisualScale = 1f;
        public float moveSpeedPerUpgradeLevel = 0.18f;
        public int paintRadiusLevelsPerBonus = 2;
        public int maxHpPerUpgradeLevel = 5;
        public int playerLevelMaxHpBonus = 10;
        public float playerLevelMoveSpeedBonus = 0.1f;
        public float playerLevelDefenseBonus = 0.5f;
        [Min(1f)] public float xpRequirementGrowthStart = 1.35f;
        [Min(1f)] public float xpRequirementGrowthEnd = 1.1f;
        [Min(2)] public int xpRequirementGrowthStartLevel = 2;
        [Min(3)] public int xpRequirementGrowthEndLevel = 39;
        [Min(0f)] public float xpRequirementFlatBonus = 3f;
        public float reviveSecondsReductionPerUpgradeLevel = 0.7f;
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
        public int towerMaxHp = 200;
        public int towerMaxHpPerUpgradeLevel = 30;
        public float ballistaRange = 9.5f;
        public float ballistaCooldown = 1.15f;
        public int ballistaDamage = 5;
        public int ballistaMaxHp = 150;
        public float towerCannonRange = 10f;
        public float towerCannonCooldown = 3f;
        public int towerCannonDamage = 8;
        public float towerCannonExplosionRadius = 1.25f;
        public float towerCannonProjectileSpeed = 9.5f;
        public float towerCannonProjectileLifetime = 4.2f;
        public float towerCannonProjectileVisualScale = 0.32f;
        public float towerCannonKnockback = 2.2f;
        public int upgradedTowerMaxHp = 900;
        public int upgradedTowerRegenBonus = 3;
        public int upgradedTowerCannonDamageBonus = 10;
        public float upgradedTowerCannonExplosionRadiusMultiplier = 2f;
        public int upgradedTowerImmediatePaintRadiusCells = 15;
        public int woodenWallMaxHp = 100;
        public int watchTowerMaxHp = 150;
        public float watchTowerAutoPaintIntervalSeconds = 2f;
        public int watchTowerAutoPaintRadiusCells = 10;
        public int watchTowerDamage = 1;
        public int upgradedWatchTowerDamageBonus = 3;
        [Header("Combat")]
        public int baseAttackPower = 6;
        public float slashCooldown = 1.05f;
        public float arrowCooldown = 0.75f;
        public float fireballCooldown = 1.45f;
        public float minAttackCooldownMultiplier = 0.45f;
        [Header("Run Weapon Upgrades")]
        [Min(1)]
        public int runAttackPowerBonus = 2;
        [Min(1)] public int runReducedAttackPowerBonus = 1;
        public float runAttackCooldownMultiplier = 0.92f;
        [Min(0f)] public float runAreaRangeBonus = 0.2f;
        [Min(0f)] public float runMediumRangeBonus = 0.375f;
        [Min(0f)] public float runProjectileRangeBonus = 0.75f;
        [Min(0f)] public float runWeaponKnockbackBonus = 0.5f;
        [Min(0f)] public float runExplosionRadiusBonus = 0.375f;
        [Min(1)] public int runProjectileCountBonus = 1;
        [Min(0f)] public float runShieldRotationSpeedBonus = 20f;
        [Range(0f, 1f)] public float runSlowBonus = 0.05f;
        [Min(0f)] public float runArrowRainDurationBonus = 0.4f;
        [Min(0f)] public float runThunderBallDurationBonus = 0.5f;
        public int slashDamageBonus = 2;
        public float slashRange = 1.6f;
        public float slashOffset = 1.05f;
        [Header("Weapon Evolution")]
        [Min(0)] public int swordRushBaseAttackPower = 16;
        [Min(0)] public int goldenBowBaseAttackPower = 16;
        [Min(0)] public int fireMissileBaseAttackPower = 16;
        [Min(0)] public int dualShieldBaseAttackPower = 12;
        [Min(0)] public int goddessBlessingBaseAttackPower = 12;
        [Min(0)] public int bananaBaseAttackPower = 12;
        [Min(0)] public int excaliburBaseAttackPower = 12;
        [Min(0)] public int arrowShowerBaseAttackPower = 10;
        [Min(0)] public int machineGunBaseAttackPower = 80;
        [Min(0)] public int frostStormBaseAttackPower = 5;
        [Min(0)] public int thunderStormBaseAttackPower = 10;
        [Min(0f)] public float swordRushBaseRange = 3.2f;
        [Min(1)] public int swordRushStrikeCount = 5;
        [Min(0f)] public float swordRushStrikeIntervalSeconds = 0.09f;
        [Min(0f)] public float bananaBaseRange = 1.4f;
        [Min(0)] public int bananaBaseProjectileCountBonus = 3;
        [Min(0.1f)] public float excaliburTravelSpeedCellsPerSecond = 10f;
        [Min(0.05f)] public float excaliburDamageIntervalSeconds = 0.2f;
        [Min(0.05f)] public float excaliburCooldownSeconds = 3f;
        [Range(1f, 120f)] public float excaliburBaseArcDegrees = 30f;
        [Range(30f, 170f)] public float excaliburMaxArcDegrees = 150f;
        [Range(0.05f, 1f)] public float excaliburInitialRadiusCells = 0.25f;
        [Min(0.05f)] public float excaliburBandWidthCells = 3f;
        [Min(0.05f)] public float arrowShowerStrikeIntervalSeconds = 0.25f;
        [Min(0.05f)] public float evolvedGroundStrikeRadius = 0.7f;
        [Min(0.1f)] public float evolvedGroundStrikeTargetRadiusCells = 15f;
        [Min(0.05f)] public float machineGunShotIntervalSeconds = 0.2f;
        [Min(0)] public int machineGunBaseAttackCountBonus = 10;
        [Min(0.05f)] public float fireMissileBaseCooldownSeconds = 0.5f;
        [Range(0.05f, 1f)] public float fireMissileProjectileSpeedMultiplier = 0.75f;
        [Range(0f, 360f)] public float fireMissileLaunchArcDegrees = 180f;
        [Min(1f)] public float fireMissileHomingTurnSpeedDegrees = 180f;
        [Min(0)] public int frostStormTargetCount = 5;
        [Min(0)] public int thunderStormOrbitCount = 3;
        [Min(0)] public int goddessBlessingHealAmount = 5;
        public float arrowRangeCells = 10f;
        public float arrowRangeCellsPerLevel = 1f;
        public float fireballExplosionRadius = 1.1f;
        public float fireballFlightCells = 10f;
        public float fireballFlightCellsPerLevel = 1f;
        public float shieldOrbitRadiusCells = 2f;
        public float shieldRotationSpeedDegrees = 90f;
        public float shieldHitCooldownSeconds = 0.35f;
        [Range(0f, 1f)]
        public float weaponSpecialEffectControlThreshold = 0.5f;
        [Min(1f)]
        public float weaponSpecialEffectMultiplier = 2f;
        [Header("Area Control Range Scaling")]
        [Range(0f, 1f)]
        public float areaControlRangeScaleStartRatio = 0.5f;
        [Range(0f, 1f)]
        public float areaControlRangeScaleFullRatio = 1f;
        [Min(1f)]
        public float areaControlRangeScaleMaxMultiplier = 2f;
        [Min(0.1f)]
        public float areaControlRangeEvaluationIntervalSeconds = 1f;
        [Header("Weapon Levels")]
        public WeaponLevelDefinition[] slashWeaponLevels;
        public WeaponLevelDefinition[] arrowWeaponLevels;
        public WeaponLevelDefinition[] fireballWeaponLevels;
        public WeaponLevelDefinition[] shieldWeaponLevels;
        public WeaponLevelDefinition[] flagWeaponLevels;
        public WeaponLevelDefinition[] boomerangSwordWeaponLevels;
        public WeaponLevelDefinition[] auraSwordWeaponLevels;
        public WeaponLevelDefinition[] arrowRainWeaponLevels;
        public WeaponLevelDefinition[] gunWeaponLevels;
        public WeaponLevelDefinition[] frostWeaponLevels;
        public WeaponLevelDefinition[] thunderBallWeaponLevels;
        [Header("Player Advanced Stats")]
        public float baseKnockback = 1f;
        public float knockbackForceUnit = 2.2f;
        public float knockbackDuration = 0.16f;
        public float advancedNormalEnemyKnockbackWeight = 2f;
        public float eliteEnemyKnockbackWeight = 2f;
        public float bossEnemyKnockbackWeight = 10f;
        [Min(1f)] public float bossEnemyCollisionMass = 1000000f;
        public float autoRegenIntervalSeconds = 2f;
        public int defensePerUpgradeLevel = 1;
        public float xpGainMultiplierPerUpgradeLevel = 0.1f;
        public int autoRegenPerUpgradeLevel = 1;
        public int runKnockbackBonus = 1;
        public int runDefenseBonus = 1;
        public float runXpGainMultiplierBonus = 0.1f;
        public int runAutoRegenBonus = 1;
        [Header("Permanent Skill Effects")]
        public float ballistaRangePerUpgradeLevel = 0.75f;
        public int ballistaDamagePerUpgradeLevel = 2;
        public int wallMaxHpPerSkill = 20;
        public int watchTowerRangePerUpgradeLevel = 2;
        public int watchTowerDamagePerUpgradeLevel = 1;
        public int buildingAutoRegenPerUpgradeLevel = 1;
        public float enemyTerritorySlowReductionPerUpgradeLevel = 0.1f;
        public int towerAutoRegenPerUpgradeLevel = 1;
        public int roundEndTokenReward = 3;
        public int roundEndTokenRewardPerUpgradeLevel = 1;
        public int eliteTimedSpawnCountPerUpgradeLevel = 1;
        public float baseRoundTimeLimitSeconds = 60f;
        public float projectileSpeed = 11.5f;

        public CharacterBaseStatsDefinition GetCharacterBaseStats(CharacterType type)
        {
            CharacterBaseStatsDefinition selected;
            switch (type)
            {
                case CharacterType.Archer:
                    selected = archerBaseStats;
                    break;
                case CharacterType.Mage:
                    selected = mageBaseStats;
                    break;
                default:
                    selected = knightBaseStats;
                    break;
            }

            return selected ?? knightBaseStats ?? CharacterBaseStatsDefinition.Create(40, 2.1f, 1, 6f, 3, 1.1f, 0);
        }
        public float projectileLifetime = 4.2f;
        public float projectileVisualScale = 1.35f;

        [Header("Enemies")]
        public float enemyBaseSpeed = 0.9f;
        [Min(0f)] public float enemyMoveSpeedBonusPerStage = 0.2f;
        [Min(0f)] public float normalEnemyPlayerAggroRangeCells = 5f;
        public float enemyVisualScale = 1f;
        public int enemyBaseHp = 14;
        public int enemyDamage = 3;
        public float spawnInterval = 1.8f;
        public float enemySpawnRadius = 28f;
        public float difficultyRampSeconds = 55f;
        public float playerTerritorySlow = 0.35f;
        public float spawnDirectionChangeSeconds = 10f;
        [Range(1f, 180f)]
        public float spawnDirectionArcDegrees = 60f;
        public int maxAliveEnemies = 160;
        public float bossTimeSeconds = 300f;
        public string bossAnnouncement = "オークキング出現！";
        public float bossSpecialAttackCooldownSeconds = 5f;
        public float bossSpecialAttackRaiseSeconds = 0.5f;
        public float bossSpecialAttackSlamSeconds = 0.35f;
        public float bossSpecialAttackRecoverSeconds = 0.15f;
        public float bossShockwaveRangeCells = 10f;
        public int bossShockwaveSegmentCount = 5;
        public float bossShockwaveStepIntervalSeconds = 0.12f;
        public float bossShockwaveDisplaySeconds = 1f;
        public Vector2 bossShockwaveSizeCells = new Vector2(2f, 2f);
        public int bossShockwaveDamageMultiplier = 1;
        [Range(0f, 1f)] public float stageTransitionFlashPeakAlpha = 0.92f;
        [Min(0.01f)] public float stageTransitionFlashInSeconds = 0.05f;
        [Min(0f)] public float stageTransitionFlashHoldSeconds = 0.06f;
        [Min(0.01f)] public float stageTransitionFlashOutSeconds = 0.2f;
        [Min(0f)] public float stageTransitionEnemyHitDelaySeconds = 0.24f;
        [Min(0.5f)] public float stageTransitionEnemyDefeatTimeoutSeconds = 1.2f;
        public float goblinLordDarkOrbSpeed = 2.4f;
        public float goblinLordDarkOrbLifetimeSeconds = 8f;
        public float goblinLordDarkOrbDamageRadius = 1.25f;
        public float goblinLordDarkOrbDamageIntervalSeconds = 0.45f;
        [Range(0.1f, 2f)]
        public float goblinLordDarkOrbDamageMultiplier = 0.5f;
        public float goblinLordDarkOrbVisualScale = 1f;
        public float lichSummonRadius = 4f;
        public float lichSummonCircleDurationSeconds = 2.2f;
        public int lichSummonSkeletonCount = 10;
        public int lichSummonSkeletonKnightCount = 10;
        public float dragonBreathMouthClosedSeconds = 0.55f;
        public float dragonBreathMouthOpenSeconds = 0.32f;
        public float dragonBreathProjectileSpeed = 4.2f;
        public float dragonBreathRangeCells = 15f;
        public Vector2 dragonBreathHitboxSizeCells = new Vector2(3f, 3f);
        public float dragonBreathExplosionRadiusCells = 3f;
        [Range(0.1f, 2f)]
        public float dragonBreathDamageMultiplier = 0.75f;
        public float dragonBreathProjectileVisualScale = 1f;
        public float dragonBreathExplosionDurationSeconds = 0.28f;
        public EnemyDefinition[] enemyDefinitions;

        [Header("Progression")]
        public int xpPerEnemy = 1;
        public int tokenKillsDivisor = 10;

        public void EnsureWeaponLevelDefaults()
        {
            slashWeaponLevels = EnsureWeaponLevels(
                slashWeaponLevels,
                WeaponType.Slash,
                slashCooldown,
                0f,
                slashRange,
                baseKnockback,
                0f);
            arrowWeaponLevels = EnsureWeaponLevels(
                arrowWeaponLevels,
                WeaponType.Arrow,
                arrowCooldown,
                projectileSpeed,
                ArrowRangeWorld(1, TileGrid.DefaultCellSize),
                baseKnockback,
                0f);
            fireballWeaponLevels = EnsureWeaponLevels(
                fireballWeaponLevels,
                WeaponType.Fireball,
                fireballCooldown,
                projectileSpeed,
                FireballFlightRangeWorld(1, TileGrid.DefaultCellSize),
                baseKnockback,
                fireballExplosionRadius);
            shieldWeaponLevels = EnsureWeaponLevels(
                shieldWeaponLevels,
                WeaponType.Shield,
                1f,
                0f,
                ShieldOrbitRadiusWorld(TileGrid.DefaultCellSize),
                baseKnockback,
                0f);
            flagWeaponLevels = EnsureWeaponLevels(
                flagWeaponLevels,
                WeaponType.Flag,
                1f,
                0f,
                3f * TileGrid.DefaultCellSize,
                0f,
                0f);
            boomerangSwordWeaponLevels = EnsureWeaponLevels(
                boomerangSwordWeaponLevels,
                WeaponType.BoomerangSword,
                1.6f,
                projectileSpeed,
                1f * TileGrid.DefaultCellSize,
                baseKnockback,
                0f);
            auraSwordWeaponLevels = EnsureWeaponLevels(
                auraSwordWeaponLevels,
                WeaponType.AuraSword,
                1.35f,
                projectileSpeed,
                3f * TileGrid.DefaultCellSize,
                baseKnockback,
                0f);
            arrowRainWeaponLevels = EnsureWeaponLevels(
                arrowRainWeaponLevels,
                WeaponType.ArrowRain,
                4f,
                0f,
                3f * TileGrid.DefaultCellSize,
                0f,
                0f);
            gunWeaponLevels = EnsureWeaponLevels(
                gunWeaponLevels,
                WeaponType.Gun,
                3f,
                projectileSpeed * 1.5f,
                15f * TileGrid.DefaultCellSize,
                0f,
                0f);
            frostWeaponLevels = EnsureWeaponLevels(
                frostWeaponLevels,
                WeaponType.Frost,
                2f,
                0f,
                3f * TileGrid.DefaultCellSize,
                0f,
                0f);
            thunderBallWeaponLevels = EnsureWeaponLevels(
                thunderBallWeaponLevels,
                WeaponType.ThunderBall,
                3f,
                projectileSpeed * 0.35f,
                2f * TileGrid.DefaultCellSize,
                0f,
                0f);
        }

        public WeaponStatBlock GetWeaponStats(WeaponType type, int level)
        {
            EnsureWeaponLevelDefaults();
            var source = WeaponLevelsFor(type);
            int index = Mathf.Clamp(level, 1, MaxWeaponLevel) - 1;
            var definition = source[index];
            return new WeaponStatBlock
            {
                level = definition.level,
                attackPower = definition.attackPower,
                cooldownSeconds = Mathf.Max(0.05f, definition.cooldownSeconds),
                projectileSpeed = Mathf.Max(0f, definition.projectileSpeed),
                range = Mathf.Max(0f, definition.range),
                knockback = Mathf.Max(0f, definition.knockback),
                projectileCount = Mathf.Max(1, definition.projectileCount),
                explosionRadius = Mathf.Max(0f, definition.explosionRadius),
                rotationSpeed = Mathf.Max(0f, definition.rotationSpeed),
                durationSeconds = Mathf.Max(0f, definition.durationSeconds),
                slowAmount = Mathf.Clamp01(definition.slowAmount),
                damageIntervalSeconds = Mathf.Max(0.05f, definition.damageIntervalSeconds),
                distance = Mathf.Max(0f, definition.distance)
            };
        }

        public int GetRunAttackPowerBonus(WeaponType type)
        {
            type = WeaponCatalog.BaseWeaponOf(type);
            switch (type)
            {
                case WeaponType.Shield:
                case WeaponType.Flag:
                case WeaponType.AuraSword:
                case WeaponType.ThunderBall:
                    return Mathf.Max(1, runReducedAttackPowerBonus);
                default:
                    return Mathf.Max(1, runAttackPowerBonus);
            }
        }

        WeaponLevelDefinition[] WeaponLevelsFor(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Arrow: return arrowWeaponLevels;
                case WeaponType.Fireball: return fireballWeaponLevels;
                case WeaponType.Shield: return shieldWeaponLevels;
                case WeaponType.Flag: return flagWeaponLevels;
                case WeaponType.BoomerangSword: return boomerangSwordWeaponLevels;
                case WeaponType.AuraSword: return auraSwordWeaponLevels;
                case WeaponType.ArrowRain: return arrowRainWeaponLevels;
                case WeaponType.Gun: return gunWeaponLevels;
                case WeaponType.Frost: return frostWeaponLevels;
                case WeaponType.ThunderBall: return thunderBallWeaponLevels;
                default: return slashWeaponLevels;
            }
        }

        WeaponLevelDefinition[] EnsureWeaponLevels(
            WeaponLevelDefinition[] source,
            WeaponType type,
            float baseCooldown,
            float baseProjectileSpeed,
            float baseRange,
            float baseKnockbackValue,
            float baseExplosionRadius)
        {
            var result = new WeaponLevelDefinition[MaxWeaponLevel];
            for (int i = 0; i < MaxWeaponLevel; i++)
            {
                var existing = FindWeaponLevel(source, i + 1);
                result[i] = existing ?? CreateDefaultWeaponLevel(
                    i + 1,
                    type,
                    baseCooldown,
                    baseProjectileSpeed,
                    baseRange,
                    baseKnockbackValue,
                    baseExplosionRadius);
                NormalizeWeaponLevel(result[i], type, baseProjectileSpeed, baseExplosionRadius);
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
            WeaponType type,
            float baseCooldown,
            float baseProjectileSpeed,
            float baseRange,
            float baseKnockbackValue,
            float baseExplosionRadius)
        {
            int bonusLevel = Mathf.Max(0, level - 1);
            if (IsAdvancedWeapon(type))
            {
                return CreateAdvancedWeaponLevel(level, type, baseCooldown, baseProjectileSpeed, baseRange, baseKnockbackValue);
            }

            bool usesProjectile = type == WeaponType.Arrow || type == WeaponType.Fireball;
            return new WeaponLevelDefinition
            {
                level = level,
                attackPower = baseAttackPower + bonusLevel,
                cooldownSeconds = Mathf.Max(0.05f, baseCooldown * Mathf.Max(minAttackCooldownMultiplier, 1f - bonusLevel * 0.06f)),
                projectileSpeed = usesProjectile ? baseProjectileSpeed + bonusLevel * 0.25f : 0f,
                range = type == WeaponType.Fireball
                    ? FireballFlightRangeWorld(level, TileGrid.DefaultCellSize)
                    : type == WeaponType.Arrow
                        ? ArrowRangeWorld(level, TileGrid.DefaultCellSize)
                        : type == WeaponType.Shield
                            ? ShieldOrbitRadiusWorld(TileGrid.DefaultCellSize)
                            : baseRange + bonusLevel * 0.08f,
                knockback = type == WeaponType.Slash || type == WeaponType.Shield ? baseKnockbackValue + bonusLevel : 0f,
                projectileCount = type == WeaponType.Arrow ? 1 + bonusLevel / 3 : type == WeaponType.Shield ? 3 : 1,
                explosionRadius = type == WeaponType.Fireball ? baseExplosionRadius + bonusLevel * 0.25f : 0f,
                rotationSpeed = type == WeaponType.Shield ? shieldRotationSpeedDegrees + bonusLevel * 10f : 0f
            };
        }

        WeaponLevelDefinition CreateAdvancedWeaponLevel(
            int level,
            WeaponType type,
            float baseCooldown,
            float baseProjectileSpeed,
            float baseRange,
            float baseKnockbackValue)
        {
            int bonusLevel = Mathf.Max(0, level - 1);
            var definition = new WeaponLevelDefinition
            {
                level = level,
                attackPower = baseAttackPower + bonusLevel,
                cooldownSeconds = Mathf.Max(0.05f, baseCooldown * Mathf.Max(minAttackCooldownMultiplier, 1f - bonusLevel * 0.05f)),
                projectileSpeed = baseProjectileSpeed,
                range = Mathf.Max(0.05f, baseRange),
                knockback = baseKnockbackValue,
                projectileCount = 1,
                durationSeconds = 0f,
                slowAmount = 0f,
                damageIntervalSeconds = 0.5f,
                distance = 0f
            };

            switch (type)
            {
                case WeaponType.Flag:
                    definition.attackPower = 3 + bonusLevel;
                    definition.range = 1.7f + bonusLevel * 0.1f;
                    definition.slowAmount = 0.3f;
                    definition.damageIntervalSeconds = Mathf.Max(0.2f, 1f - bonusLevel * 0.04f);
                    break;
                case WeaponType.BoomerangSword:
                    definition.attackPower = 7 + bonusLevel;
                    definition.projectileCount = 1 + bonusLevel / 3;
                    definition.range = (1f + bonusLevel * 0.08f) * TileGrid.DefaultCellSize;
                    definition.distance = (8f + bonusLevel * 0.4f) * TileGrid.DefaultCellSize;
                    definition.knockback = baseKnockback + bonusLevel * 0.35f;
                    break;
                case WeaponType.AuraSword:
                    definition.attackPower = 6 + bonusLevel;
                    definition.projectileCount = 1 + bonusLevel / 3;
                    definition.range = (3f + bonusLevel * 0.2f) * TileGrid.DefaultCellSize;
                    definition.distance = (10f + bonusLevel * 0.5f) * TileGrid.DefaultCellSize;
                    definition.knockback = baseKnockback + bonusLevel * 0.25f;
                    break;
                case WeaponType.ArrowRain:
                    definition.attackPower = 5 + bonusLevel;
                    definition.range = (3f + bonusLevel * 0.2f) * TileGrid.DefaultCellSize;
                    definition.distance = 10f * TileGrid.DefaultCellSize;
                    definition.durationSeconds = 3f + bonusLevel * 0.12f;
                    definition.damageIntervalSeconds = 0.35f;
                    break;
                case WeaponType.Gun:
                    definition.attackPower = 50 + bonusLevel * 2;
                    definition.projectileCount = 1 + bonusLevel / 4;
                    definition.range = (15f + bonusLevel * 0.5f) * TileGrid.DefaultCellSize;
                    definition.distance = definition.range;
                    break;
                case WeaponType.Frost:
                    definition.attackPower = 3 + bonusLevel;
                    definition.range = 1.7f + bonusLevel * 0.1f;
                    definition.distance = 5f * TileGrid.DefaultCellSize;
                    definition.durationSeconds = 2.4f + bonusLevel * 0.1f;
                    definition.slowAmount = 0.3f;
                    definition.damageIntervalSeconds = Mathf.Max(0.25f, baseCooldown * 0.5f);
                    break;
                case WeaponType.ThunderBall:
                    definition.attackPower = 3 + bonusLevel;
                    definition.projectileCount = 1 + bonusLevel / 3;
                    definition.range = 1f + bonusLevel * 0.1f;
                    definition.durationSeconds = 5f + bonusLevel * 0.2f;
                    definition.damageIntervalSeconds = 0.45f;
                    break;
            }

            return definition;
        }

        static bool IsAdvancedWeapon(WeaponType type)
        {
            return type == WeaponType.Flag ||
                type == WeaponType.BoomerangSword ||
                type == WeaponType.AuraSword ||
                type == WeaponType.ArrowRain ||
                type == WeaponType.Gun ||
                type == WeaponType.Frost ||
                type == WeaponType.ThunderBall;
        }

        void NormalizeWeaponLevel(WeaponLevelDefinition definition, WeaponType type, float baseProjectileSpeed, float baseExplosionRadius)
        {
            if (definition == null) return;
            int bonusLevel = Mathf.Max(0, definition.level - 1);
            if (type == WeaponType.Arrow)
            {
                float defaultRange = ArrowRangeWorld(definition.level, TileGrid.DefaultCellSize);
                if (definition.range <= 0f || definition.range > defaultRange + TileGrid.DefaultCellSize * 2f)
                {
                    definition.range = defaultRange;
                }

                if (definition.projectileCount <= 0) definition.projectileCount = 1 + bonusLevel / 3;
                definition.explosionRadius = 0f;
                definition.knockback = 0f;
                return;
            }

            if (type == WeaponType.Fireball)
            {
                float defaultFlightRange = FireballFlightRangeWorld(definition.level, TileGrid.DefaultCellSize);
                if (definition.explosionRadius <= 0f)
                {
                    definition.explosionRadius = baseExplosionRadius + bonusLevel * 0.25f;
                }

                if (definition.range <= definition.explosionRadius + 0.001f || definition.range > defaultFlightRange + 0.001f)
                {
                    definition.range = defaultFlightRange;
                }

                if (definition.projectileCount <= 0) definition.projectileCount = 1;
                definition.knockback = 0f;
                return;
            }

            if (type == WeaponType.Shield)
            {
                definition.cooldownSeconds = 1f;
                definition.projectileSpeed = 0f;
                definition.range = ShieldOrbitRadiusWorld(TileGrid.DefaultCellSize);
                if (definition.projectileCount <= 0) definition.projectileCount = 3;
                if (definition.knockback <= 0f) definition.knockback = baseKnockback + bonusLevel;
                definition.explosionRadius = 0f;
                if (definition.rotationSpeed <= 0f) definition.rotationSpeed = shieldRotationSpeedDegrees + bonusLevel * 10f;
                return;
            }

            if (IsAdvancedWeapon(type))
            {
                if (definition.range <= 0f) definition.range = TileGrid.DefaultCellSize;
                if (definition.cooldownSeconds <= 0f) definition.cooldownSeconds = 1f;
                if (definition.projectileCount <= 0) definition.projectileCount = 1;
                if (definition.damageIntervalSeconds <= 0f) definition.damageIntervalSeconds = 0.5f;
                definition.slowAmount = Mathf.Clamp01(definition.slowAmount);
                return;
            }

            definition.projectileCount = 1;
            definition.explosionRadius = 0f;
        }

        public float FireballFlightRangeWorld(int level, float cellSize)
        {
            int bonusLevel = Mathf.Max(0, level - 1);
            float cells = fireballFlightCells + bonusLevel * Mathf.Max(0f, fireballFlightCellsPerLevel);
            return Mathf.Max(0.05f, cells * Mathf.Max(0.01f, cellSize));
        }

        public float ArrowRangeWorld(int level, float cellSize)
        {
            int bonusLevel = Mathf.Max(0, level - 1);
            float cells = arrowRangeCells + bonusLevel * Mathf.Max(0f, arrowRangeCellsPerLevel);
            return Mathf.Max(0.05f, cells * Mathf.Max(0.01f, cellSize));
        }

        public float ShieldOrbitRadiusWorld(float cellSize)
        {
            return Mathf.Max(0.05f, Mathf.Max(0f, shieldOrbitRadiusCells) * Mathf.Max(0.01f, cellSize));
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
                    damageMultiplier = 2f,
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
                    damageMultiplier = 4f,
                    speedMultiplier = 0.95f,
                    cellSize = 1.5f,
                    xpValue = Mathf.Max(5, xpPerEnemy * 5),
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
                    damageMultiplier = 3f,
                    speedMultiplier = 0.82f,
                    cellSize = 2f,
                    xpValue = Mathf.Max(2, xpPerEnemy * 2),
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
                    damageMultiplier = 6f,
                    speedMultiplier = 0.76f,
                    cellSize = 2.5f,
                    xpValue = Mathf.Max(10, xpPerEnemy * 10),
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
                    hpMultiplier = 80f,
                    damageMultiplier = 8f,
                    speedMultiplier = 0.31f,
                    cellSize = 4f,
                    xpValue = 80,
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
                    hpMultiplier = 4f,
                    damageMultiplier = 4f,
                    speedMultiplier = 1f,
                    cellSize = 1f,
                    xpValue = Mathf.Max(4, xpPerEnemy * 4),
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
                    hpMultiplier = 20f,
                    damageMultiplier = 8f,
                    speedMultiplier = 0.95f,
                    cellSize = 1.5f,
                    xpValue = Mathf.Max(20, xpPerEnemy * 20),
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
                    hpMultiplier = 8f,
                    damageMultiplier = 5f,
                    speedMultiplier = 0.82f,
                    cellSize = 2f,
                    xpValue = Mathf.Max(8, xpPerEnemy * 8),
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
                    hpMultiplier = 40f,
                    damageMultiplier = 10f,
                    speedMultiplier = 0.76f,
                    cellSize = 2.5f,
                    xpValue = Mathf.Max(40, xpPerEnemy * 40),
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
                    hpMultiplier = 320f,
                    damageMultiplier = 16f,
                    speedMultiplier = 0.31f,
                    cellSize = 4f,
                    xpValue = 320,
                    tokenValue = 5,
                    boss = true,
                    outlineColor = new Color(1f, 0.08f, 0.04f, 1f),
                    outlineThickness = 0.075f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.Skeleton,
                    displayName = "スケルトン",
                    spriteKey = "EnemySkeleton",
                    animationSpeedMultiplier = 0.45f,
                    hpMultiplier = 12f,
                    damageMultiplier = 7f,
                    speedMultiplier = 1.02f,
                    cellSize = 1f,
                    xpValue = Mathf.Max(12, xpPerEnemy * 12),
                    tokenValue = 0,
                    outlineColor = Color.black,
                    outlineThickness = 0.018f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.EliteSkeleton,
                    displayName = "エリートスケルトン",
                    spriteKey = "EnemySkeleton",
                    animationSpeedMultiplier = 0.45f,
                    hpMultiplier = 60f,
                    damageMultiplier = 14f,
                    speedMultiplier = 0.95f,
                    cellSize = 1.5f,
                    xpValue = Mathf.Max(60, xpPerEnemy * 60),
                    tokenValue = 1,
                    elite = true,
                    outlineColor = new Color(1f, 0.86f, 0.12f, 1f),
                    outlineThickness = 0.055f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.SkeletonKnight,
                    displayName = "スケルトンナイト",
                    spriteKey = "EnemySkeletonKnight",
                    animationSpeedMultiplier = 0.45f,
                    hpMultiplier = 18f,
                    damageMultiplier = 8f,
                    speedMultiplier = 0.8f,
                    cellSize = 2f,
                    xpValue = Mathf.Max(18, xpPerEnemy * 18),
                    tokenValue = 0,
                    outlineColor = Color.black,
                    outlineThickness = 0.02f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.EliteSkeletonKnight,
                    displayName = "エリートスケルトンナイト",
                    spriteKey = "EnemySkeletonKnight",
                    animationSpeedMultiplier = 0.45f,
                    hpMultiplier = 90f,
                    damageMultiplier = 16f,
                    speedMultiplier = 0.74f,
                    cellSize = 2.5f,
                    xpValue = Mathf.Max(90, xpPerEnemy * 90),
                    tokenValue = 1,
                    elite = true,
                    outlineColor = new Color(1f, 0.86f, 0.12f, 1f),
                    outlineThickness = 0.055f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.Lich,
                    displayName = "リッチ",
                    spriteKey = "EnemyLich",
                    animationSpeedMultiplier = 0.45f,
                    hpMultiplier = 640f,
                    damageMultiplier = 24f,
                    speedMultiplier = 0.31f,
                    cellSize = 4f,
                    xpValue = 640,
                    tokenValue = 7,
                    boss = true,
                    outlineColor = new Color(1f, 0.08f, 0.04f, 1f),
                    outlineThickness = 0.075f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.Lizard,
                    displayName = "リザード",
                    spriteKey = "EnemyLizard",
                    animationSpeedMultiplier = 0.45f,
                    hpMultiplier = 27f,
                    damageMultiplier = 26f / 3f,
                    speedMultiplier = 1f,
                    cellSize = 1f,
                    xpValue = Mathf.Max(27, xpPerEnemy * 27),
                    tokenValue = 0,
                    outlineColor = Color.black,
                    outlineThickness = 0.018f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.EliteLizard,
                    displayName = "エリートリザード",
                    spriteKey = "EnemyLizard",
                    animationSpeedMultiplier = 0.45f,
                    hpMultiplier = 135f,
                    damageMultiplier = 52f / 3f,
                    speedMultiplier = 0.94f,
                    cellSize = 1.5f,
                    xpValue = Mathf.Max(135, xpPerEnemy * 135),
                    tokenValue = 1,
                    elite = true,
                    outlineColor = new Color(1f, 0.86f, 0.12f, 1f),
                    outlineThickness = 0.055f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.Lizardman,
                    displayName = "リザードマン",
                    spriteKey = "EnemyLizardman",
                    animationSpeedMultiplier = 0.45f,
                    hpMultiplier = 40.5f,
                    damageMultiplier = 32f / 3f,
                    speedMultiplier = 0.78f,
                    cellSize = 2f,
                    xpValue = Mathf.Max(41, xpPerEnemy * 41),
                    tokenValue = 0,
                    outlineColor = Color.black,
                    outlineThickness = 0.02f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.EliteLizardman,
                    displayName = "エリートリザードマン",
                    spriteKey = "EnemyLizardman",
                    animationSpeedMultiplier = 0.45f,
                    hpMultiplier = 202.5f,
                    damageMultiplier = 64f / 3f,
                    speedMultiplier = 0.72f,
                    cellSize = 2.5f,
                    xpValue = Mathf.Max(203, xpPerEnemy * 203),
                    tokenValue = 1,
                    elite = true,
                    outlineColor = new Color(1f, 0.86f, 0.12f, 1f),
                    outlineThickness = 0.055f
                },
                new EnemyDefinition
                {
                    kind = EnemyKind.Dragon,
                    displayName = "ドラゴン",
                    spriteKey = "EnemyDragon",
                    animationSpeedMultiplier = 0.45f,
                    hpMultiplier = 1280f,
                    damageMultiplier = 32f,
                    speedMultiplier = 0.31f,
                    cellSize = 4f,
                    xpValue = 1280,
                    tokenValue = 10,
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
