using UnityEngine;

namespace AreaSurvivors
{
    [CreateAssetMenu(menuName = "Area Survivors/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Player")]
        public float playerMoveSpeed = 4.2f;
        public int playerMaxHp = 40;
        public float playerReviveSeconds = 6f;
        public float enemyTerritorySlow = 0.35f;
        public int paintRadius = 1;

        [Header("Tower")]
        public int towerMaxHp = 160;

        [Header("Combat")]
        public int baseAttackPower = 6;
        public float knightCooldown = 1.05f;
        public float archerCooldown = 0.75f;
        public float mageCooldown = 1.45f;
        public float projectileSpeed = 9f;

        [Header("Enemies")]
        public float enemyBaseSpeed = 1.65f;
        public int enemyBaseHp = 14;
        public int enemyDamage = 4;
        public float spawnInterval = 1.35f;
        public float difficultyRampSeconds = 35f;
        public float playerTerritorySlow = 0.35f;

        [Header("Progression")]
        public int xpPerEnemy = 1;
        public int tokenKillsDivisor = 8;
    }
}
