namespace AreaSurvivors
{
    public struct StatBlock
    {
        public int maxHp;
        public float moveSpeed;
        public int paintRadius;
        public float reviveSeconds;
        public float defense;
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
        public float rotationSpeed;
        public float durationSeconds;
        public float slowAmount;
        public float damageIntervalSeconds;
        public float distance;
    }

    public enum WeaponType
    {
        Slash,
        Arrow,
        Fireball,
        Shield,
        Flag,
        BoomerangSword,
        AuraSword,
        ArrowRain,
        Gun,
        Frost,
        ThunderBall
    }

    public enum WeaponAttributeType
    {
        None,
        Melee,
        Ranged,
        Magic,
        Defense
    }
}
