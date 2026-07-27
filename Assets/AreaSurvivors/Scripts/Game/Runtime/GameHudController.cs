using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
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
        const string LeftStatusHudGroup = "LeftStatusHud";
        const string TopCenterHudGroup = "TopCenterHud";
        const string RightStatusHudGroup = "RightStatusHud";
        const float HudOverlapPadding = 96f;
        static readonly Vector2 PlayerPanelSize = new Vector2(390f, 318f);
        static readonly Vector2 PlayerIconSize = new Vector2(58f, 58f);

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
        Health playerHealth;
        TileGrid weaponHudGrid;
        bool weaponHudDirty = true;
        Coroutine weaponHudRefreshRoutine;
        bool warnedMissingPlayerStatsHud;
        bool warnedMissingWeaponStatsHud;
        Text tokenText;
        RectTransform bossPanel;
        Text bossNameText;
        Image bossHpFill;
        Text bossHpText;
        Text announcementText;
        AnnouncementBannerTextAnimator announcementAnimator;
        RelicHudPanel relicHud;
        EnemyController activeBoss;
        Health bossHealth;
        Coroutine announcementRoutine;
        Text stageText;
        readonly List<FloatingHudDamage> damagePopups = new List<FloatingHudDamage>();

        public void Initialize(TowerController tower, GameManager owner)
        {
            gameManager = owner;
            player = owner != null ? owner.Player : null;
            BindWeaponHudRefreshSources();
            towerController = tower;
            towerHealth = tower != null ? tower.GetComponent<Health>() : null;
            if (towerHealth != null) towerHealth.Damaged += OnTowerDamaged;
            if (towerController != null) towerController.Upgraded += OnTowerUpgraded;

            var canvas = FindHudCanvas();
            if (canvas == null) canvas = CreateCanvas();

            BindSceneRunStats(canvas.transform);
            BuildStagePanel(canvas.transform);
            BindSceneBossHud(canvas.transform);
            BuildPlayerPanel(canvas.transform);
            BuildTowerPanel(canvas.transform);
            BindRelicHud(canvas.transform);
            ConfigureHudOverlapGroups(canvas.transform);
            UpdatePlayerPanel();
            RefreshWeaponStatsIfDirty();
            UpdateTokenHud();
            UpdateTowerPanel();
            UpdateBossHud();
        }

        void OnDestroy()
        {
            if (towerHealth != null) towerHealth.Damaged -= OnTowerDamaged;
            if (towerController != null) towerController.Upgraded -= OnTowerUpgraded;
            if (bossHealth != null) bossHealth.Died -= OnBossDied;
            if (gameManager != null) gameManager.CombatModifiersChanged -= MarkWeaponHudDirty;
            if (weaponHudGrid != null) weaponHudGrid.ControlChanged -= MarkWeaponHudDirty;
            if (playerHealth != null)
            {
                playerHealth.Damaged -= OnPlayerHealthChanged;
                playerHealth.Healed -= OnPlayerHealthChanged;
            }
            LocalizationService.LanguageChanged -= RefreshWeaponStatsForLanguageChange;
        }

        static void SetDirectChildActive(Transform parent, string path, bool active)
        {
            var child = parent != null ? parent.Find(path) : null;
            if (child != null) child.gameObject.SetActive(active);
        }

        void Update()
        {
            if (player == null && gameManager != null)
            {
                player = gameManager.Player;
                BindPlayerHealth();
                MarkWeaponHudDirty();
            }
            BindWeaponHudGrid();
            UpdatePlayerPanel();
            UpdateTokenHud();
            UpdateTowerPanel();
            UpdateBossHud();
            TickDamagePopups();
        }

        public void RefreshRelics()
        {
            relicHud?.Refresh(true);
            MarkWeaponHudDirty();
        }

        public void RefreshWeaponStats()
        {
            MarkWeaponHudDirty();
        }

        void BindWeaponHudRefreshSources()
        {
            if (gameManager != null)
            {
                gameManager.CombatModifiersChanged -= MarkWeaponHudDirty;
                gameManager.CombatModifiersChanged += MarkWeaponHudDirty;
            }

            LocalizationService.LanguageChanged -= RefreshWeaponStatsForLanguageChange;
            LocalizationService.LanguageChanged += RefreshWeaponStatsForLanguageChange;
            BindWeaponHudGrid();
            BindPlayerHealth();
            MarkWeaponHudDirty();
        }

        void BindWeaponHudGrid()
        {
            var nextGrid = gameManager != null ? gameManager.grid : null;
            if (weaponHudGrid == nextGrid) return;
            if (weaponHudGrid != null) weaponHudGrid.ControlChanged -= MarkWeaponHudDirty;
            weaponHudGrid = nextGrid;
            if (weaponHudGrid != null) weaponHudGrid.ControlChanged += MarkWeaponHudDirty;
            MarkWeaponHudDirty();
        }

        void BindPlayerHealth()
        {
            var nextHealth = player != null ? player.Health : null;
            if (playerHealth == nextHealth) return;
            if (playerHealth != null)
            {
                playerHealth.Damaged -= OnPlayerHealthChanged;
                playerHealth.Healed -= OnPlayerHealthChanged;
            }

            playerHealth = nextHealth;
            if (playerHealth != null)
            {
                playerHealth.Damaged += OnPlayerHealthChanged;
                playerHealth.Healed += OnPlayerHealthChanged;
            }
        }

        void OnPlayerHealthChanged(Health _, int __)
        {
            MarkWeaponHudDirty();
        }

        void MarkWeaponHudDirty()
        {
            weaponHudDirty = true;
            if (weaponHudRefreshRoutine == null && isActiveAndEnabled)
            {
                weaponHudRefreshRoutine = StartCoroutine(RefreshWeaponStatsDeferred());
            }
        }

        void RefreshWeaponStatsForLanguageChange()
        {
            weaponHudDirty = true;
            RefreshWeaponStatsIfDirty();
        }

        IEnumerator RefreshWeaponStatsDeferred()
        {
            yield return null;
            weaponHudRefreshRoutine = null;
            RefreshWeaponStatsIfDirty();
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

        void BindRelicHud(Transform parent)
        {
            relicHud = parent != null ? parent.GetComponentInChildren<RelicHudPanel>(true) : null;
            if (relicHud != null) relicHud.Initialize(gameManager);
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
            announcementAnimator = announcementText != null
                ? announcementText.GetComponent<AnnouncementBannerTextAnimator>()
                : null;
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
            if (bossNameText != null) bossNameText.text = boss != null
                ? LocalizationService.LocalizeSource(boss.displayName)
                : "";
            UpdateBossHud();
        }

        public void ShowAnnouncement(string message)
        {
            if (announcementText == null || string.IsNullOrEmpty(message)) return;
            if (announcementRoutine != null) StopCoroutine(announcementRoutine);
            announcementRoutine = StartCoroutine(AnnouncementRoutine(LocalizationService.LocalizeSource(message)));
        }

        IEnumerator AnnouncementRoutine(string message)
        {
            var root = announcementText.transform.parent.gameObject;
            root.SetActive(true);
            announcementText.text = message;
            var color = announcementText.color;
            color.a = 1f;
            announcementText.color = color;

            if (announcementAnimator != null)
            {
                yield return announcementAnimator.Play(message);
            }
            else
            {
                yield return new WaitForSecondsRealtime(1.8f);
            }

            root.SetActive(false);
            if (announcementAnimator != null) announcementAnimator.ResetVisual();
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
            SetPauseDetailsVisible(false);
        }

        public void SetPauseDetailsVisible(bool visible)
        {
            if (playerStatsPanel != null && playerStatsPanel.gameObject.activeSelf != visible)
            {
                playerStatsPanel.gameObject.SetActive(visible);
            }

            weaponHud.SetDetailedMode(visible);
            weaponHud.Update(player != null ? player.weapon : null);
            weaponHudDirty = false;
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
            if (playerDefenseText != null) playerDefenseText.text = stats.defense.ToString("0.#");
            if (playerXpGainText != null) playerXpGainText.text = stats.xpGainMultiplier.ToString("0.0") + "x";
            if (playerRegenText != null) playerRegenText.text = stats.autoRegen.ToString();
        }

        void RefreshWeaponStatsIfDirty()
        {
            if (!weaponHudDirty) return;
            weaponHudDirty = false;
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

        static void ConfigureHudOverlapGroups(Transform hud)
        {
            ConfigureGroupPanel(hud, "Player", LeftStatusHudGroup);
            ConfigureGroupPanel(hud, "Player Status", LeftStatusHudGroup);
            ConfigureGroupPanel(hud, "Slash Weapon Status", LeftStatusHudGroup);
            ConfigureGroupPanel(hud, "Arrow Weapon Status", LeftStatusHudGroup);
            ConfigureGroupPanel(hud, "Fireball Weapon Status", LeftStatusHudGroup);

            ConfigureGroupPanel(hud, "Area Control Panel", TopCenterHudGroup);
            ConfigureGroupPanel(hud, "Stage Panel", TopCenterHudGroup);
            ConfigureGroupPanel(hud, "Timer Panel", TopCenterHudGroup);

            ConfigureGroupPanel(hud, "Tower Status", RightStatusHudGroup);
            ConfigureGroupPanel(hud, "Token Resource", RightStatusHudGroup);
            ConfigureGroupPanel(hud, "Kill Panel", RightStatusHudGroup);
        }

        static void ConfigureGroupPanel(Transform hud, string name, string groupId)
        {
            var rect = hud != null ? hud.Find(name) as RectTransform : null;
            if (rect == null) return;
            ConfigureOverlapFader(rect, groupId);
        }

        static void ConfigureOverlapFader(RectTransform panel, string groupId)
        {
            var group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            var fader = panel.GetComponent<HudOverlapFader>();
            if (fader == null) fader = panel.gameObject.AddComponent<HudOverlapFader>();
            fader.backgroundAlpha = 0.5f;
            fader.overlapAlpha = 0.2f;
            fader.padding = HudOverlapPadding;
            fader.fadeSpeed = 10f;
            fader.SetGroup(groupId);
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
            text.font = JapaneseFontProvider.Font;
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
