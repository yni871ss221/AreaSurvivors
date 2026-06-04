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

    public enum GridObjectType
    {
        Empty,
        Tower,
        Ballista,
        Fence,
        Tree,
        Rock,
        Pond
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
        public int width = 96;
        public int height = 96;
        public float cellSize = 0.7f;
        public Sprite tileSprite;
        public Sprite paintSprite;
        public Tilemap groundTilemap;
        public Tilemap paintTilemap;
        public Tilemap objectTilemap;
        public TileBase groundTile;
        public TileBase paintTile;
        public Color neutral = new Color(0.43f, 0.58f, 0.31f);
        public Color player = new Color(0.24f, 0.55f, 0.95f, 0.52f);
        public Color enemy = new Color(0.85f, 0.25f, 0.22f, 0.50f);
        public bool showGridLines = true;
        public Color gridLineColor = new Color(0.08f, 0.12f, 0.08f, 0.16f);
        public float gridLineThickness = 0.012f;
        public int gridLineSortingOrder = 930;

        TileOwner[,] owners;
        GridObjectRecord[,] objects;
        GameObject gridLineOverlay;
        Mesh gridLineMesh;
        Material gridLineMaterial;

        void Awake()
        {
            if (showGridLines) CreateGridLineOverlay();
        }

        public void Build()
        {
            owners = new TileOwner[width, height];
            objects = new GridObjectRecord[width, height];
            groundTilemap.ClearAllTiles();
            paintTilemap.ClearAllTiles();
            if (objectTilemap != null) objectTilemap.ClearAllTiles();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = GridToCell(x, y);
                    groundTilemap.SetTile(cell, groundTile);
                    groundTilemap.SetTileFlags(cell, TileFlags.None);
                    groundTilemap.SetColor(cell, Color.white);
                }
            }

            CreateGridLineOverlay();
        }

        public Vector3 GridToWorld(int x, int y)
        {
            return groundTilemap.GetCellCenterWorld(GridToCell(x, y));
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
            return GetObject(cell) != null;
        }

        public bool HasFlag(Vector3Int cell, GridCellFlags flag)
        {
            var record = GetObject(cell);
            return record != null && (record.flags & flag) != 0;
        }

        public bool CanPlaceObject(Vector3Int originCell, Vector2Int footprint)
        {
            footprint = NormalizeFootprint(footprint);
            foreach (var cell in FootprintCells(originCell, footprint))
            {
                if (!ContainsCell(cell) || IsOccupied(cell)) return false;
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
            return owners[x, y];
        }

        public TileOwner GetOwner(Vector3Int cell)
        {
            int x, y;
            if (!TryCellToGrid(cell, out x, out y) || owners == null) return TileOwner.Neutral;
            return owners[x, y];
        }

        public void Paint(Vector3 world, TileOwner owner, int radius)
        {
            int cx, cy;
            if (!TryWorldToGrid(world, out cx, out cy)) return;
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height) continue;
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > radius * radius) continue;
                    owners[x, y] = owner;
                    var cell = GridToCell(x, y);
                    paintTilemap.SetTile(cell, owner == TileOwner.Neutral ? null : paintTile);
                    paintTilemap.SetTileFlags(cell, TileFlags.None);
                    paintTilemap.SetColor(cell, owner == TileOwner.Player ? player : enemy);
                }
            }
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

        static void DestroyUnityObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
