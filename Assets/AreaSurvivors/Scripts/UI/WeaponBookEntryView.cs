using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class WeaponBookEntryView : MonoBehaviour
    {
        static readonly Color NormalPanelColor = new Color(0.08f, 0.17f, 0.12f, 0.94f);
        static readonly Color SelectedPanelColor = new Color(0.13f, 0.31f, 0.2f, 0.98f);
        static readonly Color LockedIconColor = new Color(0f, 0f, 0f, 0.78f);

        public string weaponId;
        public string displayName;
        public bool unlockedByDefault;
        public bool futureWeapon;
        public UpgradeType requiredUpgrade;
        public bool usesRuntimeStats;
        public WeaponType weaponType;
        public WeaponAttributeType attributeType;

        [TextArea(2, 4)] public string featureDescription;
        [TextArea(2, 5)] public string initialStatsText;
        [TextArea(2, 4)] public string specialEffectDescription;

        public Button button;
        public Image background;
        public Image icon;
        public Image silhouetteOverlay;
        public Text nameText;

        WeaponBookScreen owner;

        public string LocalizedDisplayName => WeaponCatalog.DisplayName(weaponType);
        public string LocalizedFeatureDescription => LocalizationService.LocalizeSource(featureDescription);
        public string LocalizedInitialStatsText => LocalizationService.LocalizeSource(initialStatsText);
        public string LocalizedSpecialEffectDescription => LocalizationService.LocalizeSource(specialEffectDescription);

        public bool IsUnlocked
        {
            get
            {
                if (unlockedByDefault) return true;
                if (futureWeapon) return false;
                return ProgressionStore.IsUnlocked(requiredUpgrade);
            }
        }

        public void Initialize(WeaponBookScreen screen)
        {
            owner = screen;
            if (button == null) button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClicked);
            }

            Refresh();
        }

        public void Refresh()
        {
            bool unlocked = IsUnlocked;
            if (nameText != null) nameText.text = unlocked ? LocalizedDisplayName : "LOCK";
            if (silhouetteOverlay != null) silhouetteOverlay.gameObject.SetActive(!unlocked);
            if (icon != null) icon.color = unlocked ? Color.white : LockedIconColor;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            var highlight = GetComponent<UiSelectionHighlight>();
            if (highlight != null) highlight.SetNormalBackgroundColor(NormalPanelColor);
            if (background != null) background.color = selected ? SelectedPanelColor : NormalPanelColor;
        }

        void OnClicked()
        {
            AudioManager.PlayButtonConfirm();
            if (owner == null) return;
            if (IsUnlocked)
            {
                owner.Select(this);
            }
            else
            {
                owner.ShowLockedMessage(this);
            }
        }
    }
}
