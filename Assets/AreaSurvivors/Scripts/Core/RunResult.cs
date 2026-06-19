using System.Collections.Generic;

namespace AreaSurvivors
{
    public sealed class RunResult
    {
        public int kills;
        public int damageDealt;
        public int level;
        public int tokensEarned;
        public int woodEarned;
        public int stoneEarned;
        public float survivedSeconds;
        public bool gameClear;
        public int clearedStage;
        public int unlockedStage;
        public string clearMessage;
        public List<string> upgrades = new List<string>();

        public static RunResult Last { get; set; }
    }
}
