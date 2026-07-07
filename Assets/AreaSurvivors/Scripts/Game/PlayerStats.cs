using UnityEngine;

namespace AreaSurvivors
{
    public sealed class PlayerStats : MonoBehaviour
    {
        GameConfig config;
        int runMaxHpBonus;
        int runPaintRadiusBonus;
        int runDefenseBonus;
        int runAutoRegenBonus;
        int runResourceGainBonus;
        float runMoveSpeedMultiplier = 1f;
        float runXpGainMultiplierBonus;
        float runWorkSpeedMultiplierBonus;

        public StatBlock Current { get; private set; }

        public void Initialize(GameConfig gameConfig)
        {
            config = gameConfig;
            runMaxHpBonus = 0;
            runPaintRadiusBonus = 0;
            runDefenseBonus = 0;
            runAutoRegenBonus = 0;
            runResourceGainBonus = 0;
            runMoveSpeedMultiplier = 1f;
            runXpGainMultiplierBonus = 0f;
            runWorkSpeedMultiplierBonus = 0f;
            Recalculate();
        }

        public void MultiplyMoveSpeed(float multiplier) { runMoveSpeedMultiplier *= Mathf.Max(0.01f, multiplier); Recalculate(); }
        public void AddPaintRadius(int value) { runPaintRadiusBonus += value; Recalculate(); }
        public void AddMaxHp(int value) { runMaxHpBonus += value; Recalculate(); }
        public void AddDefense(int value) { runDefenseBonus += value; Recalculate(); }
        public void AddXpGainMultiplier(float value) { runXpGainMultiplierBonus += value; Recalculate(); }
        public void AddAutoRegen(int value) { runAutoRegenBonus += value; Recalculate(); }
        public void AddWorkSpeedMultiplier(float value) { runWorkSpeedMultiplierBonus += value; Recalculate(); }
        public void AddResourceGain(int value) { runResourceGainBonus += value; Recalculate(); }
        public void Refresh() { Recalculate(); }

        void Recalculate()
        {
            if (config == null)
            {
                Current = default;
                return;
            }

            int paintLevels = Mathf.Max(1, config.paintRadiusLevelsPerBonus);
            int moveSpeedUpgradeLevel = ProgressionStore.GetLevel(UpgradeType.MoveSpeed) + ProgressionStore.GetLevel(UpgradeType.MoveSpeedAdvanced);
            int paintRadiusUpgradeLevel = ProgressionStore.GetLevel(UpgradeType.PaintRadius) + ProgressionStore.GetLevel(UpgradeType.PaintRadiusAdvanced);

            Current = new StatBlock
            {
                maxHp = config.playerMaxHp + ProgressionStore.GetLevel(UpgradeType.MaxHp) * config.maxHpPerUpgradeLevel + runMaxHpBonus + RelicEffects.MaxHpBonus,
                moveSpeed = ((config.playerMoveSpeed * runMoveSpeedMultiplier) + moveSpeedUpgradeLevel * config.moveSpeedPerUpgradeLevel) * RelicEffects.MoveSpeedMultiplier,
                paintRadius = config.paintRadius + paintRadiusUpgradeLevel / paintLevels + runPaintRadiusBonus,
                reviveSeconds = Mathf.Max(config.minReviveSeconds, config.playerReviveSeconds - ProgressionStore.GetLevel(UpgradeType.ReviveSpeed) * config.reviveSecondsReductionPerUpgradeLevel),
                defense = config.baseDefense + ProgressionStore.GetLevel(UpgradeType.Defense) * config.defensePerUpgradeLevel + runDefenseBonus,
                xpGainMultiplier = (config.baseXpGainMultiplier + ProgressionStore.GetLevel(UpgradeType.XpGain) * config.xpGainMultiplierPerUpgradeLevel + runXpGainMultiplierBonus) * RelicEffects.XpGainMultiplier,
                autoRegen = config.baseAutoRegen + ProgressionStore.GetLevel(UpgradeType.AutoRegen) * config.autoRegenPerUpgradeLevel + runAutoRegenBonus,
                workSpeedMultiplier = config.baseWorkSpeedMultiplier + ProgressionStore.GetLevel(UpgradeType.WorkSpeed) * config.workSpeedMultiplierPerUpgradeLevel + runWorkSpeedMultiplierBonus,
                resourceGainBonus = config.baseResourceGainBonus + ProgressionStore.GetLevel(UpgradeType.ResourceGain) * config.resourceGainPerUpgradeLevel + runResourceGainBonus
            };
        }
    }
}
