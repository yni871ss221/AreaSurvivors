using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class BuildPlacementController : MonoBehaviour
    {
        public GameConfig config;
        public TileGrid grid;
        public PlayerController player;
        public GameObject ballistaPrefab;
        [FormerlySerializedAs("woodenWallPrefab")]
        public GameObject woodenWallPrefab;
        public GameObject woodenGatePrefab;
        public GameObject watchTowerPrefab;
        public Sprite ballistaPreviewSprite;
        [FormerlySerializedAs("woodenWallPreviewSprite")]
        public Sprite woodenWallPreviewSprite;
        public Sprite woodenGatePreviewSprite;
        public Sprite woodenGateOpenSprite;
        public Sprite watchTowerPreviewSprite;
        public TileBase ballistaTile;
        [FormerlySerializedAs("woodenWallTile")]
        public TileBase woodenWallTile;
        public TileBase woodenGateTile;
        public TileBase watchTowerTile;
        public Tilemap buildPreviewTilemap;
        public TileBase buildPreviewTile;
        public float maxPlaceDistance = 4.2f;
        public int ballistaStock = 4;
        [FormerlySerializedAs("wallStock")]
        public int wallStock = 4;
        public Text buildText;


        BuildMode buildMode = BuildMode.Ballista;
        BuildBlockReason buildBlockReason = BuildBlockReason.NoCell;
        float buildFeedbackTimer;
        GameObject buildPreviewRoot;
        GameObject buildPreviewInstance;
        GameObject buildPreviewPrefab;
        PaperMeshVisual[] buildPreviewVisuals;
        Vector3Int currentBuildCell;
        readonly List<Vector3Int> buildPreviewCells = new List<Vector3Int>();
        bool buildSelectionActive;
        bool hasBuildCell;
        bool canPlaceBuild;
        bool buildSceneMode;

        public int SelectedHudSlot { get; private set; } = -1;

        enum BuildMode
        {
            Ballista,
            WoodenWall,
            WoodenGate,
            WatchTower
        }

        enum BuildBlockReason
        {
            None,
            NoCell,
            NoStock,
            NoResources,
            TooFar,
            Blocked,
            OutOfMap,
            NotPlayerTerritory,
            MissingPrefab
        }

        public void Initialize(GameConfig runConfig, TileGrid runGrid, PlayerController runPlayer)
        {
            config = runConfig;
            grid = runGrid;
            player = runPlayer;
            buildSceneMode = runPlayer == null;
            EnsureWoodenBarrierResources();
            HideBuildPreview();
            UpdateBuildStatus();
        }

        void EnsureWoodenBarrierResources()
        {
            if (woodenWallPreviewSprite == null)
            {
                woodenWallPreviewSprite = GetPrefabBaseSprite(woodenWallPrefab);
            }
            if (woodenGatePreviewSprite == null) woodenGatePreviewSprite = GetPrefabBaseSprite(woodenGatePrefab);
            if (woodenGateOpenSprite == null)
            {
                var gate = woodenGatePrefab != null ? woodenGatePrefab.GetComponent<WoodenBarrier>() : null;
                woodenGateOpenSprite = gate != null ? gate.openGateSprite : null;
            }
        }

        static Sprite GetPrefabBaseSprite(GameObject prefab)
        {
            if (prefab == null) return null;
            var barrier = prefab.GetComponent<WoodenBarrier>();
            if (barrier != null && barrier.barrierSprite != null) return barrier.barrierSprite;
            var visualSet = prefab.GetComponent<BuildingPrefabVisualSet>();
            if (visualSet != null)
            {
                visualSet.BindMissingVisualsFromChildren();
                if (visualSet.completeVisual != null && visualSet.completeVisual.sprite != null) return visualSet.completeVisual.sprite;
            }
            return null;
        }

        public void CancelActiveSelection()
        {
            CancelBuildSelection();
        }

        public void Tick()
        {
            if (grid == null)
            {
                return;
            }

            if (Time.timeScale <= 0f)
            {
                if (buildPreviewRoot != null) buildPreviewRoot.SetActive(false);
                ClearBuildFootprintPreview();
                return;
            }

            if (buildSceneMode)
            {
                CancelBuildSelection();
                return;
            }

            HandleBuildModeInput();
            if (buildSelectionActive && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape)))
            {
                CancelBuildSelection();
                return;
            }

            UpdateBuildPreview();
            bool confirmBuild = Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.E);
            if (confirmBuild && !IsPointerOverUi())
            {
                if (canPlaceBuild)
                {
                    PlaceCurrentBuild();
                }
                else if (hasBuildCell)
                {
                    buildFeedbackTimer = 0.32f;
                    UpdateBuildStatus();
                }
            }

            if (buildFeedbackTimer > 0f)
            {
                buildFeedbackTimer = Mathf.Max(0f, buildFeedbackTimer - Time.deltaTime);
                UpdateBuildStatus();
            }
        }

        public void SelectBallista()
        {
            if (!IsSlotUnlocked(2))
            {
                buildSelectionActive = false;
                SelectedHudSlot = -1;
                UpdateBuildStatus();
                return;
            }
            buildMode = BuildMode.Ballista;
            buildSelectionActive = true;
            SelectedHudSlot = 2;
            UpdateBuildStatus();
        }

        public void SelectWoodenWall()
        {
            buildMode = BuildMode.WoodenWall;
            buildSelectionActive = true;
            SelectedHudSlot = 0;
            HideBuildPreview();
            UpdateBuildStatus();
        }

        public void SelectWoodenGate()
        {
            buildMode = BuildMode.WoodenGate;
            buildSelectionActive = true;
            SelectedHudSlot = 1;
            HideBuildPreview();
            UpdateBuildStatus();
        }

        public void SelectWatchTower()
        {
            if (!IsSlotUnlocked(3))
            {
                buildSelectionActive = false;
                SelectedHudSlot = -1;
                UpdateBuildStatus();
                return;
            }

            buildMode = BuildMode.WatchTower;
            buildSelectionActive = true;
            SelectedHudSlot = 3;
            UpdateBuildStatus();
        }

        public bool TryPlaceAtCell(Vector3Int cell)
        {
            if (grid == null || grid.groundTilemap == null) return false;
            if (buildSceneMode) return false;
            var footprint = CurrentBuildFootprint();
            var footprintOrigin = CurrentBuildFootprintOrigin(cell);
            var world = GridObjectVisual.FootprintBottomCenterToWorld(grid, footprintOrigin, footprint);
            BuildBlockReason reason;
            if (!CanPlaceCurrentBuild(footprintOrigin, world, out reason))
            {
                buildBlockReason = reason;
                UpdateBuildStatus();
                return false;
            }

            var prefab = CurrentBuildPrefab();
            if (prefab == null) return false;

            var instance = Instantiate(prefab, world, Quaternion.identity);
            ConfigurePlacedObject(instance);

            var marker = instance.GetComponent<GridObjectMarker>();
            if (marker != null && !grid.TryRegisterObject(footprintOrigin, marker.type, marker.flags, instance, marker.footprint))
            {
                Destroy(instance);
                return false;
            }

            if (!SpendBuildResources())
            {
                if (marker != null) grid.ClearObject(footprintOrigin);
                Destroy(instance);
                buildBlockReason = BuildBlockReason.NoResources;
                UpdateBuildStatus();
                return false;
            }
            RegisterPlacedObject(instance, footprintOrigin);

            if (buildSceneMode)
            {
                var construction = instance.GetComponent<IBuildableConstruction>();
                construction?.CompleteImmediately();
            }

            SetBuildObjectTile(footprintOrigin);
            SavePlacedBuilding(instance, footprintOrigin);
            buildBlockReason = BuildBlockReason.NoCell;
            UpdateBuildStatus();
            return true;
        }

        public void RestoreStageBuildings(int stage)
        {
            if (grid == null) return;
            var set = ProgressionStore.GetStageBuildings(stage);
            if (set.buildings == null) return;
            foreach (var saved in set.buildings)
            {
                RestoreSavedBuilding(saved);
            }
        }

        void RestoreSavedBuilding(SavedBuildingData saved)
        {
            if (saved == null) return;
            var prefab = PrefabForSavedKind(saved.kind);
            if (prefab == null) return;
            var originCell = new Vector3Int(saved.x, saved.y, 0);
            var footprint = FootprintForSavedKind(saved.kind);
            if (!grid.CanPlaceObject(originCell, footprint)) return;

            var world = GridObjectVisual.FootprintBottomCenterToWorld(grid, originCell, footprint);
            var instance = Instantiate(prefab, world, Quaternion.identity);
            ConfigurePlacedObject(instance, saved.kind);
            var marker = instance.GetComponent<GridObjectMarker>();
            if (marker != null && !grid.TryRegisterObject(originCell, marker.type, marker.flags, instance, marker.footprint))
            {
                Destroy(instance);
                return;
            }

            RegisterPlacedObject(instance, originCell);
            instance.GetComponent<IBuildableConstruction>()?.CompleteImmediately();
            ConfigurePersistentState(instance, saved);
            if (saved.upgraded)
            {
                var upgradeTarget = instance.GetComponent<BuildingUpgradeTarget>();
                if (upgradeTarget != null) upgradeTarget.CompleteUpgrade();
            }
            SetBuildObjectTile(originCell, saved.kind);
        }

        static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void HandleBuildModeInput()
        {
            if (buildSceneMode)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SelectWoodenWall();
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SelectWoodenGate();
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                if (IsSlotUnlocked(2)) SelectBallista();
            }
            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                if (IsSlotUnlocked(3)) SelectWatchTower();
            }
        }

        void UpdateBuildPreview()
        {
            if (!buildSelectionActive)
            {
                HideBuildPreview();
                UpdateBuildStatus();
                return;
            }

            CreateOrUpdateBuildPreview();
            if (buildPreviewRoot == null || buildPreviewInstance == null) return;

            if (IsPointerOverUi())
            {
                hasBuildCell = false;
                canPlaceBuild = false;
                buildBlockReason = BuildBlockReason.NoCell;
                HideBuildPreview();
                UpdateBuildStatus();
                return;
            }

            hasBuildCell = TryGetPointerCell(out currentBuildCell);
            canPlaceBuild = false;
            buildBlockReason = BuildBlockReason.NoCell;
            if (hasBuildCell)
            {
                var footprint = CurrentBuildFootprint();
                var footprintOrigin = CurrentBuildFootprintOrigin(currentBuildCell);
                var world = GridObjectVisual.FootprintBottomCenterToWorld(grid, footprintOrigin, footprint);
                buildPreviewRoot.transform.position = world + new Vector3(0f, 0f, -0.02f);
                canPlaceBuild = CanPlaceCurrentBuild(footprintOrigin, world, out buildBlockReason);
                DrawBuildFootprintPreview(footprintOrigin, footprint, canPlaceBuild);
            }
            else
            {
                ClearBuildFootprintPreview();
            }

            buildPreviewRoot.SetActive(hasBuildCell);
            ApplyBuildPreviewAppearance();
            UpdateBuildStatus();
        }

        void CancelBuildSelection()
        {
            buildSelectionActive = false;
            buildFeedbackTimer = 0f;
            buildBlockReason = BuildBlockReason.NoCell;
            HideBuildPreview();
            UpdateBuildStatus();
        }

        void HideBuildPreview()
        {
            hasBuildCell = false;
            canPlaceBuild = false;
            ClearBuildFootprintPreview();
            if (buildPreviewRoot != null) buildPreviewRoot.SetActive(false);
        }

        bool TryGetPointerCell(out Vector3Int cell)
        {
            cell = default(Vector3Int);
            if (grid == null || grid.groundTilemap == null || Camera.main == null) return false;
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.forward, Vector3.zero);
            if (!ground.Raycast(ray, out float distance)) return false;
            var world = ray.GetPoint(distance);
            cell = grid.WorldToCell(world);
            return grid.ContainsCell(cell);
        }

        bool IsNearPlayer(Vector3 world)
        {
            if (player == null) return true;
            return (player.transform.position - world).sqrMagnitude <= maxPlaceDistance * maxPlaceDistance;
        }

        void PlaceCurrentBuild()
        {
            if (grid == null || !hasBuildCell || !canPlaceBuild) return;
            TryPlaceAtCell(currentBuildCell);
        }

        bool CanPlaceCurrentBuild(Vector3Int cell, Vector3 world, out BuildBlockReason reason)
        {
            if (CurrentBuildPrefab() == null)
            {
                reason = BuildBlockReason.MissingPrefab;
                return false;
            }

            if (!HasBuildResources())
            {
                reason = BuildBlockReason.NoResources;
                return false;
            }

            if (grid == null || !grid.ContainsCell(cell))
            {
                reason = BuildBlockReason.OutOfMap;
                return false;
            }

            if (!buildSceneMode && !IsNearPlayer(world))
            {
                reason = BuildBlockReason.TooFar;
                return false;
            }

            if (buildSceneMode && !IsInsideCenterChunk(cell, CurrentBuildFootprint()))
            {
                reason = BuildBlockReason.TooFar;
                return false;
            }

            if (!grid.CanPlaceObject(cell, CurrentBuildFootprint()))
            {
                reason = BuildBlockReason.Blocked;
                return false;
            }

            if (!IsPlayerOwnedFootprint(cell, CurrentBuildFootprint()))
            {
                reason = BuildBlockReason.NotPlayerTerritory;
                return false;
            }

            reason = BuildBlockReason.None;
            return true;
        }

        bool IsPlayerOwnedFootprint(Vector3Int originCell, Vector2Int footprint)
        {
            return grid != null && grid.IsFootprintOwnedBy(originCell, footprint, TileOwner.Player);
        }

        bool HasBuildResources()
        {
            var cost = CurrentBuildCost();
            if (buildSceneMode) return ProgressionStore.HasPersistentResources(cost.x, cost.y);
            return GameManager.Instance == null || GameManager.Instance.HasResources(cost.x, cost.y);
        }

        bool SpendBuildResources()
        {
            var cost = CurrentBuildCost();
            if (buildSceneMode)
            {
                bool spent = ProgressionStore.TrySpendPersistentResources(cost.x, cost.y);
                if (spent) GameManager.Instance?.SyncPersistentResources();
                return spent;
            }

            return GameManager.Instance == null || GameManager.Instance.TrySpendResources(cost.x, cost.y);
        }

        bool IsInsideCenterChunk(Vector3Int originCell, Vector2Int footprint)
        {
            if (grid == null || grid.groundChunkCells <= 0) return false;
            int chunkCells = Mathf.Max(1, grid.groundChunkCells);
            int centerMinX = grid.width / 2 - chunkCells / 2;
            int centerMinY = grid.height / 2 - chunkCells / 2;
            int centerMaxX = centerMinX + chunkCells;
            int centerMaxY = centerMinY + chunkCells;
            foreach (var cell in BuildFootprintCells(originCell, footprint))
            {
                if (!grid.TryCellToGrid(cell, out var x, out var y)) return false;
                if (x < centerMinX || x >= centerMaxX || y < centerMinY || y >= centerMaxY) return false;
            }

            return true;
        }

        void RegisterPlacedObject(GameObject instance, Vector3Int footprintOrigin)
        {
            var ballista = instance.GetComponent<BallistaTower>();
            if (ballista != null) ballista.RegisterBuildPlacement(grid, footprintOrigin);
            var barrier = instance.GetComponent<WoodenBarrier>();
            if (barrier != null) barrier.RegisterBuildPlacement(grid, footprintOrigin);
            var watchTower = instance.GetComponent<WatchTower>();
            if (watchTower != null) watchTower.RegisterBuildPlacement(grid, footprintOrigin);
        }

        void ConfigurePlacedObject(GameObject instance)
        {
            ConfigurePlacedObject(instance, CurrentSavedBuildingKind());
        }

        void ConfigurePlacedObject(GameObject instance, SavedBuildingKind kind)
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
                instance.name = kind == SavedBuildingKind.WoodenGate ? "WoodenGate" : "WoodenWall";
                barrier.config = config;
                barrier.gate = kind == SavedBuildingKind.WoodenGate;
                if (barrier.gate)
                {
                    if (woodenGatePreviewSprite != null) barrier.barrierSprite = woodenGatePreviewSprite;
                    if (woodenGateOpenSprite != null) barrier.openGateSprite = woodenGateOpenSprite;
                }
                else if (woodenWallPreviewSprite != null)
                {
                    barrier.barrierSprite = woodenWallPreviewSprite;
                    barrier.openGateSprite = null;
                }
                barrier.RefreshConfiguredSprites();
            }

            var watchTower = instance.GetComponent<WatchTower>();
            if (watchTower != null)
            {
                watchTower.config = config;
                watchTower.grid = grid;
            }
        }

        void SavePlacedBuilding(GameObject instance, Vector3Int originCell)
        {
            if (!buildSceneMode || GameManager.Instance == null) return;
            var set = ProgressionStore.GetStageBuildings(GameManager.Instance.CurrentStage);
            var saved = new SavedBuildingData
            {
                kind = CurrentSavedBuildingKind(),
                x = originCell.x,
                y = originCell.y,
                upgraded = false,
                destroyed = false
            };
            set.buildings.Add(saved);
            ConfigurePersistentState(instance, saved);
            ProgressionStore.Save();
        }

        static void ConfigurePersistentState(GameObject instance, SavedBuildingData saved)
        {
            if (instance == null || saved == null) return;
            var state = instance.GetComponent<BuildingPersistentState>();
            if (state == null) state = instance.AddComponent<BuildingPersistentState>();
            state.Configure(saved);
        }

        SavedBuildingKind CurrentSavedBuildingKind()
        {
            switch (buildMode)
            {
                case BuildMode.WoodenWall: return SavedBuildingKind.WoodenWall;
                case BuildMode.WoodenGate: return SavedBuildingKind.WoodenGate;
                case BuildMode.WatchTower: return SavedBuildingKind.WatchTower;
                default: return SavedBuildingKind.Ballista;
            }
        }

        GameObject PrefabForSavedKind(SavedBuildingKind kind)
        {
            switch (kind)
            {
                case SavedBuildingKind.WoodenWall: return woodenWallPrefab;
                case SavedBuildingKind.WoodenGate: return woodenGatePrefab != null ? woodenGatePrefab : woodenWallPrefab;
                case SavedBuildingKind.WatchTower: return watchTowerPrefab;
                default: return ballistaPrefab;
            }
        }

        Vector2Int FootprintForSavedKind(SavedBuildingKind kind)
        {
            var prefab = PrefabForSavedKind(kind);
            var marker = prefab != null ? prefab.GetComponent<GridObjectMarker>() : null;
            if (marker != null) return marker.footprint;
            if (kind == SavedBuildingKind.WatchTower) return new Vector2Int(2, 2);
            if (kind == SavedBuildingKind.WoodenWall) return Vector2Int.one;
            if (kind == SavedBuildingKind.WoodenGate) return new Vector2Int(3, 1);
            return Vector2Int.one;
        }

        void SetBuildObjectTile(Vector3Int cell)
        {
            if (grid == null || grid.objectTilemap == null) return;
            if (buildMode == BuildMode.Ballista)
            {
                grid.objectTilemap.SetTile(cell, ballistaTile);
                return;
            }
            if (buildMode == BuildMode.WatchTower)
            {
                grid.objectTilemap.SetTile(cell, watchTowerTile);
                return;
            }

            grid.objectTilemap.SetTile(cell, buildMode == BuildMode.WoodenGate ? (woodenGateTile != null ? woodenGateTile : woodenWallTile) : woodenWallTile);
        }

        void SetBuildObjectTile(Vector3Int cell, SavedBuildingKind kind)
        {
            if (grid == null || grid.objectTilemap == null) return;
            switch (kind)
            {
                case SavedBuildingKind.Ballista:
                    grid.objectTilemap.SetTile(cell, ballistaTile);
                    return;
                case SavedBuildingKind.WatchTower:
                    grid.objectTilemap.SetTile(cell, watchTowerTile);
                    return;
                case SavedBuildingKind.WoodenGate:
                    grid.objectTilemap.SetTile(cell, woodenGateTile != null ? woodenGateTile : woodenWallTile);
                    return;
                default:
                    grid.objectTilemap.SetTile(cell, woodenWallTile);
                    return;
            }
        }

        void DrawBuildFootprintPreview(Vector3Int originCell, Vector2Int footprint, bool valid)
        {
            ClearBuildFootprintPreview();
            if (buildPreviewTilemap == null || buildPreviewTile == null) return;

            var color = valid ? new Color(0.35f, 1f, 0.45f, 0.42f) : BuildFootprintBlockedColor(buildBlockReason);
            foreach (var cell in BuildFootprintCells(originCell, footprint))
            {
                buildPreviewTilemap.SetTile(cell, buildPreviewTile);
                buildPreviewTilemap.SetTileFlags(cell, TileFlags.None);
                buildPreviewTilemap.SetColor(cell, color);
                buildPreviewCells.Add(cell);
            }
        }

        void ClearBuildFootprintPreview()
        {
            if (buildPreviewTilemap == null || buildPreviewCells.Count == 0) return;
            foreach (var cell in buildPreviewCells)
            {
                buildPreviewTilemap.SetTile(cell, null);
            }

            buildPreviewCells.Clear();
        }

        GameObject CurrentBuildPrefab()
        {
            if (buildMode == BuildMode.Ballista) return ballistaPrefab;
            if (buildMode == BuildMode.WatchTower) return watchTowerPrefab;
            if (buildMode == BuildMode.WoodenGate) return woodenGatePrefab != null ? woodenGatePrefab : woodenWallPrefab;
            return woodenWallPrefab;
        }

        Vector2Int CurrentBuildFootprint()
        {
            if (buildMode == BuildMode.Ballista) return new Vector2Int(2, 2);
            if (buildMode == BuildMode.WatchTower) return new Vector2Int(2, 2);
            if (buildMode == BuildMode.WoodenWall) return Vector2Int.one;
            return new Vector2Int(3, 1);
        }

        Vector2Int CurrentBuildCost()
        {
            if (config == null)
            {
                if (buildMode == BuildMode.Ballista) return new Vector2Int(50, 30);
                if (buildMode == BuildMode.WatchTower) return new Vector2Int(50, 50);
                if (buildMode == BuildMode.WoodenGate) return new Vector2Int(20, 0);
                return new Vector2Int(10, 0);
            }
            if (buildMode == BuildMode.Ballista)
            {
                return new Vector2Int(Mathf.Max(0, config.ballistaWoodCost), Mathf.Max(0, config.ballistaStoneCost));
            }
            if (buildMode == BuildMode.WatchTower)
            {
                return new Vector2Int(Mathf.Max(0, config.watchTowerWoodCost), Mathf.Max(0, config.watchTowerStoneCost));
            }
            if (buildMode == BuildMode.WoodenGate)
            {
                return new Vector2Int(Mathf.Max(0, config.woodenGateWoodCost), Mathf.Max(0, config.woodenGateStoneCost));
            }

            return new Vector2Int(Mathf.Max(0, config.woodenWallWoodCost), Mathf.Max(0, config.woodenWallStoneCost));
        }

        public string GetHudCostLabel(int slot)
        {
            if (!IsSlotUnlocked(slot)) return "\u30ed\u30c3\u30af";
            int wood = 0;
            int stone = 0;
            if (config != null)
            {
                if (slot == 0)
                {
                    wood = config.woodenWallWoodCost;
                    stone = config.woodenWallStoneCost;
                }
                else if (slot == 1)
                {
                    wood = config.woodenGateWoodCost;
                    stone = config.woodenGateStoneCost;
                }
                else if (slot == 2)
                {
                    wood = config.ballistaWoodCost;
                    stone = config.ballistaStoneCost;
                }
                else if (slot == 3)
                {
                    wood = config.watchTowerWoodCost;
                    stone = config.watchTowerStoneCost;
                }
            }
            else
            {
                if (slot == 0)
                {
                    wood = 10;
                    stone = 0;
                }
                else if (slot == 1)
                {
                    wood = 20;
                    stone = 0;
                }
                else if (slot == 2)
                {
                    wood = 50;
                    stone = 30;
                }
                else if (slot == 3)
                {
                    wood = 50;
                    stone = 50;
                }
            }

            return BuildCostLabel(Mathf.Max(0, wood), Mathf.Max(0, stone));
        }

        public bool IsSlotUnlocked(int slot)
        {
            if (slot == 4 || slot == 5) return false;
            if (slot == 0 || slot == 1) return ProgressionStore.IsUnlocked(UpgradeType.UnlockWall);
            if (slot == 2) return ProgressionStore.IsUnlocked(UpgradeType.UnlockBallista);
            if (slot == 3) return ProgressionStore.IsUnlocked(UpgradeType.UnlockWatchTower);
            return true;
        }

        static string BuildCostLabel(int wood, int stone)
        {
            if (wood > 0 && stone > 0) return "\u6728" + wood + " \u77f3" + stone;
            if (wood > 0) return "\u6728" + wood;
            if (stone > 0) return "\u77f3" + stone;
            return "0";
        }

        Vector3Int CurrentBuildFootprintOrigin(Vector3Int pointerCell)
        {
            if (buildMode == BuildMode.Ballista) return pointerCell + Vector3Int.down;
            if (buildMode == BuildMode.WatchTower) return pointerCell + Vector3Int.down;
            return pointerCell;
        }

        static IEnumerable<Vector3Int> BuildFootprintCells(Vector3Int originCell, Vector2Int footprint)
        {
            footprint = new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
            int minX = originCell.x - (footprint.x - 1) / 2;
            int minY = originCell.y - (footprint.y - 1) / 2;
            for (int y = 0; y < footprint.y; y++)
            {
                for (int x = 0; x < footprint.x; x++)
                {
                    yield return new Vector3Int(minX + x, minY + y, originCell.z);
                }
            }
        }

        void ApplyBuildPreviewAppearance()
        {
            var color = canPlaceBuild ? new Color(0.45f, 1f, 0.58f, 0.58f) : BuildPreviewBlockedColor(buildBlockReason);
            if (buildPreviewVisuals == null) return;
            foreach (var visual in buildPreviewVisuals)
            {
                if (visual != null) visual.color = color;
            }
        }

        static Color BuildPreviewBlockedColor(BuildBlockReason reason)
        {
            if (reason == BuildBlockReason.NoStock || reason == BuildBlockReason.NoResources) return new Color(0.75f, 0.75f, 0.75f, 0.42f);
            if (reason == BuildBlockReason.TooFar) return new Color(1f, 0.78f, 0.22f, 0.5f);
            if (reason == BuildBlockReason.NotPlayerTerritory) return new Color(1f, 0.45f, 0.12f, 0.5f);
            return new Color(1f, 0.24f, 0.22f, 0.48f);
        }

        static Color BuildFootprintBlockedColor(BuildBlockReason reason)
        {
            if (reason == BuildBlockReason.NoStock || reason == BuildBlockReason.NoResources) return new Color(0.62f, 0.62f, 0.62f, 0.36f);
            if (reason == BuildBlockReason.TooFar) return new Color(1f, 0.72f, 0.16f, 0.42f);
            if (reason == BuildBlockReason.NotPlayerTerritory) return new Color(1f, 0.48f, 0.12f, 0.42f);
            return new Color(1f, 0.18f, 0.12f, 0.42f);
        }

        void CreateOrUpdateBuildPreview()
        {
            var prefab = CurrentBuildPrefab();
            if (prefab == null) return;
            if (buildPreviewRoot != null && buildPreviewPrefab == prefab) return;

            if (buildPreviewRoot != null) Destroy(buildPreviewRoot);
            buildPreviewPrefab = prefab;
            buildPreviewRoot = new GameObject("Build Preview");
            buildPreviewRoot.transform.SetParent(transform, false);
            buildPreviewInstance = Instantiate(prefab, buildPreviewRoot.transform);
            buildPreviewInstance.name = prefab.name + " Preview";
            buildPreviewInstance.transform.localPosition = Vector3.zero;
            buildPreviewInstance.transform.localRotation = prefab.transform.localRotation;
            buildPreviewInstance.transform.localScale = prefab.transform.localScale;
            ConfigurePlacedObject(buildPreviewInstance);
            PreparePreviewInstance(buildPreviewInstance);
            buildPreviewRoot.SetActive(false);
        }

        void PreparePreviewInstance(GameObject instance)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider2D>(true))
            {
                collider.enabled = false;
            }

            foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>(true))
            {
                body.simulated = false;
            }

            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                if (behaviour is PaperMeshVisual || behaviour is PaperBillboard) continue;
                behaviour.enabled = false;
            }

            foreach (var canvas in instance.GetComponentsInChildren<Canvas>(true))
            {
                canvas.gameObject.SetActive(false);
            }

            DestroyPreviewPart(instance.transform, "Build Fill");
            DestroyPreviewPart(instance.transform, "Ghost");
            SetPreviewPartActive(instance.transform, "Build Fill Image", false);
            SetPreviewPartActive(instance.transform, "Hammer", false);
            SetPreviewPartActive(instance.transform, "Completion Sparkle", false);
            SetPreviewPartActive(instance.transform, "Ghost Image", false);
            SetPreviewPartActive(instance.transform, "Complete Image", true);

            var previewVisuals = new List<PaperMeshVisual>();
            var allVisuals = instance.GetComponentsInChildren<PaperMeshVisual>(true);
            foreach (var visual in allVisuals)
            {
                if (visual == null) continue;
                bool isPreviewImage = HasAncestorNamed(visual.transform, "Complete Image");
                visual.order = 3200;
                visual.visible = isPreviewImage;
                if (isPreviewImage)
                {
                    visual.color = new Color(0.30f, 0.82f, 1f, 0.42f);
                    previewVisuals.Add(visual);
                }
            }

            buildPreviewVisuals = previewVisuals.ToArray();

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = HasAncestorNamed(renderer.transform, "Complete Image");
            }
        }

        void DestroyPreviewPart(Transform root, string name)
        {
            var targets = new List<GameObject>();
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == name) targets.Add(child.gameObject);
            }

            foreach (var target in targets)
            {
                target.SetActive(false);
                Destroy(target);
            }
        }

        static bool HasAncestorNamed(Transform transform, string name)
        {
            for (var current = transform; current != null; current = current.parent)
            {
                if (current.name == name) return true;
            }

            return false;
        }

        static void SetPreviewPartActive(Transform root, string name, bool active)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) child.gameObject.SetActive(active);
            }
        }

        void UpdateBuildStatus()
        {
            if (buildText == null) return;
            var label = buildMode == BuildMode.Ballista ? "3 \u30d0\u30ea\u30b9\u30bf" :
                buildMode == BuildMode.WatchTower ? "4 \u76e3\u8996\u5854" :
                buildMode == BuildMode.WoodenGate ? "2 \u6728\u306e\u57ce\u9580" : "1 \u6728\u306e\u57ce\u58c1";
            var cost = CurrentBuildCost();
            var status = !buildSelectionActive ? "選択待ち" : hasBuildCell || buildBlockReason != BuildBlockReason.NoCell ? BuildStatusLabel(buildBlockReason) : "E/Click";
            buildText.text = $"{label}\n{BuildCostLabel(cost.x, cost.y)}\n{status}";
            buildText.color = CurrentBuildStatusColor();
        }

        static string BuildStatusLabel(BuildBlockReason reason)
        {
            if (reason == BuildBlockReason.None) return "\u914d\u7f6e\u53ef";
            if (reason == BuildBlockReason.NoStock) return "\u5728\u5eab\u306a\u3057";
            if (reason == BuildBlockReason.NoResources) return "\u8cc7\u6e90\u4e0d\u8db3";
            if (reason == BuildBlockReason.TooFar) return "\u9060\u3059\u304e\u308b";
            if (reason == BuildBlockReason.Blocked) return "\u914d\u7f6e\u4e0d\u53ef";
            if (reason == BuildBlockReason.OutOfMap) return "\u7bc4\u56f2\u5916";
            if (reason == BuildBlockReason.NotPlayerTerritory) return "\u9752\u5e8a\u306e\u307f";
            if (reason == BuildBlockReason.MissingPrefab) return "\u672a\u8a2d\u5b9a";
            return "E/Click";
        }

        static Color BuildStatusColor(BuildBlockReason reason)
        {
            if (reason == BuildBlockReason.None) return new Color(0.66f, 1f, 0.72f, 1f);
            if (reason == BuildBlockReason.NoCell) return Color.white;
            if (reason == BuildBlockReason.NoStock || reason == BuildBlockReason.NoResources) return new Color(0.74f, 0.74f, 0.74f, 1f);
            if (reason == BuildBlockReason.TooFar) return new Color(1f, 0.82f, 0.32f, 1f);
            return new Color(1f, 0.46f, 0.42f, 1f);
        }

        Color CurrentBuildStatusColor()
        {
            var color = BuildStatusColor(buildBlockReason);
            if (buildFeedbackTimer > 0f && buildBlockReason != BuildBlockReason.None)
            {
                float pulse = 0.5f + Mathf.PingPong(buildFeedbackTimer * 14f, 0.5f);
                color = Color.Lerp(color, Color.white, pulse);
            }

            return color;
        }
    }
}
