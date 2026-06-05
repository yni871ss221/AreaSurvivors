using System;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class NaturalLandmarkSpawner : MonoBehaviour
    {
        [Serializable]
        public sealed class LandmarkSpec
        {
            public string name = "Tree1";
            public string resourcePath = "Generated/Landmarks/Tree1";
            public Vector2Int footprint = Vector2Int.one;
            public int count = 12;
            public Vector2 colliderPadding = new Vector2(0.04f, 0.04f);
        }

        public int seed = 20260605;
        public int centerClearanceCells = 16;
        public int edgePaddingCells = 4;
        public int maxPlacementAttemptsPerObject = 80;
        public bool addOutline = true;
        public Color outlineColor = Color.black;
        public float outlineThickness = 0.018f;
        public LandmarkSpec[] landmarks =
        {
            new LandmarkSpec { name = "Tree1", resourcePath = "Generated/Landmarks/Tree1", footprint = new Vector2Int(1, 1), count = 16 },
            new LandmarkSpec { name = "Forest2", resourcePath = "Generated/Landmarks/Forest2", footprint = new Vector2Int(2, 2), count = 7 },
            new LandmarkSpec { name = "Forest4", resourcePath = "Generated/Landmarks/Forest4", footprint = new Vector2Int(4, 4), count = 3 },
            new LandmarkSpec { name = "Forest8", resourcePath = "Generated/Landmarks/Forest8", footprint = new Vector2Int(8, 8), count = 1 }
        };

        Transform spawnedRoot;
        bool spawned;

        public void Spawn(TileGrid grid)
        {
            if (spawned || grid == null || landmarks == null) return;
            spawned = true;

            spawnedRoot = new GameObject("Natural Landmarks").transform;
            spawnedRoot.SetParent(transform, false);
            var random = new System.Random(seed);
            foreach (var spec in landmarks)
            {
                SpawnSpec(grid, random, spec);
            }
        }

        void SpawnSpec(TileGrid grid, System.Random random, LandmarkSpec spec)
        {
            if (spec == null || spec.count <= 0) return;
            var sprite = LoadSprite(spec.resourcePath);
            if (sprite == null) return;

            var footprint = NormalizeFootprint(spec.footprint);
            int placed = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(1, spec.count * Mathf.Max(1, maxPlacementAttemptsPerObject));
            while (placed < spec.count && attempts++ < maxAttempts)
            {
                var cell = RandomCell(grid, random, footprint);
                if (!CanUseCell(grid, cell, footprint)) continue;
                CreateLandmark(grid, spec, sprite, cell, footprint);
                placed++;
            }
        }

        Vector3Int RandomCell(TileGrid grid, System.Random random, Vector2Int footprint)
        {
            int pad = Mathf.Max(0, edgePaddingCells) + Mathf.Max(footprint.x, footprint.y);
            int minX = pad;
            int minY = pad;
            int maxX = Mathf.Max(minX + 1, grid.width - pad);
            int maxY = Mathf.Max(minY + 1, grid.height - pad);
            return grid.GridToCell(random.Next(minX, maxX), random.Next(minY, maxY));
        }

        bool CanUseCell(TileGrid grid, Vector3Int originCell, Vector2Int footprint)
        {
            if (!grid.CanPlaceObject(originCell, footprint)) return false;
            if (!grid.TryCellToGrid(originCell, out var x, out var y)) return false;

            var center = new Vector2(grid.width * 0.5f, grid.height * 0.5f);
            float clearance = Mathf.Max(0, centerClearanceCells) + Mathf.Max(footprint.x, footprint.y) * 0.5f;
            return Vector2.Distance(new Vector2(x, y), center) > clearance;
        }

        void CreateLandmark(TileGrid grid, LandmarkSpec spec, Sprite sprite, Vector3Int originCell, Vector2Int footprint)
        {
            var root = new GameObject(spec.name);
            root.transform.SetParent(spawnedRoot, true);
            root.transform.position = FootprintBottomCenterToWorld(grid, originCell, footprint);

            var marker = root.AddComponent<GridObjectMarker>();
            marker.type = GridObjectType.Tree;
            marker.flags = GridCellFlags.BlocksMovement | GridCellFlags.BlocksBuilding | GridCellFlags.Natural;
            marker.footprint = footprint;
            if (!grid.TryRegisterObject(originCell, marker.type, marker.flags, root, footprint))
            {
                Destroy(root);
                return;
            }

            var obstacle = root.AddComponent<Obstacle>();
            obstacle.visualSize = sprite.bounds.size;

            var visualObject = new GameObject("Paper Visual");
            visualObject.transform.SetParent(root.transform, false);
            var visual = visualObject.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, 1000);
            var billboard = visualObject.AddComponent<PaperBillboard>();
            billboard.faceCamera = true;
            if (addOutline)
            {
                var outline = visualObject.AddComponent<RuntimeSpriteOutline>();
                outline.outlineColor = outlineColor;
                outline.thickness = outlineThickness;
            }

            var ySort = root.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.renderers = new[] { visual.Renderer };

            var collider = root.AddComponent<BoxCollider2D>();
            var min = MinCell(originCell, footprint);
            var max = new Vector3Int(min.x + footprint.x - 1, min.y + footprint.y - 1, originCell.z);
            var minWorld = grid.groundTilemap.GetCellCenterWorld(min);
            var maxWorld = grid.groundTilemap.GetCellCenterWorld(max);
            var span = new Vector2(
                Mathf.Abs(maxWorld.x - minWorld.x) + grid.cellSize,
                Mathf.Abs(maxWorld.y - minWorld.y) + grid.cellSize * 0.72f);
            collider.size = new Vector2(
                Mathf.Max(0.1f, span.x - spec.colliderPadding.x),
                Mathf.Max(0.1f, span.y - spec.colliderPadding.y));
            collider.offset = new Vector2(0f, collider.size.y * 0.5f);
        }

        static Sprite LoadSprite(string resourcePath)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f), 128f);
        }

        static Vector3 FootprintBottomCenterToWorld(TileGrid grid, Vector3Int originCell, Vector2Int footprint)
        {
            var min = MinCell(originCell, footprint);
            var max = new Vector3Int(min.x + footprint.x - 1, min.y, originCell.z);
            var bottomCenter = (grid.groundTilemap.GetCellCenterWorld(min) + grid.groundTilemap.GetCellCenterWorld(max)) * 0.5f;
            var up = grid.groundTilemap.GetCellCenterWorld(min + Vector3Int.up) - grid.groundTilemap.GetCellCenterWorld(min);
            return bottomCenter - up * 0.5f;
        }

        static Vector3Int MinCell(Vector3Int originCell, Vector2Int footprint)
        {
            footprint = NormalizeFootprint(footprint);
            return new Vector3Int(
                originCell.x - (footprint.x - 1) / 2,
                originCell.y - (footprint.y - 1) / 2,
                originCell.z);
        }

        static Vector2Int NormalizeFootprint(Vector2Int footprint)
        {
            return new Vector2Int(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y));
        }
    }
}
