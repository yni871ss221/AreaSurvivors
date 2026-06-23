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
        float roundTimeLimit;
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
            sessionMode = RunState.ConsumeNextMapMode();
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
            int stage = RunState.ConsumeNextStartStage();
            SyncFixedBuildingSlots(stage);

            if (sessionMode == MapSessionMode.Game)
            {
                Player = Instantiate(playerPrefab, grid.GridToWorld(grid.width / 2, grid.height / 2 - 6), Quaternion.identity);
                if (spawner != null) Player.damagePopupPrefab = spawner.damagePopupPrefab;
                Player.Configure(config, grid, CharacterType.Knight);
            }

            if (sessionMode == MapSessionMode.Game) ProgressionStore.ReviveStageBuildings(stage);
            if (buildPlacement != null) buildPlacement.Initialize(config, grid, sessionMode == MapSessionMode.Build ? null : Player);
            if (buildPlacement != null) buildPlacement.RestoreStageBuildings(stage);
            PolishHud();
            ConfigureGameHud();

            var cameraFollow = Camera.main.GetComponent<CameraFollow>();
            if (cameraFollow != null && sessionMode == MapSessionMode.Game && Player != null) cameraFollow.Configure(Player.transform, Tower.transform, config);
            if (sessionMode == MapSessionMode.Game) BeginStage(stage);
            else BeginBuildMode(stage);
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
            public Vector2Int footprint;
            public Vector2Int desiredOffset;
            public bool requiresUnlock = true;
        }

        static readonly FixedBuildingSlotDefinition[] FixedBuildingSlotDefinitions =
        {
            new FixedBuildingSlotDefinition
            {
                kind = SavedBuildingKind.WoodenWall,
                unlockType = UpgradeType.StartingWood,
                footprint = new Vector2Int(3, 1),
                desiredOffset = new Vector2Int(-9, 0),
                requiresUnlock = false
            },
            new FixedBuildingSlotDefinition
            {
                kind = SavedBuildingKind.WoodenGate,
                unlockType = UpgradeType.StartingWood,
                footprint = new Vector2Int(3, 1),
                desiredOffset = new Vector2Int(9, 0),
                requiresUnlock = false
            },
            new FixedBuildingSlotDefinition
            {
                kind = SavedBuildingKind.Ballista,
                unlockType = UpgradeType.UnlockBallista,
                footprint = new Vector2Int(2, 2),
                desiredOffset = new Vector2Int(-8, 8)
            },
            new FixedBuildingSlotDefinition
            {
                kind = SavedBuildingKind.Ballista,
                unlockType = UpgradeType.UnlockBallista,
                footprint = new Vector2Int(2, 2),
                desiredOffset = new Vector2Int(8, 8)
            },
            new FixedBuildingSlotDefinition
            {
                kind = SavedBuildingKind.WatchTower,
                unlockType = UpgradeType.UnlockWatchTower,
                footprint = new Vector2Int(2, 2),
                desiredOffset = new Vector2Int(0, 10)
            }
        };

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
                if (!TryFindFixedSlotOrigin(towerOrigin, definition.footprint, definition.desiredOffset, out var originCell)) continue;

                var saved = i < existing.Count ? existing[i] : null;
                if (saved == null) saved = new SavedBuildingData();
                var previousKind = saved.kind;
                saved.kind = definition.kind;
                saved.x = originCell.x;
                saved.y = originCell.y;
                saved.destroyed = false;
                if (previousKind != definition.kind) saved.upgraded = false;
                result.Add(saved);
            }

            return result;
        }

        bool TryFindFixedSlotOrigin(Vector3Int towerOrigin, Vector2Int footprint, Vector2Int desiredOffset, out Vector3Int originCell)
        {
            footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            for (int radius = 0; radius <= 5; radius++)
            {
                foreach (var offset in EnumerateFixedSlotOffsets(desiredOffset, radius))
                {
                    originCell = towerOrigin + new Vector3Int(offset.x, offset.y, 0);
                    if (!grid.ContainsCell(originCell)) continue;
                    if (!grid.CanPlaceObject(originCell, footprint)) continue;
                    if (!HasPlayerTerritory(originCell, footprint)) continue;
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
                if (!bossActive) elapsed += Time.deltaTime * currentStageSpeedMultiplier;
                hudElapsed = Mathf.Max(0f, roundTimeLimit - elapsed);
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
                var label = ConfigureLevelUpButtonIcon(upgradeButtons[i], choices[index].iconResource);
                if (label != null) label.text = choices[index].label;
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
            var pool = new List<RunUpgradeChoice>
            {
                new RunUpgradeChoice("\u79fb\u52d5\u901f\u5ea6 +" + Mathf.RoundToInt((config.runMoveSpeedMultiplier - 1f) * 100f) + "%", StatIconCatalog.MoveSpeed, () => Player.StatsSource.MultiplyMoveSpeed(config.runMoveSpeedMultiplier)),
                new RunUpgradeChoice("\u5857\u308a\u7bc4\u56f2 +" + config.runPaintRadiusBonus, StatIconCatalog.Paint, () => Player.StatsSource.AddPaintRadius(config.runPaintRadiusBonus)),
                new RunUpgradeChoice("\u6700\u5927HP +" + config.runMaxHpBonus, StatIconCatalog.MaxHp, () => Player.StatsSource.AddMaxHp(config.runMaxHpBonus)),
                new RunUpgradeChoice("\u9632\u5fa1 +" + config.runDefenseBonus, StatIconCatalog.Defense, () => Player.StatsSource.AddDefense(config.runDefenseBonus)),
                new RunUpgradeChoice("\u7d4c\u9a13\u5024 +" + config.runXpGainMultiplierBonus.ToString("0.0") + "x", StatIconCatalog.Xp, () => Player.StatsSource.AddXpGainMultiplier(config.runXpGainMultiplierBonus)),
                new RunUpgradeChoice("\u81ea\u52d5\u56de\u5fa9 +" + config.runAutoRegenBonus, StatIconCatalog.Regen, () => Player.StatsSource.AddAutoRegen(config.runAutoRegenBonus)),
                new RunUpgradeChoice("\u4f5c\u696d\u901f\u5ea6 +" + config.runWorkSpeedMultiplierBonus.ToString("0.0") + "x", StatIconCatalog.Work, () => Player.StatsSource.AddWorkSpeedMultiplier(config.runWorkSpeedMultiplierBonus)),
                new RunUpgradeChoice("\u8cc7\u6e90\u7372\u5f97 +" + config.runResourceGainBonus, StatIconCatalog.Resource, () => Player.StatsSource.AddResourceGain(config.runResourceGainBonus))
            };
            var result = new List<RunUpgradeChoice>();
            var weapon = Player != null ? Player.weapon : null;
            if (weapon != null)
            {
                if (weapon.CanLevelUpSlash)
                {
                    int nextLevel = Mathf.Min(GameConfig.MaxWeaponLevel, weapon.SlashLevel + 1);
                    result.Add(new RunUpgradeChoice("\u30b9\u30e9\u30c3\u30b7\u30e5 Lv " + weapon.SlashLevel + " > " + nextLevel, StatIconCatalog.WeaponLevel, () => weapon.LevelUpSlash()));
                }
                if (weapon.CanLevelUpArrow)
                {
                    string label = weapon.ArrowUnlocked
                        ? "\u5f13 Lv " + weapon.ArrowLevel + " > " + Mathf.Min(GameConfig.MaxWeaponLevel, weapon.ArrowLevel + 1)
                        : "\u5f13\u3092\u89e3\u653e";
                    result.Add(new RunUpgradeChoice(label, StatIconCatalog.WeaponLevel, () => weapon.LevelUpArrow()));
                }
                if (weapon.CanLevelUpFireball)
                {
                    string label = weapon.FireballUnlocked
                        ? "\u706b\u306e\u7389 Lv " + weapon.FireballLevel + " > " + Mathf.Min(GameConfig.MaxWeaponLevel, weapon.FireballLevel + 1)
                        : "\u706b\u306e\u7389\u3092\u89e3\u653e";
                    result.Add(new RunUpgradeChoice(label, StatIconCatalog.WeaponLevel, () => weapon.LevelUpFireball()));
                }
            }

            while (result.Count < 3 && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
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

        static Text ConfigureLevelUpButtonIcon(Button button, string iconResource)
        {
            if (button == null) return null;
            var label = GetLevelUpButtonLabel(button);
            var sprite = StatIconCatalog.Load(iconResource);
            var iconTransform = button.transform.Find("Upgrade Icon");
            var icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            if (sprite == null)
            {
                if (icon != null) icon.gameObject.SetActive(false);
                ConfigureLevelUpButtonLabel(label, false);
                return label;
            }

            if (icon == null)
            {
                icon = new GameObject("Upgrade Icon").AddComponent<Image>();
                icon.transform.SetParent(button.transform, false);
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                icon.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                icon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                icon.rectTransform.anchoredPosition = new Vector2(28f, 0f);
                icon.rectTransform.sizeDelta = new Vector2(28f, 28f);
                var outline = icon.gameObject.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2f, -2f);
                outline.useGraphicAlpha = true;
            }

            icon.gameObject.SetActive(true);
            icon.sprite = sprite;
            icon.color = Color.white;

            ConfigureLevelUpButtonLabel(label, true);
            return label;
        }

        static Text GetLevelUpButtonLabel(Button button)
        {
            var labelTransform = button.transform.Find("Label");
            var label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label != null) return label;

            return button.GetComponentInChildren<Text>();
        }

        static void ConfigureLevelUpButtonLabel(Text label, bool hasIcon)
        {
            if (label == null) return;
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.offsetMin = new Vector2(hasIcon ? 72f : 18f, 0f);
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
            if (currentStage == 1)
            {
                int nextStage = unlockedNextStage ? currentStage + 1 : 2;
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
            roundTimeLimit = RoundTimeLimitSeconds();
            elapsed = 0f;
            hudElapsed = roundTimeLimit;
            bossActive = false;
            if (timerText != null) timerText.color = Color.white;
            if (spawner != null)
            {
                spawner.useUpperChunkSpawn = false;
                spawner.BeginStage(config, grid, Tower.EnemyTarget, currentStage, 0f, currentStageSpeedMultiplier);
            }
            gameHud?.SetStage(currentStage);
        }

        void BeginBuildMode(int stage)
        {
            currentStage = Mathf.Max(1, stage);
            currentStageSpeedMultiplier = 1f;
            elapsed = 0f;
            hudElapsed = 0f;
            bossActive = false;
            if (spawner != null) spawner.gameObject.SetActive(false);
            if (levelUpPanel != null) levelUpPanel.SetActive(false);
            if (timerText != null) timerText.text = string.Empty;
            ConfigureBuildModeCamera();
            gameHud?.SetStage(currentStage);
        }

        void ConfigureBuildModeCamera()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var follow = camera.GetComponent<CameraFollow>();
            if (follow != null)
            {
                follow.target = null;
                follow.anchor = null;
                follow.enabled = false;
            }
            var buildCamera = camera.GetComponent<BuildModeCameraController>();
            if (buildCamera == null) buildCamera = camera.gameObject.AddComponent<BuildModeCameraController>();
            buildCamera.enabled = true;
            buildCamera.Configure(grid, Tower != null ? Tower.transform : null);
        }

        float RoundTimeLimitSeconds()
        {
            return Mathf.Max(1f, config.baseRoundTimeLimitSeconds + ProgressionStore.GetLevel(UpgradeType.RoundTimeLimit) * config.roundTimeLimitSecondsPerUpgradeLevel);
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
            public readonly Action apply;

            public RunUpgradeChoice(string label, string iconResource, Action apply)
            {
                this.label = label;
                this.iconResource = iconResource;
                this.apply = apply;
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
        Text playerWorkText;
        Text playerResourceText;
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
            if (playerPanel == null)
            {
                playerPanel = CreatePanel(parent, "Player Status", new Vector2(14f, -12f), PlayerPanelSize, Vector2.up, Vector2.up);
                AddFrame(playerPanel, PlayerPanelSize);
            }

            var statsRoot = splitPlayerRoot != null ? parent.Find("Player Status") : playerPanel;
            playerStatsPanel = statsRoot != null ? statsRoot.GetComponent<RectTransform>() : playerPanel;
            if (splitPlayerRoot != null) HideLegacyPlayerTiles(playerStatsPanel);

            var portraitFrame = EnsureIconFrame(playerPanel, "Character Frame", new Vector2(18f, -36f), new Vector2(70f, 70f));
            var portrait = EnsureImage(portraitFrame, "Character Image", PlayerIconSize);
            portrait.sprite = player != null ? player.PortraitSprite : LoadHudSprite("Knight", null);
            portrait.preserveAspect = true;

            BuildPaintGauge(playerPanel);

            playerHpFill = EnsureHorizontalBar(playerPanel, "Player HP Bar", new Vector2(174f, -36f), new Vector2(190f, 24f), HpRed, out playerHpText);
            playerXpFill = EnsureHorizontalBar(playerPanel, "Player XP Bar", new Vector2(174f, -72f), new Vector2(190f, 20f), HpBlue, out playerLevelText);
            playerSpeedText = BindStatText(playerStatsPanel, "Speed Text") ?? EnsureStatText(playerStatsPanel, "Speed Text", new Vector2(8f, -18f));
            playerPaintText = BindStatText(playerStatsPanel, "Paint Text") ?? EnsureStatText(playerStatsPanel, "Paint Text", new Vector2(8f, -48f));
            playerReviveText = BindStatText(playerStatsPanel, "Revive Text") ?? EnsureStatText(playerStatsPanel, "Revive Text", new Vector2(8f, -78f));
            playerDefenseText = BindStatText(playerStatsPanel, "Defense Text") ?? EnsureStatText(playerStatsPanel, "Defense Text", new Vector2(8f, -108f));
            playerXpGainText = BindStatText(playerStatsPanel, "Xp Gain Text") ?? EnsureStatText(playerStatsPanel, "Xp Gain Text", new Vector2(8f, -138f));
            playerRegenText = BindStatText(playerStatsPanel, "Regen Text") ?? EnsureStatText(playerStatsPanel, "Regen Text", new Vector2(8f, -168f));
            playerWorkText = BindStatText(playerStatsPanel, "Work Text") ?? EnsureStatText(playerStatsPanel, "Work Text", new Vector2(8f, -198f));
            playerResourceText = BindStatText(playerStatsPanel, "Resource Text") ?? EnsureStatText(playerStatsPanel, "Resource Text", new Vector2(8f, -228f));
        }

        void BuildPaintGauge(RectTransform parent)
        {
            var breakdown = parent.Find("Control Breakdown");
            var breakdownRoot = breakdown != null ? breakdown.GetComponent<RectTransform>() : null;
            if (breakdownRoot == null) return;

            paintControlBlueSegment = BindControlSegment(breakdownRoot, "Blue Segment");
            paintControlNeutralSegment = BindControlSegment(breakdownRoot, "Neutral Segment");
            paintControlRedSegment = BindControlSegment(breakdownRoot, "Red Segment");
            if (paintControlBlueSegment == null || paintControlNeutralSegment == null || paintControlRedSegment == null) return;
            paintControlBlueText = BindControlSegmentText(paintControlBlueSegment);
            paintControlNeutralText = BindControlSegmentText(paintControlNeutralSegment);
            paintControlRedText = BindControlSegmentText(paintControlRedSegment);
            if (paintControlBlueText == null || paintControlNeutralText == null || paintControlRedText == null) return;
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
            if (playerWorkText != null) playerWorkText.text = stats.workSpeedMultiplier.ToString("0.0") + "x";
            if (playerResourceText != null) playerResourceText.text = "+" + stats.resourceGainBonus;
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
            const float totalWidth = 190f;
            const float minWidth = 32f;
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

        static Text EnsureStatText(RectTransform parent, string name, Vector2 position)
        {
            var oldDirectText = parent.Find(name);
            if (oldDirectText != null && oldDirectText.GetComponent<Text>() != null) oldDirectText.gameObject.SetActive(false);

            var boxName = name + " Box";
            var box = parent.Find(boxName) as RectTransform;
            if (box == null)
            {
                box = CreatePanel(parent, boxName, position, new Vector2(104f, 26f), Vector2.up, Vector2.up);
                box.GetComponent<Image>().color = SlotColor;
                AddFrame(box, box.sizeDelta);
            }

            var labelTransform = box.Find("Name") ?? box.Find("Label");
            var label = labelTransform != null ? labelTransform.GetComponent<Text>() : null;
            if (label == null) label = CreateText(box, "Name", "", 13, Vector2.zero, new Vector2(64f, 22f), TextAnchor.MiddleLeft);
            label.gameObject.name = "Name";
            label.text = StatLabel(name);
            label.alignment = TextAnchor.MiddleLeft;
            SetStatColumns(label.rectTransform, 0f, 0.62f, 5f, -2f);

            var valueTransform = box.Find("Value");
            var value = valueTransform != null ? valueTransform.GetComponent<Text>() : null;
            if (value == null) value = CreateText(box, "Value", "-", 13, Vector2.zero, new Vector2(38f, 22f), TextAnchor.MiddleRight);
            value.alignment = TextAnchor.MiddleRight;
            SetStatColumns(value.rectTransform, 0.62f, 1f, 2f, -5f);

            var divider = box.Find("Divider");
            if (divider == null)
            {
                var dividerObject = new GameObject("Divider");
                dividerObject.transform.SetParent(box, false);
                var image = dividerObject.AddComponent<Image>();
                image.color = new Color(0.58f, 0.68f, 0.40f, 0.65f);
                image.raycastTarget = false;
                var rect = image.rectTransform;
                rect.anchorMin = new Vector2(0.62f, 0.15f);
                rect.anchorMax = new Vector2(0.62f, 0.85f);
                rect.sizeDelta = new Vector2(1f, 0f);
                rect.anchoredPosition = Vector2.zero;
            }

            return value;
        }

        static string StatLabel(string name)
        {
            switch (name)
            {
                case "Speed Text": return "速度";
                case "Paint Text": return "塗り";
                case "Revive Text": return "復活";
                case "Defense Text": return "防御";
                case "Xp Gain Text": return "経験";
                case "Regen Text": return "回復";
                case "Work Text": return "作業";
                case "Resource Text": return "資源";
                default: return name;
            }
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
