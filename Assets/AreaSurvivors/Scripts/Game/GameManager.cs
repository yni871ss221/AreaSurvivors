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
        public NaturalLandmarkSpawner naturalLandmarks;
        public GameHudController gameHud;
        public Text timerText;
        public Text killText;
        public Text levelText;
        public Slider xpBar;
        public GameObject levelUpPanel;
        public Button[] upgradeButtons;

        public PlayerController Player { get; private set; }
        public TowerController Tower { get; private set; }
        public int CurrentLevel => level;
        public int CurrentXp => xp;
        public int XpToNext => xpToNext;
        public int Wood { get; private set; }
        public int Stone { get; private set; }

        int kills;
        int level = 1;
        int xp;
        int xpToNext = 5;
        int damageDealt;
        float elapsed;
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
            config = Instantiate(config);
            Wood = Mathf.Max(0, config.startingWood);
            Stone = Mathf.Max(0, config.startingStone);
            if (grid != null)
            {
                grid.Build();
                grid.RegisterSceneObjects();
            }

            Tower = sceneTower != null ? sceneTower : FindObjectOfType<TowerController>();
            if (Tower == null)
            {
                Debug.LogError("GameManager requires a scene TowerController.");
                enabled = false;
                return;
            }

            Tower.transform.position = grid.GridToWorld(grid.width / 2, grid.height / 2);
            Tower.Configure(config.towerMaxHp + ProgressionStore.GetLevel(UpgradeType.TowerMaxHp) * 12);
            if (Tower.hpBar != null) Tower.hpBar.gameObject.SetActive(false);
            var towerMarker = Tower.GetComponent<GridObjectMarker>();
            if (towerMarker != null) towerMarker.Register(grid);
            var towerRootCell = grid.WorldToCell(Tower.transform.position) + Vector3Int.down * 5;
            var towerRootWorld = grid.groundTilemap.GetCellCenterWorld(towerRootCell);
            Tower.ConfigureEnemyTarget(towerRootWorld);
            grid.PaintImmediate(towerRootWorld, TileOwner.Player, InitialTowerTerritoryRadius);
            SpawnNaturalLandmarks(towerRootCell);

            Player = Instantiate(playerPrefab, grid.GridToWorld(grid.width / 2, grid.height / 2 - 6), Quaternion.identity);
            Player.Configure(config, grid, RunState.SelectedCharacter);
            if (buildPlacement != null) buildPlacement.Initialize(config, grid, Player);
            PolishHud();
            ConfigureGameHud();

            var cameraFollow = Camera.main.GetComponent<CameraFollow>();
            if (cameraFollow != null) cameraFollow.Configure(Player.transform, Tower.transform, config);
            spawner.Begin(config, grid, Tower.EnemyTarget);
            UpdateHud();
        }

        void SpawnNaturalLandmarks(Vector3Int centerCell)
        {
            if (grid == null) return;
            if (naturalLandmarks == null) naturalLandmarks = GetComponent<NaturalLandmarkSpawner>();
            if (naturalLandmarks == null) naturalLandmarks = gameObject.AddComponent<NaturalLandmarkSpawner>();
            naturalLandmarks.Spawn(grid, centerCell);
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            if (buildPlacement != null) buildPlacement.Tick();
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

        public void AddResource(ResourceType type, int amount)
        {
            amount = Mathf.Max(0, amount);
            if (type == ResourceType.Wood) Wood += amount;
            else Stone += amount;
        }

        public void AddExperience(int amount)
        {
            xp += Mathf.Max(1, amount);
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
                var label = upgradeButtons[i].GetComponentInChildren<Text>();
                label.text = choices[index].label;
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
                new RunUpgradeChoice("\u653b\u6483\u529b +2", () => config.baseAttackPower += 2),
                new RunUpgradeChoice("\u653b\u6483\u9593\u9694 -8%", () => { config.knightCooldown *= .92f; config.archerCooldown *= .92f; config.mageCooldown *= .92f; }),
                new RunUpgradeChoice("\u79fb\u52d5\u901f\u5ea6 +8%", () => config.playerMoveSpeed *= 1.08f),
                new RunUpgradeChoice("\u5857\u308a\u7bc4\u56f2 +1", () => config.paintRadius += 1),
                new RunUpgradeChoice("\u6700\u5927HP +8", () => Player.Health.SetMax(Player.Health.maxHp + 8))
            };
            var result = new List<RunUpgradeChoice>();
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
            Player.Configure(config, grid, Player.characterType);
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
            Time.timeScale = 1f;
            int tokensEarned = Mathf.Max(0, kills / Mathf.Max(1, config.tokenKillsDivisor));
            RunResult.Last = new RunResult
            {
                kills = kills,
                damageDealt = damageDealt,
                level = level,
                tokensEarned = tokensEarned,
                survivedSeconds = elapsed,
                upgrades = new List<string>(runUpgrades)
            };
            ProgressionStore.AddRunRewards(kills, config.tokenKillsDivisor);
            SceneManager.LoadScene(SceneNames.GameOver);
        }

        void UpdateHud()
        {
            if (timerText != null)
            {
                var span = TimeSpan.FromSeconds(elapsed);
                timerText.text = $"{span.Minutes:00}:{span.Seconds:00}";
            }
            if (killText != null) killText.text = $"\u6483\u7834 {kills}";
            if (levelText != null) levelText.text = $"Lv {level}";
            if (xpBar != null) xpBar.value = xpToNext <= 0 ? 0f : (float)xp / xpToNext;
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
            public readonly Action apply;

            public RunUpgradeChoice(string label, Action apply)
            {
                this.label = label;
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
        static readonly Vector2 PlayerPanelSize = new Vector2(390f, 228f);
        static readonly Vector2 PlayerIconSize = new Vector2(58f, 58f);
        static readonly Vector2 WeaponIconSize = new Vector2(48f, 48f);

        BuildPlacementController buildPlacement;
        GameManager gameManager;
        PlayerController player;
        Health towerHealth;
        Sprite towerIconSprite;
        RectTransform towerPanel;
        RectTransform playerPanel;
        RectTransform playerStatsPanel;
        Image hpFill;
        Image playerHpFill;
        Image playerXpFill;
        Text hpText;
        Text playerHpText;
        Text playerLevelText;
        Text playerAttackText;
        Text playerCooldownText;
        Text playerSpeedText;
        Text playerPaintText;
        Text woodText;
        Text stoneText;
        Text[] stockLabels;
        Image[] slotBackplates;
        UiSelectionHighlight[] slotHighlights;
        readonly List<FloatingHudDamage> damagePopups = new List<FloatingHudDamage>();
        int selectedSlot;

        public void Initialize(BuildPlacementController placement, TowerController tower, GameManager owner)
        {
            buildPlacement = placement;
            gameManager = owner;
            player = owner != null ? owner.Player : null;
            towerHealth = tower != null ? tower.GetComponent<Health>() : null;
            towerIconSprite = Resources.Load<Sprite>("Generated/Tower") ?? CreateTowerSpriteFromRenderer(tower);
            if (towerHealth != null) towerHealth.Damaged += OnTowerDamaged;

            var canvas = FindHudCanvas();
            if (canvas == null) canvas = CreateCanvas();

            HideLegacyBuildStatus(canvas.transform);
            BindSceneRunStats(canvas.transform);
            BindSceneResourceHud(canvas.transform);
            BuildPlayerPanel(canvas.transform);
            BuildConstructionMenu(canvas.transform);
            BuildTowerPanel(canvas.transform);
            UpdatePlayerPanel();
            UpdateResourceHud();
            UpdateBuildSlots();
            UpdateTowerPanel();
        }

        void OnDestroy()
        {
            if (towerHealth != null) towerHealth.Damaged -= OnTowerDamaged;
        }

        void Update()
        {
            if (player == null && gameManager != null) player = gameManager.Player;
            UpdatePlayerPanel();
            UpdateResourceHud();
            UpdateBuildSlots();
            UpdateTowerPanel();
            TickDamagePopups();
        }

        void BindSceneRunStats(Transform parent)
        {
            if (parent == null || gameManager == null) return;
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
        }

        void BindSceneResourceHud(Transform parent)
        {
            woodText = FindText(parent, "Wood Resource/Amount");
            stoneText = FindText(parent, "Stone Resource/Amount");
            SetResourceIcon(parent, "Wood Resource/Icon", "WoodIcon");
            SetResourceIcon(parent, "Stone Resource/Icon", "StoneIcon");
        }

        void UpdateResourceHud()
        {
            if (gameManager == null) return;
            if (woodText != null) woodText.text = gameManager.Wood.ToString();
            if (stoneText != null) stoneText.text = gameManager.Stone.ToString();
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

            var weaponFrame = EnsureIconFrame(playerPanel, "Weapon Frame", new Vector2(96f, -42f), new Vector2(58f, 58f));
            var weapon = EnsureImage(weaponFrame, "Weapon Image", WeaponIconSize);
            weapon.sprite = WeaponSprite(player != null ? player.characterType : CharacterType.Knight);
            weapon.preserveAspect = true;

            playerHpFill = EnsureHorizontalBar(playerPanel, "Player HP Bar", new Vector2(174f, -36f), new Vector2(190f, 24f), HpRed, out playerHpText);
            playerXpFill = EnsureHorizontalBar(playerPanel, "Player XP Bar", new Vector2(174f, -72f), new Vector2(190f, 20f), HpBlue, out playerLevelText);
            playerAttackText = EnsureStatText(playerStatsPanel, "Attack Text", new Vector2(58f, -102f));
            playerCooldownText = EnsureStatText(playerStatsPanel, "Cooldown Text", new Vector2(58f, -132f));
            playerSpeedText = EnsureStatText(playerStatsPanel, "Speed Text", new Vector2(58f, -162f));
            playerPaintText = EnsureStatText(playerStatsPanel, "Paint Text", new Vector2(58f, -192f));
        }

        static void HideLegacyPlayerTiles(RectTransform statsRoot)
        {
            if (statsRoot == null) return;
            HideHudChild(statsRoot, "Character Frame");
            HideHudChild(statsRoot, "Weapon Frame");
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

            var portrait = playerPanel.Find("Character Frame/Character Image")?.GetComponent<Image>();
            if (portrait != null) portrait.sprite = player.PortraitSprite;
            var weapon = playerPanel.Find("Weapon Frame/Weapon Image")?.GetComponent<Image>();
            if (weapon != null) weapon.sprite = WeaponSprite(player.characterType);

            var weaponController = player.weapon;
            if (playerAttackText != null) playerAttackText.text = "攻撃: " + (weaponController != null ? weaponController.AttackPower.ToString() : "-");
            if (playerCooldownText != null) playerCooldownText.text = "間隔: " + (weaponController != null ? weaponController.CurrentCooldown.ToString("0.0") + "s" : "-");
            if (playerSpeedText != null) playerSpeedText.text = "速度: " + player.MoveSpeed.ToString("0.0");
            if (playerPaintText != null) playerPaintText.text = "塗り: " + player.PaintRadius;
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
            if (image == null)
            {
                image = new GameObject(name).AddComponent<Image>();
                image.transform.SetParent(parent, false);
            }
            image.raycastTarget = false;
            image.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = Vector2.zero;
            image.rectTransform.sizeDelta = size;
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

            var existing = box.Find("Label");
            var text = existing != null ? existing.GetComponent<Text>() : null;
            if (text == null) text = CreateText(box, "Label", "", 13, Vector2.zero, new Vector2(96f, 22f), TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.rectTransform.sizeDelta = new Vector2(96f, 22f);
            return text;
        }

        static Sprite WeaponSprite(CharacterType type)
        {
            if (type == CharacterType.Archer) return Resources.Load<Sprite>("Generated/Arrow") ?? Resources.Load<Sprite>("Arrow");
            if (type == CharacterType.Mage) return Resources.Load<Sprite>("Generated/Fireball") ?? Resources.Load<Sprite>("Fireball");
            return Resources.Load<Sprite>("Generated/Slash_0") ?? Resources.Load<Sprite>("Slash");
        }

        void BuildConstructionMenu(Transform parent)
        {
            var existing = parent.Find("Construction Menu");
            var root = existing != null ? existing.GetComponent<RectTransform>() : null;
            if (root == null)
            {
                root = CreatePanel(parent, "Construction Menu", new Vector2(16f, 16f), new Vector2(276f, 96f), Vector2.zero, Vector2.zero);
                AddFrame(root, new Vector2(276f, 96f));
            }

            stockLabels = new Text[3];
            slotBackplates = new Image[3];
            slotHighlights = new UiSelectionHighlight[3];
            ConfigureBuildSlot(root, 0, "1", LoadHudSprite("Ballista", buildPlacement != null ? buildPlacement.ballistaPreviewSprite : null), new Vector2(42f, 48f), () =>
            {
                selectedSlot = 0;
                buildPlacement?.SelectBallista();
            });
            ConfigureBuildSlot(root, 1, "2", LoadHudSprite("FenceHorizontal", buildPlacement != null ? buildPlacement.horizontalFencePreviewSprite : null), new Vector2(112f, 48f), () =>
            {
                selectedSlot = 1;
                buildPlacement?.SelectFence(false);
            });
            ConfigureBuildSlot(root, 2, "3", LoadHudSprite("FenceVertical", buildPlacement != null ? buildPlacement.verticalFencePreviewSprite : null), new Vector2(182f, 48f), () =>
            {
                selectedSlot = 2;
                buildPlacement?.SelectFence(true);
            });

            var statusTransform = root.Find("Build Status");
            var status = statusTransform != null ? statusTransform.GetComponent<Text>() : null;
            if (status == null) status = CreateText(root, "Build Status", "", 14, new Vector2(238f, 48f), new Vector2(64f, 58f), TextAnchor.MiddleCenter);
            status.gameObject.SetActive(true);
            if (buildPlacement != null) buildPlacement.buildText = status;
        }

        void ConfigureBuildSlot(RectTransform parent, int index, string key, Sprite sprite, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            var slotName = "Build Slot " + key;
            var slot = parent.Find(slotName);
            var buttonObject = slot != null ? slot.gameObject : new GameObject(slotName);
            buttonObject.transform.SetParent(parent, false);
            var image = buttonObject.GetComponent<Image>();
            if (image == null) image = buttonObject.AddComponent<Image>();
            var button = buttonObject.GetComponent<Button>();
            if (button == null) button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            var highlight = buttonObject.GetComponent<UiSelectionHighlight>();
            if (highlight == null) highlight = buttonObject.AddComponent<UiSelectionHighlight>();
            highlight.padding = 4f;
            highlight.thickness = 3f;
            slotHighlights[index] = highlight;
            var selectOnHover = buttonObject.GetComponent<SelectOnPointerEnter>();
            if (selectOnHover != null) Destroy(selectOnHover);
            button.colors = new ColorBlock
            {
                normalColor = SlotColor,
                highlightedColor = SlotSelectedColor,
                pressedColor = new Color(0.06f, 0.10f, 0.08f, 0.98f),
                selectedColor = SlotSelectedColor,
                disabledColor = new Color(0.12f, 0.12f, 0.12f, 0.7f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            var rect = buttonObject.GetComponent<RectTransform>();
            if (slot == null)
            {
                image.color = SlotColor;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
                rect.sizeDelta = new Vector2(58f, 66f);
                AddFrame(rect, rect.sizeDelta);
            }

            if (sprite != null)
            {
                var iconTransform = rect.Find("Icon");
                var icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
                if (icon == null) icon = new GameObject("Icon").AddComponent<Image>();
                icon.transform.SetParent(rect, false);
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.rectTransform.anchoredPosition = new Vector2(0f, -2f);
                icon.rectTransform.sizeDelta = index == 2 ? new Vector2(30f, 48f) : new Vector2(46f, 44f);
            }

            var keyTransform = rect.Find("Key");
            var keyText = keyTransform != null ? keyTransform.GetComponent<Text>() : null;
            if (keyText == null) keyText = CreateText(rect, "Key", key, 16, new Vector2(-18f, 22f), new Vector2(24f, 22f), TextAnchor.MiddleCenter);
            keyText.text = key;
            var stockTransform = rect.Find("Stock");
            stockLabels[index] = stockTransform != null ? stockTransform.GetComponent<Text>() : null;
            if (stockLabels[index] == null) stockLabels[index] = CreateText(rect, "Stock", "", 11, new Vector2(0f, -22f), new Vector2(56f, 18f), TextAnchor.MiddleCenter);
            stockLabels[index].fontSize = 11;
            stockLabels[index].rectTransform.anchoredPosition = new Vector2(0f, -22f);
            stockLabels[index].rectTransform.sizeDelta = new Vector2(58f, 18f);
            slotBackplates[index] = image;
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

            var towerSprite = LoadHudSprite("Tower", towerIconSprite);
            if (towerSprite != null)
            {
                var towerImageTransform = towerPanel.Find("Tower Image");
                var towerImage = towerImageTransform != null ? towerImageTransform.GetComponent<Image>() : null;
                if (towerImage == null) towerImage = new GameObject("Tower Image").AddComponent<Image>();
                towerImage.transform.SetParent(towerPanel, false);
                towerImage.sprite = towerSprite;
                towerImage.preserveAspect = true;
                towerImage.raycastTarget = false;
                AnchorTopCenter(towerImage.rectTransform);
                towerImage.rectTransform.anchoredPosition = TowerIconPosition;
                towerImage.rectTransform.sizeDelta = TowerIconSize;
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

        void UpdateBuildSlots()
        {
            if (buildPlacement == null || stockLabels == null) return;
            selectedSlot = buildPlacement.SelectedHudSlot;
            if (stockLabels[0] != null) stockLabels[0].text = buildPlacement.GetHudCostLabel(0);
            if (stockLabels[1] != null) stockLabels[1].text = buildPlacement.GetHudCostLabel(1);
            if (stockLabels[2] != null) stockLabels[2].text = buildPlacement.GetHudCostLabel(2);
            for (int i = 0; i < slotBackplates.Length; i++)
            {
                if (slotBackplates[i] != null) slotBackplates[i].color = i == selectedSlot ? SlotSelectedColor : SlotColor;
                if (slotHighlights != null && i < slotHighlights.Length && slotHighlights[i] != null) slotHighlights[i].forceSelected = i == selectedSlot;
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

        static void SetResourceIcon(Transform parent, string path, string spriteName)
        {
            if (parent == null) return;
            var target = parent.Find(path);
            var image = target != null ? target.GetComponent<Image>() : null;
            if (image == null) return;
            image.sprite = LoadHudSprite(spriteName, null);
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
            var sprite = Resources.Load<Sprite>("Generated/" + name);
            if (sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>("Generated/" + name);
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
