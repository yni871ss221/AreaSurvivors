using System.Collections.Generic;

namespace AreaSurvivors
{
    public sealed class RunResult
    {
        public int kills;
        public int damageDealt;
        public int level;
        public int tokensEarned;
        public float survivedSeconds;
        public List<string> upgrades = new List<string>();

        public static RunResult Last { get; set; }
    }
}
