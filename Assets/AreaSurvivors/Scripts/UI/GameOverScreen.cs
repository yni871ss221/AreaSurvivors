using System;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class GameOverScreen : MonoBehaviour
    {
        public GameObject root;
        public GameObject defeatBackground;
        public GameObject clearBackground;
        public Text titleText;
        public Text survivedValueText;
        public Text killsValueText;
        public Text damageValueText;
        public Text levelValueText;
        public Text tokensValueText;
        public Text reachedStageValueText;
        public Text upgradeText;
        public GameObject upgradePanel;
        public GameObject relicSummaryPanel;
        public Text[] relicSummaryTexts;
        public RelicSummaryItem[] relicSummaryItems;
        public DamageReportColumn[] damageReportColumns;
        public Text clearMessageText;
        public GameObject stageUnlockPopupRoot;
        public Text stageUnlockHeaderText;
        public Text stageUnlockMessageText;
        public Text missionCompleteText;
        public StageUnlockBossIconBinding[] stageUnlockBossIcons;
        public Button stageUnlockOkButton;
        public Button lobbyButton;
        public SceneNavigator navigator;
        public GameOverIntroAnimator introAnimator;

        [Serializable]
        public sealed class StageUnlockBossIconBinding
        {
            public int stage;
            public GameObject icon;
        }

        [Serializable]
        public sealed class RelicSummaryItem
        {
            public GameObject root;
            public Text nameText;
            public RelicIconSet icons;

            public void Set(RunRelicReportEntry entry, bool active)
            {
                if (root != null) root.SetActive(active);
                if (!active) return;

                string name = entry != null ? entry.displayName : string.Empty;
                if (entry != null && entry.convertedToToken) name += "（変換）";
                SetText(nameText, name);
                if (icons != null) icons.Set(entry != null ? entry.type : RelicType.None);
            }
        }

        [Serializable]
        public sealed class RelicIconSet
        {
            public RelicIconBinding[] bindings;

            public void Set(RelicType type)
            {
                if (bindings == null) return;
                RelicIconBinding activeBinding = null;
                RelicIconBinding fallbackBinding = null;
                for (int i = 0; i < bindings.Length; i++)
                {
                    var binding = bindings[i];
                    if (binding == null || binding.icon == null) continue;
                    binding.icon.SetActive(false);
                    if (binding.type == type)
                    {
                        activeBinding = binding;
                    }
                    if (fallbackBinding == null && type != RelicType.None)
                    {
                        fallbackBinding = binding;
                    }
                }

                if (activeBinding == null) activeBinding = fallbackBinding;
                if (activeBinding == null || activeBinding.icon == null || type == RelicType.None) return;

                activeBinding.icon.SetActive(true);
                activeBinding.icon.transform.localScale = Vector3.one * RelicCatalog.IconScale(type);
                var image = activeBinding.icon.GetComponent<Image>();
                if (image == null) image = activeBinding.icon.GetComponentInChildren<Image>(true);
                if (image != null)
                {
                    image.sprite = LoadIcon(RelicCatalog.Get(type));
                    image.color = Color.white;
                    image.preserveAspect = true;
                }
            }

            static Sprite LoadIcon(RelicDefinition definition)
            {
                var sprite = definition != null ? GeneratedSpriteLoader.Load(definition.iconPath) : null;
                return sprite != null ? sprite : GeneratedSpriteLoader.Load("TreasureChest");
            }
        }

        [Serializable]
        public sealed class RelicIconBinding
        {
            public RelicType type;
            public GameObject icon;
        }

        [Serializable]
        public sealed class DamageReportColumn
        {
            public GameObject root;
            public Text titleText;
            public Text totalDamageText;
            public Text dpsText;
            public DamageReportIconSet icons;

            public void Set(RunDamageReportEntry entry, bool active)
            {
                if (root != null) root.SetActive(active);
                if (!active) return;

                SetText(titleText, entry != null && !string.IsNullOrWhiteSpace(entry.label) ? entry.label : "-");
                SetText(totalDamageText, entry != null ? entry.totalDamage.ToString() : "-");
                SetText(dpsText, entry != null ? FormatDps(entry.Dps) : "-");
                if (icons != null) icons.Set(entry);
            }
        }

        [Serializable]
        public sealed class DamageReportIconSet
        {
            public GameObject centerTower;
            public GameObject ballista;
            public GameObject watchTower;
            public GameObject slash;
            public GameObject arrow;
            public GameObject fireball;
            public GameObject shield;
            public GameObject flag;
            public GameObject boomerangSword;
            public GameObject auraSword;
            public GameObject arrowRain;
            public GameObject gun;
            public GameObject frost;
            public GameObject thunderBall;

            public void Set(RunDamageReportEntry entry)
            {
                HideAll();
                if (entry == null) return;

                if (entry.sourceKind == RunDamageSourceKind.Building)
                {
                    SetActive(BuildingIcon(entry.building), true);
                    return;
                }

                if (entry.sourceKind == RunDamageSourceKind.Weapon)
                {
                    SetActive(WeaponIcon(entry.weapon), true);
                }
            }

            void HideAll()
            {
                SetActive(centerTower, false);
                SetActive(ballista, false);
                SetActive(watchTower, false);
                SetActive(slash, false);
                SetActive(arrow, false);
                SetActive(fireball, false);
                SetActive(shield, false);
                SetActive(flag, false);
                SetActive(boomerangSword, false);
                SetActive(auraSword, false);
                SetActive(arrowRain, false);
                SetActive(gun, false);
                SetActive(frost, false);
                SetActive(thunderBall, false);
            }

            GameObject BuildingIcon(RunDamageBuildingSource source)
            {
                switch (source)
                {
                    case RunDamageBuildingSource.CenterTower: return centerTower;
                    case RunDamageBuildingSource.Ballista: return ballista;
                    case RunDamageBuildingSource.WatchTower: return watchTower;
                    default: return null;
                }
            }

            GameObject WeaponIcon(WeaponType type)
            {
                switch (type)
                {
                    case WeaponType.Arrow: return arrow;
                    case WeaponType.Fireball: return fireball;
                    case WeaponType.Shield: return shield;
                    case WeaponType.Flag: return flag;
                    case WeaponType.BoomerangSword: return boomerangSword;
                    case WeaponType.AuraSword: return auraSword;
                    case WeaponType.ArrowRain: return arrowRain;
                    case WeaponType.Gun: return gun;
                    case WeaponType.Frost: return frost;
                    case WeaponType.ThunderBall: return thunderBall;
                    default: return slash;
                }
            }

            static void SetActive(GameObject target, bool active)
            {
                if (target != null) target.SetActive(active);
            }
        }

        void Start()
        {
            AudioManager.StopBgm();
            var result = RunResult.Last ?? new RunResult();
            ConfigureBackground(result.gameClear);

            var survived = TimeSpan.FromSeconds(result.survivedSeconds);
            SetText(titleText, result.gameClear ? "GAME CLEAR" : "GAME OVER");
            if (titleText != null) titleText.color = result.gameClear ? new Color(0.66f, 1f, 0.64f) : new Color(1f, 0.76f, 0.62f);
            SetText(survivedValueText, $"{survived.Minutes:00}:{survived.Seconds:00}");
            SetText(killsValueText, result.kills.ToString());
            SetText(damageValueText, result.damageDealt.ToString());
            SetText(levelValueText, $"Lv {result.level}");
            SetText(tokensValueText, result.tokensEarned.ToString());
            SetText(reachedStageValueText, $"STAGE {Mathf.Max(1, result.reachedStage)}");
            HideUpgradePanel();
            SetAcquiredRelics(result);
            SetDamageReport(result);

            bool hasClearMessage = !string.IsNullOrWhiteSpace(result.clearMessage);
            if (clearMessageText != null)
            {
                clearMessageText.gameObject.SetActive(hasClearMessage);
                clearMessageText.text = hasClearMessage ? result.clearMessage : string.Empty;
            }

            bool missionCompletePopup;
            bool hasStageUnlockPopup = ConfigureStageUnlockPopup(result, out missionCompletePopup);

            if (lobbyButton != null && navigator != null)
            {
                lobbyButton.onClick.RemoveListener(navigator.LoadLobby);
                lobbyButton.onClick.AddListener(navigator.LoadLobby);
            }

            if (root != null) root.SetActive(true);
            if (introAnimator != null) introAnimator.Play(result.gameClear, hasStageUnlockPopup, missionCompletePopup);
        }

        void ConfigureBackground(bool gameClear)
        {
            if (defeatBackground != null) defeatBackground.SetActive(!gameClear);
            if (clearBackground != null) clearBackground.SetActive(gameClear);
        }

        void Update()
        {
            if (stageUnlockPopupRoot != null && stageUnlockPopupRoot.activeInHierarchy)
            {
                var popupCandidates = new Selectable[] { stageUnlockOkButton, lobbyButton };
                if (UiSelectionUtility.TickControllerSubmit(popupCandidates)) return;
                UiSelectionUtility.ConfigureVerticalNavigation(popupCandidates);
                UiSelectionUtility.EnsureSelection(popupCandidates);
                return;
            }

            if (UiSelectionUtility.TickControllerSubmit(lobbyButton)) return;
            UiSelectionUtility.ConfigureVerticalNavigation(lobbyButton);
            UiSelectionUtility.EnsureSelection(lobbyButton);
        }

        bool ConfigureStageUnlockPopup(RunResult result, out bool missionComplete)
        {
            missionComplete = result != null && result.gameClear && result.clearedStage >= 4;
            int unlockedStage = result != null ? result.unlockedStage : 0;
            bool stageUnlocked = result != null && result.gameClear && unlockedStage >= 2 && unlockedStage <= 4;
            bool active = missionComplete || stageUnlocked;
            if (stageUnlockPopupRoot != null) stageUnlockPopupRoot.SetActive(active);
            if (!active) return false;

            if (stageUnlockHeaderText != null) stageUnlockHeaderText.gameObject.SetActive(!missionComplete);

            if (stageUnlockMessageText != null)
            {
                stageUnlockMessageText.gameObject.SetActive(!missionComplete);
                SetText(stageUnlockMessageText, missionComplete ? string.Empty : $"ステージ{unlockedStage}が解放されました");
            }

            if (missionCompleteText != null)
            {
                missionCompleteText.gameObject.SetActive(missionComplete);
                SetText(missionCompleteText, missionComplete ? "MISSION COMPLETE" : string.Empty);
            }

            if (stageUnlockBossIcons != null)
            {
                for (int i = 0; i < stageUnlockBossIcons.Length; i++)
                {
                    var binding = stageUnlockBossIcons[i];
                    if (binding == null || binding.icon == null) continue;
                    binding.icon.SetActive(!missionComplete && binding.stage == unlockedStage);
                }
            }

            if (stageUnlockOkButton != null) stageUnlockOkButton.interactable = false;
            return true;
        }

        void HideUpgradePanel()
        {
            if (upgradePanel != null)
            {
                upgradePanel.SetActive(false);
                return;
            }

            if (upgradeText != null && upgradeText.transform.parent != null)
            {
                upgradeText.transform.parent.gameObject.SetActive(false);
            }
        }

        void SetDamageReport(RunResult result)
        {
            if (damageReportColumns == null) return;
            var report = result.damageReport;
            for (int i = 0; i < damageReportColumns.Length; i++)
            {
                var column = damageReportColumns[i];
                if (column == null) continue;
                var entry = report != null && i < report.Count ? report[i] : null;
                bool active = entry != null && entry.visible;
                column.Set(entry, active);
            }
        }

        void SetAcquiredRelics(RunResult result)
        {
            var entries = result != null ? result.acquiredRelicEntries : null;
            bool hasEntryRelics = entries != null && entries.Count > 0;
            if (relicSummaryPanel != null) relicSummaryPanel.SetActive(true);

            if (relicSummaryItems != null && relicSummaryItems.Length > 0)
            {
                for (int i = 0; i < relicSummaryItems.Length; i++)
                {
                    var item = relicSummaryItems[i];
                    if (item == null) continue;
                    var entry = hasEntryRelics && i < entries.Count ? entries[i] : null;
                    bool active = entry != null || i == 0 && !hasEntryRelics;
                    item.Set(entry, active);
                    if (entry == null && active) SetText(item.nameText, "なし");
                }

                return;
            }

            var relics = result != null ? result.acquiredRelics : null;
            bool hasRelics = relics != null && relics.Count > 0;
            if (relicSummaryTexts == null) return;

            for (int i = 0; i < relicSummaryTexts.Length; i++)
            {
                string value = hasRelics && i < relics.Count ? relics[i] : i == 0 && !hasRelics ? "なし" : string.Empty;
                SetText(relicSummaryTexts[i], value);
                if (relicSummaryTexts[i] != null) relicSummaryTexts[i].gameObject.SetActive(!string.IsNullOrEmpty(value));
            }
        }

        static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        static string FormatDps(float value)
        {
            return value >= 10f ? value.ToString("0") : value.ToString("0.0");
        }
    }
}
