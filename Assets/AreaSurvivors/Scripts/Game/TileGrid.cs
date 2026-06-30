using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AreaSurvivors
{
    public enum TileOwner
    {
        Neutral,
        Player,
        Enemy
    }

    public struct TileControlSummary
    {
        public int playerCells;
        public int enemyCells;
        public int neutralCells;
        public int totalCells;

        public float playerRatio => totalCells <= 0 ? 0f : (float)playerCells / totalCells;
        public float enemyRatio => totalCells <= 0 ? 0f : (float)enemyCells / totalCells;
        public float neutralRatio => totalCells <= 0 ? 0f : (float)neutralCells / totalCells;
    }

    public enum GridObjectType
    {
        Empty = 0,
        Tower = 1,
        Ballista = 2,
        WoodenWall = 4,
        Tree = 5,
        Rock = 6,
        Pond = 7,
        WatchTower = 9
    }

    [System.Flags]
    public enum GridCellFlags
    {
        None = 0,
        BlocksMovement = 1 << 0,
        BlocksBuilding = 1 << 1,
        BlocksProjectiles = 1 << 2,
        Defensive = 1 << 3,
        Natural = 1 << 4
    }

    public sealed class GridObjectRecord
    {
        public GridObjectType type;
        public GridCellFlags flags;
        public GameObject instance;
        public Vector3Int originCell;
        public Vector2Int footprint;
    }

    public sealed class TileGrid : MonoBehaviour
    {
        public const int DefaultChunkCells = 25;
        public const int DefaultMapChunkColumns = 3;
        public const int DefaultMapChunkRows = 3;
        public const float DefaultCellSize = 0.7f;

        public int width = 96;
        public int height = 136;
        public float cellSize = DefaultCellSize;
        public Sprite tileSprite;
        public Sprite paintSprite;
        public Tilemap groundTilemap;
        public Tilemap paintTilemap;
        public Tilemap objectTilemap;
        public TileBase groundTile;
        public TileBase paintTile;
        [Header("Ground Details")]
        public bool useGroundChunkBackground = true;
        public string groundChunkResourcePath = "Generated/MapChunks/GrassChunk";
        public int groundChunkCells = 25;
        public int groundChunkSortingOrder = -25;
        public bool useGroundVariants = true;
        [Range(0f, 1f)]
        public float grassDetailChance = 0.08f;
        [Range(0f, 1f)]
        public float dirtDetailChance = 0.025f;
        [Range(0f, 1f)]
        public float pathDetailChance = 0.18f;
        public int groundVariantSeed = 173;
        public Color neutral = new Color(0.43f, 0.58f, 0.31f);
        public Color player = new Color(0.24f, 0.55f, 0.95f, 0.52f);
        public Color enemy = new Color(0.85f, 0.25f, 0.22f, 0.50f);
        public float paintTransitionSeconds = 2f;
        [Range(0.5f, 1f)]
        public float controlledThreshold = 0.95f;
        public bool showGridLines = true;
        public Color gridLineColor = new Color(0.08f, 0.12f, 0.08f, 0.16f);
        public float gridLineThickness = 0.012f;
        public int gridLineSortingOrder = 930;

        TileOwner[,] owners;
        float[,] controlValues;
        float[,] targetControlValues;
        bool[,] paintDirty;
        int playerControlledCellCount;
        int enemyControlledCellCount;
        GridObjectRecord[,] objects;
        GameObject gridLineOverlay;
        Mesh gridLineMesh;
        Material gridLineMaterial;
        GameObject groundChunkRoot;
        readonly List<Mesh> groundChunkMeshes = new List<Mesh>();
        Material groundChunkMaterial;
        TileBase[] grassDetailTiles;
        TileBase[] dirtDetailTiles;
        TileBase[] pathDetailTiles;
        readonly List<TileBase> generatedGroundTiles = new List<TileBase>();

        public void ApplySquareChunkMapLayout(int columns = DefaultMapChunkColumns, int rows = DefaultMapChunkRows)
        {
            int chunkCells = Mathf.Max(1, groundChunkCells > 0 ? groundChunkCells : DefaultChunkCells);
            groundChunkCells = chunkCells;
            width = chunkCells * Mathf.Max(1, columns);
            height = chunkCells * Mathf.Max(1, rows);
        }

        void Awake()
        {
            if (showGridLines) CreateGridLineOverlay();
        }

        void Update()
        {
            UpdatePaintTransitions();
        }

        public void Build()
        {
            if (!useGroundChunkBackground) EnsureGroundVariantTiles();
            owners = new TileOwner[width, height];
            controlValues = new float[width, height];
            targetControlValues = new float[width, height];
            paintDirty = new bool[width, height];
            playerControlledCellCount = 0;
            enemyControlledCellCount = 0;
            objects = new GridObjectRecord[width, height];
            groundTilemap.ClearAllTiles();
            paintTilemap.ClearAllTiles();
            if (objectTilemap != null) objectTilemap.ClearAllTiles();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = GridToCell(x, y);
                    groundTilemap.SetTile(cell, ChooseGroundTile(x, y));
                    groundTilemap.SetTileFlags(cell, TileFlags.None);
                    groundTilemap.SetColor(cell, Color.white);
                }
            }

            CreateGroundChunkBackground();
            CreateGridLineOverlay();
        }

        TileBase ChooseGroundTile(int x, int y)
        {
            if (useGroundChunkBackground) return groundTile;
            if (!useGroundVariants) return groundTile;

            float pathStrength = DecorativePathStrength(x, y);
            if (pathStrength > 0f && HasTiles(pathDetailTiles))
            {
                float chance = Mathf.Lerp(dirtDetailChance, pathDetailChance, pathStrength);
                if (Hash01(x, y, 17) < chance) return PickTile(pathDetailTiles, x, y, 29);
            }

            if (HasTiles(dirtDetailTiles) && Hash01(x, y, 41) < dirtDetailChance)
            {
                return PickTile(dirtDetailTiles, x, y, 43);
            }

            if (HasTiles(grassDetailTiles) && Hash01(x, y, 53) < grassDetailChance)
            {
                return PickTile(grassDetailTiles, x, y, 59);
            }

            return groundTile;
        }

        void CreateGroundChunkBackground()
        {
            DestroyGroundChunkBackground();

            var groundRenderer = groundTilemap != null ? groundTilemap.GetComponent<TilemapRenderer>() : null;
            if (!useGroundChunkBackground || groundTilemap == null)
            {
                if (groundRenderer != null) groundRenderer.enabled = true;
                return;
            }

            var texture = GeneratedSpriteLoader.IsGeneratedPath(groundChunkResourcePath)
                ? GeneratedSpriteLoader.LoadTexture(groundChunkResourcePath)
                : Resources.Load<Texture2D>(groundChunkResourcePath);
            if (texture == null)
            {
                if (groundRenderer != null) groundRenderer.enabled = true;
                return;
            }

            if (groundRenderer != null) groundRenderer.enabled = false;
            groundChunkRoot = new GameObject("Ground Chunk Background");
            groundChunkRoot.transform.SetParent(transform, false);

            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            groundChunkMaterial = new Material(shader)
            {
                name = "Ground Chunk Background Material",
                mainTexture = texture,
                hideFlags = HideFlags.HideAndDontSave
            };
            groundChunkMaterial.color = Color.white;

            int chunkCells = Mathf.Max(1, groundChunkCells);
            int columns = Mathf.CeilToInt(width / (float)chunkCells);
            int rows = Mathf.CeilToInt(height / (float)chunkCells);
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int startX = x * chunkCells;
                    int startY = y * chunkCells;
                    int cellsX = Mathf.Min(chunkCells, width - startX);
                    int cellsY = Mathf.Min(chunkCells, height - startY);
                    CreateGroundChunk(startX, startY, cellsX, cellsY);
                }
            }
        }

        void CreateGroundChunk(int startX, int startY, int cellsX, int cellsY)
        {
            if (cellsX <= 0 || cellsY <= 0) return;
            Vector3 firstCenter = groundTilemap.GetCellCenterWorld(GridToCell(startX, startY));
            Vector3 rightStep = groundTilemap.GetCellCenterWorld(GridToCell(startX + 1, startY)) - firstCenter;
            Vector3 upStep = groundTilemap.GetCellCenterWorld(GridToCell(startX, startY + 1)) - firstCenter;
            Vector3 bottomLeft = firstCenter - rightStep * 0.5f - upStep * 0.5f;
            Vector3 worldCenter = bottomLeft + rightStep * (cellsX * 0.5f) + upStep * (cellsY * 0.5f);
            Vector3 localCenter = groundChunkRoot.transform.InverseTransformPoint(worldCenter);
            Vector3 localRight = groundChunkRoot.transform.InverseTransformVector(rightStep * cellsX);
            Vector3 localUp = groundChunkRoot.transform.InverseTransformVector(upStep * cellsY);

            var mesh = new Mesh
            {
                name = "Ground Chunk Mesh",
                hideFlags = HideFlags.HideAndDontSave
            };
            mesh.vertices = new[]
            {
                localCenter - localRight * 0.5f - localUp * 0.5f,
                localCenter + localRight * 0.5f - localUp * 0.5f,
                localCenter - localRight * 0.5f + localUp * 0.5f,
                localCenter + localRight * 0.5f + localUp * 0.5f
            };
            float uMax = cellsX / (float)Mathf.Max(1, groundChunkCells);
            float vMax = cellsY / (float)Mathf.Max(1, groundChunkCells);
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(uMax, 0f),
                new Vector2(0f, vMax),
                new Vector2(uMax, vMax)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            groundChunkMeshes.Add(mesh);

            var chunk = new GameObject($"Ground Chunk {startX / Mathf.Max(1, groundChunkCells)} {startY / Mathf.Max(1, groundChunkCells)}");
            chunk.transform.SetParent(groundChunkRoot.transform, false);
            var filter = chunk.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = chunk.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = groundChunkMaterial;
            renderer.sortingOrder = groundChunkSortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void EnsureGroundVariantTiles()
        {
            if (!useGroundVariants || HasTiles(grassDetailTiles) || HasTiles(dirtDetailTiles) || HasTiles(pathDetailTiles)) return;

            var sprites = GeneratedSpriteLoader.LoadAll("GroundVariants");
            if (sprites == null || sprites.Length == 0) return;

            var grass = new List<TileBase>();
            var dirt = new List<TileBase>();
            var paths = new List<TileBase>();
            foreach (var sprite in sprites)
            {
                if (sprite == null || sprite.name.Contains("Preview")) continue;
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = "Ground Variant " + sprite.name;
                tile.sprite = sprite;
                tile.color = Color.white;
                tile.flags = TileFlags.None;
                tile.hideFlags = HideFlags.HideAndDontSave;
                generatedGroundTiles.Add(tile);

                string name = sprite.name.ToLowerInvariant();
                if (name.Contains("path") || name.Contains("trail") || name.Contains("winding"))
                {
                    paths.Add(tile);
                }
                else if (name.Contains("dirt") || name.Contains("dry") || name.Contains("scrub") || name.Contains("clearing") || name.Contains("patch"))
                {
                    dirt.Add(tile);
                }
                else
                {
                    grass.Add(tile);
                }
            }

            grassDetailTiles = grass.ToArray();
            dirtDetailTiles = dirt.ToArray();
            pathDetailTiles = paths.Count > 0 ? paths.ToArray() : dirtDetailTiles;
        }

        float DecorativePathStrength(int x, int y)
        {
            float xf = x - width * 0.5f;
            float yf = y - height * 0.5f;
            float diagonalCenter = Mathf.Sin((x + groundVariantSeed) * 0.13f) * 5.8f + xf * 0.32f - 8f;
            float crossingCenter = Mathf.Sin((x - groundVariantSeed) * 0.09f) * 4.5f - xf * 0.18f + 14f;
            float verticalCenter = Mathf.Sin((y + groundVariantSeed) * 0.11f) * 5.2f + 18f;
            float diagonal = BandStrength(yf, diagonalCenter, 7.5f);
            float crossing = BandStrength(yf, crossingCenter, 5.8f) * 0.62f;
            float vertical = BandStrength(xf, verticalCenter, 5.2f) * 0.48f;
            float brokenNoise = Mathf.PerlinNoise((x + groundVariantSeed) * 0.055f, (y - groundVariantSeed) * 0.055f);
            float strength = Mathf.Max(diagonal, crossing, vertical) * Mathf.Lerp(0.35f, 1f, brokenNoise);
            return Mathf.Clamp01(strength);
        }

        static float BandStrength(float value, float center, float radius)
        {
            return Mathf.Clamp01(1f - Mathf.Abs(value - center) / Mathf.Max(0.001f, radius));
        }

        TileBase PickTile(TileBase[] tiles, int x, int y, int salt)
        {
            if (!HasTiles(tiles)) return groundTile;
            int index = Mathf.FloorToInt(Hash01(x, y, salt) * tiles.Length);
            return tiles[Mathf.Clamp(index, 0, tiles.Length - 1)];
        }

        float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                uint h = (uint)(groundVariantSeed + salt * 374761393);
                h ^= (uint)(x * 668265263);
                h ^= (uint)(y * 2246822519);
                h ^= h >> 13;
                h *= 1274126177;
                h ^= h >> 16;
                return (h & 0x00FFFFFF) / 16777216f;
            }
        }

        static bool HasTiles(TileBase[] tiles)
        {
            return tiles != null && tiles.Length > 0;
        }

        public Vector3 GridToWorld(int x, int y)
        {
            return groundTilemap.GetCellCenterWorld(GridToCell(x, y));
        }

        public Bounds GetWorldBounds()
        {
            if (groundTilemap == null || width <= 0 || height <= 0)
            {
                return new Bounds(transform.position, Vector3.zero);
            }

            Vector3 first = groundTilemap.GetCellCenterWorld(GridToCell(0, 0));
            Vector3 last = groundTilemap.GetCellCenterWorld(GridToCell(width - 1, height - 1));
            Vector3 rightStep = width > 1
                ? groundTilemap.GetCellCenterWorld(GridToCell(1, 0)) - first
                : new Vector3(cellSize, 0f, 0f);
            Vector3 upStep = height > 1
                ? groundTilemap.GetCellCenterWorld(GridToCell(0, 1)) - first
                : new Vector3(0f, cellSize, 0f);
            var size = new Vector3(Mathf.Abs(rightStep.x) * width, Mathf.Abs(upStep.y) * height, 0.1f);
            return new Bounds((first + last) * 0.5f, size);
        }

        public Vector3 FootprintCenterToWorld(Vector3Int originCell, Vector2Int footprint)
        {
            footprint = NormalizeFootprint(footprint);
            int minX = originCell.x - (footprint.x - 1) / 2;
            int minY = originCell.y - (footprint.y - 1) / 2;
            var min = new Vector3Int(minX, minY, originCell.z);
            var max = new Vector3Int(minX + footprint.x - 1, minY + footprint.y - 1, originCell.z);
            return (groundTilemap.GetCellCenterWorld(min) + groundTilemap.GetCellCenterWorld(max)) * 0.5f;
        }

        public bool TryWorldToGrid(Vector3 world, out int x, out int y)
        {
            var cell = groundTilemap.WorldToCell(world);
            x = cell.x + width / 2;
            y = cell.y + height / 2;
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        public Vector3Int GridToCell(int x, int y)
        {
            return new Vector3Int(x - width / 2, y - height / 2, 0);
        }

        public Vector3Int WorldToCell(Vector3 world)
        {
            return groundTilemap.WorldToCell(world);
        }

        public bool TryCellToGrid(Vector3Int cell, out int x, out int y)
        {
            x = cell.x + width / 2;
            y = cell.y + height / 2;
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        public bool ContainsCell(Vector3Int cell)
        {
            int x, y;
            return TryCellToGrid(cell, out x, out y);
        }

        public GridObjectRecord GetObject(Vector3Int cell)
        {
            int x, y;
            if (!TryCellToGrid(cell, out x, out y) || objects == null) return null;
            return objects[x, y];
        }

        public bool IsOccupied(Vector3Int cell)
        {
            return HasObject(cell);
        }

        public bool HasObject(Vector3Int cell)
        {
            return GetObject(cell) != null;
        }

        public bool HasFlag(Vector3Int cell, GridCellFlags flag)
        {
            var record = GetObject(cell);
            return record != null && (record.flags & flag) != 0;
        }

        public bool IsBlockedForBuilding(Vector3Int cell)
        {
            return HasFlag(cell, GridCellFlags.BlocksBuilding);
        }

        public bool IsBlockedForMovement(Vector3Int cell)
        {
            return HasFlag(cell, GridCellFlags.BlocksMovement);
        }

        public bool IsBlockedForMovement(Vector3Int cell, TileOwner mover)
        {
            if (mover == TileOwner.Enemy)
            {
                return HasFlag(cell, GridCellFlags.BlocksMovement) || HasFlag(cell, GridCellFlags.BlocksBuilding);
            }

            return IsBlockedForMovement(cell);
        }

        public bool IsOwnedBy(Vector3Int cell, TileOwner owner)
        {
            return GetOwner(cell) == owner;
        }

        public bool IsFootprintOwnedBy(Vector3Int originCell, Vector2Int footprint, TileOwner owner)
        {
            footprint = NormalizeFootprint(footprint);
            foreach (var cell in FootprintCells(originCell, footprint))
            {
                if (!ContainsCell(cell) || GetOwner(cell) != owner) return false;
            }

            return true;
        }

        public bool IsFootprintClear(Vector3Int originCell, Vector2Int footprint)
        {
            footprint = NormalizeFootprint(footprint);
            foreach (var cell in FootprintCells(originCell, footprint))
            {
                if (!ContainsCell(cell) || HasObject(cell)) return false;
            }

            return true;
        }

        public bool CanPlaceObject(Vector3Int originCell, Vector2Int footprint)
        {
            footprint = NormalizeFootprint(footprint);
            foreach (var cell in FootprintCells(originCell, footprint))
            {
                if (!ContainsCell(cell) || IsBlockedForBuilding(cell)) return false;
            }

            return true;
        }

        public bool TryRegisterObject(Vector3Int originCell, GridObjectType type, GridCellFlags flags, GameObject instance, Vector2Int footprint)
        {
            footprint = NormalizeFootprint(footprint);
            if (!CanPlaceObject(originCell, footprint)) return false;

            var record = new GridObjectRecord
            {
                type = type,
                flags = flags,
                instance = instance,
                originCell = originCell,
                footprint = footprint
            };

            foreach (var cell in FootprintCells(originCell, footprint))
            {
                int x, y;
                if (TryCellToGrid(cell, out x, out y)) objects[x, y] = record;
            }

            return true;
        }

        public bool TryRegisterObject(Vector3Int originCell, GridObjectType type, GridCellFlags flags, GameObject instance)
        {
            return TryRegisterObject(originCell, type, flags, instance, Vector2Int.one);
        }

        public void ClearObject(Vector3Int originCell)
        {
            var record = GetObject(originCell);
            if (record == null) return;
            foreach (var cell in FootprintCells(record.originCell, record.footprint))
            {
                int x, y;
                if (TryCellToGrid(cell, out x, out y) && objects[x, y] == record) objects[x, y] = null;
                if (objectTilemap != null) objectTilemap.SetTile(cell, null);
            }
        }

        public int RegisterSceneObjects()
        {
            int registered = 0;
            var markers = FindObjectsOfType<GridObjectMarker>();
            foreach (var marker in markers)
            {
                if (marker != null && marker.Register(this)) registered++;
            }

            return registered;
        }

        public TileOwner GetOwner(Vector3 world)
        {
            int x, y;
            if (!TryWorldToGrid(world, out x, out y)) return TileOwner.Neutral;
            return OwnerFromControl(controlValues[x, y]);
        }

        public TileOwner GetOwner(Vector3Int cell)
        {
            int x, y;
            if (!TryCellToGrid(cell, out x, out y) || controlValues == null) return TileOwner.Neutral;
            return OwnerFromControl(controlValues[x, y]);
        }

        public float GetControl(Vector3 world)
        {
            int x, y;
            if (!TryWorldToGrid(world, out x, out y) || controlValues == null) return 0f;
            return controlValues[x, y];
        }

        public float GetMoveMultiplier(Vector3 world, TileOwner mover, float slowedMultiplier)
        {
            int x, y;
            if (!TryWorldToGrid(world, out x, out y) || controlValues == null) return 1f;

            var cell = GridToCell(x, y);
            if (IsBlockedForMovement(cell, mover)) return 0f;

            float control = controlValues[x, y];
            float target = targetControlValues[x, y];
            if (!paintDirty[x, y] && Mathf.Abs(control) < 0.01f && Mathf.Abs(target) < 0.01f) return 1f;

            if (mover == TileOwner.Player && control >= 0f) return 1f;
            if (mover == TileOwner.Enemy && control <= 0f) return 1f;
            return Mathf.Clamp01(slowedMultiplier);
        }

        public TileControlSummary GetControlSummary()
        {
            int totalCells = width * height;
            return new TileControlSummary
            {
                playerCells = playerControlledCellCount,
                enemyCells = enemyControlledCellCount,
                neutralCells = Mathf.Max(0, totalCells - playerControlledCellCount - enemyControlledCellCount),
                totalCells = totalCells
            };
        }

        public float GetPlayerControlRatio()
        {
            return GetControlSummary().playerRatio;
        }

        public void GetControlCellCounts(out int playerCells, out int enemyCells, out int neutralCells)
        {
            var summary = GetControlSummary();
            playerCells = summary.playerCells;
            enemyCells = summary.enemyCells;
            neutralCells = summary.neutralCells;
        }

        public void Paint(Vector3 world, TileOwner owner, int radius)
        {
            Paint(world, owner, radius, false);
        }

        public void PaintImmediate(Vector3 world, TileOwner owner, int radius)
        {
            Paint(world, owner, radius, true);
        }

        public void PaintEllipse(Vector3 world, TileOwner owner, int radiusX, int radiusY)
        {
            PaintEllipse(world, owner, radiusX, radiusY, false);
        }

        public void PaintEllipseImmediate(Vector3 world, TileOwner owner, int radiusX, int radiusY)
        {
            PaintEllipse(world, owner, radiusX, radiusY, true);
        }

        void Paint(Vector3 world, TileOwner owner, int radius, bool immediate)
        {
            PaintEllipse(world, owner, radius, radius, immediate);
        }

        void PaintEllipse(Vector3 world, TileOwner owner, int radiusX, int radiusY, bool immediate)
        {
            int cx, cy;
            if (!TryWorldToGrid(world, out cx, out cy)) return;
            radiusX = Mathf.Max(0, radiusX);
            radiusY = Mathf.Max(0, radiusY);
            float target = ControlFromOwner(owner);
            for (int y = cy - radiusY; y <= cy + radiusY; y++)
            {
                for (int x = cx - radiusX; x <= cx + radiusX; x++)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height) continue;
                    if (!IsInsideEllipse(x - cx, y - cy, radiusX, radiusY)) continue;
                    targetControlValues[x, y] = target;
                    if (immediate)
                    {
                        controlValues[x, y] = target;
                        paintDirty[x, y] = false;
                        ApplyPaintVisual(x, y, target);
                    }
                    else
                    {
                        paintDirty[x, y] = true;
                    }
                }
            }
        }

        static bool IsInsideEllipse(int dx, int dy, int radiusX, int radiusY)
        {
            if (radiusX <= 0 && radiusY <= 0) return dx == 0 && dy == 0;
            if (radiusX <= 0) return dx == 0 && Mathf.Abs(dy) <= radiusY;
            if (radiusY <= 0) return dy == 0 && Mathf.Abs(dx) <= radiusX;
            float normalizedX = dx / (float)radiusX;
            float normalizedY = dy / (float)radiusY;
            return normalizedX * normalizedX + normalizedY * normalizedY <= 1f;
        }

        void UpdatePaintTransitions()
        {
            if (controlValues == null || targetControlValues == null || paintDirty == null || paintTilemap == null) return;

            float step = paintTransitionSeconds <= 0f ? float.PositiveInfinity : Time.deltaTime * 2f / paintTransitionSeconds;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!paintDirty[x, y]) continue;

                    float current = controlValues[x, y];
                    float target = targetControlValues[x, y];
                    float next = Mathf.MoveTowards(current, target, step);
                    controlValues[x, y] = next;
                    ApplyPaintVisual(x, y, next);

                    if (Mathf.Approximately(next, target))
                    {
                        paintDirty[x, y] = false;
                    }
                }
            }
        }

        void ApplyPaintVisual(int x, int y, float control)
        {
            var previousOwner = owners[x, y];
            var nextOwner = OwnerFromControl(control);
            if (previousOwner != nextOwner)
            {
                AdjustOwnerCounts(previousOwner, nextOwner);
                owners[x, y] = nextOwner;
            }
            var cell = GridToCell(x, y);
            if (Mathf.Abs(control) < 0.01f && Mathf.Abs(targetControlValues[x, y]) < 0.01f)
            {
                paintTilemap.SetTile(cell, null);
                return;
            }

            paintTilemap.SetTile(cell, paintTile);
            paintTilemap.SetTileFlags(cell, TileFlags.None);
            float t = Mathf.InverseLerp(-1f, 1f, control);
            var color = Color.Lerp(enemy, player, t);
            color.a = Mathf.Lerp(0.38f, Mathf.Max(enemy.a, player.a), Mathf.Abs(control));
            paintTilemap.SetColor(cell, color);
        }

        void AdjustOwnerCounts(TileOwner previousOwner, TileOwner nextOwner)
        {
            if (previousOwner == nextOwner) return;
            if (previousOwner == TileOwner.Player) playerControlledCellCount = Mathf.Max(0, playerControlledCellCount - 1);
            else if (previousOwner == TileOwner.Enemy) enemyControlledCellCount = Mathf.Max(0, enemyControlledCellCount - 1);

            if (nextOwner == TileOwner.Player) playerControlledCellCount++;
            else if (nextOwner == TileOwner.Enemy) enemyControlledCellCount++;
        }

        TileOwner OwnerFromControl(float control)
        {
            float threshold = Mathf.Clamp01(controlledThreshold);
            if (control >= threshold) return TileOwner.Player;
            if (control <= -threshold) return TileOwner.Enemy;
            return TileOwner.Neutral;
        }

        static float ControlFromOwner(TileOwner owner)
        {
            if (owner == TileOwner.Player) return 1f;
            if (owner == TileOwner.Enemy) return -1f;
            return 0f;
        }

        static Vector2Int NormalizeFootprint(Vector2Int footprint)
        {
            return new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        }

        static System.Collections.Generic.IEnumerable<Vector3Int> FootprintCells(Vector3Int originCell, Vector2Int footprint)
        {
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

        void CreateGridLineOverlay()
        {
            DestroyGridLineOverlay();
            if (!showGridLines || groundTilemap == null || width <= 0 || height <= 0) return;

            gridLineOverlay = new GameObject("Grid Line Overlay");
            gridLineOverlay.transform.SetParent(transform, false);

            var meshFilter = gridLineOverlay.AddComponent<MeshFilter>();
            var meshRenderer = gridLineOverlay.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            gridLineMaterial = new Material(shader);
            gridLineMaterial.color = gridLineColor;
            meshRenderer.sharedMaterial = gridLineMaterial;
            meshRenderer.sortingOrder = gridLineSortingOrder;

            gridLineMesh = BuildGridLineMesh();
            meshFilter.sharedMesh = gridLineMesh;
        }

        Mesh BuildGridLineMesh()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float thickness = Mathf.Max(0.001f, gridLineThickness);
            Vector3 firstCenter = groundTilemap.GetCellCenterWorld(GridToCell(0, 0));
            Vector3 rightStep = groundTilemap.GetCellCenterWorld(GridToCell(1, 0)) - firstCenter;
            Vector3 upStep = groundTilemap.GetCellCenterWorld(GridToCell(0, 1)) - firstCenter;
            if (width == 1) rightStep = new Vector3(cellSize, 0f, 0f);
            if (height == 1) upStep = new Vector3(0f, 0.5f, 0f);

            Vector3 bottomLeft = firstCenter - rightStep * 0.5f - upStep * 0.5f;
            for (int x = 0; x <= width; x++)
            {
                Vector3 start = bottomLeft + rightStep * x;
                AddLineQuad(vertices, triangles, start, start + upStep * height, thickness);
            }

            for (int y = 0; y <= height; y++)
            {
                Vector3 start = bottomLeft + upStep * y;
                AddLineQuad(vertices, triangles, start, start + rightStep * width, thickness);
            }

            var mesh = new Mesh();
            mesh.name = "Grid Line Overlay Mesh";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        void AddLineQuad(List<Vector3> vertices, List<int> triangles, Vector3 worldStart, Vector3 worldEnd, float thickness)
        {
            Vector3 start = gridLineOverlay.transform.InverseTransformPoint(worldStart);
            Vector3 end = gridLineOverlay.transform.InverseTransformPoint(worldEnd);
            Vector3 direction = end - start;
            if (direction.sqrMagnitude <= Mathf.Epsilon) return;

            direction.Normalize();
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f) * (thickness * 0.5f);
            int index = vertices.Count;
            vertices.Add(start - perpendicular);
            vertices.Add(start + perpendicular);
            vertices.Add(end + perpendicular);
            vertices.Add(end - perpendicular);
            triangles.Add(index);
            triangles.Add(index + 1);
            triangles.Add(index + 2);
            triangles.Add(index);
            triangles.Add(index + 2);
            triangles.Add(index + 3);
        }

        void DestroyGridLineOverlay()
        {
            if (gridLineOverlay == null)
            {
                var existing = transform.Find("Grid Line Overlay");
                if (existing != null) DestroyUnityObject(existing.gameObject);
            }

            if (gridLineOverlay != null) DestroyUnityObject(gridLineOverlay);
            if (gridLineMesh != null) DestroyUnityObject(gridLineMesh);
            if (gridLineMaterial != null) DestroyUnityObject(gridLineMaterial);
            gridLineOverlay = null;
            gridLineMesh = null;
            gridLineMaterial = null;
        }

        void DestroyGroundChunkBackground()
        {
            if (groundChunkRoot == null)
            {
                var existing = transform.Find("Ground Chunk Background");
                if (existing != null) DestroyUnityObject(existing.gameObject);
            }

            if (groundChunkRoot != null) DestroyUnityObject(groundChunkRoot);
            foreach (var mesh in groundChunkMeshes)
            {
                if (mesh != null) DestroyUnityObject(mesh);
            }
            groundChunkMeshes.Clear();
            if (groundChunkMaterial != null) DestroyUnityObject(groundChunkMaterial);
            groundChunkRoot = null;
            groundChunkMaterial = null;
        }

        static void DestroyUnityObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
