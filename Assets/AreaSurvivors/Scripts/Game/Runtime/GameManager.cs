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
        readonly RunDamageTracker runDamageTracker = new RunDamageTracker();
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
            UpdateLevelUpButtonHover();
            UpdateHud();
        }

        public void RegisterKill()
        {
            kills++;
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

        public void AddExperience(int amount)
        {
            float multiplier = Player != null ? Mathf.Max(0f, Player.Stats.xpGainMultiplier) : 1f;
            xpRemainder += Mathf.Max(1, amount) * multiplier;
            int gained = Mathf.FloorToInt(xpRemainder);
            if (gained <= 0) return;
            xpRemainder -= gained;
            xp += gained;
            int gainedLevels = 0;
            while (xp >= xpToNext)
            {
                xp -= xpToNext;
                level++;
                gainedLevels++;
                ApplyPlayerLevelStatBonus();
                xpToNext = CalculateNextXpRequirement(xpToNext, level);
            }
            QueueRunLevelUps(gainedLevels);
            UpdateHud();
        }

        void QueueRunLevelUps(int count)
        {
            if (count <= 0) return;
            pendingRunLevelUps += count;
            TryShowNextRunLevelUp();
        }

        bool TryShowNextRunLevelUp()
        {
            if (pendingRunLevelUps <= 0 ||
                pendingOpeningLevelUps > 0 ||
                Player == null ||
                levelUpPanel == null ||
                levelUpPanel.activeInHierarchy)
            {
                return false;
            }

            pendingRunLevelUps--;
            ShowLevelUp();
            return true;
        }

        int CalculateNextXpRequirement(int currentRequirement, int currentLevel)
        {
            float growthStart = config != null ? Mathf.Max(1f, config.xpRequirementGrowthStart) : 1.35f;
            float growthEnd = config != null ? Mathf.Max(1f, config.xpRequirementGrowthEnd) : 1.1f;
            int growthStartLevel = config != null ? Mathf.Max(2, config.xpRequirementGrowthStartLevel) : 2;
            int growthEndLevel = config != null
                ? Mathf.Max(growthStartLevel + 1, config.xpRequirementGrowthEndLevel)
                : 39;
            float flatBonus = config != null ? Mathf.Max(0f, config.xpRequirementFlatBonus) : 3f;
            int clampedLevel = Mathf.Clamp(currentLevel, growthStartLevel, growthEndLevel);
            float progress = Mathf.InverseLerp(growthStartLevel, growthEndLevel, clampedLevel);
            float growth = Mathf.Lerp(growthStart, growthEnd, progress);
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, currentRequirement) * growth + flatBonus));
        }

        void ApplyPlayerLevelStatBonus()
        {
            if (Player == null) return;

            Player.StatsSource?.SetLevelStatBonusCount(Mathf.Max(0, level - 1));
            Player.ApplyCurrentStats(false);
        }

        void BeginOpeningPlayerLevelBonus()
        {
            pendingOpeningLevelUps = Mathf.Clamp(
                ProgressionStore.GetLevel(UpgradeType.OpeningPlayerLevel),
                0,
                ProgressionStore.GetMaxLevel(UpgradeType.OpeningPlayerLevel));
            if (pendingOpeningLevelUps <= 0 || Player == null) return;

            xp = 0;
            xpRemainder = 0f;
            ShowNextOpeningLevelUp();
        }

        void ShowNextOpeningLevelUp()
        {
            if (pendingOpeningLevelUps <= 0 || Player == null) return;

            pendingOpeningLevelUps--;
            level++;
            xp = 0;
            xpRemainder = 0f;
            ApplyPlayerLevelStatBonus();
            xpToNext = CalculateNextXpRequirement(xpToNext, level);
            UpdateHud();
            ShowLevelUp();
        }

        void ShowLevelUp()
        {
            AudioManager.PlaySfx(SfxTrack.LevelUp);
            Time.timeScale = 0f;
            ShowLevelUpInputBlocker(true);
            levelUpPanel.SetActive(true);
            levelUpPanel.transform.SetAsLastSibling();
            RefreshLevelUpChoices();
        }

        void RefreshLevelUpChoices()
        {
            var choices = RollUpgrades();
            int buttonCount = upgradeButtons != null ? upgradeButtons.Length : 0;
            for (int i = 0; i < buttonCount; i++)
            {
                int index = i;
                var button = upgradeButtons[i];
                if (button == null) continue;
                button.gameObject.SetActive(index < choices.Count);
                button.onClick.RemoveAllListeners();
                if (index >= choices.Count) continue;
                var choice = choices[index];
                ConfigureLevelUpButton(button, choice);
                button.onClick.AddListener(() => ApplyRunUpgrade(choice));
            }

            ConfigureLevelUpActionButtons();
            ConfigureLevelUpNavigation();
            SelectFirstLevelUpButton();
            StartCoroutine(SelectFirstLevelUpButtonNextFrame());
        }

        void ConfigureLevelUpActionButtons()
        {
            ConfigureLevelUpActionButton(
                skipLevelUpButton,
                LocalizationService.Format("スキップ 残り{0}", "SKIP ({0} LEFT)", remainingLevelUpSkips),
                remainingLevelUpSkips > 0,
                SkipLevelUp);
            ConfigureLevelUpActionButton(
                rerollLevelUpButton,
                LocalizationService.Format("リロール 残り{0}", "REROLL ({0} LEFT)", remainingLevelUpRerolls),
                remainingLevelUpRerolls > 0,
                RerollLevelUp);
        }

        static void ConfigureLevelUpActionButton(Button button, string labelText, bool interactable, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.text = labelText;
            button.interactable = interactable;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        void ConfigureLevelUpNavigation()
        {
            var activeUpgrades = new List<Button>();
            if (upgradeButtons != null)
            {
                for (int i = 0; i < upgradeButtons.Length; i++)
                {
                    if (UiSelectionUtility.IsSelectable(upgradeButtons[i])) activeUpgrades.Add(upgradeButtons[i]);
                }
            }

            var skip = UiSelectionUtility.IsSelectable(skipLevelUpButton) ? skipLevelUpButton : null;
            var reroll = UiSelectionUtility.IsSelectable(rerollLevelUpButton) ? rerollLevelUpButton : null;
            var firstAction = skip != null ? skip : reroll;
            for (int i = 0; i < activeUpgrades.Count; i++)
            {
                var button = activeUpgrades[i];
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.wrapAround = false;
                navigation.selectOnUp = i > 0 ? activeUpgrades[i - 1] : button;
                navigation.selectOnDown = i + 1 < activeUpgrades.Count
                    ? activeUpgrades[i + 1]
                    : firstAction != null ? firstAction : button;
                navigation.selectOnLeft = button;
                navigation.selectOnRight = button;
                button.navigation = navigation;
            }

            var lastUpgrade = activeUpgrades.Count > 0 ? activeUpgrades[activeUpgrades.Count - 1] : null;
            ConfigureLevelUpActionNavigation(skipLevelUpButton, null, reroll, lastUpgrade);
            ConfigureLevelUpActionNavigation(rerollLevelUpButton, skip, null, lastUpgrade);
        }

        static void ConfigureLevelUpActionNavigation(Button button, Button leftTarget, Button rightTarget, Button upTarget)
        {
            if (button == null) return;

            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = false;
            navigation.selectOnUp = upTarget != null ? upTarget : button;
            navigation.selectOnDown = button;
            navigation.selectOnLeft = leftTarget != null ? leftTarget : button;
            navigation.selectOnRight = rightTarget != null ? rightTarget : button;
            button.navigation = navigation;
        }

        void SelectFirstLevelUpButton()
        {
            var first = UiSelectionUtility.FirstSelectable(ActiveLevelUpButtons());
            if (first == null) return;

            UiSelectionUtility.SelectFirst(first);
            SetLevelUpHover(first as Button);
        }

        IEnumerator SelectFirstLevelUpButtonNextFrame()
        {
            yield return null;
            SelectFirstLevelUpButton();
        }

        List<RunUpgradeChoice> RollUpgrades()
        {
            var pool = new List<RunUpgradeChoice>();
            var weapon = Player != null ? Player.weapon : null;
            var evolutionChoices = new List<RunUpgradeChoice>();
            if (weapon != null)
            {
                bool canAcquireNewWeapon = weapon.HasOpenWeaponSlot;
                if (canAcquireNewWeapon)
                {
                    if (!weapon.SlashUnlocked)
                    {
                        pool.Add(RunUpgradeChoice.NewWeapon(WeaponType.Slash, () => weapon.UnlockSlash()));
                    }

                    foreach (var weaponType in WeaponCatalog.UnlockableWeapons)
                    {
                        if (weapon.IsWeaponUnlocked(weaponType)) continue;
                        if (!ProgressionStore.IsUnlocked(WeaponCatalog.UnlockUpgrade(weaponType))) continue;
                        var capturedType = weaponType;
                        pool.Add(RunUpgradeChoice.NewWeapon(capturedType, () => weapon.UnlockWeapon(capturedType)));
                    }
                }

                if (weapon.CanEvolveSlash)
                {
                    evolutionChoices.Add(RunUpgradeChoice.Evolution(WeaponType.SwordRush, () => weapon.EvolveSlash()));
                }
                if (weapon.CanEvolveBoomerangSword)
                {
                    evolutionChoices.Add(RunUpgradeChoice.Evolution(WeaponType.Banana, () => weapon.EvolveBoomerangSword()));
                }
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.AuraSword, WeaponType.Excalibur);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Arrow, WeaponType.GoldenBow);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.ArrowRain, WeaponType.ArrowShower);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Gun, WeaponType.MachineGun);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Fireball, WeaponType.FireMissile);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Frost, WeaponType.FrostStorm);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.ThunderBall, WeaponType.ThunderStorm);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Shield, WeaponType.DualShield);
                AddEvolutionChoice(evolutionChoices, weapon, WeaponType.Flag, WeaponType.GoddessBlessing);

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
            for (int i = 0; i < evolutionChoices.Count && result.Count < 3; i++) result.Add(evolutionChoices[i]);
            while (result.Count < 3 && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }

        static void AddEvolutionChoice(List<RunUpgradeChoice> choices, WeaponController weapon, WeaponType sourceType, WeaponType evolutionType)
        {
            if (weapon == null || !weapon.CanEvolveWeapon(sourceType)) return;
            choices.Add(RunUpgradeChoice.Evolution(evolutionType, () => weapon.EvolveWeapon(sourceType)));
        }

        void AddSlashUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.SlashStats;
            var displayType = weapon.GetDisplayWeaponType(WeaponType.Slash);
            int attackBonus = config.GetRunAttackPowerBonus(WeaponType.Slash);
            float cooldownMultiplier = Mathf.Clamp(config.runAttackCooldownMultiplier, 0.05f, 1f);
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃力 " + weapon.SlashAttackPower + ">" + (weapon.SlashAttackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddSlashAttack(attackBonus)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier),
                StatIconCatalog.Cooldown,
                () => weapon.MultiplySlashCooldown(cooldownMultiplier)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "ノックバック " + Number(stats.knockback) + ">" + Number(stats.knockback + config.runWeaponKnockbackBonus),
                StatIconCatalog.Knockback,
                () => weapon.AddSlashKnockback(config.runWeaponKnockbackBonus)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + config.runMediumRangeBonus),
                StatIconCatalog.Range,
                () => weapon.AddSlashRange(config.runMediumRangeBonus)));
        }

        void AddArrowUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.ArrowStats;
            var displayType = weapon.GetDisplayWeaponType(WeaponType.Arrow);
            int attackBonus = config.GetRunAttackPowerBonus(WeaponType.Arrow);
            float cooldownMultiplier = Mathf.Clamp(config.runAttackCooldownMultiplier, 0.05f, 1f);
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddArrowAttack(attackBonus)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier),
                StatIconCatalog.Cooldown,
                () => weapon.MultiplyArrowCooldown(cooldownMultiplier)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "矢の本数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus),
                StatIconCatalog.Projectile,
                () => weapon.AddArrowProjectileCount(config.runProjectileCountBonus)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "射程 " + Number(stats.range) + ">" + Number(stats.range + config.runProjectileRangeBonus),
                StatIconCatalog.Range,
                () => weapon.AddArrowRange(config.runProjectileRangeBonus)));
        }

        void AddFireballUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.FireballStats;
            var displayType = weapon.GetDisplayWeaponType(WeaponType.Fireball);
            int attackBonus = config.GetRunAttackPowerBonus(WeaponType.Fireball);
            float cooldownMultiplier = Mathf.Clamp(config.runAttackCooldownMultiplier, 0.05f, 1f);
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddFireballAttack(attackBonus)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier),
                StatIconCatalog.Cooldown,
                () => weapon.MultiplyFireballCooldown(cooldownMultiplier)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "爆発範囲 " + Number(stats.explosionRadius) + ">" + Number(stats.explosionRadius + config.runExplosionRadiusBonus),
                StatIconCatalog.Range,
                () => weapon.AddFireballExplosionRadius(config.runExplosionRadiusBonus)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "射程 " + Number(weapon.FireballRange) + ">" + Number(weapon.FireballRange + config.runProjectileRangeBonus),
                StatIconCatalog.Range,
                () => weapon.AddFireballRange(config.runProjectileRangeBonus)));
        }

        void AddShieldUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon)
        {
            var stats = weapon.ShieldStats;
            var displayType = weapon.GetDisplayWeaponType(WeaponType.Shield);
            int attackBonus = config.GetRunAttackPowerBonus(WeaponType.Shield);
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddShieldAttack(attackBonus)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "シールド数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus),
                StatIconCatalog.Defense,
                () => weapon.AddShieldCount(config.runProjectileCountBonus)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "ノックバック " + Number(stats.knockback) + ">" + Number(stats.knockback + config.runWeaponKnockbackBonus),
                StatIconCatalog.Knockback,
                () => weapon.AddShieldKnockback(config.runWeaponKnockbackBonus)));
            pool.Add(new RunUpgradeChoice(
                displayType,
                "回転速度 " + Number(stats.rotationSpeed) + ">" + Number(stats.rotationSpeed + config.runShieldRotationSpeedBonus),
                StatIconCatalog.MoveSpeed,
                () => weapon.AddShieldRotationSpeed(config.runShieldRotationSpeedBonus)));
        }

        void AddAdvancedWeaponUpgradeChoices(List<RunUpgradeChoice> pool, WeaponController weapon, WeaponType type)
        {
            var stats = weapon.GetWeaponStatsFor(type);
            var displayType = weapon.GetDisplayWeaponType(type);
            int attackBonus = config.GetRunAttackPowerBonus(type);
            float cooldownMultiplier = Mathf.Clamp(config.runAttackCooldownMultiplier, 0.05f, 1f);
            pool.Add(new RunUpgradeChoice(
                displayType,
                "攻撃力 " + stats.attackPower + ">" + (stats.attackPower + attackBonus),
                StatIconCatalog.Attack,
                () => weapon.AddWeaponAttack(type, attackBonus)));

            switch (type)
            {
                case WeaponType.Flag:
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + config.runAreaRangeBonus), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, config.runAreaRangeBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "速度低下 " + Percent(stats.slowAmount) + ">" + Percent(stats.slowAmount + config.runSlowBonus), StatIconCatalog.MoveSpeed, () => weapon.AddWeaponSlow(type, config.runSlowBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃間隔 " + Seconds(stats.damageIntervalSeconds) + ">" + Seconds(stats.damageIntervalSeconds * cooldownMultiplier), StatIconCatalog.Cooldown, () => weapon.MultiplyWeaponDamageInterval(type, cooldownMultiplier)));
                    break;
                case WeaponType.BoomerangSword:
                    pool.Add(new RunUpgradeChoice(displayType, "剣本数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, config.runProjectileCountBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + config.runMediumRangeBonus), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, config.runMediumRangeBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier), StatIconCatalog.Cooldown, () => weapon.MultiplyWeaponCooldown(type, cooldownMultiplier)));
                    break;
                case WeaponType.AuraSword:
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃回数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, config.runProjectileCountBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + config.runAreaRangeBonus), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, config.runAreaRangeBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃距離 " + Number(stats.distance) + ">" + Number(stats.distance + config.runProjectileRangeBonus), StatIconCatalog.Range, () => weapon.AddWeaponDistance(type, config.runProjectileRangeBonus)));
                    break;
                case WeaponType.ArrowRain:
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + config.runMediumRangeBonus), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, config.runMediumRangeBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃時間 " + Seconds(stats.durationSeconds) + ">" + Seconds(stats.durationSeconds + config.runArrowRainDurationBonus), StatIconCatalog.Cooldown, () => weapon.AddWeaponDuration(type, config.runArrowRainDurationBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier), StatIconCatalog.Cooldown, () => weapon.MultiplyWeaponCooldown(type, cooldownMultiplier)));
                    break;
                case WeaponType.Gun:
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier), StatIconCatalog.Cooldown, () => weapon.MultiplyWeaponCooldown(type, cooldownMultiplier)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃距離 " + Number(stats.distance) + ">" + Number(stats.distance + config.runProjectileRangeBonus), StatIconCatalog.Range, () => weapon.AddWeaponDistance(type, config.runProjectileRangeBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃回数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, config.runProjectileCountBonus)));
                    break;
                case WeaponType.Frost:
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + config.runAreaRangeBonus), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, config.runAreaRangeBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "速度低下 " + Percent(stats.slowAmount) + ">" + Percent(stats.slowAmount + config.runSlowBonus), StatIconCatalog.MoveSpeed, () => weapon.AddWeaponSlow(type, config.runSlowBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃間隔 " + Seconds(stats.cooldownSeconds) + ">" + Seconds(stats.cooldownSeconds * cooldownMultiplier), StatIconCatalog.Cooldown, () => weapon.MultiplyWeaponCooldown(type, cooldownMultiplier)));
                    break;
                case WeaponType.ThunderBall:
                    pool.Add(new RunUpgradeChoice(displayType, "攻撃範囲 " + Number(stats.range) + ">" + Number(stats.range + config.runAreaRangeBonus), StatIconCatalog.Range, () => weapon.AddWeaponRange(type, config.runAreaRangeBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "弾数 " + stats.projectileCount + ">" + (stats.projectileCount + config.runProjectileCountBonus), StatIconCatalog.Projectile, () => weapon.AddWeaponCount(type, config.runProjectileCountBonus)));
                    pool.Add(new RunUpgradeChoice(displayType, "持続時間 " + Seconds(stats.durationSeconds) + ">" + Seconds(stats.durationSeconds + config.runThunderBallDurationBonus), StatIconCatalog.Cooldown, () => weapon.AddWeaponDuration(type, config.runThunderBallDurationBonus)));
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
            if (choice == null || !TryBeginLevelUpAction()) return;

            choice.apply();
            if (!choice.isNewWeapon && !choice.isEvolution && choice.hasWeaponType && Player != null && Player.weapon != null)
            {
                Player.weapon.RegisterRunWeaponUpgrade(choice.weaponType);
            }
            runUpgrades.Add(choice.sourceLabel);
            Player.ApplyCurrentStats(false);
            CombatModifiersChanged?.Invoke();
            CompleteCurrentLevelUp();
        }

        void SkipLevelUp()
        {
            if (remainingLevelUpSkips <= 0 || !TryBeginLevelUpAction()) return;

            remainingLevelUpSkips--;
            CompleteCurrentLevelUp();
        }

        void RerollLevelUp()
        {
            if (remainingLevelUpRerolls <= 0 || !TryBeginLevelUpAction()) return;

            remainingLevelUpRerolls--;
            RefreshLevelUpChoices();
        }

        bool TryBeginLevelUpAction()
        {
            if (levelUpPanel == null || !levelUpPanel.activeInHierarchy || lastLevelUpActionFrame == Time.frameCount) return false;

            lastLevelUpActionFrame = Time.frameCount;
            return true;
        }

        void CompleteCurrentLevelUp()
        {
            levelUpPanel.SetActive(false);
            ShowLevelUpInputBlocker(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (pendingOpeningLevelUps > 0)
            {
                ShowNextOpeningLevelUp();
                return;
            }
            if (TryShowNextRunLevelUp()) return;

            Time.timeScale = 1f;
        }

        static void ConfigureLevelUpButton(Button button, RunUpgradeChoice choice)
        {
            if (button == null || choice == null) return;

            var evolutionPresentation = button.GetComponent<EvolutionChoicePresentation>();
            if (evolutionPresentation != null)
            {
                evolutionPresentation.SetEvolution(choice.isEvolution, choice.weaponType);
            }
            else if (choice.isEvolution)
            {
                Debug.LogError("EvolutionChoicePresentation is missing from a level-up choice button.");
            }

            var weaponIcon = FindImage(button.transform, "Weapon Icon Panel/Weapon Icon")
                ?? FindImage(button.transform, "Weapon Icon");
            var weaponName = FindText(button.transform, "Weapon Name Text");
            var upgradeText = FindText(button.transform, "Upgrade Text");
            var label = FindText(button.transform, "Label");
            if (weaponIcon == null || weaponName == null || upgradeText == null)
            {
                ConfigureLegacyLevelUpButton(button, choice);
                return;
            }

            if (label != null) label.gameObject.SetActive(false);
            if (!choice.isEvolution) SetImage(weaponIcon, GeneratedSpriteLoader.Load(choice.weaponIconResource), true);
            weaponName.text = LocalizationService.LocalizeSource(choice.weaponName);
            upgradeText.text = LocalizationService.LocalizeSource(choice.upgradeText);
            ConfigureLevelUpButtonTypeIcon(button, choice.hasAttributeType, choice.attributeType);

            var upgradeIcon = FindImage(button.transform, "Upgrade Icon");
            var newWeaponMark = FindText(button.transform, "New Weapon Mark");
            var newWeaponStars = button.transform.Find("New Weapon Stars");
            if (newWeaponStars != null) newWeaponStars.gameObject.SetActive(false);
            upgradeText.gameObject.SetActive(true);
            upgradeText.alignment = TextAnchor.MiddleLeft;
            if (choice.isNewWeapon)
            {
                if (upgradeIcon != null) upgradeIcon.gameObject.SetActive(false);
                upgradeText.gameObject.SetActive(false);
                if (newWeaponStars != null) newWeaponStars.gameObject.SetActive(true);
                if (newWeaponMark != null)
                {
                    newWeaponMark.gameObject.SetActive(true);
                    newWeaponMark.text = "NEW";
                    newWeaponMark.alignment = TextAnchor.MiddleLeft;
                    newWeaponMark.color = new Color32(255, 216, 74, 255);
                }
                return;
            }

            if (choice.isEvolution)
            {
                if (upgradeIcon != null) upgradeIcon.gameObject.SetActive(false);
                if (newWeaponMark != null) newWeaponMark.gameObject.SetActive(false);
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
                if (label != null) label.text = LocalizationService.LocalizeSource(choice.label);
                return;
            }

            if (icon == null)
            {
                ConfigureLevelUpButtonTypeIcon(button, choice.hasAttributeType, choice.attributeType);
                ConfigureLevelUpButtonLabel(label, false, choice.hasAttributeType);
                if (label != null) label.text = LocalizationService.LocalizeSource(choice.label);
                return;
            }

            icon.gameObject.SetActive(true);
            icon.sprite = sprite;
            icon.color = Color.white;

            bool typeVisible = ConfigureLevelUpButtonTypeIcon(button, choice.hasAttributeType, choice.attributeType);
            ConfigureLevelUpButtonLabel(label, true, typeVisible);
            if (label != null) label.text = LocalizationService.LocalizeSource(choice.label);
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
        }

        void UpdateLevelUpButtonHover()
        {
            if (levelUpPanel == null || !levelUpPanel.activeSelf) return;
            var candidates = ActiveLevelUpButtons();
            if (UiSelectionUtility.TickControllerSubmit(candidates)) return;
            UiSelectionUtility.EnsureSelection(candidates);

            Button hoveredButton = null;
            if (UiSelectionUtility.PointerCanDriveFocus())
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    var button = candidates[i] as Button;
                    if (IsPointerOverButton(button))
                    {
                        hoveredButton = button;
                        break;
                    }
                }
            }

            if (hoveredButton != null)
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(hoveredButton.gameObject);
                SetLevelUpHover(hoveredButton);
                return;
            }

            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < candidates.Length; i++)
            {
                var button = candidates[i] as Button;
                if (button != null && button.gameObject == current)
                {
                    SetLevelUpHover(button);
                    return;
                }
            }
        }

        void SetLevelUpHover(Button hoveredButton)
        {
            if (upgradeButtons == null) return;
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                var button = upgradeButtons[i];
                if (button == null) continue;
                var image = button.GetComponent<Image>();
                if (image != null) image.color = button == hoveredButton ? UpgradeHoverColor : UpgradeNormalColor;
                var highlight = button.GetComponent<UiSelectionHighlight>();
                if (highlight != null) highlight.forceSelected = false;
            }
        }

        Selectable[] ActiveLevelUpButtons()
        {
            var candidates = new List<Selectable>();
            if (upgradeButtons != null)
            {
                for (int i = 0; i < upgradeButtons.Length; i++)
                {
                    if (UiSelectionUtility.IsSelectable(upgradeButtons[i])) candidates.Add(upgradeButtons[i]);
                }
            }
            if (UiSelectionUtility.IsSelectable(skipLevelUpButton)) candidates.Add(skipLevelUpButton);
            if (UiSelectionUtility.IsSelectable(rerollLevelUpButton)) candidates.Add(rerollLevelUpButton);

            return candidates.ToArray();
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
            if (levelUpPanel == null || levelUpInputBlocker == null) return;

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

        public void BeginTowerCollapseCutscene(TowerController tower)
        {
            if (endingCutsceneActive || gameEnding) return;
            endingCutsceneActive = true;
            FreezeGameplayForEndingCutscene(tower != null ? tower.EnemyTarget : null, null);
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
            bool firstClear = IsFirstBossDefeatForCurrentStage(boss);
            if (ShouldSpawnBossRelicChest(firstClear, currentStage)) DropBossRelicChest(boss);
            StartCoroutine(BossDefeatedRoutine(boss));
        }

        public static bool ShouldSpawnBossRelicChest(bool firstClear, int stage)
        {
            return !firstClear && stage >= 1 && stage < 4;
        }

        public static bool ShouldGrantRelicBeforeGameClear(bool firstClear, int stage)
        {
            return !firstClear && stage >= 4;
        }

        public bool IsFirstBossDefeatForCurrentStage(EnemyController boss)
        {
            return boss != null && boss.boss && !ProgressionStore.IsStageCleared(currentStage);
        }

        public void BeginBossDefeatCutscene(EnemyController boss)
        {
            if (boss == null || !IsFirstBossDefeatForCurrentStage(boss) || endingCutsceneActive || gameEnding) return;
            endingCutsceneActive = true;
            FreezeGameplayForEndingCutscene(boss.transform, boss);
        }

        void DropBossRelicChest(EnemyController boss)
        {
            if (boss == null) return;
            SpawnRelicChest(boss.transform.position);
        }

        IEnumerator BossDefeatedRoutine(EnemyController boss)
        {
            bool firstClear = IsFirstBossDefeatForCurrentStage(boss);
            int defeatedDifficulty = ProgressionStore.GetStageDifficulty(currentStage);
            int bossTokenReward = boss != null ? Mathf.Max(0, boss.tokenValue) : 0;
            bool unlockedNextStage = ProgressionStore.MarkStageCleared(currentStage, defeatedDifficulty);
            UnlockNextDifficultyForBossClear(currentStage, defeatedDifficulty);
            RecordBossClear(boss, firstClear, unlockedNextStage, unlockedNextStage ? currentStage + 1 : 0);
            ReviveBuildingsOnBossDefeat();
            if (firstClear)
            {
                yield return FirstBossClearRewardRoutine(bossTokenReward);
                yield return FirstBossDefeatEndRoutine(currentStage, unlockedNextStage ? currentStage + 1 : 0);
                yield break;
            }

            if (currentStage < 4)
            {
                int nextStage = unlockedNextStage ? currentStage + 1 : Mathf.Min(currentStage + 1, 4);
                yield return StageTransitionRoutine(boss, nextStage);
            }
            else
            {
                yield return GameClearRoutine(
                    boss,
                    currentStage,
                    unlockedNextStage ? currentStage + 1 : 0,
                    string.Empty,
                    ShouldGrantRelicBeforeGameClear(firstClear, currentStage));
            }
        }

        static bool UnlockNextDifficultyForBossClear(int stage, int defeatedDifficulty)
        {
            int nextDifficulty = Mathf.Clamp(defeatedDifficulty + 1, ProgressionStore.MinStageDifficulty, ProgressionStore.MaxStageDifficulty);
            if (nextDifficulty <= defeatedDifficulty) return false;
            return ProgressionStore.UnlockStageDifficulty(stage, nextDifficulty);
        }

        void ReviveBuildingsOnBossDefeat()
        {
            if (!ProgressionStore.IsUnlocked(UpgradeType.ReviveBuildingsOnBossDefeat)) return;
            BuildingRevivalState.ReviveDestroyedBuildings(grid, 0.5f);
        }

        IEnumerator GameClearRoutine(
            EnemyController boss,
            int clearedStage,
            int unlockedStage,
            string clearMessage,
            bool grantRelicBeforeEnd)
        {
            gameEnding = true;
            StopGameplayActionAudio();
            spawner?.StopAndClearEnemies(boss);
            ShowAnnouncement("GAME CLEAR");
            if (grantRelicBeforeEnd)
            {
                yield return AcquireRelicRewardRoutine();
            }
            yield return new WaitForSeconds(1.8f);
            EndRun(true, clearedStage, unlockedStage, clearMessage);
        }

        IEnumerator FirstBossDefeatEndRoutine(int clearedStage, int unlockedStage)
        {
            gameEnding = true;
            ShowAnnouncement("STAGE CLEAR");
            yield return new WaitForSecondsRealtime(0.45f);
            EndRun(true, clearedStage, unlockedStage, string.Empty);
        }

        IEnumerator FirstBossClearRewardRoutine(int bossTokenReward)
        {
            if (bossTokenReward > 0)
            {
                AddRunTokens(bossTokenReward);
                yield return new WaitForSecondsRealtime(0.35f);
            }

            yield return AcquireRelicRewardRoutine();
            if (endingCutsceneActive && !gameEnding) Time.timeScale = 0f;
        }

        IEnumerator AcquireRelicRewardRoutine()
        {
            AudioManager.PlaySfx(SfxTrack.RelicChestPickup);
            if (!RelicCatalog.TryPickRandom(out var definition))
            {
                ShowAnnouncement(LocalizationService.Text("レリックが見つかりません", "No relic found"));
                yield break;
            }

            if (!ProgressionStore.UnlockRelic(definition.type))
            {
                ShowAnnouncement(LocalizationService.Text("レリックが見つかりません", "No relic found"));
                yield break;
            }

            Player?.StatsSource?.Refresh();
            Player?.ApplyCurrentStats(false);
            bool closed = false;
            ShowRelicAcquisition(definition, 0, () => closed = true);
            while (!closed) yield return null;
        }

        IEnumerator StageTransitionRoutine(EnemyController boss, int nextStage)
        {
            gameEnding = true;
            spawner?.StopSpawning();
            yield return DefeatRemainingEnemiesForStageTransition(boss);
            yield return AttractRemainingStageRewards();
            ShowAnnouncement("ROUND " + nextStage);
            yield return new WaitForSeconds(1.2f);
            if (boss != null) Destroy(boss.gameObject);
            gameEnding = false;
            BeginStage(nextStage, 0f, true);
        }

        IEnumerator DefeatRemainingEnemiesForStageTransition(EnemyController boss)
        {
            var remainingEnemies = new List<EnemyController>();
            foreach (var enemy in EnemyController.ActiveEnemies)
            {
                if (enemy == null || enemy == boss || enemy.boss) continue;
                enemy.SetActionLocked(true, enemy.FacingDirection);
                remainingEnemies.Add(enemy);
            }

            if (screenFade != null)
            {
                yield return screenFade.FlashWhite(
                    config != null ? config.stageTransitionFlashPeakAlpha : 0.92f,
                    config != null ? config.stageTransitionFlashInSeconds : 0.05f,
                    config != null ? config.stageTransitionFlashHoldSeconds : 0.06f,
                    config != null ? config.stageTransitionFlashOutSeconds : 0.2f);
            }

            float hitDelaySeconds = config != null ? config.stageTransitionEnemyHitDelaySeconds : 0.24f;
            foreach (var enemy in remainingEnemies)
            {
                if (enemy != null) enemy.BeginStageTransitionDefeat(hitDelaySeconds);
            }

            float timeoutSeconds = config != null ? config.stageTransitionEnemyDefeatTimeoutSeconds : 1.2f;
            float elapsedSeconds = 0f;
            while (elapsedSeconds < timeoutSeconds)
            {
                if (!HasRemainingStageTransitionEnemy(remainingEnemies)) yield break;
                elapsedSeconds += Time.unscaledDeltaTime;
                yield return null;
            }

            Debug.LogWarning(
                "Stage transition enemy defeat exceeded its normal timeout. " +
                "Remaining enemies will be defeated immediately so their rewards are preserved.");
            foreach (var enemy in remainingEnemies)
            {
                if (enemy != null) enemy.ForceStageTransitionDefeat();
            }

            while (HasRemainingStageTransitionEnemy(remainingEnemies))
            {
                yield return null;
            }
        }

        static bool HasRemainingStageTransitionEnemy(List<EnemyController> enemies)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null) return true;
            }
            return false;
        }

        IEnumerator AttractRemainingStageRewards()
        {
            if (Player == null) yield break;

            var pickups = new List<AttractablePickup>();
            PickupAttractionRegistry.CopyActiveTo(pickups);
            float longestEstimatedTravelSeconds = 0f;

            for (int i = 0; i < pickups.Count; i++)
            {
                var pickup = pickups[i];
                if (pickup == null) continue;
                longestEstimatedTravelSeconds = Mathf.Max(
                    longestEstimatedTravelSeconds,
                    pickup.EstimateStageTransitionAttractionSeconds(Player));
                pickup.BeginStageTransitionAttraction(Player);
            }

            float timeoutSeconds =
                longestEstimatedTravelSeconds *
                StageTransitionPickupAttractionTimeoutMultiplier +
                StageTransitionPickupAttractionTimeoutPaddingSeconds;
            float elapsedSeconds = 0f;
            while (elapsedSeconds < timeoutSeconds &&
                   HasActiveStageTransitionAttraction(pickups))
            {
                elapsedSeconds += Time.unscaledDeltaTime;
                yield return null;
            }

            for (int i = 0; i < pickups.Count; i++)
            {
                if (pickups[i] != null) pickups[i].CompleteStageTransitionAttraction();
            }
        }

        static bool HasActiveStageTransitionAttraction(
            List<AttractablePickup> pickups)
        {
            for (int i = 0; i < pickups.Count; i++)
            {
                if (pickups[i] != null &&
                    pickups[i].IsStageTransitionAttracting)
                {
                    return true;
                }
            }
            return false;
        }

        public void ShowAnnouncement(string message)
        {
            gameHud?.ShowAnnouncement(message);
        }

        public void ShowRelicAcquisition(RelicDefinition definition)
        {
            ShowRelicAcquisition(definition, 0);
        }

        public void ShowRelicAcquisition(RelicDefinition definition, int duplicateTokenReward)
        {
            ShowRelicAcquisition(definition, duplicateTokenReward, null);
        }

        public void ShowRelicAcquisition(RelicDefinition definition, int duplicateTokenReward, Action onClosed)
        {
            if (definition == null)
            {
                ShowAnnouncement(LocalizationService.Text("レリック獲得", "Relic acquired"));
                onClosed?.Invoke();
                return;
            }

            runRelics.Add(duplicateTokenReward > 0
                ? definition.displayNameSource + "（変換）"
                : definition.displayNameSource);
            tokenRuntime.AddRelicDuplicateTokens(duplicateTokenReward);
            runRelicEntries.Add(new RunRelicReportEntry
            {
                type = definition.type,
                displayName = definition.displayNameSource,
                convertedToToken = duplicateTokenReward > 0
            });
            CombatModifiersChanged?.Invoke();
            gameHud?.RefreshRelics();
            if (relicAcquisitionPanelPrefab == null)
            {
                ShowAnnouncement(duplicateTokenReward > 0
                    ? LocalizationService.Format("レリック変換: トークン +{0}", "Relic converted: +{0} tokens", duplicateTokenReward)
                    : LocalizationService.Format("レリック獲得: {0}", "Relic acquired: {0}", definition.displayName));
                onClosed?.Invoke();
                return;
            }

            var panel = Instantiate(relicAcquisitionPanelPrefab);
            panel.Show(definition, duplicateTokenReward, () =>
            {
                ShowAnnouncement(duplicateTokenReward > 0
                    ? LocalizationService.Format("レリック変換: トークン +{0}", "Relic converted: +{0} tokens", duplicateTokenReward)
                    : LocalizationService.Format("レリック獲得: {0}", "Relic acquired: {0}", definition.displayName));
                onClosed?.Invoke();
            });
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
            StopGameplayActionAudio();
            int guaranteedTokens;
            int tokenBaseBeforeMultiplier;
            float endTokenMultiplier;
            int tokenEarned = EndTokenReward(out guaranteedTokens, out tokenBaseBeforeMultiplier, out endTokenMultiplier);
            int tokenBalanceBeforeEndReward = ProgressionStore.Data.tokens;
            RunResult.Last = new RunResult
            {
                kills = kills,
                damageDealt = damageDealt,
                level = level,
                tokensEarned = tokenEarned,
                reachedStage = currentStage,
                survivedSeconds = elapsed,
                gameClear = clear,
                clearedStage = clearedStage,
                unlockedStage = unlockedStage,
                allStagesDifficultyFiveCleared = clear && ProgressionStore.AreAllStagesClearedAtMaxDifficulty(),
                clearMessage = clearMessage,
                upgrades = new List<string>(runUpgrades),
                acquiredRelics = new List<string>(runRelics),
                acquiredRelicEntries = new List<RunRelicReportEntry>(runRelicEntries),
                damageReport = runDamageTracker.BuildReport()
            };
            ProgressionStore.AddRunTokens(kills, tokenEarned);
            int tokenBalanceAfterEndReward = ProgressionStore.Data.tokens;
            WriteTokenRunLog(
                clear,
                clearedStage,
                unlockedStage,
                tokenEarned,
                guaranteedTokens,
                tokenBaseBeforeMultiplier,
                endTokenMultiplier,
                tokenBalanceBeforeEndReward,
                tokenBalanceAfterEndReward);
            runtimeResourceDiagnostics.LogSnapshot(SceneNames.GameEnd);
            SceneManager.LoadScene(SceneNames.GameEnd);
        }

        void StopGameplayActionAudio()
        {
            var weapon = Player != null ? Player.weapon : null;
            if (weapon == null && Player != null) weapon = Player.GetComponentInChildren<WeaponController>();
            if (weapon != null) weapon.StopRuntimeWeapons();
            AudioManager.StopSfx();
        }

        void FreezeGameplayForEndingCutscene(Transform focusTarget, EnemyController visibleBoss)
        {
            Time.timeScale = 0f;
            StopGameplayActionAudio();
            spawner?.StopAndClearEnemies(visibleBoss);
        }

        public IEnumerator WaitForEndingCutsceneCamera(Transform focusTarget)
        {
            var cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null && focusTarget != null)
            {
                const float moveSeconds = 3.8f;
                yield return cameraFollow.MoveToCutsceneTarget(focusTarget, moveSeconds);
            }
        }

        int EndTokenReward()
        {
            return EndTokenReward(out _, out _, out _);
        }

        int EndTokenReward(out int guaranteedTokens, out int baseTokens, out float multiplier)
        {
            guaranteedTokens = config.roundEndTokenReward
                + ProgressionStore.GetLevel(UpgradeType.EndTokenGain) * config.roundEndTokenRewardPerUpgradeLevel;
            baseTokens = Mathf.Max(0, RunTokens) + Mathf.Max(0, guaranteedTokens);
            multiplier = RelicEffects.EndTokenMultiplier;
            return Mathf.Max(0, Mathf.RoundToInt(baseTokens * multiplier));
        }

        void WriteTokenRunLog(
            bool clear,
            int clearedStage,
            int unlockedStage,
            int tokenEarned,
            int guaranteedTokens,
            int tokenBaseBeforeMultiplier,
            float endTokenMultiplier,
            int tokenBalanceBeforeEndReward,
            int tokenBalanceAfterEndReward)
        {
            var localNow = DateTime.Now;
            var utcNow = DateTime.UtcNow;
            var entry = new TokenRunLogEntry
            {
                sessionId = string.IsNullOrEmpty(runSessionId) ? Guid.NewGuid().ToString("N") : runSessionId,
                timestampLocal = localNow.ToString("yyyy-MM-dd HH:mm:ss"),
                timestampUtc = utcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                appVersion = Application.version,
                unityVersion = Application.unityVersion,
                gameClear = clear,
                startStage = runStartStage,
                startStageDifficulty = runStartStageDifficulty,
                reachedStage = currentStage,
                reachedStageDifficulty = ProgressionStore.GetStageDifficulty(currentStage),
                clearedStage = clearedStage,
                unlockedStage = unlockedStage,
                survivedSeconds = elapsed,
                survivedTime = FormatRunTime(elapsed),
                level = level,
                kills = kills,
                damageDealt = damageDealt,
                runTokensBeforeEndReward = RunTokens,
                killMilestoneTokens = tokenRuntime.KillMilestoneTokens,
                elapsedTimeTokens = tokenRuntime.ElapsedTimeTokens,
                tokenOrbTokens = tokenRuntime.TokenOrbTokens,
                paintAreaTokens = tokenRuntime.PaintAreaTokens,
                relicDuplicateTokens = tokenRuntime.RelicDuplicateTokens,
                guaranteedEndTokens = guaranteedTokens,
                endTokenGainLevel = ProgressionStore.GetLevel(UpgradeType.EndTokenGain),
                endTokenMultiplier = endTokenMultiplier,
                endTokenBaseBeforeMultiplier = tokenBaseBeforeMultiplier,
                finalEndRewardTokens = tokenEarned,
                tokenBalanceAtRunStart = tokenRuntime.TokenBalanceAtRunStart,
                tokenBalanceBeforeEndReward = tokenBalanceBeforeEndReward,
                tokenBalanceAfterEndReward = tokenBalanceAfterEndReward,
                totalTokenBalanceIncrease = tokenBalanceAfterEndReward - tokenRuntime.TokenBalanceAtRunStart,
                killTokenDivisor = config != null ? Mathf.Max(1, config.tokenKillsDivisor) : 1,
                killTokenRemainder = tokenRuntime.KillTokenProgress,
                elapsedTokenIntervalSeconds = TokenRuntimeService.ElapsedTokenRewardIntervalSeconds,
                nextElapsedTokenRewardSeconds = tokenRuntime.NextElapsedTokenRewardSeconds,
                paintAreaTokenThreshold = TokenRuntimeService.PaintAreaTokenThreshold,
                paintAreaTokenRemainder = tokenRuntime.PaintAreaTokenProgress,
                reachedStageSummary = StageLogSummary(),
                bossClearSummary = BossClearLogSummary(),
                reachedStages = new List<RunStageLogEntry>(runReachedStages),
                bossClears = new List<RunBossClearLogEntry>(runBossClears),
                upgrades = new List<string>(runUpgrades),
                acquiredRelics = new List<string>(runRelics)
            };

            TokenRunLogger.Append(entry);
        }

        string StageLogSummary()
        {
            if (runReachedStages.Count == 0) return string.Empty;
            var parts = new List<string>();
            for (int i = 0; i < runReachedStages.Count; i++)
            {
                var entry = runReachedStages[i];
                parts.Add($"STAGE {entry.stage}(難易度{entry.difficulty}, {entry.reachedTime})");
            }

            return string.Join(" -> ", parts);
        }

        string BossClearLogSummary()
        {
            if (runBossClears.Count == 0) return string.Empty;
            var parts = new List<string>();
            for (int i = 0; i < runBossClears.Count; i++)
            {
                var entry = runBossClears[i];
                string unlock = entry.unlockedNextStage ? $", unlock:{entry.unlockedStage}" : string.Empty;
                parts.Add($"STAGE {entry.stage} {entry.bossName} {entry.clearedTime} first:{entry.firstClear}{unlock}");
            }

            return string.Join(" | ", parts);
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
            BeginStage(stage, 0f);
        }

        void BeginStage(int stage, float startStageElapsedSeconds)
        {
            BeginStage(stage, startStageElapsedSeconds, false);
        }

        void BeginStage(int stage, float startStageElapsedSeconds, bool preserveRunElapsed)
        {
            currentStage = Mathf.Max(1, stage);
            float displayElapsedOffset = preserveRunElapsed ? elapsed : StageStartDisplaySeconds();
            elapsed = displayElapsedOffset + Mathf.Max(0f, startStageElapsedSeconds);
            hudElapsed = elapsed;
            tokenRuntime.SetElapsedTokenRewardSchedule(elapsed);
            bossActive = false;
            RecordStageReached(currentStage, runReachedStages.Count == 0);
            AudioManager.PlayBgm(BgmTrack.GameNormal);
            if (timerText != null) timerText.color = Color.white;
            if (spawner != null)
            {
                spawner.useUpperChunkSpawn = false;
                spawner.BeginStage(
                    config,
                    grid,
                    Tower.EnemyTarget,
                    currentStage,
                    displayElapsedOffset,
                    startStageElapsedSeconds,
                    Player != null ? Player.transform : null);
            }
            gameHud?.SetStage(currentStage);
        }

        void RecordStageReached(int stage, bool startStage)
        {
            runReachedStages.Add(new RunStageLogEntry
            {
                stage = Mathf.Max(1, stage),
                difficulty = ProgressionStore.GetStageDifficulty(stage),
                reachedSeconds = elapsed,
                reachedTime = FormatRunTime(elapsed),
                startStage = startStage
            });
        }

        void RecordBossClear(EnemyController boss, bool firstClear, bool unlockedNextStage, int unlockedStage)
        {
            runBossClears.Add(new RunBossClearLogEntry
            {
                stage = currentStage,
                difficulty = ProgressionStore.GetStageDifficulty(currentStage),
                bossName = !string.IsNullOrWhiteSpace(boss != null ? boss.displayName : null) ? boss.displayName : "Boss",
                enemyKind = boss != null ? boss.enemyKind.ToString() : string.Empty,
                firstClear = firstClear,
                unlockedNextStage = unlockedNextStage,
                unlockedStage = unlockedStage,
                clearedSeconds = elapsed,
                clearedTime = FormatRunTime(elapsed),
                kills = kills,
                level = level,
                runTokens = RunTokens
            });
        }

        static string FormatRunTime(float seconds)
        {
            return TimeSpan.FromSeconds(Mathf.Max(0f, seconds)).ToString(@"mm\:ss");
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
            }

            public RunUpgradeChoice(WeaponType weaponType, string upgradeText, string iconResource, Action apply)
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
