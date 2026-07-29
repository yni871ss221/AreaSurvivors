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
    {        void ApplyRunUpgrade(RunUpgradeChoice choice)
        {
            if (choice == null || !TryBeginLevelUpAction()) return;

            choice.apply();
            if (choice.hasDiminishingStat && Player != null && Player.weapon != null)
            {
                Player.weapon.RegisterRunUpgradeSelection(choice.weaponType, choice.diminishingStat);
            }
            if (!choice.isNewWeapon && !choice.isEvolution && choice.hasWeaponType && Player != null && Player.weapon != null)
            {
                Player.weapon.RegisterRunWeaponUpgrade(choice.weaponType);
            }
            runUpgrades.Add(choice.sourceLabel);
            runDifficultyTelemetry.RecordUpgrade(level, currentStage, elapsed, choice.sourceLabel);
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
            if (levelUpPanel == null ||
                !levelUpPanel.activeInHierarchy ||
                IsLevelUpInputBlockedByFrontModal() ||
                lastLevelUpActionFrame == Time.frameCount)
            {
                return false;
            }

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
            if (levelUpPanel == null ||
                !levelUpPanel.activeSelf ||
                IsLevelUpInputBlockedByFrontModal())
            {
                return;
            }

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

    }
}
