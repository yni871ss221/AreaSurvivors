namespace AreaSurvivors
{
    public enum MapSessionMode
    {
        Game,
        Build
    }

    public static class RunState
    {
        public static CharacterType SelectedCharacter = CharacterType.Knight;
        public static MapSessionMode NextMapMode = MapSessionMode.Game;
        static int nextStartStage = 1;

        public static void SetNextStartStage(int stage)
        {
            nextStartStage = UnityEngine.Mathf.Max(1, stage);
            NextMapMode = MapSessionMode.Game;
        }

        public static void SetNextBuildStage(int stage)
        {
            nextStartStage = UnityEngine.Mathf.Max(1, stage);
            NextMapMode = MapSessionMode.Build;
        }

        public static int ConsumeNextStartStage()
        {
            int stage = UnityEngine.Mathf.Max(1, nextStartStage);
            nextStartStage = 1;
            return stage;
        }

        public static MapSessionMode ConsumeNextMapMode()
        {
            var mode = NextMapMode;
            NextMapMode = MapSessionMode.Game;
            return mode;
        }
    }
}
