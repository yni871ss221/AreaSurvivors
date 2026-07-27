using System;
using System.Collections.Generic;

namespace AreaSurvivors
{
    [Serializable]
    public sealed class SaveData
    {
        public int tokens;
        public int totalKills;
        public int playCount;
        public int highestUnlockedStage = 1;
        public int highestClearedStage;
        public int selectedStage = 1;
        public bool openingStoryCompleted;
        public bool endingCreditsViewed;
        [NonSerialized] internal bool endingCreditsViewedWasSerialized;
        public CharacterType selectedCharacter;
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
        public List<RelicRecord> relics = new List<RelicRecord>();
        public List<WeaponEvolutionRecord> discoveredWeaponEvolutions = new List<WeaponEvolutionRecord>();
        public List<StageDifficultyRecord> stageDifficulties = new List<StageDifficultyRecord>();
    }

    [Serializable]
    public sealed class UpgradeLevel
    {
        public UpgradeType type;
        public int level;
    }

    [Serializable]
    public sealed class RelicRecord
    {
        public RelicType type;
    }

    [Serializable]
    public sealed class WeaponEvolutionRecord
    {
        public WeaponType type;
    }

    [Serializable]
    public sealed class StageDifficultyRecord
    {
        public int stage = 1;
        public int difficulty = 1;
        public int maxUnlockedDifficulty = 1;
        public int highestClearedDifficulty;
    }

}
