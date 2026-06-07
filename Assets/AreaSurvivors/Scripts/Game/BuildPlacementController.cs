using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
        public GameObject horizontalFencePrefab;
        public GameObject verticalFencePrefab;
        public Sprite ballistaPreviewSprite;
        public Sprite horizontalFencePreviewSprite;
        public Sprite verticalFencePreviewSprite;
        public TileBase ballistaTile;
        public TileBase horizontalFenceTile;
        public TileBase verticalFenceTile;
        public Tilemap buildPreviewTilemap;
        public TileBase buildPreviewTile;
        public float maxPlaceDistance = 4.2f;
        public int ballistaStock = 4;
        public int fenceStock = 4;
        public Text buildText;

        const float VerticalFenceWorldYOffset = 0f;

        BuildMode buildMode = BuildMode.Ballista;
        bool fenceVertical;
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

        public int SelectedHudSlot { get; private set; } = -1;

        enum BuildMode
        {
            Ballista,
            Fence
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
            HideBuildPreview();
            UpdateBuildStatus();
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
            buildMode = BuildMode.Ballista;
            buildSelectionActive = true;
            SelectedHudSlot = 0;
            UpdateBuildStatus();
        }

        public void SelectFence(bool vertical)
        {
            buildMode = BuildMode.Fence;
            fenceVertical = vertical;
            buildSelectionActive = true;
            SelectedHudSlot = vertical ? 2 : 1;
            UpdateBuildStatus();
        }

        public bool TryPlaceAtCell(Vector3Int cell)
        {
            if (grid == null || grid.groundTilemap == null) return false;
            var footprint = CurrentBuildFootprint();
            var footprintOrigin = CurrentBuildFootprintOrigin(cell);
            var world = grid.FootprintCenterToWorld(footprintOrigin, footprint) + CurrentBuildWorldOffset();
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

            var ballista = instance.GetComponent<BallistaTower>();
            if (ballista != null) ballista.RegisterBuildPlacement(grid, footprintOrigin);
            var fence = instance.GetComponent<DefensiveFence>();
            if (fence != null) fence.RegisterBuildPlacement(grid, footprintOrigin);

            SetBuildObjectTile(footprintOrigin);
            buildBlockReason = BuildBlockReason.NoCell;
            UpdateBuildStatus();
            return true;
        }

        static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        void HandleBuildModeInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SelectBallista();
            }
            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SelectFence(false);
            }
            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SelectFence(true);
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                SelectFence(!fenceVertical);
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
                var world = grid.FootprintCenterToWorld(footprintOrigin, footprint) + CurrentBuildWorldOffset();
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

            if (!IsNearPlayer(world))
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
            if (grid == null) return false;
            foreach (var cell in BuildFootprintCells(originCell, footprint))
            {
                if (grid.GetOwner(cell) != TileOwner.Player) return false;
            }

            return true;
        }

        bool HasBuildResources()
        {
            var cost = CurrentBuildCost();
            return GameManager.Instance == null || GameManager.Instance.HasResources(cost.x, cost.y);
        }

        bool SpendBuildResources()
        {
            var cost = CurrentBuildCost();
            return GameManager.Instance == null || GameManager.Instance.TrySpendResources(cost.x, cost.y);
        }

        void ConfigurePlacedObject(GameObject instance)
        {
            var ballista = instance.GetComponent<BallistaTower>();
            if (ballista != null)
            {
                ballista.config = config;
                ballista.grid = grid;
            }

            var fence = instance.GetComponent<DefensiveFence>();
            if (fence != null) fence.config = config;
        }

        void SetBuildObjectTile(Vector3Int cell)
        {
            if (grid == null || grid.objectTilemap == null) return;
            if (buildMode == BuildMode.Ballista)
            {
                grid.objectTilemap.SetTile(cell, ballistaTile);
                return;
            }

            grid.objectTilemap.SetTile(cell, fenceVertical ? verticalFenceTile : horizontalFenceTile);
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
            return fenceVertical ? verticalFencePrefab : horizontalFencePrefab;
        }

        Vector2Int CurrentBuildFootprint()
        {
            if (buildMode == BuildMode.Ballista) return new Vector2Int(2, 2);
            return fenceVertical ? new Vector2Int(1, 2) : new Vector2Int(2, 1);
        }

        Vector2Int CurrentBuildCost()
        {
            if (config == null) return buildMode == BuildMode.Ballista ? new Vector2Int(50, 30) : new Vector2Int(10, 0);
            if (buildMode == BuildMode.Ballista)
            {
                return new Vector2Int(Mathf.Max(0, config.ballistaWoodCost), Mathf.Max(0, config.ballistaStoneCost));
            }

            return new Vector2Int(Mathf.Max(0, config.fenceWoodCost), Mathf.Max(0, config.fenceStoneCost));
        }

        public string GetHudCostLabel(int slot)
        {
            bool ballista = slot == 0;
            int wood = 0;
            int stone = 0;
            if (config != null)
            {
                wood = ballista ? config.ballistaWoodCost : config.fenceWoodCost;
                stone = ballista ? config.ballistaStoneCost : config.fenceStoneCost;
            }
            else
            {
                wood = ballista ? 50 : 10;
                stone = ballista ? 30 : 0;
            }

            return BuildCostLabel(Mathf.Max(0, wood), Mathf.Max(0, stone));
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
            return buildMode == BuildMode.Fence && fenceVertical ? pointerCell + Vector3Int.down : pointerCell;
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

        Vector3 CurrentBuildWorldOffset()
        {
            if (buildMode == BuildMode.Ballista) return CellStep(Vector3Int.up);
            return buildMode == BuildMode.Fence && fenceVertical ? new Vector3(0f, VerticalFenceWorldYOffset, 0f) : Vector3.zero;
        }

        Vector3 CellStep(Vector3Int direction)
        {
            if (grid == null || grid.groundTilemap == null) return new Vector3(direction.x * 0.7f, direction.y * 0.5f, 0f);
            return grid.groundTilemap.GetCellCenterWorld(direction) - grid.groundTilemap.GetCellCenterWorld(Vector3Int.zero);
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
            buildPreviewInstance.transform.localRotation = Quaternion.identity;
            buildPreviewInstance.transform.localScale = prefab.transform.localScale;
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
                if (behaviour is PaperMeshVisual || behaviour is PaperBillboard) continue;
                behaviour.enabled = false;
            }

            foreach (var canvas in instance.GetComponentsInChildren<Canvas>(true))
            {
                canvas.gameObject.SetActive(false);
            }

            DestroyPreviewPart(instance.transform, "Build Fill");
            DestroyPreviewPart(instance.transform, "Complete");
            DestroyPreviewPart(instance.transform, "Ghost");
            SetPreviewPartActive(instance.transform, "Build Fill Image", false);
            SetPreviewPartActive(instance.transform, "Complete Image", false);
            SetPreviewPartActive(instance.transform, "Hammer", false);
            SetPreviewPartActive(instance.transform, "Completion Sparkle", false);
            SetPreviewPartActive(instance.transform, "Ghost Image", true);

            var previewVisuals = new List<PaperMeshVisual>();
            var allVisuals = instance.GetComponentsInChildren<PaperMeshVisual>(true);
            foreach (var visual in allVisuals)
            {
                if (visual == null) continue;
                bool isGhostImage = HasAncestorNamed(visual.transform, "Ghost Image");
                visual.order = 3200;
                visual.visible = isGhostImage;
                if (isGhostImage) previewVisuals.Add(visual);
            }

            buildPreviewVisuals = previewVisuals.ToArray();

            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = HasAncestorNamed(renderer.transform, "Ghost Image");
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
            var label = buildMode == BuildMode.Ballista ? "1 \u30d0\u30ea\u30b9\u30bf" : fenceVertical ? "3 \u7e26\u67f5" : "2 \u6a2a\u67f5";
            var cost = CurrentBuildCost();
            var status = !buildSelectionActive ? "選択待ち" : hasBuildCell || buildBlockReason != BuildBlockReason.NoCell ? BuildStatusLabel(buildBlockReason) : "E/Click";
            buildText.text = $"{label} {BuildCostLabel(cost.x, cost.y)}  {status}";
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
