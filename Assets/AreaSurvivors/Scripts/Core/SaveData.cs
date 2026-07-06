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
        public int wood;
        public int stone;
        public int highestUnlockedStage = 1;
        public int highestClearedStage;
        public int selectedStage = 1;
        public CharacterType selectedCharacter;
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
        public List<RelicRecord> relics = new List<RelicRecord>();
        public List<StageDifficultyRecord> stageDifficulties = new List<StageDifficultyRecord>();
        public List<StageBuildingSet> stageBuildings = new List<StageBuildingSet>();
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
    public sealed class StageDifficultyRecord
    {
        public int stage = 1;
        public int difficulty = 1;
    }

    public enum SavedBuildingKind
    {
        WoodenWall = 0,
        Ballista = 2,
        WatchTower = 3,
        CarpenterHut = 4,
        WorkerHut = 5
    }

    [Serializable]
    public sealed class StageBuildingSet
    {
        public int stage = 1;
        public List<SavedBuildingData> buildings = new List<SavedBuildingData>();
    }

    [Serializable]
    public sealed class SavedBuildingData
    {
        public SavedBuildingKind kind;
        public int x;
        public int y;
        public bool upgraded;
        public bool destroyed;
    }
}
