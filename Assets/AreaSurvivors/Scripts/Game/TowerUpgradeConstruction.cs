using UnityEngine;

namespace AreaSurvivors
{
    public sealed class TowerUpgradeConstruction : MonoBehaviour, IBuildableConstruction
    {
        TowerController tower;
        GameConfig config;
        TileGrid grid;
        GameManager owner;
        Sprite upgradedSprite;
        float progress;
        float assistedBuildTimer;
        int touchingPlayers;
        Transform activeBuilder;
        bool completed;
        bool canceling;
        Collider2D[] towerColliders;
        Collider2D playerCollider;
        Vector3Int originCell;
        Vector2Int footprint = new Vector2Int(3, 3);

        const float BuildDecaySecondsMultiplier = 3f;
        const float ColliderContactTolerance = 0.02f;

        public bool IsBuilt => completed;
        public TileGrid Grid => grid;
        public Vector3Int OriginCell => originCell;
        public Vector2Int Footprint => footprint;

        public void Configure(TowerController target, GameConfig gameConfig, TileGrid tileGrid, GameManager gameManager, Sprite sprite)
        {
            tower = target;
            config = gameConfig;
            grid = tileGrid;
            owner = gameManager;
            upgradedSprite = sprite;
            towerColliders = GetComponents<Collider2D>();
            var player = owner != null ? owner.Player : GameManager.Instance != null ? GameManager.Instance.Player : null;
            playerCollider = player != null ? player.GetComponent<Collider2D>() : null;
            if (grid != null)
            {
                originCell = grid.WorldToCell(transform.position);
                var marker = GetComponent<GridObjectMarker>();
                if (marker != null) footprint = marker.footprint;
            }
            tower?.ShowUpgradeConstruction(progress, true, false);
        }

        void Update()
        {
            if (completed || canceling) return;
            var contactBuilder = ColliderContactBuilder();
            bool hasBuilderContact = contactBuilder != null;
            if (hasBuilderContact)
            {
                AddBuildWork(WorkSpeedMultiplier(), contactBuilder);
            }
            else if (progress > 0f)
            {
                if (assistedBuildTimer > 0f)
                {
                    assistedBuildTimer = Mathf.Max(0f, assistedBuildTimer - Time.deltaTime);
                }
                else
                {
                    progress = Mathf.Clamp01(progress - Time.deltaTime / Mathf.Max(0.1f, BuildSeconds() * BuildDecaySecondsMultiplier));
                    tower?.ShowUpgradeConstruction(progress, true, false);
                }
            }

            tower?.ShowUpgradeConstruction(progress, true, ShouldShowHammer());
        }

        void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider.GetComponent<PlayerController>() == null) return;
            touchingPlayers++;
            activeBuilder = collision.collider.transform;
            playerCollider = collision.collider;
        }

        void OnCollisionExit2D(Collision2D collision)
        {
            if (collision.collider.GetComponent<PlayerController>() == null) return;
            touchingPlayers = Mathf.Max(0, touchingPlayers - 1);
            if (touchingPlayers == 0 || activeBuilder == collision.collider.transform) activeBuilder = null;
            if (playerCollider == collision.collider) playerCollider = null;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            touchingPlayers++;
            activeBuilder = other.transform;
            playerCollider = other;
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            touchingPlayers = Mathf.Max(0, touchingPlayers - 1);
            if (touchingPlayers == 0 || activeBuilder == other.transform) activeBuilder = null;
            if (playerCollider == other) playerCollider = null;
        }

        public void AddBuildWork(float workSpeedMultiplier, Transform builder = null)
        {
            if (completed || canceling) return;
            progress = Mathf.Clamp01(progress + Time.deltaTime * Mathf.Max(0f, workSpeedMultiplier) / Mathf.Max(0.1f, BuildSeconds()));
            assistedBuildTimer = 0.18f;
            if (builder != null) activeBuilder = builder;
            tower?.ShowUpgradeConstruction(progress, true, ShouldShowHammer());
            if (progress >= 1f) CompleteBuild();
        }

        public void CancelAndRefund()
        {
            if (completed || canceling) return;
            canceling = true;
            if (owner != null && config != null)
            {
                owner.AddResource(ResourceType.Wood, config.towerUpgradeWoodCost);
                owner.AddResource(ResourceType.Stone, config.towerUpgradeStoneCost);
            }
            tower?.ClearPendingUpgrade(this);
            Destroy(this);
        }

        void CompleteBuild()
        {
            if (completed) return;
            completed = true;
            progress = 1f;
            tower?.CompleteUpgrade(config, grid, upgradedSprite);
            Destroy(this);
        }

        float BuildSeconds()
        {
            return config != null ? Mathf.Max(0.1f, config.towerUpgradeBuildSeconds) : 5f;
        }

        bool ShouldShowHammer()
        {
            return ColliderContactBuilder() != null || assistedBuildTimer > 0f;
        }

        Transform ColliderContactBuilder()
        {
            var player = owner != null ? owner.Player : GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return null;
            if (playerCollider == null) playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider == null) return touchingPlayers > 0 ? player.transform : null;
            if (towerColliders == null || towerColliders.Length == 0) towerColliders = GetComponents<Collider2D>();
            foreach (var towerCollider in towerColliders)
            {
                if (towerCollider == null || !towerCollider.enabled) continue;
                var distance = towerCollider.Distance(playerCollider);
                if (distance.isOverlapped || distance.distance <= ColliderContactTolerance) return player.transform;
            }
            return null;
        }

        static float WorkSpeedMultiplier()
        {
            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            return player != null ? Mathf.Max(0.05f, player.Stats.workSpeedMultiplier) : 1f;
        }
    }
}
