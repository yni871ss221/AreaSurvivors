namespace AreaSurvivors
{
    public enum MapSessionMode
    {
        Game,
        Build
    }

    public enum BossTestSpawnSide
    {
        Up,
        Down,
        Left,
        Right
    }

    public static class RunState
    {
        public static CharacterType SelectedCharacter = CharacterType.Knight;
        static int nextStartStage = 1;
        static float nextStartStageElapsedSeconds;
        static bool hasNextStartStageElapsed;
        static bool hasNextBossTestSpawnSide;
        static BossTestSpawnSide nextBossTestSpawnSide;
        static bool hasNextTestStartingWeapon;
        static WeaponType nextTestStartingWeapon;

        public static void SetNextStartStage(int stage)
        {
            nextStartStage = UnityEngine.Mathf.Max(1, stage);
            hasNextBossTestSpawnSide = false;
        }

        public static int ConsumeNextStartStage()
        {
            int stage = UnityEngine.Mathf.Max(1, nextStartStage);
            nextStartStage = 1;
            return stage;
        }

        public static void SetNextStartStageElapsed(float seconds)
        {
            nextStartStageElapsedSeconds = UnityEngine.Mathf.Max(0f, seconds);
            hasNextStartStageElapsed = true;
        }

        public static float ConsumeNextStartStageElapsed()
        {
            if (!hasNextStartStageElapsed) return 0f;
            hasNextStartStageElapsed = false;
            float seconds = UnityEngine.Mathf.Max(0f, nextStartStageElapsedSeconds);
            nextStartStageElapsedSeconds = 0f;
            return seconds;
        }

        public static void SetNextBossTestSpawnSide(BossTestSpawnSide side)
        {
            nextBossTestSpawnSide = side;
            hasNextBossTestSpawnSide = true;
        }

        public static bool TryConsumeNextBossTestSpawnSide(out BossTestSpawnSide side)
        {
            side = nextBossTestSpawnSide;
            if (!hasNextBossTestSpawnSide) return false;
            hasNextBossTestSpawnSide = false;
            nextBossTestSpawnSide = default;
            return true;
        }

        public static void SetNextWeaponTest(WeaponType weaponType)
        {
            hasNextTestStartingWeapon = true;
            nextTestStartingWeapon = weaponType;
            SetNextStartStage(1);
            nextStartStageElapsedSeconds = 0f;
            hasNextStartStageElapsed = false;
            hasNextBossTestSpawnSide = false;
        }

        public static bool TryConsumeNextTestStartingWeapon(out WeaponType weaponType)
        {
            weaponType = nextTestStartingWeapon;
            if (!hasNextTestStartingWeapon) return false;
            hasNextTestStartingWeapon = false;
            nextTestStartingWeapon = default;
            return true;
        }
    }
}
