using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class WeaponAttributeIconSet : MonoBehaviour
    {
        public Image meleeIcon;
        public Image rangedIcon;
        public Image magicIcon;
        public Image defenseIcon;

        public void Show(WeaponAttributeType attributeType)
        {
            SetIconActive(meleeIcon, attributeType == WeaponAttributeType.Melee);
            SetIconActive(rangedIcon, attributeType == WeaponAttributeType.Ranged);
            SetIconActive(magicIcon, attributeType == WeaponAttributeType.Magic);
            SetIconActive(defenseIcon, attributeType == WeaponAttributeType.Defense);
        }

        public void Hide()
        {
            SetIconActive(meleeIcon, false);
            SetIconActive(rangedIcon, false);
            SetIconActive(magicIcon, false);
            SetIconActive(defenseIcon, false);
        }

        static void SetIconActive(Image icon, bool active)
        {
            if (icon != null) icon.gameObject.SetActive(active);
        }
    }
}
