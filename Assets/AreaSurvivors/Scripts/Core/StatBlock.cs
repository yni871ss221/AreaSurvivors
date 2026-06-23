namespace AreaSurvivors
{
    public struct StatBlock
    {
        public int maxHp;
        public float moveSpeed;
        public int paintRadius;
        public float reviveSeconds;
        public int defense;
        public float xpGainMultiplier;
        public int autoRegen;
        public float workSpeedMultiplier;
        public int resourceGainBonus;
    }

    public struct WeaponStatBlock
    {
        public int level;
        public int attackPower;
        public float cooldownSeconds;
        public float projectileSpeed;
        public float range;
        public float knockback;
        public int projectileCount;
        public float explosionRadius;
    }

    public enum WeaponType
    {
        Slash,
        Arrow,
        Fireball
    }
}
