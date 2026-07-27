namespace AreaSurvivors
{
    public static class CharacterUnlockCatalog
    {
        public static bool IsUnlocked(CharacterType type)
        {
            if (type == CharacterType.Knight) return true;
            return TryGetUnlockUpgrade(type, out var upgrade) && ProgressionStore.IsUnlocked(upgrade);
        }

        public static bool TryGetUnlockUpgrade(CharacterType type, out UpgradeType upgrade)
        {
            switch (type)
            {
                case CharacterType.Archer:
                    upgrade = UpgradeType.UnlockArcher;
                    return true;
                case CharacterType.Mage:
                    upgrade = UpgradeType.UnlockMage;
                    return true;
                default:
                    upgrade = default;
                    return false;
            }
        }
    }
}
