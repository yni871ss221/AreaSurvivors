using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameConfig config;
        public TileGrid grid;
        public PlayerController playerPrefab;
        public TowerController sceneTower;
        public EnemySpawner spawner;
        public BuildPlacementController buildPlacement;
        public BuildingUpgradeController buildingUpgrade;
        public GameHudController gameHud;
        public Text timerText;
        public Text killText;
        public Text levelText;
        public Slider xpBar;
        public GameObject levelUpPanel;
        public Button[] upgradeButtons;

        public PlayerController Player { get; private set; }
        public TowerController Tower { get; private set; }
        public int CurrentStage => currentStage;
        public int CurrentLevel => level;
        public int CurrentXp => xp;
        public int XpToNext => xpToNext;
        public int Wood { get; private set; }
        public int Stone { get; private set; }
        public int RunTokens { get; private set; }
        public MapSessionMode SessionMode => sessionMode;

        int kills;
        int level = 1;
        int xp;
        int xpToNext = 5;
        int damageDealt;
        float elapsed;
        float hudElapsed;
        float xpRemainder;
        int currentStage = 1;
        float currentStageSpeedMultiplier = 1f;
        MapSessionMode sessionMode = MapSessionMode.Game;
        bool bossActive;
        bool gameEnding;
        GameObject levelUpInputBlocker;
        readonly List<string> runUpgrades = new List<string>();
        const int InitialTowerTerritoryRadius = 10;
        static readonly Color UpgradeNormalColor = new Color(0.12f, 0.20f, 0.16f, 0.94f);
        static readonly Color UpgradeHoverColor = new Color(0.106f, 0.353f, 0.216f, 0.98f);
        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Time.timeScale = 1f;
            AudioManager.PlayBgm(BgmTrack.GameNormal);

            sessionMode = MapSessionMode.Game;
            config = Instantiate(config);
            config.EnsureEnemySpawnDefaults();
            config.EnsureWeaponLevelDefaults();
            Wood = sessionMode == MapSessionMode.Build ? ProgressionStore.Data.wood : 0;
            Stone = sessionMode == MapSessionMode.Build ? ProgressionStore.Data.stone : 0;
            if (grid != null)
            {
                grid.ApplySquareChunkMapLayout();
                grid.Build();
                RebuildMapPerimeter();
            }

            Tower = sceneTower != null ? sceneTower : FindObjectOfType<TowerController>();
            if (Tower == null)
            {
                Debug.LogError("GameManager requires a scene TowerController.");
                enabled = false;
                return;
            }

            var towerMarker = Tower.GetComponent<GridObjectMarker>();
            var towerOriginCell = grid.GridToCell(grid.width / 2, grid.height / 2);
            Tower.AlignToGridFootprint(grid, towerOriginCell);
            Tower.Configure(config.towerMaxHp + ProgressionStore.GetLevel(UpgradeType.TowerMaxHp) * config.towerMaxHpPerUpgradeLevel);
            ConfigureTowerRegeneration();
            ConfigureTowerCannon();
            if (Tower.hpBar != null) Tower.hpBar.gameObject.SetActive(false);
            if (towerMarker != null) towerMarker.Register(grid);
            var towerRootWorld = GridObjectVisual.FootprintOriginToWorld(grid, towerOriginCell);
            grid.PaintImmediate(towerRootWorld, TileOwner.Player, InitialTowerTerritoryRadius);
            ApplyUnlockedCenterTowerUpgrade();
            int stage = RunState.ConsumeNextStartStage();
            SyncFixedBuildingSlots(stage);

            if (sessionMode == MapSessionMode.Game)
            {
                Player = Instantiate(playerPrefab, grid.GridToWorld(grid.width / 2, grid.height / 2 - 6), Quaternion.identity);
                if (spawner != null) Player.damagePopupPrefab = spawner.damagePopupPrefab;
                Player.Configure(config, grid, CharacterType.Knight);
            }

            if (sessionMode == MapSessionMode.Game) ProgressionStore.ReviveStageBuildings(stage);
            if (buildPlacement != null)
            {
                if (spawner != null) buildPlacement.damagePopupPrefab = spawner.damagePopupPrefab;
                buildPlacement.Initialize(config, grid, sessionMode == MapSessionMode.Build ? null : Player);
            }
            if (buildPlacement != null) buildPlacement.RestoreStageBuildings(stage);
            PolishHud();
            ConfigureGameHud();

            var cameraFollow = Camera.main.GetComponent<CameraFollow>();
            if (cameraFollow != null && sessionMode == MapSessionMode.Game && Player != null) cameraFollow.Configure(Player.transform, Tower.transform, config);
            BeginStage(stage);
            UpdateHud();
        }

        void RebuildMapPerimeter()
        {
            var perimeter = FindObjectOfType<MapPerimeterController>();
            if (perimeter == null || grid == null) return;
            perimeter.grid = grid;
            perimeter.Rebuild();
        }

        void ConfigureTowerRegeneration()
        {
            if (Tower == null) return;
            var regeneration = Tower.GetComponent<AutoRegeneration>();
            if (regeneration == null) regeneration = Tower.gameObject.AddComponent<AutoRegeneration>();
            regeneration.amount = ProgressionStore.GetLevel(UpgradeType.TowerAutoRegen) * config.towerAutoRegenPerUpgradeLevel;
            regeneration.intervalSeconds = config.autoRegenIntervalSeconds;
            regeneration.popupPrefab = spawner != null ? spawner.damagePopupPrefab : null;
            regeneration.popupOffset = new Vector3(0f, 0.7f, 0f);
        }

        void ConfigureTowerCannon()
        {
            if (Tower == null) return;
            var cannon = Tower.GetComponent<TowerCannonController>();
            if (cannon == null) cannon = Tower.gameObject.AddComponent<TowerCannonController>();
            cannon.Configure(config);
        }

        void ApplyUnlockedCenterTowerUpgrade()
        {
            if (Tower == null || !ProgressionStore.IsUnlocked(UpgradeType.UnlockTowerUpgrade)) return;
            Tower.CompleteUpgrade(config, grid, Tower.GetConfiguredUpgradeSprite());
        }

        void SyncFixedBuildingSlots(int stage)
        {
            if (grid == null) return;
            var fixedBuildings = BuildFixedStageBuildings(stage);
            ProgressionStore.ReplaceStageBuildings(stage, fixedBuildings);
        }

        sealed class FixedBuildingSlotDefinition
        {
            public SavedBuildingKind kind;
            public UpgradeType unlockType;
            public UpgradeType fixedSlotUpgradeType;
            public Vector2Int footprint;
            public Vector2Int desiredOffset;
            public bool requiresUnlock = true;
            public bool requiresPlayerTerritory = true;
            public bool upgradesWithFixedSlotSkill;
        }

        const int FixedLayoutTowerCenterColumn = 13;
        const int FixedLayoutTowerCenterRow = 13;

        static readonly FixedBuildingSlotDefinition[] FixedBuildingSlotDefinitions = BuildFixedBuildingSlotDefinitions();

        static FixedBuildingSlotDefinition[] BuildFixedBuildingSlotDefinitions()
        {
            var slots = new List<FixedBuildingSlotDefinition>();

            AddWallLine(slots, 6, 6, 11, 6);
            AddWallLine(slots, 6, 7, 6, 11);
            AddWallLine(slots, 15, 6, 20, 6);
            AddWallLine(slots, 20, 7, 20, 11);
            AddWallLine(slots, 6, 15, 6, 20);
            AddWallLine(slots, 7, 20, 11, 20);
            AddWallLine(slots, 20, 15, 20, 20);
            AddWallLine(slots, 15, 20, 19, 20);

            AddBallista(slots, 7, 8);
            AddBallista(slots, 18, 8);
            AddBallista(slots, 7, 19);
            AddBallista(slots, 18, 19);

            AddWatchTower(slots, 2, 3);
            AddWatchTower(slots, 23, 3);
            AddWatchTower(slots, 2, 24);
            AddWatchTower(slots, 23, 24);

            AddOuterWallLine(slots, 1, 1, 11, 1);
            AddOuterWallLine(slots, 15, 1, 25, 1);
            AddOuterWallLine(slots, 1, 2, 1, 11);
            AddOuterWallLine(slots, 25, 2, 25, 11);
            AddOuterWallLine(slots, 1, 15, 1, 24);
            AddOuterWallLine(slots, 25, 15, 25, 24);
            AddOuterWallLine(slots, 1, 25, 11, 25);
            AddOuterWallLine(slots, 15, 25, 25, 25);

            return slots.ToArray();
        }

        static void AddWallLine(List<FixedBuildingSlotDefinition> slots, int startColumn, int startRow, int endColumn, int endRow)
        {
            int columnStep = Math.Sign(endColumn - startColumn);
            int rowStep = Math.Sign(endRow - startRow);
            int length = Mathf.Max(Mathf.Abs(endColumn - startColumn), Mathf.Abs(endRow - startRow));
            for (int i = 0; i <= length; i++)
            {
                slots.Add(CreateFixedWallSlot(startColumn + columnStep * i, startRow + rowStep * i));
            }
        }

        static void AddOuterWallLine(List<FixedBuildingSlotDefinition> slots, int startColumn, int startRow, int endColumn, int endRow)
        {
            int columnStep = Math.Sign(endColumn - startColumn);
            int rowStep = Math.Sign(endRow - startRow);
            int length = Mathf.Max(Mathf.Abs(endColumn - startColumn), Mathf.Abs(endRow - startRow));
            for (int i = 0; i <= length; i++)
            {
                slots.Add(CreateFixedOuterWallSlot(startColumn + columnStep * i, startRow + rowStep * i));
            }
        }

        static void AddBallista(List<FixedBuildingSlotDefinition> slots, int leftColumn, int lowerRow)
        {
            slots.Add(new FixedBuildingSlotDefinition
            {
                kind = SavedBuildingKind.Ballista,
                unlockType = UpgradeType.UnlockBallista,
                fixedSlotUpgradeType = UpgradeType.BallistaUpgrade,
                footprint = new Vector2Int(2, 2),
                desiredOffset = OffsetFromLayoutCell(leftColumn, lowerRow),
                upgradesWithFixedSlotSkill = true
            });
        }

        static void AddWatchTower(List<FixedBuildingSlotDefinition> slots, int leftColumn, int lowerRow)
        {
            slots.Add(new FixedBuildingSlotDefinition
            {
                kind = SavedBuildingKind.WatchTower,
                unlockType = UpgradeType.UnlockWatchTower,
                fixedSlotUpgradeType = UpgradeType.WatchTowerUpgrade,
                footprint = new Vector2Int(2, 2),
                desiredOffset = OffsetFromLayoutCell(leftColumn, lowerRow),
                requiresPlayerTerritory = false,
                upgradesWithFixedSlotSkill = true
            });
        }

        static FixedBuildingSlotDefinition CreateFixedWallSlot(int column, int row)
        {
            return new FixedBuildingSlotDefinition
            {
                kind = SavedBuildingKind.WoodenWall,
                unlockType = UpgradeType.UnlockWall,
                fixedSlotUpgradeType = UpgradeType.WallUpgrade,
                footprint = Vector2Int.one,
                desiredOffset = OffsetFromLayoutCell(column, row),
                upgradesWithFixedSlotSkill = true
            };
        }

        static FixedBuildingSlotDefinition CreateFixedOuterWallSlot(int column, int row)
        {
            return new FixedBuildingSlotDefinition
            {
                kind = SavedBuildingKind.WoodenWall,
                unlockType = UpgradeType.UnlockWall2,
                fixedSlotUpgradeType = UpgradeType.Wall2Upgrade,
                footprint = Vector2Int.one,
                desiredOffset = OffsetFromLayoutCell(column, row),
                requiresPlayerTerritory = false,
                upgradesWithFixedSlotSkill = true
            };
        }

        static Vector2Int OffsetFromLayoutCell(int column, int row)
        {
            return new Vector2Int(column - FixedLayoutTowerCenterColumn, FixedLayoutTowerCenterRow - row);
        }

        List<SavedBuildingData> BuildFixedStageBuildings(int stage)
        {
            var result = new List<SavedBuildingData>();
            if (grid == null) return result;

            var existing = ProgressionStore.GetStageBuildings(stage).buildings;
            var towerOrigin = grid.GridToCell(grid.width / 2, grid.height / 2);
            for (int i = 0; i < FixedBuildingSlotDefinitions.Length; i++)
            {
                var definition = FixedBuildingSlotDefinitions[i];
                if (definition.requiresUnlock && !ProgressionStore.IsUnlocked(definition.unlockType)) continue;
                if (!TryFindFixedSlotOrigin(towerOrigin, definition.footprint, definition.desiredOffset, definition.requiresPlayerTerritory, out var originCell)) continue;

                var saved = FindExistingFixedBuilding(existing, definition.kind, originCell);
                if (saved == null) saved = new SavedBuildingData();
                var previousKind = saved.kind;
                saved.kind = definition.kind;
                saved.x = originCell.x;
                saved.y = originCell.y;
                saved.destroyed = false;
                if (previousKind != definition.kind) saved.upgraded = false;
                if (definition.upgradesWithFixedSlotSkill) saved.upgraded = ProgressionStore.IsUnlocked(definition.fixedSlotUpgradeType);
                result.Add(saved);
            }

            return result;
        }

        static SavedBuildingData FindExistingFixedBuilding(List<SavedBuildingData> existing, SavedBuildingKind kind, Vector3Int originCell)
        {
            if (existing == null) return null;
            foreach (var saved in existing)
            {
                if (saved == null) continue;
                if (saved.kind == kind && saved.x == originCell.x && saved.y == originCell.y) return saved;
            }

            return null;
        }

        bool TryFindFixedSlotOrigin(Vector3Int towerOrigin, Vector2Int footprint, Vector2Int desiredOffset, bool requiresPlayerTerritory, out Vector3Int originCell)
        {
            footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            for (int radius = 0; radius <= 5; radius++)
            {
                foreach (var offset in EnumerateFixedSlotOffsets(desiredOffset, radius))
                {
                    originCell = towerOrigin + new Vector3Int(offset.x, offset.y, 0);
                    if (!grid.ContainsCell(originCell)) continue;
                    if (!grid.CanPlaceObject(originCell, footprint)) continue;
                    if (requiresPlayerTerritory && !HasPlayerTerritory(originCell, footprint)) continue;
                    return true;
                }
            }

            originCell = default(Vector3Int);
            return false;
        }

        static IEnumerable<Vector2Int> EnumerateFixedSlotOffsets(Vector2Int desiredOffset, int radius)
        {
            if (radius == 0)
            {
                yield return desiredOffset;
                yield break;
            }

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) != radius) continue;
                    yield return desiredOffset + new Vector2Int(dx, dy);
                }
            }
        }

        bool HasPlayerTerritory(Vector3Int originCell, Vector2Int footprint)
        {
            return grid != null && grid.IsFootprintOwnedBy(originCell, footprint, TileOwner.Player);
        }

        void Update()
        {
            if (sessionMode == MapSessionMode.Game)
            {
                if (!bossActive) elapsed += Time.deltaTime;
                hudElapsed = elapsed;
            }
            else
            {
                hudElapsed = 0f;
            }

            if (sessionMode == MapSessionMode.Build && buildPlacement != null && (buildingUpgrade == null || !buildingUpgrade.IsActive)) buildPlacement.Tick();
            UpdateLevelUpButtonHover();
            UpdateHud();
        }

        public void RegisterKill()
        {
            kills++;
        }

        public void RegisterDamageDealt(int amount)
        {
            damageDealt += Mathf.Max(0, amount);
        }

        public bool HasResources(int wood, int stone)
        {
            return Wood >= Mathf.Max(0, wood) && Stone >= Mathf.Max(0, stone);
        }

        public bool TrySpendResources(int wood, int stone)
        {
            wood = Mathf.Max(0, wood);
            stone = Mathf.Max(0, stone);
            if (!HasResources(wood, stone)) return false;
            Wood -= wood;
            Stone -= stone;
            return true;
        }

        public void SyncPersistentResources()
        {
            if (sessionMode != MapSessionMode.Build) return;
            Wood = Mathf.Max(0, ProgressionStore.Data.wood);
            Stone = Mathf.Max(0, ProgressionStore.Data.stone);
        }

        public void AddResource(ResourceType type, int amount)
        {
            amount = Mathf.Max(0, amount);
            if (type == ResourceType.Wood) Wood += amount;
            else Stone += amount;
        }

        public void AddPersistentResourcesForTesting(int wood, int stone)
        {
            if (sessionMode != MapSessionMode.Build) return;
            ProgressionStore.AddPersistentResources(wood, stone);
            SyncPersistentResources();
        }

        public void AddRunTokens(int amount)
        {
            RunTokens += Mathf.Max(0, amount);
            UpdateHud();
        }

        public void AddExperience(int amount)
        {
            float multiplier = Player != null ? Mathf.Max(0f, Player.Stats.xpGainMultiplier) : 1f;
            xpRemainder += Mathf.Max(1, amount) * multiplier;
            int gained = Mathf.FloorToInt(xpRemainder);
            if (gained <= 0) return;
            xpRemainder -= gained;
            xp += gained;
            while (xp >= xpToNext)
            {
                xp -= xpToNext;
                level++;
                xpToNext = Mathf.RoundToInt(xpToNext * 1.35f + 3);
                ShowLevelUp();
            }
            UpdateHud();
        }

        void ShowLevelUp()
        {
            AudioManager.PlaySfx(SfxTrack.LevelUp);
            Time.timeScale = 0f;
            ShowLevelUpInputBlocker(true);
            levelUpPanel.SetActive(true);
            levelUpPanel.transform.SetAsLastSibling();
            var choices = RollUpgrades();
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                int index = i;
                EnsureSelectionHighlight(upgradeButtons[i]);
                upgradeButtons[i].gameObject.SetActive(index < choices.Count);
                if (index >= choices.Count) continue;
                ConfigureLevelUpButton(upgradeButtons[i], choices[index]);
                upgradeButtons[i].onClick.RemoveAllListeners();
                upgradeButtons[i].onClick.AddListener(() => ApplyRunUpgrade(choices[index]));
            }

            SelectFirstUpgrade();
            StartCoroutine(SelectFirstUpgradeNextFrame());
        }

        void SelectFirstUpgrade()
        {
            if (upgradeButtons.Length == 0 || upgradeButtons[0] == null) return;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(upgradeButtons[0].gameObject);
            }
            upgradeButtons[0].Select();
            SetUpgradeHover(0);
        }

        IEnumerator SelectFirstUpgradeNextFrame()
        {
            yield return null;
            SelectFirstUpgrade();
        }

        List<RunUpgradeChoice> RollUpgrades()
        {
            var pool = new List<RunUpgradeChoice>();
            var weapon = Player != null ? Player.weapon : null;
            if (weapon != null)
            {
                bool canAcquireNewWeapon = weapon.HasOpenWeaponSlot;
                if (canAcquireNewWeapon)
                {
                    foreach (var weaponType in WeaponCatalog.UnlockableWeapons)
                    {
                        if (weapon.IsWeaponUnlocked(weaponType)) continue;
                        if (!ProgressionStore.IsUnlocked(WeaponCatalog.UnlockUpgrade(weaponType))) continue;
                        var capturedType = weaponType;
                        pool.Add(RunUpgradeChoice.NewWeapon(capturedType, () => weapon.UnlockWeapon(capturedType)));
                    }
                }

                if (weapon.SlashUnlocked) AddSlashUpgradeChoices(pool, weapon);
                if (weapon.ArrowUnlocked) AddArrowUpgradeChoices(pool, weapon);
                if (weapon.FireballUnlocked) AddFireballUpgradeChoices(pool, weapon);
                if (weapon.ShieldUnlocked) AddShieldUpgradeChoices(pool, weapon);
                foreach (var weaponType in WeaponCatalog.UnlockableWeapons)
                {
                    if (!WeaponCatalog.IsAdvanced(weaponType) || !weapon.IsWeaponUnlocked(weaponType)) continue;
                    AddAdvancedWeaponUpgradeChoices(pool, weapon, weaponType);
                }
            }

            var result = new List<RunUpgradeChoice>();
            while (result.Count < 3 && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }

        void AddSlashUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.SlashStats;
            int attackBonus = Mathf.Max(1, config.runAttackPowerBonus);
            float cooldownMultiplier = Mathf.Clamp(config.runAttackCooldownMultiplier, 0.05f, 1f);
            pool.Add(new RunUpgradeChoice(
                WeaponType.Slash,
                "攻撃力 " + weapon.SlashAttackPower + ">" + (weapon.SlashAttackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddSlashAttack(attackBonus)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Slash,
                "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier),
                StatIconCatalog.Cooldown,
                () => weapon.MultiplySlashCooldown(cooldownMultiplier)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Slash,
                "ノックバック " + Number(stats.knockback) + ">" + Number(stats.knockback + WeaponController.SlashKnockbackUpgradeAmount),
                StatIconCatalog.Knockback,
                () => weapon.AddSlashKnockback(WeaponController.SlashKnockbackUpgradeAmount)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Slash,
                "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + WeaponController.SlashRangeUpgradeAmount),
                StatIconCatalog.Range,
                () => weapon.AddSlashRange(WeaponController.SlashRangeUpgradeAmount)));
        }

        void AddArrowUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.ArrowStats;
            int attackBonus = Mathf.Max(1, config.runAttackPowerBonus);
            float cooldownMultiplier = Mathf.Clamp(config.runAttackCooldownMultiplier, 0.05f, 1f);
            pool.Add(new RunUpgradeChoice(
                WeaponType.Arrow,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddArrowAttack(attackBonus)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Arrow,
                "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier),
                StatIconCatalog.Cooldown,
                () => weapon.MultiplyArrowCooldown(cooldownMultiplier)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Arrow,
                "矢の本数 " + stats.projectileCount + ">" + (stats.projectileCount + 1),
                StatIconCatalog.Projectile,
                () => weapon.AddArrowProjectileCount(1)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Arrow,
                "射程 " + Number(stats.range) + ">" + Number(stats.range + WeaponController.ProjectileRangeUpgradeAmount),
                StatIconCatalog.Range,
                () => weapon.AddArrowRange(WeaponController.ProjectileRangeUpgradeAmount)));
        }

        void AddFireballUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.FireballStats;
            int attackBonus = Mathf.Max(1, config.runAttackPowerBonus);
            float cooldownMultiplier = Mathf.Clamp(config.runAttackCooldownMultiplier, 0.05f, 1f);
            pool.Add(new RunUpgradeChoice(
                WeaponType.Fireball,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddFireballAttack(attackBonus)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Fireball,
                "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier),
                StatIconCatalog.Cooldown,
                () => weapon.MultiplyFireballCooldown(cooldownMultiplier)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Fireball,
                "爆発範囲 " + Number(stats.explosionRadius) + ">" + Number(stats.explosionRadius + WeaponController.FireballExplosionUpgradeAmount),
                StatIconCatalog.Range,
                () => weapon.AddFireballExplosionRadius(WeaponController.FireballExplosionUpgradeAmount)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Fireball,
                "射程 " + Number(weapon.FireballRange) + ">" + Number(weapon.FireballRange + WeaponController.ProjectileRangeUpgradeAmount),
                StatIconCatalog.Range,
                () => weapon.AddFireballRange(WeaponController.ProjectileRangeUpgradeAmount)));
        }

        void AddShieldUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.ShieldStats;
            int attackBonus = Mathf.Max(1, config.runAttackPowerBonus);
            pool.Add(new RunUpgradeChoice(
                WeaponType.Shield,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddShieldAttack(attackBonus)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Shield,
                "シールド数 " + stats.projectileCount + ">" + (stats.projectileCount + 1),
                StatIconCatalog.Defense,
                () => weapon.AddShieldCount(1)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Shield,
                "ノックバック " + Number(stats.knockback) + ">" + Number(stats.knockback + WeaponController.ShieldKnockbackUpgradeAmount),
                StatIconCatalog.Knockback,
                () => weapon.AddShieldKnockback(WeaponController.ShieldKnockbackUpgradeAmount)));
            pool.Add(new RunUpgradeChoice(
                WeaponType.Shield,
                "回転速度 " + Number(stats.rotationSpeed) + ">" + Number(stats.rotationSpeed + WeaponController.ShieldRotationSpeedUpgradeAmount),
                StatIconCatalog.MoveSpeed,
                () => weapon.AddShieldRotationSpeed(WeaponController.ShieldRotationSpeedUpgradeAmount)));
        }

        void AddAdvancedWeaponUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon, WeaponType type)
        {
            var stats = weapon.GetWeaponStatsFor(type);
            int attackBonus = Mathf.Max(1, config.runAttackPowerBonus);
            float cooldownMultiplier = Mathf.Clamp(config.runAttackCooldownMultiplier, 0.05f, 1f);
            pool.Add(new RunUpgradeChoice(
                type,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddWeaponAttack(type, attackBonus)));

            switch (type)
            {
                case WeaponType.Flag:
                    pool.Add(new RunUpgradeChoice(type, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + WeaponController.ProjectileRangeUpgradeAmount), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, WeaponController.ProjectileRangeUpgradeAmount)));
                    pool.Add(new RunUpgradeChoice(type, "速度低下 " + Percent(stats.slowAmount) + ">" + Percent(stats.slowAmount + 0.05f), StatIconCatalog.MoveSpeed, () => weapon.AddWeaponSlow(type, 0.05f)));
                    pool.Add(new RunUpgradeChoice(type, "攻撃間隔 " + Seconds(stats.damageIntervalSeconds) + ">" + Seconds(stats.damageIntervalSeconds * cooldownMultiplier), StatIconCatalog.Cooldown, () => weapon.MultiplyWeaponDamageInterval(type, cooldownMultiplier)));
                    break;
                case WeaponType.BoomerangSword:
                    pool.Add(new RunUpgradeChoice(type, "剣本数 " + stats.projectileCount + ">" + (stats.projectileCount + 1), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, 1)));
                    pool.Add(new RunUpgradeChoice(type, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + WeaponController.SlashRangeUpgradeAmount), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, WeaponController.SlashRangeUpgradeAmount)));
                    pool.Add(new RunUpgradeChoice(type, "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier), StatIconCatalog.Cooldown, () => weapon.MultiplyWeaponCooldown(type, cooldownMultiplier)));
                    break;
                case WeaponType.AuraSword:
                    pool.Add(new RunUpgradeChoice(type, "攻撃回数 " + stats.projectileCount + ">" + (stats.projectileCount + 1), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, 1)));
                    pool.Add(new RunUpgradeChoice(type, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + WeaponController.ProjectileRangeUpgradeAmount), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, WeaponController.ProjectileRangeUpgradeAmount)));
                    pool.Add(new RunUpgradeChoice(type, "攻撃距離 " + Number(stats.distance) + ">" + Number(stats.distance + WeaponController.ProjectileRangeUpgradeAmount), StatIconCatalog.Range, () => weapon.AddWeaponDistance(type, WeaponController.ProjectileRangeUpgradeAmount)));
                    break;
                case WeaponType.ArrowRain:
                    pool.Add(new RunUpgradeChoice(type, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + WeaponController.ProjectileRangeUpgradeAmount), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, WeaponController.ProjectileRangeUpgradeAmount)));
                    pool.Add(new RunUpgradeChoice(type, "攻撃時間 " + Seconds(stats.durationSeconds) + ">" + Seconds(stats.durationSeconds + 0.4f), StatIconCatalog.Cooldown, () => weapon.AddWeaponDuration(type, 0.4f)));
                    pool.Add(new RunUpgradeChoice(type, "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier), StatIconCatalog.Cooldown, () => weapon.MultiplyWeaponCooldown(type, cooldownMultiplier)));
                    break;
                case WeaponType.Gun:
                    pool.Add(new RunUpgradeChoice(type, "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier), StatIconCatalog.Cooldown, () => weapon.MultiplyWeaponCooldown(type, cooldownMultiplier)));
                    pool.Add(new RunUpgradeChoice(type, "攻撃距離 " + Number(stats.distance) + ">" + Number(stats.distance + WeaponController.ProjectileRangeUpgradeAmount), StatIconCatalog.Range, () => weapon.AddWeaponDistance(type, WeaponController.ProjectileRangeUpgradeAmount)));
                    pool.Add(new RunUpgradeChoice(type, "攻撃回数 " + stats.projectileCount + ">" + (stats.projectileCount + 1), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, 1)));
                    break;
                case WeaponType.Frost:
                    pool.Add(new RunUpgradeChoice(type, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + WeaponController.ProjectileRangeUpgradeAmount), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, WeaponController.ProjectileRangeUpgradeAmount)));
                    pool.Add(new RunUpgradeChoice(type, "速度低下 " + Percent(stats.slowAmount) + ">" + Percent(stats.slowAmount + 0.05f), StatIconCatalog.MoveSpeed, () => weapon.AddWeaponSlow(type, 0.05f)));
                    pool.Add(new RunUpgradeChoice(type, "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier), StatIconCatalog.Cooldown, () => weapon.MultiplyWeaponCooldown(type, cooldownMultiplier)));
                    break;
                case WeaponType.ThunderBall:
                    pool.Add(new RunUpgradeChoice(type, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + WeaponController.ProjectileRangeUpgradeAmount), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, WeaponController.ProjectileRangeUpgradeAmount)));
                    pool.Add(new RunUpgradeChoice(type, "弾数 " + stats.projectileCount + ">" + (stats.projectileCount + 1), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, 1)));
                    pool.Add(new RunUpgradeChoice(type, "持続時間 " + Seconds(stats.durationSeconds) + ">" + Seconds(stats.durationSeconds + 0.5f), StatIconCatalog.Cooldown, () => weapon.AddWeaponDuration(type, 0.5f)));
                    break;
            }
        }

        static string Number(float value)
        {
            return value.ToString("0.##");
        }

        static string Seconds(float value)
        {
            return value.ToString("0.##") + "s";
        }

        static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        void ApplyRunUpgrade(RunUpgradeChoice choice)
        {
            choice.apply();
            runUpgrades.Add(choice.label);
            Player.ApplyCurrentStats(false);
            levelUpPanel.SetActive(false);
            ShowLevelUpInputBlocker(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Time.timeScale = 1f;
        }

        static void EnsureSelectionHighlight(Button button)
        {
            if (button == null) return;
            button.transition = Selectable.Transition.None;
            var highlight = button.GetComponent<UiSelectionHighlight>();
            if (highlight == null) highlight = button.gameObject.AddComponent<UiSelectionHighlight>();
            highlight.padding = 6f;
            highlight.thickness = 4f;
            highlight.forceSelected = false;
            highlight.enabled = true;
            if (button.GetComponent<SelectOnPointerEnter>() == null) button.gameObject.AddComponent<SelectOnPointerEnter>();
        }

        static void ConfigureLevelUpButton(Button button, RunUpgradeChoice choice)
        {
            if (button == null || choice == null) return;

            var weaponIcon = FindImage(button.transform, "Weapon Icon");
            var weaponName = FindText(button.transform, "Weapon Name Text");
            var upgradeText = FindText(button.transform, "Upgrade Text");
            var label = FindText(button.transform, "Label");
            if (weaponIcon == null || weaponName == null || upgradeText == null)
            {
                ConfigureLegacyLevelUpButton(button, choice);
                return;
            }

            if (label != null) label.gameObject.SetActive(false);
            SetImage(weaponIcon, GeneratedSpriteLoader.Load(choice.weaponIconResource), true);
            weaponName.text = choice.weaponName;
            upgradeText.text = choice.upgradeText;
            ConfigureLevelUpButtonTypeIcon(button, choice.hasAttributeType, choice.attributeType);

            var upgradeIcon = FindImage(button.transform, "Upgrade Icon");
            var newWeaponMark = FindText(button.transform, "New Weapon Mark");
            if (choice.isNewWeapon)
            {
                if (upgradeIcon != null) upgradeIcon.gameObject.SetActive(false);
                if (newWeaponMark != null)
                {
                    newWeaponMark.gameObject.SetActive(true);
                    newWeaponMark.text = "★";
                }
                return;
            }

            SetImage(upgradeIcon, StatIconCatalog.Load(choice.iconResource), true);
            if (newWeaponMark != null) newWeaponMark.gameObject.SetActive(false);
        }

        static void ConfigureLegacyLevelUpButton(Button button, RunUpgradeChoice choice)
        {
            var label = GetLevelUpButtonLabel(button);
            var sprite = StatIconCatalog.Load(choice.iconResource);
            var icon = FindImage(button.transform, "Upgrade Icon");
            if (sprite == null)
            {
                if (icon != null) icon.gameObject.SetActive(false);
                ConfigureLevelUpButtonTypeIcon(button, false, WeaponAttributeType.None);
                ConfigureLevelUpButtonLabel(label, false, false);
                if (label != null) label.text = choice.label;
                return;
            }

            if (icon == null)
            {
                ConfigureLevelUpButtonTypeIcon(button, choice.hasAttributeType, choice.attributeType);
                ConfigureLevelUpButtonLabel(label, false, choice.hasAttributeType);
                if (label != null) label.text = choice.label;
                return;
            }

            icon.gameObject.SetActive(true);
            icon.sprite = sprite;
            icon.color = Color.white;

            bool typeVisible = ConfigureLevelUpButtonTypeIcon(button, choice.hasAttributeType, choice.attributeType);
            ConfigureLevelUpButtonLabel(label, true, typeVisible);
            if (label != null) label.text = choice.label;
        }

        static bool ConfigureLevelUpButtonTypeIcon(Button button, bool hasAttributeType, WeaponAttributeType attributeType)
        {
            var iconSetTransform = button != null ? button.transform.Find("Weapon Type Icons") : null;
            var iconSet = iconSetTransform != null ? iconSetTransform.GetComponent<WeaponAttributeIconSet>() : null;
            if (iconSet == null) return false;

            if (hasAttributeType && attributeType != WeaponAttributeType.None)
            {
                iconSet.gameObject.SetActive(true);
                iconSet.Show(attributeType);
                return true;
            }

            iconSet.Hide();
            iconSet.gameObject.SetActive(false);
            return false;
        }

        static Text GetLevelUpButtonLabel(Button button)
        {
            var labelTransform = button.transform.Find("Label");
            var label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label != null) return label;

            return button.GetComponentInChildren<Text>();
        }

        static Image FindImage(Transform parent, string name)
        {
            var child = parent != null ? parent.Find(name) : null;
            return child != null ? child.GetComponent<Image>() : null;
        }

        static Text FindText(Transform parent, string name)
        {
            var child = parent != null ? parent.Find(name) : null;
            return child != null ? child.GetComponent<Text>() : null;
        }

        static void SetImage(Image image, Sprite sprite, bool visible)
        {
            if (image == null) return;
            image.gameObject.SetActive(visible && sprite != null);
            image.sprite = sprite;
            image.color = Color.white;
        }

        static void ConfigureLevelUpButtonLabel(Text label, bool hasIcon, bool hasTypeIcon)
        {
            if (label == null) return;
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.offsetMin = new Vector2(hasIcon ? (hasTypeIcon ? 92f : 72f) : 18f, 0f);
            label.rectTransform.offsetMax = new Vector2(-12f, 0f);
        }

        void UpdateLevelUpButtonHover()
        {
            if (levelUpPanel == null || !levelUpPanel.activeSelf || upgradeButtons == null) return;
            int hoverIndex = -1;
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                if (IsPointerOverButton(upgradeButtons[i]))
                {
                    hoverIndex = i;
                    break;
                }
            }

            if (hoverIndex >= 0)
            {
                SetUpgradeHover(hoverIndex);
                return;
            }

            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                if (upgradeButtons[i] != null && upgradeButtons[i].gameObject == current)
                {
                    SetUpgradeHover(i);
                    return;
                }
            }
        }

        void SetUpgradeHover(int index)
        {
            if (upgradeButtons == null) return;
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                var button = upgradeButtons[i];
                if (button == null) continue;
                var image = button.GetComponent<Image>();
                if (image != null) image.color = i == index ? UpgradeHoverColor : UpgradeNormalColor;
                var highlight = button.GetComponent<UiSelectionHighlight>();
                if (highlight != null) highlight.forceSelected = false;
            }
        }

        static bool IsPointerOverButton(Button button)
        {
            if (button == null || !button.gameObject.activeInHierarchy) return false;
            var rect = button.GetComponent<RectTransform>();
            if (rect == null) return false;
            var canvas = button.GetComponentInParent<Canvas>();
            Camera eventCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) eventCamera = canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, eventCamera);
        }

        void ShowLevelUpInputBlocker(bool visible)
        {
            if (levelUpPanel == null) return;
            if (levelUpInputBlocker == null)
            {
                var parent = levelUpPanel.transform.parent;
                levelUpInputBlocker = new GameObject("Level Up Input Blocker");
                levelUpInputBlocker.transform.SetParent(parent, false);
                var image = levelUpInputBlocker.AddComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.01f);
                image.raycastTarget = true;
                var rect = image.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            levelUpInputBlocker.SetActive(visible);
            if (visible)
            {
                levelUpInputBlocker.transform.SetSiblingIndex(levelUpPanel.transform.GetSiblingIndex());
                levelUpPanel.transform.SetAsLastSibling();
            }
        }

        public void GameOver()
        {
            EndRun(false);
        }

        public void BossSpawned(EnemyController boss)
        {
            if (boss == null) return;
            bossActive = true;
            AudioManager.PlayBgm(BgmTrack.GameBoss);
            if (timerText != null) timerText.color = Color.red;
            gameHud?.ShowBoss(boss);
            ShowAnnouncement(spawner != null ? spawner.CurrentBossAnnouncement : config.bossAnnouncement);
        }

        public void BossDefeated(EnemyController boss)
        {
            if (gameEnding) return;
            StartCoroutine(BossDefeatedRoutine(boss));
        }

        IEnumerator BossDefeatedRoutine(EnemyController boss)
        {
            bool unlockedNextStage = ProgressionStore.MarkStageCleared(currentStage);
            if (currentStage < 4)
            {
                int nextStage = unlockedNextStage ? currentStage + 1 : Mathf.Min(currentStage + 1, 4);
                yield return StageTransitionRoutine(boss, nextStage);
            }
            else
            {
                yield return GameClearRoutine(boss, currentStage, unlockedNextStage ? currentStage + 1 : 0, string.Empty);
            }
        }

        IEnumerator GameClearRoutine(EnemyController boss, int clearedStage, int unlockedStage, string clearMessage)
        {
            gameEnding = true;
            spawner?.StopAndClearEnemies(boss);
            ShowAnnouncement("GAME CLEAR");
            yield return new WaitForSeconds(1.8f);
            EndRun(true, clearedStage, unlockedStage, clearMessage);
        }

        IEnumerator StageTransitionRoutine(EnemyController boss, int nextStage)
        {
            gameEnding = true;
            spawner?.StopAndClearEnemies(boss);
            ShowAnnouncement("ROUND " + nextStage);
            yield return new WaitForSeconds(1.2f);
            if (boss != null) Destroy(boss.gameObject);
            ProgressionStore.ReviveStageBuildings(nextStage);
            if (buildPlacement != null) buildPlacement.RestoreStageBuildings(nextStage);
            gameEnding = false;
            BeginStage(nextStage);
        }

        public void ShowAnnouncement(string message)
        {
            gameHud?.ShowAnnouncement(message);
        }

        void EndRun(bool clear)
        {
            EndRun(clear, clear ? currentStage : 0, 0, string.Empty);
        }

        void EndRun(bool clear, int clearedStage, int unlockedStage, string clearMessage)
        {
            if (!clear && gameEnding) return;
            gameEnding = true;
            Time.timeScale = 1f;
            int rewardWood = EndWoodReward();
            int rewardStone = EndStoneReward();
            int woodEarned = Mathf.Max(0, Wood) + rewardWood;
            int stoneEarned = Mathf.Max(0, Stone) + rewardStone;
            RunResult.Last = new RunResult
            {
                kills = kills,
                damageDealt = damageDealt,
                level = level,
                tokensEarned = EndTokenReward(),
                woodEarned = woodEarned,
                stoneEarned = stoneEarned,
                reachedStage = currentStage,
                survivedSeconds = elapsed,
                gameClear = clear,
                clearedStage = clearedStage,
                unlockedStage = unlockedStage,
                clearMessage = clearMessage,
                upgrades = new List<string>(runUpgrades)
            };
            ProgressionStore.AddRunTokens(kills, EndTokenReward());
            ProgressionStore.AddPersistentResources(woodEarned, stoneEarned);
            SceneManager.LoadScene(SceneNames.GameEnd);
        }

        int EndTokenReward()
        {
            float multiplier = 1f + ProgressionStore.GetLevel(UpgradeType.EndTokenGain) * config.endTokenGainMultiplierPerUpgradeLevel;
            return Mathf.Max(0, Mathf.RoundToInt(RunTokens * multiplier));
        }

        int EndWoodReward()
        {
            return Mathf.Max(0, config.roundEndWoodReward + ProgressionStore.GetLevel(UpgradeType.StartingWood) * config.roundEndWoodRewardPerUpgradeLevel);
        }

        int EndStoneReward()
        {
            return Mathf.Max(0, config.roundEndStoneReward + ProgressionStore.GetLevel(UpgradeType.StartingStone) * config.roundEndStoneRewardPerUpgradeLevel);
        }

        void UpdateHud()
        {
            if (timerText != null)
            {
                var span = TimeSpan.FromSeconds(hudElapsed);
                timerText.text = $"{span.Minutes:00}:{span.Seconds:00}";
                if (!bossActive) timerText.color = Color.white;
            }
            if (killText != null) killText.text = kills.ToString();
            if (levelText != null) levelText.text = $"Lv {level}";
            if (xpBar != null) xpBar.value = xpToNext <= 0 ? 0f : (float)xp / xpToNext;
            gameHud?.SetStage(currentStage);
        }

        void BeginStage(int stage)
        {
            currentStage = Mathf.Max(1, stage);
            currentStageSpeedMultiplier = ProgressionStore.IsFastStage(currentStage) ? 2f : 1f;
            hudElapsed = elapsed;
            bossActive = false;
            AudioManager.PlayBgm(BgmTrack.GameNormal);
            if (timerText != null) timerText.color = Color.white;
            if (spawner != null)
            {
                spawner.useUpperChunkSpawn = false;
                spawner.BeginStage(config, grid, Tower.EnemyTarget, currentStage, 0f, currentStageSpeedMultiplier);
            }
            gameHud?.SetStage(currentStage);
        }

        float StageStartDisplaySeconds()
        {
            return Mathf.Max(0, currentStage - 1) * config.bossTimeSeconds;
        }

        void PolishHud()
        {
            HideLegacyPlayerProgressHud();
            if (timerText != null) HideChild(timerText.transform.parent, "Run Stats Backplate");
        }

        void ConfigureGameHud()
        {
            if (gameHud == null) gameHud = GetComponent<GameHudController>();
            if (gameHud == null) gameHud = gameObject.AddComponent<GameHudController>();
            gameHud.Initialize(buildPlacement, Tower, this);
        }

        void HideLegacyPlayerProgressHud()
        {
            if (levelText != null)
            {
                HideChild(levelText.transform.parent, "Level Backplate");
                levelText.gameObject.SetActive(false);
            }
            if (xpBar != null)
            {
                HideChild(xpBar.transform.parent, "XP Backplate");
                xpBar.gameObject.SetActive(false);
            }
        }

        static void HideChild(Transform parent, string name)
        {
            if (parent == null) return;
            var child = parent.Find(name);
            if (child != null) child.gameObject.SetActive(false);
        }

        static void AddBackplate(Transform parent, string name, Vector2 position, Vector2 size)
        {
            if (parent == null || parent.Find(name) != null) return;
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = new Color(0.03f, 0.045f, 0.045f, 0.62f);
            image.raycastTarget = false;
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            image.transform.SetAsFirstSibling();
        }

        sealed class RunUpgradeChoice
        {
            public readonly string label;
            public readonly string iconResource;
            public readonly string weaponName;
            public readonly string weaponIconResource;
            public readonly string upgradeText;
            public readonly bool isNewWeapon;
            public readonly bool hasAttributeType;
            public readonly WeaponAttributeType attributeType;
            public readonly Action apply;

            public RunUpgradeChoice(string label, string iconResource, Action apply)
            {
                this.label = label;
                this.iconResource = iconResource;
                weaponName = string.Empty;
                weaponIconResource = null;
                upgradeText = label;
                isNewWeapon = false;
                this.apply = apply;
                hasAttributeType = false;
                attributeType = WeaponAttributeType.None;
            }

            public RunUpgradeChoice(WeaponType weaponType, string upgradeText, string iconResource, Action apply)
            {
                weaponName = WeaponDisplayName(weaponType);
                weaponIconResource = WeaponIconResource(weaponType);
                this.upgradeText = upgradeText;
                this.label = weaponName + " " + upgradeText;
                this.iconResource = iconResource;
                this.attributeType = WeaponAttributeCatalog.ForWeapon(weaponType);
                this.apply = apply;
                isNewWeapon = false;
                hasAttributeType = this.attributeType != WeaponAttributeType.None;
            }

            RunUpgradeChoice(WeaponType weaponType, bool newWeapon, Action apply)
            {
                weaponName = WeaponDisplayName(weaponType);
                weaponIconResource = WeaponIconResource(weaponType);
                upgradeText = "新規武器：" + weaponName + "を獲得";
                label = upgradeText;
                iconResource = StatIconCatalog.WeaponLevel;
                attributeType = WeaponAttributeCatalog.ForWeapon(weaponType);
                this.apply = apply;
                isNewWeapon = newWeapon;
                hasAttributeType = attributeType != WeaponAttributeType.None;
            }

            public static RunUpgradeChoice NewWeapon(WeaponType weaponType, Action apply)
            {
                return new RunUpgradeChoice(weaponType, true, apply);
            }

            static string WeaponDisplayName(WeaponType weaponType)
            {
                return WeaponCatalog.DisplayName(weaponType);
            }

            static string WeaponIconResource(WeaponType weaponType)
            {
                return WeaponCatalog.IconResource(weaponType);
            }
        }
    }

    public sealed class GameHudController : MonoBehaviour
    {
        const float LowHpBlinkThreshold = 0.1f;
        static readonly Color PanelColor = new Color(0.035f, 0.05f, 0.045f, 0.72f);
        static readonly Color SlotColor = new Color(0.09f, 0.16f, 0.12f, 0.92f);
        static readonly Color SlotSelectedColor = new Color(0.114f, 0.529f, 0.298f, 0.98f);
        static readonly Color EdgeColor = new Color(0.58f, 0.68f, 0.40f, 0.9f);
        static readonly Color HpBlue = new Color(0.22f, 0.62f, 1f, 0.96f);
        static readonly Color HpYellow = new Color(1f, 0.82f, 0.20f, 0.98f);
        static readonly Color HpRed = new Color(1f, 0.18f, 0.12f, 0.98f);
        static readonly Vector2 TowerPanelSize = new Vector2(110f, 314f);
        static readonly Vector2 TowerIconSize = new Vector2(98f, 98f);
        static readonly Vector2 TowerIconPosition = new Vector2(0f, -8f);
        static readonly Vector2 TowerHpBarPosition = new Vector2(0f, -126f);
        static readonly Vector2 TowerHpBarSize = new Vector2(38f, 136f);
        static readonly Vector2 TowerHpTextPosition = new Vector2(0f, -286f);
        static readonly Vector2 PlayerPanelSize = new Vector2(390f, 318f);
        static readonly Vector2 PlayerIconSize = new Vector2(58f, 58f);

        BuildPlacementController buildPlacement;
        GameManager gameManager;
        PlayerController player;
        TowerController towerController;
        Health towerHealth;
        Image towerImage;
        RectTransform towerPanel;
        RectTransform playerPanel;
        RectTransform playerStatsPanel;
        Image hpFill;
        Image playerHpFill;
        Image playerXpFill;
        Text hpText;
        Text playerHpText;
        Text playerLevelText;
        Text playerSpeedText;
        Text playerPaintText;
        RectTransform paintControlRoot;
        RectTransform paintControlBlueSegment;
        RectTransform paintControlNeutralSegment;
        RectTransform paintControlRedSegment;
        Text paintControlBlueText;
        Text paintControlNeutralText;
        Text paintControlRedText;
        Text playerReviveText;
        Text playerDefenseText;
        Text playerXpGainText;
        Text playerRegenText;
        readonly WeaponHudPanelBinding weaponHud = new WeaponHudPanelBinding();
        bool warnedMissingPlayerStatsHud;
        bool warnedMissingWeaponStatsHud;
        Text tokenText;
        RectTransform bossPanel;
        Text bossNameText;
        Image bossHpFill;
        Text bossHpText;
        Text announcementText;
        EnemyController activeBoss;
        Health bossHealth;
        Coroutine announcementRoutine;
        Text stageText;
        readonly List<FloatingHudDamage> damagePopups = new List<FloatingHudDamage>();

        public void Initialize(BuildPlacementController placement, TowerController tower, GameManager owner)
        {
            buildPlacement = placement;
            gameManager = owner;
            player = owner != null ? owner.Player : null;
            towerController = tower;
            towerHealth = tower != null ? tower.GetComponent<Health>() : null;
            if (towerHealth != null) towerHealth.Damaged += OnTowerDamaged;
            if (towerController != null) towerController.Upgraded += OnTowerUpgraded;

            var canvas = FindHudCanvas();
            if (canvas == null) canvas = CreateCanvas();

            HideLegacyBuildStatus(canvas.transform);
            ApplySessionHudVisibility(canvas.transform);
            if (gameManager == null || gameManager.SessionMode == MapSessionMode.Game) BindSceneRunStats(canvas.transform);
            BuildStagePanel(canvas.transform);
            if (gameManager == null || gameManager.SessionMode == MapSessionMode.Game)
            {
                BindSceneBossHud(canvas.transform);
                BuildPlayerPanel(canvas.transform);
            }
            BuildTowerPanel(canvas.transform);
            UpdatePlayerPanel();
            UpdateTokenHud();
            UpdateTowerPanel();
            UpdateBossHud();
        }

        void OnDestroy()
        {
            if (towerHealth != null) towerHealth.Damaged -= OnTowerDamaged;
            if (towerController != null) towerController.Upgraded -= OnTowerUpgraded;
            if (bossHealth != null) bossHealth.Died -= OnBossDied;
        }

        void ApplySessionHudVisibility(Transform parent)
        {
            if (parent == null || gameManager == null) return;
            SetDirectChildActive(parent, "Timer Panel", true);
            SetDirectChildActive(parent, "Kill Panel", true);
            SetDirectChildActive(parent, "Boss Status", true);
            SetDirectChildActive(parent, "Player Status", true);
            SetDirectChildActive(parent, "Token Resource", true);
            SetDirectChildActive(parent, "XP Bar", true);
            SetDirectChildActive(parent, "Level Panel", true);
        }

        static void SetDirectChildActive(Transform parent, string path, bool active)
        {
            var child = parent != null ? parent.Find(path) : null;
            if (child != null) child.gameObject.SetActive(active);
        }

        void Update()
        {
            if (player == null && gameManager != null) player = gameManager.Player;
            UpdatePlayerPanel();
            UpdateTokenHud();
            UpdateTowerPanel();
            UpdateBossHud();
            TickDamagePopups();
        }

        void BindSceneRunStats(Transform parent)
        {
            if (parent == null || gameManager == null) return;
            stageText = FindText(parent, "Stage Panel/Label");
            var timer = FindText(parent, "Timer Panel/Label");
            if (timer != null)
            {
                if (gameManager.timerText != null && gameManager.timerText != timer) gameManager.timerText.gameObject.SetActive(false);
                gameManager.timerText = timer;
            }
            var kills = FindText(parent, "Kill Panel/Label");
            if (kills != null)
            {
                if (gameManager.killText != null && gameManager.killText != kills) gameManager.killText.gameObject.SetActive(false);
                gameManager.killText = kills;
            }
            ConfigureStaticHudIcon(parent, "Kill Panel/Icon");
            tokenText = FindText(parent, "Token Resource/Amount");
            ConfigureStaticHudIcon(parent, "Token Resource/Icon");
        }

        public void SetStage(int stage)
        {
            if (stageText != null) stageText.text = "STAGE " + Mathf.Max(1, stage);
        }

        void UpdateTokenHud()
        {
            if (gameManager == null || tokenText == null) return;
            tokenText.text = gameManager.RunTokens.ToString();
        }

        void BindSceneBossHud(Transform parent)
        {
            bossPanel = parent.Find("Boss Status") as RectTransform;
            bossNameText = FindText(parent, "Boss Status/Boss Name");
            bossHpText = FindText(parent, "Boss Status/Boss HP Bar/Label");
            var fill = parent.Find("Boss Status/Boss HP Bar/Fill");
            bossHpFill = fill != null ? fill.GetComponent<Image>() : null;
            announcementText = FindText(parent, "Announcement/Label");
            if (bossPanel != null) bossPanel.gameObject.SetActive(false);
            if (announcementText != null) announcementText.transform.parent.gameObject.SetActive(false);
        }

        public void ShowBoss(EnemyController boss)
        {
            if (bossHealth != null) bossHealth.Died -= OnBossDied;
            activeBoss = boss;
            bossHealth = boss != null ? boss.GetComponent<Health>() : null;
            if (bossHealth != null) bossHealth.Died += OnBossDied;
            if (bossPanel != null) bossPanel.gameObject.SetActive(boss != null);
            if (bossNameText != null) bossNameText.text = boss != null ? boss.displayName : "";
            UpdateBossHud();
        }

        public void ShowAnnouncement(string message)
        {
            if (announcementText == null || string.IsNullOrEmpty(message)) return;
            if (announcementRoutine != null) StopCoroutine(announcementRoutine);
            announcementRoutine = StartCoroutine(AnnouncementRoutine(message));
        }

        IEnumerator AnnouncementRoutine(string message)
        {
            var root = announcementText.transform.parent.gameObject;
            root.SetActive(true);
            announcementText.text = message;
            var color = announcementText.color;
            color.a = 1f;
            announcementText.color = color;
            yield return new WaitForSecondsRealtime(1.8f);
            root.SetActive(false);
            announcementRoutine = null;
        }

        void UpdateBossHud()
        {
            if (bossPanel == null || bossHealth == null) return;
            bossPanel.gameObject.SetActive(!bossHealth.IsDead);
            float normalized = bossHealth.Normalized;
            if (bossHpFill != null) bossHpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            if (bossHpText != null) bossHpText.text = bossHealth.currentHp + "/" + bossHealth.maxHp;
        }

        void OnBossDied(Health _)
        {
            if (bossPanel != null) bossPanel.gameObject.SetActive(false);
        }

        void BuildPlayerPanel(Transform parent)
        {
            var splitPlayerRoot = parent.Find("Player");
            var existing = splitPlayerRoot != null ? splitPlayerRoot : parent.Find("Player Status");
            playerPanel = existing != null ? existing.GetComponent<RectTransform>() : null;

            var statsRoot = splitPlayerRoot != null ? parent.Find("Player Status") : playerPanel;
            playerStatsPanel = statsRoot != null ? statsRoot.GetComponent<RectTransform>() : playerPanel;
            if (playerPanel == null || playerStatsPanel == null) WarnMissingPlayerStatsHud();
            if (splitPlayerRoot != null && playerStatsPanel != null) HideLegacyPlayerTiles(playerStatsPanel);

            var portrait = FindImage(playerPanel, "Character Frame/Character Image");
            if (portrait != null)
            {
                portrait.sprite = player != null ? player.PortraitSprite : LoadHudSprite("Knight", null);
                portrait.preserveAspect = true;
            }

            BuildPaintGauge(parent, playerPanel);

            playerHpFill = BindHorizontalBar(playerPanel, "Player HP Bar", out playerHpText);
            playerXpFill = BindHorizontalBar(playerPanel, "Player XP Bar", out playerLevelText);
            playerSpeedText = BindSceneStatText(playerStatsPanel, "Speed Text");
            playerPaintText = BindSceneStatText(playerStatsPanel, "Paint Text");
            playerReviveText = BindSceneStatText(playerStatsPanel, "Revive Text");
            playerDefenseText = BindSceneStatText(playerStatsPanel, "Defense Text");
            playerXpGainText = BindSceneStatText(playerStatsPanel, "Xp Gain Text");
            playerRegenText = BindSceneStatText(playerStatsPanel, "Regen Text");
            weaponHud.Bind(parent, playerStatsPanel);
            if (weaponHud.HasMissingReferences) WarnMissingWeaponStatsHud();
        }

        Text BindSceneStatText(RectTransform statsRoot, string name)
        {
            var text = BindStatText(statsRoot, name);
            if (text == null) WarnMissingPlayerStatsHud();
            return text;
        }

        void WarnMissingPlayerStatsHud()
        {
            if (warnedMissingPlayerStatsHud) return;
            warnedMissingPlayerStatsHud = true;
            Debug.LogWarning("Player status HUD rows are missing. Place Player Status stat boxes in 05_Game.unity; runtime HUD generation is intentionally disabled.");
        }

        static Image BindHorizontalBar(RectTransform parent, string name, out Text label)
        {
            label = null;
            if (parent == null) return null;
            label = FindText(parent, name + "/Label");
            return FindImage(parent, name + "/Fill");
        }

        void WarnMissingWeaponStatsHud()
        {
            if (warnedMissingWeaponStatsHud) return;
            warnedMissingWeaponStatsHud = true;
            Debug.LogWarning("Weapon status HUD rows are missing. Place Slash/Arrow/Fireball Weapon Status panels in 05_Game.unity; runtime HUD generation is intentionally disabled.");
        }

        void BuildPaintGauge(Transform hudRoot, RectTransform fallbackParent)
        {
            paintControlRoot = FindPaintGaugeRoot(hudRoot, fallbackParent);
            if (paintControlRoot == null) return;

            paintControlBlueSegment = BindControlSegment(paintControlRoot, "Blue Segment");
            paintControlNeutralSegment = BindControlSegment(paintControlRoot, "Neutral Segment");
            paintControlRedSegment = BindControlSegment(paintControlRoot, "Red Segment");
            if (paintControlBlueSegment == null || paintControlNeutralSegment == null || paintControlRedSegment == null) return;
            paintControlBlueText = BindControlSegmentText(paintControlBlueSegment);
            paintControlNeutralText = BindControlSegmentText(paintControlNeutralSegment);
            paintControlRedText = BindControlSegmentText(paintControlRedSegment);
            if (paintControlBlueText == null || paintControlNeutralText == null || paintControlRedText == null) return;
        }

        static RectTransform FindPaintGaugeRoot(Transform hudRoot, RectTransform fallbackParent)
        {
            var topPanelGauge = hudRoot != null ? hudRoot.Find("Area Control Panel/Control Breakdown") : null;
            if (topPanelGauge != null) return topPanelGauge.GetComponent<RectTransform>();

            var fallbackGauge = fallbackParent != null ? fallbackParent.Find("Control Breakdown") : null;
            return fallbackGauge != null ? fallbackGauge.GetComponent<RectTransform>() : null;
        }

        static void HideLegacyPlayerTiles(RectTransform statsRoot)
        {
            if (statsRoot == null) return;
            HideHudChild(statsRoot, "Character Frame");
            HideHudChild(statsRoot, "Player HP Bar");
            HideHudChild(statsRoot, "Player XP Bar");
        }

        static void HideHudChild(Transform parent, string name)
        {
            if (parent == null) return;
            var child = parent.Find(name);
            if (child != null) child.gameObject.SetActive(false);
        }

        void UpdatePlayerPanel()
        {
            if (playerPanel == null || player == null) return;
            var health = player.Health;
            if (playerHpFill != null && health != null)
            {
                playerHpFill.rectTransform.anchorMax = new Vector2(health.Normalized, 1f);
                playerHpFill.color = health.Normalized <= 0.3f ? HpRed : new Color(0.36f, 0.88f, 0.36f, 0.98f);
                if (playerHpText != null) playerHpText.text = health.currentHp + "/" + health.maxHp;
            }
            if (playerXpFill != null && gameManager != null)
            {
                float normalized = gameManager.XpToNext <= 0 ? 0f : (float)gameManager.CurrentXp / gameManager.XpToNext;
                playerXpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
                if (playerLevelText != null) playerLevelText.text = "Lv." + gameManager.CurrentLevel;
            }
            UpdatePaintGauge();

            var portrait = playerPanel.Find("Character Frame/Character Image")?.GetComponent<Image>();
            if (portrait != null) portrait.sprite = player.PortraitSprite;
            if (playerSpeedText != null) playerSpeedText.text = player.MoveSpeed.ToString("0.0");
            if (playerPaintText != null) playerPaintText.text = player.PaintRadius.ToString();
            if (playerReviveText != null) playerReviveText.text = player.ReviveSeconds.ToString("0.0") + "s";
            var stats = player.Stats;
            if (playerDefenseText != null) playerDefenseText.text = stats.defense.ToString();
            if (playerXpGainText != null) playerXpGainText.text = stats.xpGainMultiplier.ToString("0.0") + "x";
            if (playerRegenText != null) playerRegenText.text = stats.autoRegen.ToString();
            UpdateWeaponStatsPanel();
        }

        void UpdateWeaponStatsPanel()
        {
            weaponHud.Update(player != null ? player.weapon : null);
        }

        void UpdatePaintGauge()
        {
            if (gameManager == null || gameManager.grid == null) return;
            if (paintControlBlueText == null && paintControlNeutralText == null && paintControlRedText == null) return;
            var summary = gameManager.grid.GetControlSummary();
            UpdateControlBreakdown(summary);
        }

        static RectTransform BindControlSegment(RectTransform root, string name)
        {
            if (root == null) return null;
            var child = root.Find(name);
            return child != null ? child.GetComponent<RectTransform>() : null;
        }

        static Text BindControlSegmentText(RectTransform segment)
        {
            if (segment == null) return null;
            var label = segment.Find("Label");
            var labelText = label != null ? label.GetComponent<Text>() : null;
            return labelText != null ? labelText : segment.GetComponent<Text>();
        }

        void UpdateControlBreakdown(TileControlSummary summary)
        {
            const float minWidth = 32f;
            float totalWidth = paintControlRoot != null ? Mathf.Max(96f, paintControlRoot.rect.width) : 190f;
            int[] counts = { Mathf.Max(0, summary.playerCells), Mathf.Max(0, summary.neutralCells), Mathf.Max(0, summary.enemyCells) };
            RectTransform[] segments = { paintControlBlueSegment, paintControlNeutralSegment, paintControlRedSegment };
            Text[] labels = { paintControlBlueText, paintControlNeutralText, paintControlRedText };

            float baseline = 0f;
            for (int i = 0; i < counts.Length; i++)
            {
                baseline += minWidth;
            }

            float remaining = Mathf.Max(0f, totalWidth - baseline);
            int totalCells = Mathf.Max(1, summary.totalCells);
            float x = 0f;
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i] == null || labels[i] == null) continue;
                float share = counts[i] > 0 ? remaining * counts[i] / (float)totalCells : 0f;
                float width = minWidth + share;
                SetControlSegment(segments[i], labels[i], width, x, counts[i]);
                x += width;
            }
        }

        static void SetControlSegment(RectTransform segment, Text label, float width, float x, int count)
        {
            segment.anchorMin = new Vector2(0f, 1f);
            segment.anchorMax = new Vector2(0f, 1f);
            segment.pivot = new Vector2(0f, 1f);
            segment.anchoredPosition = new Vector2(x, 0f);
            segment.sizeDelta = new Vector2(width, segment.sizeDelta.y);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = count.ToString();
        }

        static RectTransform EnsureIconFrame(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            var existing = parent.Find(name);
            var rect = existing != null ? existing.GetComponent<RectTransform>() : null;
            if (rect == null)
            {
                rect = CreatePanel(parent, name, position, size, Vector2.up, Vector2.up);
                rect.GetComponent<Image>().color = SlotColor;
                AddFrame(rect, size);
            }
            return rect;
        }

        static Image EnsureImage(RectTransform parent, string name, Vector2 size)
        {
            var existing = parent.Find(name);
            var image = existing != null ? existing.GetComponent<Image>() : null;
            bool createdImage = image == null;
            if (createdImage)
            {
                image = new GameObject(name).AddComponent<Image>();
                image.transform.SetParent(parent, false);
            }
            image.raycastTarget = false;
            if (createdImage)
            {
                image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                image.rectTransform.anchoredPosition = Vector2.zero;
                image.rectTransform.sizeDelta = size;
            }
            return image;
        }

        static Image EnsureHorizontalBar(RectTransform parent, string name, Vector2 position, Vector2 size, Color fillColor, out Text label)
        {
            var existing = parent.Find(name);
            var root = existing != null ? existing.GetComponent<RectTransform>() : null;
            if (root == null)
            {
                root = CreatePanel(parent, name, position, size, Vector2.up, Vector2.up);
                root.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.025f, 0.88f);
            }

            var fillTransform = root.Find("Fill");
            var fill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            if (fill == null)
            {
                fill = new GameObject("Fill").AddComponent<Image>();
                fill.transform.SetParent(root, false);
            }
            fill.color = fillColor;
            fill.raycastTarget = false;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);

            var labelTransform = root.Find("Label");
            label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label == null) label = CreateText(root, "Label", "", 13, Vector2.zero, size, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = Vector2.zero;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            return fill;
        }

        static Text BindStatText(RectTransform parent, string name)
        {
            if (parent == null) return null;
            var value = parent.Find(name + " Box/Value");
            if (value != null && value.GetComponent<Text>() != null) return value.GetComponent<Text>();
            return null;
        }

        static void SetStatColumns(RectTransform rect, float minX, float maxX, float left, float right)
        {
            rect.anchorMin = new Vector2(minX, 0f);
            rect.anchorMax = new Vector2(maxX, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.offsetMin = new Vector2(left, 0f);
            rect.offsetMax = new Vector2(right, 0f);
        }

        void BuildStagePanel(Transform parent)
        {
            var existing = parent.Find("Stage Panel");
            var root = existing != null ? existing.GetComponent<RectTransform>() : null;
            if (root == null)
            {
                root = CreatePanel(parent, "Stage Panel", new Vector2(-222f, -28f), new Vector2(118f, 34f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
                AddFrame(root, root.sizeDelta);
                root.anchorMin = new Vector2(0.5f, 1f);
                root.anchorMax = new Vector2(0.5f, 1f);
                root.pivot = new Vector2(0.5f, 1f);
                root.anchoredPosition = new Vector2(-222f, -28f);
                root.sizeDelta = new Vector2(118f, 34f);
            }

            var label = FindText(root, "Label");
            if (label == null) label = CreateText(root, "Label", "", 18, Vector2.zero, root.sizeDelta, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            stageText = label;
            SetStage(gameManager != null ? gameManager.CurrentStage : 1);
        }

        void BuildTowerPanel(Transform parent)
        {
            var existing = parent.Find("Tower Status");
            towerPanel = existing != null ? existing.GetComponent<RectTransform>() : null;
            if (towerPanel == null)
            {
                towerPanel = CreatePanel(parent, "Tower Status", new Vector2(-14f, -12f), TowerPanelSize, Vector2.one, Vector2.one);
                AddFrame(towerPanel, TowerPanelSize);
            }

            var towerImageTransform = towerPanel.Find("Tower Image");
            towerImage = towerImageTransform != null ? towerImageTransform.GetComponent<Image>() : null;
            bool createdTowerImage = towerImage == null;
            if (createdTowerImage)
            {
                towerImage = new GameObject("Tower Image").AddComponent<Image>();
                towerImage.transform.SetParent(towerPanel, false);
                towerImage.sprite = LoadHudSprite("Tower", CreateTowerSpriteFromRenderer(towerController));
                AnchorTopCenter(towerImage.rectTransform);
                towerImage.rectTransform.anchoredPosition = TowerIconPosition;
                towerImage.rectTransform.sizeDelta = TowerIconSize;
            }

            if (towerImage != null)
            {
                towerImage.preserveAspect = true;
                towerImage.raycastTarget = false;
            }

            var barTransform = towerPanel.Find("Tower HP Bar");
            var barRoot = barTransform != null ? barTransform.GetComponent<RectTransform>() : null;
            if (barRoot == null) barRoot = CreatePanel(towerPanel, "Tower HP Bar", TowerHpBarPosition, TowerHpBarSize, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            barRoot.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.025f, 0.86f);
            var fillTransform = barRoot.Find("Fill");
            hpFill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
            if (hpFill == null) hpFill = new GameObject("Fill").AddComponent<Image>();
            hpFill.transform.SetParent(barRoot, false);
            hpFill.color = HpBlue;
            hpFill.rectTransform.anchorMin = Vector2.zero;
            hpFill.rectTransform.anchorMax = Vector2.one;
            hpFill.rectTransform.pivot = new Vector2(0.5f, 0f);
            hpFill.rectTransform.offsetMin = new Vector2(4f, 4f);
            hpFill.rectTransform.offsetMax = new Vector2(-4f, -4f);

            var hpTextTransform = towerPanel.Find("Tower HP Text");
            hpText = hpTextTransform != null ? hpTextTransform.GetComponent<Text>() : null;
            if (hpText == null)
            {
                hpText = CreateText(towerPanel, "Tower HP Text", "", 13, TowerHpTextPosition, new Vector2(88f, 20f), TextAnchor.MiddleCenter);
                AnchorTopCenter(hpText.rectTransform);
            }
        }

        void OnTowerUpgraded(Sprite sprite)
        {
            var nextSprite = sprite != null ? sprite : LoadHudSprite("TowerUpgrade", null);
            if (towerImage != null && nextSprite != null)
            {
                towerImage.sprite = nextSprite;
                towerImage.preserveAspect = true;
            }
        }

        void UpdateTowerPanel()
        {
            if (towerHealth == null || hpFill == null) return;
            float normalized = towerHealth.Normalized;
            hpFill.rectTransform.anchorMax = new Vector2(1f, Mathf.Clamp01(normalized));
            hpFill.color = TowerHpColor(normalized);
            if (hpText != null) hpText.text = towerHealth.currentHp + "/" + towerHealth.maxHp;
        }

        Color TowerHpColor(float normalized)
        {
            if (normalized <= LowHpBlinkThreshold)
            {
                float pulse = Mathf.PingPong(Time.unscaledTime * 4.6f, 1f);
                return Color.Lerp(HpRed, new Color(1f, 0.55f, 0.28f, 1f), pulse);
            }
            return normalized <= 0.5f ? HpYellow : HpBlue;
        }

        void OnTowerDamaged(Health _, int amount)
        {
            if (towerPanel == null || amount <= 0) return;
            var text = CreateText(towerPanel, "Tower Damage", amount.ToString(), 22, new Vector2(0f, -26f), new Vector2(72f, 32f), TextAnchor.MiddleCenter, HpRed);
            AnchorTopCenter(text.rectTransform);
            damagePopups.Add(new FloatingHudDamage(text));
        }

        void TickDamagePopups()
        {
            for (int i = damagePopups.Count - 1; i >= 0; i--)
            {
                if (damagePopups[i].Tick(Time.unscaledDeltaTime)) damagePopups.RemoveAt(i);
            }
        }

        void HideLegacyBuildStatus(Transform canvas)
        {
            if (buildPlacement == null || buildPlacement.buildText == null) return;
            if (buildPlacement.buildText.name != "Build Status")
            {
                buildPlacement.buildText.gameObject.SetActive(false);
            }
            var backplate = canvas.Find("Build Backplate");
            if (backplate != null) backplate.gameObject.SetActive(false);
        }

        static Canvas CreateCanvas()
        {
            var canvas = new GameObject("HUD").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static Canvas FindHudCanvas()
        {
            var canvases = FindObjectsOfType<Canvas>();
            foreach (var canvas in canvases)
            {
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.name == "HUD") return canvas;
            }
            foreach (var canvas in canvases)
            {
                if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) return canvas;
            }
            return null;
        }

        static RectTransform CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, Vector2 anchorMin, Vector2 anchorMax)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = PanelColor;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        static Text FindText(Transform parent, string path)
        {
            if (parent == null || string.IsNullOrEmpty(path)) return null;
            var target = parent.Find(path);
            return target != null ? target.GetComponent<Text>() : null;
        }

        static Image FindImage(Transform parent, string path)
        {
            if (parent == null || string.IsNullOrEmpty(path)) return null;
            var target = parent.Find(path);
            return target != null ? target.GetComponent<Image>() : null;
        }

        static RectTransform FindRect(Transform parent, string path)
        {
            if (parent == null || string.IsNullOrEmpty(path)) return null;
            var target = parent.Find(path);
            return target != null ? target.GetComponent<RectTransform>() : null;
        }

        static void ConfigureStaticHudIcon(Transform parent, string path)
        {
            if (parent == null) return;
            var target = parent.Find(path);
            var image = target != null ? target.GetComponent<Image>() : null;
            if (image == null) return;
            image.preserveAspect = true;
            image.color = Color.white;
            AddUiIconOutline(image);
        }

        static void AddUiIconOutline(Image image)
        {
            if (image == null) return;
            var outline = image.GetComponent<Outline>();
            if (outline == null) outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
        }

        static Text CreateText(Transform parent, string name, string value, int fontSize, Vector2 position, Vector2 size, TextAnchor alignment, Color? color = null)
        {
            var text = new GameObject(name).AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = size;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }

        static void AnchorTopCenter(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
        }

        static Sprite LoadHudSprite(string name, Sprite fallback)
        {
            if (fallback != null) return fallback;
            var sprite = GeneratedSpriteLoader.Load(name);
            if (sprite != null) return sprite;
            var texture = GeneratedSpriteLoader.LoadTexture(name);
            if (texture != null)
            {
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 128f);
            }
            return name == "Tower" ? CreateTowerHudSprite() : null;
        }

        static Sprite CreateTowerSpriteFromRenderer(TowerController tower)
        {
            if (tower == null) return null;
            var baseImage = tower.transform.Find("Base Tower Image")?.GetComponent<PaperMeshVisual>();
            if (baseImage != null && baseImage.sprite != null) return baseImage.sprite;

            var renderers = tower.GetComponentsInChildren<Renderer>(true);
            Texture2D bestTexture = null;
            int bestPixels = 0;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                foreach (var material in materials)
                {
                    var texture = material != null ? material.mainTexture as Texture2D : null;
                    if (texture == null || !texture.name.Contains("Tower")) continue;
                    int pixels = texture.width * texture.height;
                    if (pixels <= bestPixels) continue;
                    bestTexture = texture;
                    bestPixels = pixels;
                }
            }

            if (bestTexture == null) return null;
            return Sprite.Create(bestTexture, TowerTextureRect(bestTexture), new Vector2(0.5f, 0.5f), 128f);
        }

        static Rect TowerTextureRect(Texture2D texture)
        {
            float x = texture.width * 0.08f;
            float y = texture.height * 0.04f;
            float width = texture.width * 0.84f;
            float height = texture.height * 0.92f;
            return new Rect(x, y, width, height);
        }

        static Sprite CreateTowerHudSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) texture.SetPixel(x, y, clear);
            }

            Fill(texture, 7, 4, 18, 14, new Color(0.55f, 0.56f, 0.55f, 1f));
            Fill(texture, 5, 13, 22, 8, new Color(0.42f, 0.43f, 0.43f, 1f));
            Fill(texture, 7, 21, 18, 5, new Color(0.30f, 0.31f, 0.32f, 1f));
            Fill(texture, 9, 6, 4, 7, new Color(0.74f, 0.75f, 0.72f, 1f));
            Fill(texture, 19, 6, 4, 7, new Color(0.74f, 0.75f, 0.72f, 1f));
            Fill(texture, 13, 4, 6, 7, new Color(0.42f, 0.22f, 0.08f, 1f));
            Fill(texture, 12, 15, 8, 6, new Color(0.08f, 0.35f, 0.62f, 1f));
            Fill(texture, 15, 15, 2, 6, new Color(0.95f, 0.74f, 0.16f, 1f));
            Fill(texture, 13, 17, 6, 2, new Color(0.95f, 0.74f, 0.16f, 1f));
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 128f);
        }

        static void Fill(Texture2D texture, int x, int y, int width, int height, Color color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++) texture.SetPixel(px, py, color);
            }
        }

        static void AddFrame(Transform parent, Vector2 size)
        {
            UiBoxOutline.Apply(parent, EdgeColor, 2f);
        }

        sealed class FloatingHudDamage
        {
            readonly Text text;
            float age;
            const float Lifetime = 0.85f;

            public FloatingHudDamage(Text text)
            {
                this.text = text;
            }

            public bool Tick(float deltaTime)
            {
                if (text == null) return true;
                age += deltaTime;
                float t = Mathf.Clamp01(age / Lifetime);
                text.rectTransform.anchoredPosition += new Vector2(0f, 46f * deltaTime);
                text.transform.localScale = Vector3.one * Mathf.Lerp(1.18f, 0.9f, t);
                var color = text.color;
                color.a = t < 0.48f ? 1f : 1f - Mathf.InverseLerp(0.48f, 1f, t);
                text.color = color;
                if (age < Lifetime) return false;
                Destroy(text.gameObject);
                return true;
            }
        }
    }
}
