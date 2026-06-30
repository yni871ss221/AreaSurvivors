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

        public static void ConfigureAutoRegeneration(GameObject target, GameConfig config, GameObject popupPrefab = null)
        {
            if (target == null || config == null) return;
            int amount = ProgressionStore.GetLevel(UpgradeType.BuildingAutoRegen) * Mathf.Max(0, config.buildingAutoRegenPerUpgradeLevel);
            if (amount <= 0) return;

            var regeneration = target.GetComponent<AutoRegeneration>();
            if (regeneration == null) regeneration = target.AddComponent<AutoRegeneration>();
            regeneration.amount = amount;
            regeneration.intervalSeconds = config.autoRegenIntervalSeconds;
            if (popupPrefab != null) regeneration.popupPrefab = popupPrefab;
            regeneration.popupOffset = ResolvePopupOffset(target);
        }

        static Vector3 ResolvePopupOffset(GameObject target)
        {
            var healthBar = target.GetComponent<BuildingHealthBar>();
            if (healthBar != null && healthBar.hpBar != null)
            {
                var offset = healthBar.hpBar.transform.position - target.transform.position;
                if (offset.sqrMagnitude > 0.001f) return offset + new Vector3(0f, 0.14f, 0f);
            }

            var visual = target.GetComponent<GridObjectVisual>();
            if (visual != null)
            {
                return new Vector3(0f, Mathf.Max(0.65f, visual.FootprintWorldSize.y + 0.32f), 0f);
            }

            return new Vector3(0f, 0.8f, 0f);
        }
    }
}
