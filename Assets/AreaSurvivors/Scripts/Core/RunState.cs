namespace AreaSurvivors
{
    public static class RunState
    {
        public static CharacterType SelectedCharacter = CharacterType.Knight;
        static int nextStartStage = 1;

        public static void SetNextStartStage(int stage)
        {
            nextStartStage = UnityEngine.Mathf.Max(1, stage);
        }

        public static int ConsumeNextStartStage()
        {
            int stage = UnityEngine.Mathf.Max(1, nextStartStage);
            nextStartStage = 1;
            return stage;
        }
    }
}
