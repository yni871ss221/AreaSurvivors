using UnityEngine;

namespace AreaSurvivors
{
    public static class BuildingSkillEffects
    {
        public static int WallMaxHpBonus(GameConfig config)
        {
            if (config == null) return 0;
            int level =
                ProgressionStore.GetLevel(UpgradeType.WallMaxHp1) +
                ProgressionStore.GetLevel(UpgradeType.WallMaxHp2) +
                ProgressionStore.GetLevel(UpgradeType.WallMaxHp3);
            return Mathf.Max(0, level * config.wallMaxHpPerSkill);
        }

        public static void ConfigureAutoRegeneration(GameObject target, GameConfig config)
        {
            if (target == null || config == null) return;
            int amount = ProgressionStore.GetLevel(UpgradeType.BuildingAutoRegen) * Mathf.Max(0, config.buildingAutoRegenPerUpgradeLevel);
            if (amount <= 0) return;

            var regeneration = target.GetComponent<AutoRegeneration>();
            if (regeneration == null) regeneration = target.AddComponent<AutoRegeneration>();
            regeneration.amount = amount;
            regeneration.intervalSeconds = config.autoRegenIntervalSeconds;
        }
    }
}
