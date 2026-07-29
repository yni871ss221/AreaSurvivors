using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed partial class GameManager
    {        public void AddExperience(int amount)
        {
            float multiplier = Player != null ? Mathf.Max(0f, Player.Stats.xpGainMultiplier) : 1f;
            int baseAmount = Mathf.Max(1, amount);
            xpRemainder += baseAmount * multiplier;
            int gained = Mathf.FloorToInt(xpRemainder);
            runDifficultyTelemetry.RecordExperience(baseAmount, gained);
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
                runDifficultyTelemetry.RecordLevelUp(
                    level,
                    currentStage,
                    elapsed,
                    kills,
                    xp,
                    xpToNext,
                    multiplier,
                    "experience");
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
                activeRelicAcquisitionModalCount > 0 ||
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
            runDifficultyTelemetry.RecordLevelUp(
                level,
                currentStage,
                elapsed,
                kills,
                xp,
                xpToNext,
                Player != null ? Mathf.Max(0f, Player.Stats.xpGainMultiplier) : 1f,
                "opening_bonus");
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
            if (IsLevelUpInputBlockedByFrontModal()) return;

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

    }
}
