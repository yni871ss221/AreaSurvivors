using UnityEngine;

namespace AreaSurvivors
{
    public sealed class PlayerStats : MonoBehaviour
    {
        GameConfig config;
        CharacterType characterType;
        int runMaxHpBonus;
        int runPaintRadiusBonus;
        int runDefenseBonus;
        int runAutoRegenBonus;
        int levelStatBonusCount;
        float runMoveSpeedMultiplier = 1f;
        float runXpGainMultiplierBonus;

        public StatBlock Current { get; private set; }

        public void Initialize(GameConfig gameConfig, CharacterType type)
        {
            config = gameConfig;
            characterType = type;
            runMaxHpBonus = 0;
            runPaintRadiusBonus = 0;
            runDefenseBonus = 0;
            runAutoRegenBonus = 0;
            levelStatBonusCount = 0;
            runMoveSpeedMultiplier = 1f;
            runXpGainMultiplierBonus = 0f;
            Recalculate();
        }

        public void MultiplyMoveSpeed(float multiplier) { runMoveSpeedMultiplier *= Mathf.Max(0.01f, multiplier); Recalculate(); }
        public void AddPaintRadius(int value) { runPaintRadiusBonus += value; Recalculate(); }
        public void AddMaxHp(int value) { runMaxHpBonus += value; Recalculate(); }
        public void AddDefense(int value) { runDefenseBonus += value; Recalculate(); }
        public void AddXpGainMultiplier(float value) { runXpGainMultiplierBonus += value; Recalculate(); }
        public void AddAutoRegen(int value) { runAutoRegenBonus += value; Recalculate(); }
        public void SetLevelStatBonusCount(int value) { levelStatBonusCount = Mathf.Max(0, value); Recalculate(); }
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
            var baseStats = config.GetCharacterBaseStats(characterType);

            Current = new StatBlock
            {
                maxHp = baseStats.maxHp + ProgressionStore.GetLevel(UpgradeType.MaxHp) * config.maxHpPerUpgradeLevel + runMaxHpBonus + levelStatBonusCount * config.playerLevelMaxHpBonus + RelicEffects.MaxHpBonus,
                moveSpeed = ((baseStats.moveSpeed * runMoveSpeedMultiplier) + moveSpeedUpgradeLevel * config.moveSpeedPerUpgradeLevel + levelStatBonusCount * config.playerLevelMoveSpeedBonus) * RelicEffects.MoveSpeedMultiplier,
                paintRadius = baseStats.paintRadius + paintRadiusUpgradeLevel / paintLevels + runPaintRadiusBonus,
                reviveSeconds = Mathf.Max(config.minReviveSeconds, baseStats.reviveSeconds - ProgressionStore.GetLevel(UpgradeType.ReviveSpeed) * config.reviveSecondsReductionPerUpgradeLevel),
                defense = baseStats.defense + ProgressionStore.GetLevel(UpgradeType.Defense) * config.defensePerUpgradeLevel + runDefenseBonus + levelStatBonusCount * config.playerLevelDefenseBonus,
                xpGainMultiplier = (baseStats.xpGainMultiplier + ProgressionStore.GetLevel(UpgradeType.XpGain) * config.xpGainMultiplierPerUpgradeLevel + runXpGainMultiplierBonus) * RelicEffects.XpGainMultiplier,
                autoRegen = baseStats.autoRegen + ProgressionStore.GetLevel(UpgradeType.AutoRegen) * config.autoRegenPerUpgradeLevel + runAutoRegenBonus
            };
        }
    }
}
