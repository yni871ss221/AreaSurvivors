using System;
using System.Collections.Generic;

namespace AreaSurvivors
{
    [Serializable]
    public sealed class SaveData
    {
        public int tokens;
        public int totalKills;
        public int highestUnlockedStage = 1;
        public int highestClearedStage;
        public CharacterType selectedCharacter;
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
        public List<StageSpeedSetting> stageSpeedSettings = new List<StageSpeedSetting>();
    }

    [Serializable]
    public sealed class UpgradeLevel
    {
        public UpgradeType type;
        public int level;
    }

    [Serializable]
    public sealed class StageSpeedSetting
    {
        public int stage = 1;
        public bool fastMode;
    }
}
