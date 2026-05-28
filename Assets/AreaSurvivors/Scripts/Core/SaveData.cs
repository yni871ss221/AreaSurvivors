using System;
using System.Collections.Generic;

namespace AreaSurvivors
{
    [Serializable]
    public sealed class SaveData
    {
        public int tokens;
        public int totalKills;
        public CharacterType selectedCharacter;
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
    }

    [Serializable]
    public sealed class UpgradeLevel
    {
        public UpgradeType type;
        public int level;
    }
}
