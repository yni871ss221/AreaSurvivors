using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AreaSurvivors
{
    public sealed class FixedBuildingLayoutService : MonoBehaviour
    {
        enum FixedBuildingKind
        {
            WoodenWall,
            Ballista,
            WatchTower
        }

        sealed class SlotDefinition
        {
            public FixedBuildingKind kind;
            public UpgradeType unlockType;
            public UpgradeType upgradeType;
            public Vector2Int footprint;
            public Vector2Int desiredOffset;
            public bool requiresPlayerTerritory = true;
        }

        const int LayoutTowerCenterColumn = 13;
        const int LayoutTowerCenterRow = 13;

        static readonly SlotDefinition[] Slots = BuildSlotDefinitions();

        public GameObject ballistaPrefab;
        public GameObject woodenWallPrefab;
        public GameObject watchTowerPrefab;
        public Sprite woodenWallSprite;
        public TileBase ballistaTile;
        public TileBase woodenWallTile;
        public TileBase watchTowerTile;

        GameConfig config;
        TileGrid grid;
        GameObject damagePopupPrefab;

        public void Initialize(GameConfig gameConfig, TileGrid tileGrid, GameObject popupPrefab)
        {
            config = gameConfig;
            grid = tileGrid;
            damagePopupPrefab = popupPrefab;
        }

        public int SpawnUnlockedBuildings()
        {
            if (grid == null) return 0;

            int spawned = 0;
            var towerOrigin = grid.GridToCell(grid.width / 2, grid.height / 2);
            for (int i = 0; i < Slots.Length; i++)
            {
                var definition = Slots[i];
                if (!ProgressionStore.IsUnlocked(definition.unlockType)) continue;
                if (!TryFindSlotOrigin(
                        towerOrigin,
                        definition.footprint,
                        definition.desiredOffset,
                        definition.requiresPlayerTerritory,
                        out var originCell))
                {
                    continue;
                }

                if (SpawnBuilding(definition, originCell)) spawned++;
            }

            return spawned;
        }

        bool SpawnBuilding(SlotDefinition definition, Vector3Int originCell)
        {
            var prefab = PrefabFor(definition.kind);
            if (prefab == null) return false;

            var world = GridObjectVisual.FootprintBottomCenterToWorld(grid, originCell, definition.footprint);
            var instance = Instantiate(prefab, world, Quaternion.identity);
            ConfigureBuilding(instance, definition.kind);

            var marker = instance.GetComponent<GridObjectMarker>();
            if (marker != null && !grid.TryRegisterObject(originCell, marker.type, marker.flags, instance, marker.footprint))
            {
                Destroy(instance);
                return false;
            }

            RegisterBuilding(instance, originCell);
            instance.GetComponent<IBuildableConstruction>()?.CompleteImmediately();

            bool upgraded = ProgressionStore.IsUnlocked(definition.upgradeType);
            if (upgraded)
            {
                var upgradeTarget = instance.GetComponent<BuildingUpgradeTarget>();
                if (upgradeTarget != null) upgradeTarget.CompleteUpgrade();
            }

            var revivalState = instance.GetComponent<BuildingRevivalState>();
            if (revivalState == null) revivalState = instance.AddComponent<BuildingRevivalState>();
            revivalState.Configure(originCell, upgraded);
            SetObjectTile(originCell, definition.kind);
            return true;
        }

        void ConfigureBuilding(GameObject instance, FixedBuildingKind kind)
        {
            var ballista = instance.GetComponent<BallistaTower>();
            if (ballista != null)
            {
                ballista.config = config;
                ballista.grid = grid;
            }

            var barrier = instance.GetComponent<WoodenBarrier>();
            if (barrier != null)
            {
                instance.name = "WoodenWall";
                barrier.config = config;
                if (woodenWallSprite != null) barrier.barrierSprite = woodenWallSprite;
                barrier.RefreshConfiguredSprites();
            }

            var watchTower = instance.GetComponent<WatchTower>();
            if (watchTower != null)
            {
                watchTower.config = config;
                watchTower.grid = grid;
            }

            BuildingSkillEffects.ConfigureAutoRegeneration(instance, config, damagePopupPrefab);
        }

        void RegisterBuilding(GameObject instance, Vector3Int originCell)
        {
            var ballista = instance.GetComponent<BallistaTower>();
            if (ballista != null) ballista.RegisterBuildPlacement(grid, originCell);
            var barrier = instance.GetComponent<WoodenBarrier>();
            if (barrier != null) barrier.RegisterBuildPlacement(grid, originCell);
            var watchTower = instance.GetComponent<WatchTower>();
            if (watchTower != null) watchTower.RegisterBuildPlacement(grid, originCell);
        }

        void SetObjectTile(Vector3Int cell, FixedBuildingKind kind)
        {
            if (grid == null || grid.objectTilemap == null) return;
            switch (kind)
            {
                case FixedBuildingKind.Ballista:
                    grid.objectTilemap.SetTile(cell, ballistaTile);
                    break;
                case FixedBuildingKind.WatchTower:
                    grid.objectTilemap.SetTile(cell, watchTowerTile);
                    break;
                default:
                    grid.objectTilemap.SetTile(cell, woodenWallTile);
                    break;
            }
        }

        GameObject PrefabFor(FixedBuildingKind kind)
        {
            switch (kind)
            {
                case FixedBuildingKind.Ballista:
                    return ballistaPrefab;
                case FixedBuildingKind.WatchTower:
                    return watchTowerPrefab;
                default:
                    return woodenWallPrefab;
            }
        }

        bool TryFindSlotOrigin(
            Vector3Int towerOrigin,
            Vector2Int footprint,
            Vector2Int desiredOffset,
            bool requiresPlayerTerritory,
            out Vector3Int originCell)
        {
            footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            for (int radius = 0; radius <= 5; radius++)
            {
                foreach (var offset in EnumerateOffsets(desiredOffset, radius))
                {
                    originCell = towerOrigin + new Vector3Int(offset.x, offset.y, 0);
                    if (!grid.ContainsCell(originCell)) continue;
                    if (!grid.CanPlaceObject(originCell, footprint)) continue;
                    if (requiresPlayerTerritory && !HasPlayerTerritory(originCell, footprint)) continue;
                    return true;
                }
            }

            originCell = default;
            return false;
        }

        bool HasPlayerTerritory(Vector3Int originCell, Vector2Int footprint)
        {
            return grid != null && grid.IsFootprintOwnedBy(originCell, footprint, TileOwner.Player);
        }

        static IEnumerable<Vector2Int> EnumerateOffsets(Vector2Int desiredOffset, int radius)
        {
            if (radius == 0)
            {
                yield return desiredOffset;
                yield break;
            }

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) != radius) continue;
                    yield return desiredOffset + new Vector2Int(dx, dy);
                }
            }
        }

        static SlotDefinition[] BuildSlotDefinitions()
        {
            var slots = new List<SlotDefinition>();

            AddWallLine(slots, 6, 6, 11, 6);
            AddWallLine(slots, 6, 7, 6, 11);
            AddWallLine(slots, 15, 6, 20, 6);
            AddWallLine(slots, 20, 7, 20, 11);
            AddWallLine(slots, 6, 15, 6, 20);
            AddWallLine(slots, 7, 20, 11, 20);
            AddWallLine(slots, 20, 15, 20, 20);
            AddWallLine(slots, 15, 20, 19, 20);

            AddBallista(slots, 7, 8);
            AddBallista(slots, 18, 8);
            AddBallista(slots, 7, 19);
            AddBallista(slots, 18, 19);

            AddWatchTower(slots, 2, 3);
            AddWatchTower(slots, 23, 3);
            AddWatchTower(slots, 2, 24);
            AddWatchTower(slots, 23, 24);

            AddOuterWallLine(slots, 1, 1, 11, 1);
            AddOuterWallLine(slots, 15, 1, 25, 1);
            AddOuterWallLine(slots, 1, 2, 1, 11);
            AddOuterWallLine(slots, 25, 2, 25, 11);
            AddOuterWallLine(slots, 1, 15, 1, 24);
            AddOuterWallLine(slots, 25, 15, 25, 24);
            AddOuterWallLine(slots, 1, 25, 11, 25);
            AddOuterWallLine(slots, 15, 25, 25, 25);

            return slots.ToArray();
        }

        static void AddWallLine(List<SlotDefinition> slots, int startColumn, int startRow, int endColumn, int endRow)
        {
            AddLine(slots, startColumn, startRow, endColumn, endRow, false);
        }

        static void AddOuterWallLine(List<SlotDefinition> slots, int startColumn, int startRow, int endColumn, int endRow)
        {
            AddLine(slots, startColumn, startRow, endColumn, endRow, true);
        }

        static void AddLine(
            List<SlotDefinition> slots,
            int startColumn,
            int startRow,
            int endColumn,
            int endRow,
            bool outer)
        {
            int columnStep = Math.Sign(endColumn - startColumn);
            int rowStep = Math.Sign(endRow - startRow);
            int length = Mathf.Max(Mathf.Abs(endColumn - startColumn), Mathf.Abs(endRow - startRow));
            for (int i = 0; i <= length; i++)
            {
                slots.Add(CreateWallSlot(startColumn + columnStep * i, startRow + rowStep * i, outer));
            }
        }

        static void AddBallista(List<SlotDefinition> slots, int leftColumn, int lowerRow)
        {
            slots.Add(new SlotDefinition
            {
                kind = FixedBuildingKind.Ballista,
                unlockType = UpgradeType.UnlockBallista,
                upgradeType = UpgradeType.BallistaUpgrade,
                footprint = new Vector2Int(2, 2),
                desiredOffset = OffsetFromLayoutCell(leftColumn, lowerRow)
            });
        }

        static void AddWatchTower(List<SlotDefinition> slots, int leftColumn, int lowerRow)
        {
            slots.Add(new SlotDefinition
            {
                kind = FixedBuildingKind.WatchTower,
                unlockType = UpgradeType.UnlockWatchTower,
                upgradeType = UpgradeType.WatchTowerUpgrade,
                footprint = new Vector2Int(2, 2),
                desiredOffset = OffsetFromLayoutCell(leftColumn, lowerRow),
                requiresPlayerTerritory = false
            });
        }

        static SlotDefinition CreateWallSlot(int column, int row, bool outer)
        {
            return new SlotDefinition
            {
                kind = FixedBuildingKind.WoodenWall,
                unlockType = outer ? UpgradeType.UnlockWall2 : UpgradeType.UnlockWall,
                upgradeType = outer ? UpgradeType.Wall2Upgrade : UpgradeType.WallUpgrade,
                footprint = Vector2Int.one,
                desiredOffset = OffsetFromLayoutCell(column, row),
                requiresPlayerTerritory = !outer
            };
        }

        static Vector2Int OffsetFromLayoutCell(int column, int row)
        {
            return new Vector2Int(column - LayoutTowerCenterColumn, LayoutTowerCenterRow - row);
        }
    }
}
