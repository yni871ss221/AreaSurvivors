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
            public GridObjectType type = GridObjectType.Tree;
            public Vector2 colliderPadding = new Vector2(0.04f, 0.04f);
            public bool harvestable = true;
            public ResourceType resourceType = ResourceType.Wood;
        }

        [Serializable]
        public sealed class PlacementEntry
        {
            public string landmarkName = "Tree1";
            public int count = 1;
        }

        [Serializable]
        public sealed class PlacementBand
        {
            public string name = "10-20";
            public int minDistanceCells = 10;
            public int maxDistanceCells = 20;
            public PlacementEntry[] entries = Array.Empty<PlacementEntry>();
        }

        public int seed = 20260605;
        public bool randomizeSeedEachRun = true;
        public int edgePaddingCells = 4;
        public int separationCells = 1;
        public int maxPlacementAttemptsPerObject = 80;
        public bool addOutline = true;
        public Color outlineColor = Color.black;
        public float outlineThickness = 0.018f;
        public LandmarkSpec[] landmarks =
        {
            new LandmarkSpec { name = "Tree1", resourcePath = "Generated/Landmarks/Tree1", footprint = new Vector2Int(1, 1) },
            new LandmarkSpec { name = "Forest2", resourcePath = "Generated/Landmarks/Forest2", footprint = new Vector2Int(2, 2) },
            new LandmarkSpec { name = "Forest4", resourcePath = "Generated/Landmarks/Forest4", footprint = new Vector2Int(4, 4) },
            new LandmarkSpec { name = "Forest8", resourcePath = "Generated/Landmarks/Forest8", footprint = new Vector2Int(8, 8), harvestable = false },
            new LandmarkSpec { name = "Rock1", resourcePath = "Generated/Landmarks/Rock1", footprint = new Vector2Int(1, 1), type = GridObjectType.Rock, resourceType = ResourceType.Stone },
            new LandmarkSpec { name = "Rock2", resourcePath = "Generated/Landmarks/Rock2", footprint = new Vector2Int(2, 2), type = GridObjectType.Rock, resourceType = ResourceType.Stone },
            new LandmarkSpec { name = "Rock4", resourcePath = "Generated/Landmarks/Rock4", footprint = new Vector2Int(4, 4), type = GridObjectType.Rock, resourceType = ResourceType.Stone },
            new LandmarkSpec { name = "Rock8", resourcePath = "Generated/Landmarks/Rock8", footprint = new Vector2Int(8, 8), type = GridObjectType.Rock, resourceType = ResourceType.Stone, harvestable = false }
        };
        public PlacementBand[] placementBands =
        {
            new PlacementBand
            {
                name = "10-20",
                minDistanceCells = 10,
                maxDistanceCells = 20,
                entries = new[]
                {
                    new PlacementEntry { landmarkName = "Tree1", count = 3 },
                    new PlacementEntry { landmarkName = "Rock1", count = 3 }
                }
            },
            new PlacementBand
            {
                name = "20-30",
                minDistanceCells = 20,
                maxDistanceCells = 30,
                entries = new[]
                {
                    new PlacementEntry { landmarkName = "Tree1", count = 5 },
                    new PlacementEntry { landmarkName = "Rock1", count = 5 },
                    new PlacementEntry { landmarkName = "Forest2", count = 3 },
                    new PlacementEntry { landmarkName = "Rock2", count = 3 },
                    new PlacementEntry { landmarkName = "Forest4", count = 1 },
                    new PlacementEntry { landmarkName = "Rock4", count = 1 }
                }
            },
            new PlacementBand
            {
                name = "30-40",
                minDistanceCells = 30,
                maxDistanceCells = 40,
                entries = new[]
                {
                    new PlacementEntry { landmarkName = "Tree1", count = 10 },
                    new PlacementEntry { landmarkName = "Rock1", count = 10 },
                    new PlacementEntry { landmarkName = "Forest2", count = 5 },
                    new PlacementEntry { landmarkName = "Rock2", count = 5 },
                    new PlacementEntry { landmarkName = "Forest4", count = 3 },
                    new PlacementEntry { landmarkName = "Rock4", count = 3 },
                    new PlacementEntry { landmarkName = "Forest8", count = 1 },
                    new PlacementEntry { landmarkName = "Rock8", count = 1 }
                }
            }
        };

        Transform spawnedRoot;
        bool spawned;
        int lastUsedSeed;
        Vector3Int placementCenterCell;

        public int LastUsedSeed => lastUsedSeed;

        public void Spawn(TileGrid grid)
        {
            var centerCell = grid != null ? grid.GridToCell(grid.width / 2, grid.height / 2) : Vector3Int.zero;
            Spawn(grid, centerCell);
        }

        public void Spawn(TileGrid grid, Vector3Int centerCell)
        {
            if (spawned || grid == null || landmarks == null) return;
            spawned = true;
            placementCenterCell = centerCell;

            spawnedRoot = new GameObject("Natural Landmarks").transform;
            spawnedRoot.SetParent(transform, false);
            lastUsedSeed = ResolveSeed();
            var random = new System.Random(lastUsedSeed);
            var specsByName = BuildSpecLookup();
            foreach (var band in placementBands)
            {
                SpawnBand(grid, random, specsByName, band);
            }
        }

        int ResolveSeed()
        {
            if (!randomizeSeedEachRun) return seed;

            unchecked
            {
                int ticks = Environment.TickCount;
                int time = (int)DateTime.UtcNow.Ticks;
                int instance = GetInstanceID();
                return seed ^ ticks ^ time ^ instance;
            }
        }

        Dictionary<string, LandmarkSpec> BuildSpecLookup()
        {
            var specsByName = new Dictionary<string, LandmarkSpec>(StringComparer.Ordinal);
            foreach (var spec in landmarks)
            {
                if (spec == null || string.IsNullOrEmpty(spec.name)) continue;
                specsByName[spec.name] = spec;
            }

            return specsByName;
        }

        void SpawnBand(TileGrid grid, System.Random random, Dictionary<string, LandmarkSpec> specsByName, PlacementBand band)
        {
            if (band == null || band.entries == null) return;
            var entries = new List<(PlacementEntry entry, LandmarkSpec spec)>();
            foreach (var entry in band.entries)
            {
                if (entry == null || entry.count <= 0 || string.IsNullOrEmpty(entry.landmarkName)) continue;
                if (!specsByName.TryGetValue(entry.landmarkName, out var spec))
                {
                    Debug.LogWarning($"Landmark spec '{entry.landmarkName}' was not found.");
                    continue;
                }

                entries.Add((entry, spec));
            }

            entries.Sort((a, b) => Mathf.Max(b.spec.footprint.x, b.spec.footprint.y)
                .CompareTo(Mathf.Max(a.spec.footprint.x, a.spec.footprint.y)));
            foreach (var pair in entries)
            {
                SpawnEntry(grid, random, pair.spec, band, pair.entry.count);
            }
        }

        void SpawnEntry(TileGrid grid, System.Random random, LandmarkSpec spec, PlacementBand band, int count)
        {
            if (spec == null || count <= 0) return;
            var sprite = LoadSprite(spec.resourcePath);
            if (sprite == null) return;

            var footprint = NormalizeFootprint(spec.footprint);
            int placed = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(1, count * Mathf.Max(1, maxPlacementAttemptsPerObject));
            while (placed < count && attempts++ < maxAttempts)
            {
                var cell = RandomCell(grid, random, footprint);
                if (!CanUseCell(grid, cell, footprint, band)) continue;
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

        bool CanUseCell(TileGrid grid, Vector3Int originCell, Vector2Int footprint, PlacementBand band)
        {
            if (!grid.CanPlaceObject(originCell, footprint)) return false;
            if (!IsInsidePlacementBand(grid, originCell, footprint, band)) return false;
            if (!HasSeparation(grid, originCell, footprint)) return false;

            return true;
        }

        bool IsInsidePlacementBand(TileGrid grid, Vector3Int originCell, Vector2Int footprint, PlacementBand band)
        {
            if (band == null) return true;
            if (!grid.TryCellToGrid(placementCenterCell, out var centerX, out var centerY)) return false;
            if (!grid.TryCellToGrid(originCell, out var x, out var y)) return false;

            float objectRadius = Mathf.Max(footprint.x, footprint.y) * 0.5f;
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centerX, centerY));
            return distance - objectRadius >= Mathf.Max(0, band.minDistanceCells)
                && distance + objectRadius < Mathf.Max(band.minDistanceCells, band.maxDistanceCells);
        }

        bool HasSeparation(TileGrid grid, Vector3Int originCell, Vector2Int footprint)
        {
            int separation = Mathf.Max(0, separationCells);
            var min = MinCell(originCell, footprint);
            for (int x = -separation; x < footprint.x + separation; x++)
            {
                for (int y = -separation; y < footprint.y + separation; y++)
                {
                    var cell = new Vector3Int(min.x + x, min.y + y, originCell.z);
                    if (!grid.ContainsCell(cell) || grid.IsOccupied(cell)) return false;
                }
            }

            return true;
        }

        void CreateLandmark(TileGrid grid, LandmarkSpec spec, Sprite sprite, Vector3Int originCell, Vector2Int footprint)
        {
            var root = new GameObject(spec.name);
            root.transform.SetParent(spawnedRoot, true);
            root.transform.position = FootprintBottomCenterToWorld(grid, originCell, footprint);

            var marker = root.AddComponent<GridObjectMarker>();
            marker.type = spec.type;
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

            if (spec.harvestable)
            {
                var config = GameManager.Instance != null ? GameManager.Instance.config : null;
                var harvest = root.AddComponent<HarvestableResource>();
                harvest.Configure(config, grid, originCell, footprint, spec.resourceType, HarvestAmountForFootprint(config, footprint));
            }
        }

        static int HarvestAmountForFootprint(GameConfig config, Vector2Int footprint)
        {
            int cells = Mathf.Max(1, footprint.x) * Mathf.Max(1, footprint.y);
            if (config == null)
            {
                if (cells <= 1) return 100;
                if (cells <= 4) return 200;
                if (cells <= 16) return 400;
                return 800;
            }

            if (cells <= 1) return Mathf.Max(1, config.harvestAmount1Cell);
            if (cells <= 4) return Mathf.Max(1, config.harvestAmount2Cell);
            if (cells <= 16) return Mathf.Max(1, config.harvestAmount4Cell);
            return Mathf.Max(1, config.harvestAmount8Cell);
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
