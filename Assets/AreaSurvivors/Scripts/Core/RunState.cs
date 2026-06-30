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
        static int nextStartStage = 1;
        static bool hasNextTestStartingWeapon;
        static WeaponType nextTestStartingWeapon;

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

        public static void SetNextWeaponTest(WeaponType weaponType)
        {
            hasNextTestStartingWeapon = true;
            nextTestStartingWeapon = weaponType;
            SetNextStartStage(1);
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
