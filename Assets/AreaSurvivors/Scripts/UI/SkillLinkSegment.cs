using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class SkillLinkSegment : MonoBehaviour
    {
        public UpgradeType prerequisite;
        public Image image;
        public Color activeColor = new Color(0.50f, 0.92f, 0.72f, 0.85f);
        public Color inactiveColor = new Color(0.30f, 0.36f, 0.34f, 0.75f);

        public void ResolveReferences()
        {
            if (image == null) image = GetComponent<Image>();
        }

        public void ApplyState(bool active)
        {
            ResolveReferences();
            if (image != null) image.color = active ? activeColor : inactiveColor;
        }
    }
}
