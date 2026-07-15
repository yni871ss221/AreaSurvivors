using System;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class WeaponHudCompactIconSlot : MonoBehaviour
    {
        [Serializable]
        public struct IconEntry
        {
            public WeaponType weaponType;
            public GameObject icon;
        }

        public IconEntry[] icons = Array.Empty<IconEntry>();
        public Text weaponName;
        public Text weaponLevel;
        public WeaponAttributeIconSet weaponTypeIcons;
        public Image slotBackground;
        public Image infoPanelBackground;
        public Color evolvedSlotBackgroundColor = new Color(0.56f, 0.20f, 0.20f, 0.92f);
        public Color evolvedInfoBackgroundColor = new Color(0.32f, 0.10f, 0.10f, 0.86f);

        Color defaultSlotBackgroundColor;
        Color defaultInfoBackgroundColor;
        bool backgroundColorsCaptured;

        void Awake()
        {
            CaptureBackgroundColors();
        }

        public void Show(WeaponType weaponType, int level)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            SetInfoPanelActive(true);
            for (int i = 0; i < icons.Length; i++)
            {
                var icon = icons[i].icon;
                if (icon != null && icon.activeSelf != (icons[i].weaponType == weaponType))
                {
                    icon.SetActive(icons[i].weaponType == weaponType);
                }
            }

            if (weaponName != null) weaponName.text = WeaponCatalog.DisplayName(weaponType);
            if (weaponLevel != null) weaponLevel.text = "Lv." + Mathf.Max(1, level);
            if (weaponTypeIcons != null) weaponTypeIcons.Show(WeaponAttributeCatalog.ForWeapon(weaponType));
            SetEvolutionBackground(WeaponCatalog.IsEvolution(weaponType));
        }

        public void Hide()
        {
            SetInfoPanelActive(false);
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void Clear()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            SetInfoPanelActive(true);
            for (int i = 0; i < icons.Length; i++)
            {
                var icon = icons[i].icon;
                if (icon != null && icon.activeSelf) icon.SetActive(false);
            }

            if (weaponName != null) weaponName.text = string.Empty;
            if (weaponLevel != null) weaponLevel.text = string.Empty;
            if (weaponTypeIcons != null) weaponTypeIcons.Hide();
            SetEvolutionBackground(false);
        }

        void CaptureBackgroundColors()
        {
            if (backgroundColorsCaptured) return;
            backgroundColorsCaptured = true;
            if (slotBackground == null) slotBackground = GetComponent<Image>();
            defaultSlotBackgroundColor = slotBackground != null ? slotBackground.color : Color.white;
            defaultInfoBackgroundColor = infoPanelBackground != null ? infoPanelBackground.color : Color.white;
        }

        void SetEvolutionBackground(bool evolved)
        {
            CaptureBackgroundColors();
            if (slotBackground != null) slotBackground.color = evolved ? evolvedSlotBackgroundColor : defaultSlotBackgroundColor;
            if (infoPanelBackground != null) infoPanelBackground.color = evolved ? evolvedInfoBackgroundColor : defaultInfoBackgroundColor;
        }

        void SetInfoPanelActive(bool active)
        {
            GameObject infoPanel = null;
            if (weaponName != null && weaponName.transform.parent != null)
            {
                infoPanel = weaponName.transform.parent.gameObject;
            }
            else if (weaponTypeIcons != null && weaponTypeIcons.transform.parent != null)
            {
                infoPanel = weaponTypeIcons.transform.parent.gameObject;
            }
            else if (weaponLevel != null && weaponLevel.transform.parent != null)
            {
                infoPanel = weaponLevel.transform.parent.gameObject;
            }

            if (infoPanel != null && infoPanel != gameObject && infoPanel.activeSelf != active)
            {
                infoPanel.SetActive(active);
            }
        }
    }
}
