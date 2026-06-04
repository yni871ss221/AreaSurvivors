using UnityEngine;

namespace AreaSurvivors
{
    [CreateAssetMenu(menuName = "Area Survivors/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Player")]
        public float playerMoveSpeed = 2.1f;
        public int playerMaxHp = 40;
        public float playerReviveSeconds = 6f;
        public float enemyTerritorySlow = 0.35f;
        public int paintRadius = 1;
        public float playerVisualScale = 1f;

        [Header("Camera")]
        public float cameraOrthographicSize = 12.5f;
        public Vector3 cameraOffset = new Vector3(0f, -15.5f, -19f);
        public float cameraPitch = -45f;
        public float cameraZoomedInOrthographicSize = 3.9f;
        public Vector3 cameraZoomedInOffset = new Vector3(0f, -8.5f, -9f);
        public float cameraZoomedInPitch = -35f;
        [Range(0f, 1f)]
        public float cameraDefaultZoom = 0.5f;
        public float cameraZoomScrollSpeed = 0.16f;
        [Range(0f, 1f)]
        public float cameraPlayerWeight = 0.55f;

        [Header("Tower")]
        public int towerMaxHp = 160;
        public float ballistaBuildSeconds = 2.2f;
        public float ballistaRange = 9.5f;
        public float ballistaCooldown = 1.15f;
        public int ballistaDamage = 5;
        public int ballistaMaxHp = 90;
        public float fenceBuildSeconds = 1.8f;
        public int fenceMaxHp = 70;
        public int startingBallistaStock = 4;
        public int startingFenceStock = 4;

        [Header("Combat")]
        public int baseAttackPower = 6;
        public float knightCooldown = 1.05f;
        public float archerCooldown = 0.75f;
        public float mageCooldown = 1.45f;
        public float projectileSpeed = 11.5f;
        public float projectileLifetime = 4.2f;
        public float projectileVisualScale = 1.35f;

        [Header("Enemies")]
        public float enemyBaseSpeed = 0.9f;
        public float enemyVisualScale = 1f;
        public int enemyBaseHp = 14;
        public int enemyDamage = 3;
        public float spawnInterval = 1.8f;
        public float enemySpawnRadius = 28f;
        public float difficultyRampSeconds = 55f;
        public float playerTerritorySlow = 0.35f;

        [Header("Progression")]
        public int xpPerEnemy = 1;
        public int tokenKillsDivisor = 8;
    }
}
