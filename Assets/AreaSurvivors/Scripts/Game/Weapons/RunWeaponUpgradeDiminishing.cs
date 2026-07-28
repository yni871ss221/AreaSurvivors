using UnityEngine;

namespace AreaSurvivors
{
    public enum RunWeaponUpgradeStat
    {
        None = 0,
        Range = 1,
        ProjectileRange = 2,
        ExplosionRange = 3,
        Knockback = 4,
        Cooldown = 5,
        Slow = 6
    }

    public static class RunWeaponUpgradeDiminishing
    {
        public const float RetentionPerRepeat = 0.8f;

        public static float Factor(int previousSelections)
        {
            return Mathf.Pow(RetentionPerRepeat, Mathf.Max(0, previousSelections));
        }

        public static float AdditiveAmount(float baseAmount, int previousSelections)
        {
            return Mathf.Max(0f, baseAmount) * Factor(previousSelections);
        }

        public static float CooldownMultiplier(float baseMultiplier, int previousSelections)
        {
            float clampedBaseMultiplier = Mathf.Clamp(baseMultiplier, 0.05f, 1f);
            float baseReductionRate = 1f - clampedBaseMultiplier;
            return Mathf.Clamp(
                1f - baseReductionRate * Factor(previousSelections),
                0.05f,
                1f);
        }

        public static float CumulativeAdditiveAmount(float baseAmount, int selectionCount)
        {
            float total = 0f;
            for (int i = 0; i < Mathf.Max(0, selectionCount); i++)
            {
                total += AdditiveAmount(baseAmount, i);
            }
            return total;
        }

        public static float CumulativeCooldownMultiplier(float baseMultiplier, int selectionCount)
        {
            float totalMultiplier = 1f;
            for (int i = 0; i < Mathf.Max(0, selectionCount); i++)
            {
                totalMultiplier *= CooldownMultiplier(baseMultiplier, i);
            }
            return totalMultiplier;
        }
    }
}
