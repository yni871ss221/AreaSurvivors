using System.Collections.Generic;

namespace AreaSurvivors
{
    [System.Serializable]
    public sealed class RunRelicReportEntry
    {
        public RelicType type;
        public string displayName;
        public bool convertedToToken;
    }

    public sealed class RunResult
    {
        public int kills;
        public int damageDealt;
        public int level;
        public int tokensEarned;
        public int woodEarned;
        public int stoneEarned;
        public int reachedStage;
        public float survivedSeconds;
        public bool gameClear;
        public int clearedStage;
        public int unlockedStage;
        public string clearMessage;
        public List<string> upgrades = new List<string>();
        public List<string> acquiredRelics = new List<string>();
        public List<RunRelicReportEntry> acquiredRelicEntries = new List<RunRelicReportEntry>();
        public List<RunDamageReportEntry> damageReport = new List<RunDamageReportEntry>();

        public static RunResult Last { get; set; }
    }
}
