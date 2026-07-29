using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed partial class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public static bool IsWorldInputSuspended =>
            Time.timeScale <= 0f ||
            (Instance != null && Instance.gameEnding);

        public GameConfig config;
        public TileGrid grid;
        public PlayerController playerPrefab;
        public TowerController sceneTower;
        public EnemySpawner spawner;
        public FixedBuildingLayoutService fixedBuildingLayout;
        public GameHudController gameHud;
        public Text timerText;
        public Text killText;
        public Text levelText;
        public Slider xpBar;
        public GameObject levelUpPanel;
        public Button[] upgradeButtons;
        public Button skipLevelUpButton;
        public Button rerollLevelUpButton;
        public GameObject levelUpInputBlocker;
        public GameObject relicChestPrefab;
        public RelicAcquisitionPanel relicAcquisitionPanelPrefab;
        public ScreenFadeOverlay screenFade;

        public PlayerController Player { get; private set; }
        public TowerController Tower { get; private set; }
        public int CurrentStage => currentStage;
        public int CurrentLevel => level;
        public float ElapsedSeconds => elapsed;
        public IReadOnlyList<string> RunUpgrades => runUpgrades;
        public IReadOnlyList<string> RunRelics => runRelics;
        readonly RuntimeResourceDiagnostics runtimeResourceDiagnostics = new RuntimeResourceDiagnostics();
        public int CurrentXp => xp;
        public int XpToNext => xpToNext;
        public int RunTokens => tokenRuntime.RunTokens;
        public int Kills => kills;
        public bool BossActive => bossActive;
        public event Action CombatModifiersChanged;

        int kills;
        int level = 1;
        int xp;
        int xpToNext = 5;
        int damageDealt;
        float elapsed;
        float hudElapsed;
        float xpRemainder;
        int currentStage = 1;
        bool bossActive;
        bool gameEnding;
        bool endingCutsceneActive;
        readonly List<string> runUpgrades = new List<string>();
        readonly List<string> runRelics = new List<string>();
        readonly List<RunRelicReportEntry> runRelicEntries = new List<RunRelicReportEntry>();
        readonly List<RunStageLogEntry> runReachedStages = new List<RunStageLogEntry>();
        readonly List<RunBossClearLogEntry> runBossClears = new List<RunBossClearLogEntry>();
        readonly List<AttractablePickup> stageTransitionPickups =
            new List<AttractablePickup>(512);
        readonly RunDamageTracker runDamageTracker = new RunDamageTracker();
        readonly RunDifficultyTelemetry runDifficultyTelemetry = new RunDifficultyTelemetry();
        readonly TokenRuntimeService tokenRuntime = new TokenRuntimeService();
        const int LevelUpSkipLimit = 3;
        const int InitialTowerTerritoryRadius = 10;
        const float OpeningEffectsDelayAfterFadeSeconds = 0.15f;
        const float StageTransitionPickupAttractionTimeoutMultiplier = 4f;
        const float StageTransitionPickupAttractionTimeoutPaddingSeconds = 1f;
        int runStartStage = 1;
        int runStartStageDifficulty = 1;
        int pendingOpeningLevelUps;
        int pendingRunLevelUps;
        int remainingLevelUpSkips;
        int remainingLevelUpRerolls;
        int lastLevelUpActionFrame;
        int activeRelicAcquisitionModalCount;
        int levelUpInputBlockedThroughFrame = -1;
        string runSessionId;
        static readonly Color UpgradeNormalColor = new Color(0.12f, 0.20f, 0.16f, 0.94f);
        static readonly Color UpgradeHoverColor = new Color(0.106f, 0.353f, 0.216f, 0.98f);
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Duplicate GameManager detected. The duplicate component will be disabled.", this);
                enabled = false;
                Destroy(this);
                return;
            }

            Instance = this;
            runtimeResourceDiagnostics.Start();
        }

        void Start()
        {
            Time.timeScale = screenFade != null ? 0f : 1f;
            remainingLevelUpSkips = LevelUpSkipLimit;
            remainingLevelUpRerolls = ProgressionStore.GetInitialLevelUpRerollCount();
            lastLevelUpActionFrame = -1;
            AudioManager.PlayBgm(BgmTrack.GameNormal);

            runDamageTracker.Reset();
            runDifficultyTelemetry.Reset();
            runRelics.Clear();
            runRelicEntries.Clear();
            runReachedStages.Clear();
            runBossClears.Clear();
            runSessionId = Guid.NewGuid().ToString("N");
            tokenRuntime.Initialize();
            config = Instantiate(config);
            config.EnsureEnemySpawnDefaults();
            config.EnsureWeaponLevelDefaults();
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
            grid.PlayerCellsPainted += OnPlayerCellsPainted;
            ApplyUnlockedCenterTowerUpgrade();
            int stage = RunState.ConsumeNextStartStage();
            runStartStage = Mathf.Max(1, stage);
            runStartStageDifficulty = ProgressionStore.GetStageDifficulty(runStartStage);
            float startStageElapsedSeconds = RunState.ConsumeNextStartStageElapsed();
            bool hasBossTestSpawnSide = RunState.TryConsumeNextBossTestSpawnSide(out var bossTestSpawnSide);
            if (fixedBuildingLayout != null)
            {
                fixedBuildingLayout.Initialize(config, grid, spawner != null ? spawner.damagePopupPrefab : null);
                fixedBuildingLayout.SpawnUnlockedBuildings();
            }

            Player = Instantiate(playerPrefab, grid.GridToWorld(grid.width / 2, grid.height / 2 - 6), Quaternion.identity);
            if (spawner != null) Player.damagePopupPrefab = spawner.damagePopupPrefab;
            var selectedCharacter = CharacterUnlockCatalog.IsUnlocked(RunState.SelectedCharacter)
                ? RunState.SelectedCharacter
                : CharacterType.Knight;
            RunState.SelectedCharacter = selectedCharacter;
            Player.Configure(config, grid, selectedCharacter);
            runDifficultyTelemetry.Bind(Player);

            PolishHud();
            ConfigureGameHud();

            var cameraFollow = Camera.main.GetComponent<CameraFollow>();
            if (cameraFollow != null && Player != null) cameraFollow.Configure(Player.transform, Tower.transform, config);
            if (hasBossTestSpawnSide && spawner != null) spawner.SetNextBossTestSpawnSide(bossTestSpawnSide);
            UpdateHud();
            StartCoroutine(BeginGameAfterFade(stage, startStageElapsedSeconds));
        }

        IEnumerator BeginGameAfterFade(int stage, float startStageElapsedSeconds)
        {
            if (screenFade != null)
            {
                yield return screenFade.FadeFromBlack();
                yield return new WaitForSecondsRealtime(OpeningEffectsDelayAfterFadeSeconds);
            }
            else
            {
                Debug.LogError("GameManager requires a Scene-authored ScreenFadeOverlay.");
            }

            Time.timeScale = 1f;
            SpawnInitialRelicChest();
            BeginStage(stage, startStageElapsedSeconds);
            UpdateHud();
            BeginOpeningPlayerLevelBonus();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (grid != null) grid.PlayerCellsPainted -= OnPlayerCellsPainted;
            runDifficultyTelemetry.Dispose();
            runtimeResourceDiagnostics.Dispose();
        }

        void RebuildMapPerimeter()
        {
            var perimeter = FindObjectOfType<MapPerimeterController>();
            if (perimeter == null || grid == null) return;
            perimeter.grid = grid;
            perimeter.Rebuild();
        }

        void SpawnInitialRelicChest()
        {
            if (!ProgressionStore.IsUnlocked(UpgradeType.UnlockOpeningRelicChest)) return;
            if (grid == null) return;

            var position = grid.GridToWorld(grid.width / 2, grid.height / 2 - 3);
            SpawnRelicChest(position);
        }

        void SpawnRelicChest(Vector3 position)
        {
            if (relicChestPrefab == null) return;
            Instantiate(relicChestPrefab, position, Quaternion.identity);
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

        void Update()
        {
            if (!bossActive)
            {
                elapsed += Time.deltaTime;
                AwardElapsedTimeTokens();
            }

            hudElapsed = elapsed;
            runDifficultyTelemetry.UpdatePeakAliveEnemies(EnemyController.ActiveEnemyCount);
            UpdateLevelUpButtonHover();
            UpdateHud();
        }

        public void RegisterEnemySpawn(EnemyController enemy)
        {
            runDifficultyTelemetry.RecordEnemySpawn(enemy, currentStage);
        }

        public void RegisterKill(EnemyController enemy = null)
        {
            kills++;
            runDifficultyTelemetry.RecordEnemyKill(enemy, currentStage);
            SteamAchievementRuntime.ReportTotalKills(ProgressionStore.Data.totalKills + kills);
            if (kills % 100 == 0) CombatModifiersChanged?.Invoke();
            AwardKillTokens();
        }

        void AwardKillTokens()
        {
            int rewards = tokenRuntime.AwardKillTokens(gameEnding, config);
            if (rewards <= 0) return;
            AddRunTokens(rewards, RunTokenSource.KillMilestone);
        }

        void AwardElapsedTimeTokens()
        {
            int rewards = tokenRuntime.AwardElapsedTimeTokens(elapsed, gameEnding);
            if (rewards > 0) AddRunTokens(rewards, RunTokenSource.ElapsedTime);
        }

        public void RegisterDamageDealt(int amount)
        {
            damageDealt += Mathf.Max(0, amount);
        }

        public void RegisterDamageDealt(RunDamageSource source, int amount)
        {
            int safeAmount = Mathf.Max(0, amount);
            if (safeAmount <= 0) return;
            damageDealt += safeAmount;
            runDamageTracker.RegisterDamage(source, safeAmount);
        }

        public void RegisterWeaponDamage(WeaponType type, int amount)
        {
            RegisterDamageDealt(RunDamageSource.ForWeapon(type), amount);
        }

        public void RegisterBuildingDamage(RunDamageBuildingSource source, int amount)
        {
            RegisterDamageDealt(RunDamageSource.ForBuilding(source), amount);
        }

        public void RegisterWeaponSlot(WeaponType type, int slotIndex)
        {
            runDamageTracker.RegisterWeaponSlot(type, slotIndex, WeaponCatalog.DisplayNameSource(type));
        }

        public void MarkWeaponActive(WeaponType type)
        {
            if (gameEnding) return;
            runDamageTracker.MarkActive(RunDamageSource.ForWeapon(type));
        }

        public void MarkBuildingDamageSourceActive(RunDamageBuildingSource source)
        {
            if (gameEnding) return;
            runDamageTracker.MarkActive(RunDamageSource.ForBuilding(source));
        }

        public void AddRunTokens(int amount, RunTokenSource source = RunTokenSource.TokenOrb)
        {
            var result = tokenRuntime.AddRunTokens(amount, source);
            if (result.gained <= 0) return;
            if (result.attackTierChanged) CombatModifiersChanged?.Invoke();
            ShowTokenGainFeedback(result.gained);
            UpdateHud();
        }

        void OnPlayerCellsPainted(int count)
        {
            int reward = tokenRuntime.CalculatePaintAreaTokenReward(count);
            if (reward > 0) AddRunTokens(reward, RunTokenSource.PaintArea);
        }

        void ShowTokenGainFeedback(int amount)
        {
            AudioManager.PlaySfx(SfxTrack.TokenGain);
            Player?.ShowTokenGain(amount);
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
            gameHud.Initialize(Tower, this);
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
            public readonly string sourceLabel;
            public readonly string iconResource;
            public readonly string weaponName;
            public readonly string weaponIconResource;
            public readonly string upgradeText;
            public readonly bool isNewWeapon;
            public readonly bool isEvolution;
            public readonly bool hasWeaponType;
            public readonly WeaponType weaponType;
            public readonly bool hasAttributeType;
            public readonly WeaponAttributeType attributeType;
            public readonly bool hasDiminishingStat;
            public readonly RunWeaponUpgradeStat diminishingStat;
            public readonly Action apply;

            public RunUpgradeChoice(string label, string iconResource, Action apply)
            {
                this.label = label;
                sourceLabel = label;
                this.iconResource = iconResource;
                weaponName = string.Empty;
                weaponIconResource = null;
                upgradeText = label;
                isNewWeapon = false;
                isEvolution = false;
                hasWeaponType = false;
                weaponType = default;
                this.apply = apply;
                hasAttributeType = false;
                attributeType = WeaponAttributeType.None;
                hasDiminishingStat = false;
                diminishingStat = RunWeaponUpgradeStat.None;
            }

            public RunUpgradeChoice(
                WeaponType weaponType,
                string upgradeText,
                string iconResource,
                Action apply,
                RunWeaponUpgradeStat diminishingStat = RunWeaponUpgradeStat.None)
            {
                this.weaponType = weaponType;
                hasWeaponType = true;
                weaponName = WeaponDisplayName(weaponType);
                weaponIconResource = WeaponIconResource(weaponType);
                this.upgradeText = upgradeText;
                this.label = weaponName + " " + upgradeText;
                sourceLabel = WeaponCatalog.DisplayNameSource(weaponType) + " " + upgradeText;
                this.iconResource = iconResource;
                this.attributeType = WeaponAttributeCatalog.ForWeapon(weaponType);
                this.apply = apply;
                isNewWeapon = false;
                isEvolution = false;
                hasAttributeType = this.attributeType != WeaponAttributeType.None;
                this.diminishingStat = diminishingStat;
                hasDiminishingStat = diminishingStat != RunWeaponUpgradeStat.None;
            }

            RunUpgradeChoice(WeaponType weaponType, bool newWeapon, Action apply)
            {
                this.weaponType = weaponType;
                hasWeaponType = true;
                weaponName = WeaponDisplayName(weaponType);
                weaponIconResource = WeaponIconResource(weaponType);
                upgradeText = "新規武器：" + weaponName + "を獲得";
                label = upgradeText;
                sourceLabel = "新規武器：" + WeaponCatalog.DisplayNameSource(weaponType) + "を獲得";
                iconResource = StatIconCatalog.WeaponLevel;
                attributeType = WeaponAttributeCatalog.ForWeapon(weaponType);
                this.apply = apply;
                isNewWeapon = newWeapon;
                isEvolution = false;
                hasAttributeType = attributeType != WeaponAttributeType.None;
                hasDiminishingStat = false;
                diminishingStat = RunWeaponUpgradeStat.None;
            }

            RunUpgradeChoice(WeaponType evolutionType, Action apply, byte evolutionMarker)
            {
                weaponType = evolutionType;
                hasWeaponType = true;
                weaponName = WeaponDisplayName(evolutionType);
                weaponIconResource = WeaponIconResource(evolutionType);
                upgradeText = WeaponCatalog.EvolutionChoiceDescriptionSource(evolutionType);
                label = weaponName + " " + upgradeText;
                sourceLabel = WeaponCatalog.DisplayNameSource(evolutionType) + " " + upgradeText;
                iconResource = StatIconCatalog.WeaponLevel;
                attributeType = WeaponAttributeCatalog.ForWeapon(evolutionType);
                this.apply = apply;
                isNewWeapon = false;
                isEvolution = true;
                hasAttributeType = attributeType != WeaponAttributeType.None;
                hasDiminishingStat = false;
                diminishingStat = RunWeaponUpgradeStat.None;
            }

            public static RunUpgradeChoice NewWeapon(WeaponType weaponType, Action apply)
            {
                return new RunUpgradeChoice(weaponType, true, apply);
            }

            public static RunUpgradeChoice Evolution(WeaponType evolutionType, Action apply)
            {
                return new RunUpgradeChoice(evolutionType, apply, 0);
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
}
