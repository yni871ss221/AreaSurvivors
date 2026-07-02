using UnityEngine;

namespace AreaSurvivors
{
    public static class BuildingSkillEffects
    {
        const int DefaultWatchTowerDamage = 1;
        const int DefaultWatchTowerDamagePerUpgradeLevel = 1;
        const int DefaultUpgradedWatchTowerDamageBonus = 3;

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
            if (target.GetComponent<WoodenBarrier>() != null) amount += RelicEffects.WallAutoRegenBonus;
            if (amount <= 0) return;

            var regeneration = target.GetComponent<AutoRegeneration>();
            if (regeneration == null) regeneration = target.AddComponent<AutoRegeneration>();
            regeneration.amount = amount;
            regeneration.intervalSeconds = config.autoRegenIntervalSeconds;
            if (popupPrefab != null) regeneration.popupPrefab = popupPrefab;
            regeneration.popupOffset = ResolvePopupOffset(target);
        }

        public static int WatchTowerDamage(GameConfig config, bool isUpgraded)
        {
            int baseDamage = config != null ? config.watchTowerDamage : DefaultWatchTowerDamage;
            int perLevel = config != null ? config.watchTowerDamagePerUpgradeLevel : DefaultWatchTowerDamagePerUpgradeLevel;
            int upgradedBonus = 0;
            if (isUpgraded)
            {
                upgradedBonus = config != null
                    ? config.upgradedWatchTowerDamageBonus
                    : DefaultUpgradedWatchTowerDamageBonus;
            }

            return Mathf.Max(0,
                baseDamage +
                ProgressionStore.GetLevel(UpgradeType.WatchTowerDamage) * Mathf.Max(0, perLevel) +
                Mathf.Max(0, upgradedBonus));
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
