using System;
using System.Collections.Generic;

namespace AreaSurvivors
{
    [Serializable]
    public sealed class SaveData
    {
        public int tokens;
        public int totalKills;
        public int wood;
        public int stone;
        public int highestUnlockedStage = 1;
        public int highestClearedStage;
        public int selectedStage = 1;
        public CharacterType selectedCharacter;
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
        public List<StageSpeedSetting> stageSpeedSettings = new List<StageSpeedSetting>();
        public List<StageBuildingSet> stageBuildings = new List<StageBuildingSet>();
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

    public enum SavedBuildingKind
    {
        WoodenWall = 0,
        WoodenGate = 1,
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
