using UnityEngine;

namespace AreaSurvivors
{
    public static class RelicRarityVisuals
    {
        public static Color GetColor(RelicRarity rarity)
        {
            switch (rarity)
            {
                case RelicRarity.Common:
                    return new Color(0.45f, 1f, 0.36f);
                case RelicRarity.Uncommon:
                    return new Color(0.32f, 0.82f, 1f);
                case RelicRarity.Rare:
                    return new Color(0.92f, 0.48f, 1f);
                default:
                    return Color.white;
            }
        }

        public static Color GetBadgeTextColor(RelicRarity rarity)
        {
            return rarity == RelicRarity.Common
                ? new Color(0.02f, 0.12f, 0.04f)
                : new Color(0.02f, 0.05f, 0.08f);
        }
    }
}
