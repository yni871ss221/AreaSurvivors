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
        [Min(2)]
        public int separationCells = 2;
        public int maxPlacementAttemptsPerObject = 2000;
        [Header("Clear Routes")]
        public int clearRouteCount = 6;
        public float clearRouteHalfWidthCells = 2.5f;
        public float clearRouteAngleOffsetDegrees;
        [Header("Vertical Map Resources")]
        public bool useVerticalMapResourceLayout;
        public bool useOuterEightChunkResourceLayout = true;
        public int upperTree1CountPerChunk = 3;
        public int upperRock1CountPerChunk = 3;
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

        public bool CreateTestLandmark(TileGrid grid, string landmarkName, Vector3Int originCell)
        {
            if (grid == null || string.IsNullOrEmpty(landmarkName)) return false;
            var specsByName = BuildSpecLookup();
            if (!specsByName.TryGetValue(landmarkName, out var spec)) return false;
            var sprite = LoadSprite(spec.resourcePath);
            if (sprite == null) return false;

            if (spawnedRoot == null)
            {
                spawnedRoot = new GameObject("Test Natural Landmarks").transform;
                spawnedRoot.SetParent(transform, false);
            }

            var footprint = NormalizeFootprint(spec.footprint);
            if (!grid.CanPlaceObject(originCell, footprint)) return false;
            CreateLandmark(grid, spec, sprite, originCell, footprint);
            return true;
        }

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
            if (useVerticalMapResourceLayout)
            {
                SpawnVerticalMapResources(grid, random, specsByName);
                return;
            }

            if (useOuterEightChunkResourceLayout)
            {
                SpawnOuterEightChunkResources(grid, random, specsByName);
                return;
            }

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
            if (OverlapsClearRoute(grid, originCell, footprint)) return false;
            if (!HasSeparation(grid, originCell, footprint)) return false;

            return true;
        }

        void SpawnVerticalMapResources(TileGrid grid, System.Random random, Dictionary<string, LandmarkSpec> specsByName)
        {
            SpawnCenterChunkResources(grid, random, specsByName);
            SpawnChunkResources(grid, random, specsByName, -1, new[]
            {
                Entry("Tree1", 3), Entry("Rock1", 3), Entry("Forest2", 1), Entry("Rock2", 1)
            });
            SpawnChunkResources(grid, random, specsByName, -2, new[]
            {
                Entry("Tree1", 3), Entry("Rock1", 3), Entry("Forest2", 3), Entry("Rock2", 3)
            });
            SpawnChunkResources(grid, random, specsByName, -3, new[]
            {
                Entry("Tree1", 1), Entry("Rock1", 1), Entry("Forest2", 3), Entry("Rock2", 3), Entry("Forest4", 1), Entry("Rock4", 1)
            });
            SpawnChunkResources(grid, random, specsByName, -4, new[]
            {
                Entry("Forest2", 5), Entry("Rock2", 5), Entry("Forest4", 3), Entry("Rock4", 3)
            });
            SpawnChunkResources(grid, random, specsByName, -5, new[]
            {
                Entry("Forest4", 3), Entry("Rock4", 3), Entry("Forest8", 1), Entry("Rock8", 1)
            });

            var upperEntries = new[]
            {
                Entry("Tree1", upperTree1CountPerChunk),
                Entry("Rock1", upperRock1CountPerChunk)
            };
            for (int i = 1; i <= 5; i++)
            {
                SpawnChunkResources(grid, random, specsByName, i, upperEntries);
            }
        }

        void SpawnOuterEightChunkResources(TileGrid grid, System.Random random, Dictionary<string, LandmarkSpec> specsByName)
        {
            var entries = new[]
            {
                Entry("Tree1", 8),
                Entry("Rock1", 8),
                Entry("Forest2", 4),
                Entry("Rock2", 4),
                Entry("Forest4", 1),
                Entry("Rock4", 1)
            };

            var sorted = new List<(PlacementEntry entry, LandmarkSpec spec)>();
            foreach (var entry in entries)
            {
                if (entry == null || !specsByName.TryGetValue(entry.landmarkName, out var spec)) continue;
                sorted.Add((entry, spec));
            }

            sorted.Sort((a, b) => Mathf.Max(b.spec.footprint.x, b.spec.footprint.y)
                .CompareTo(Mathf.Max(a.spec.footprint.x, a.spec.footprint.y)));

            foreach (var pair in sorted)
            {
                SpawnOuterChunkEntry(grid, random, pair.spec, pair.entry.count);
            }
        }

        void SpawnOuterChunkEntry(TileGrid grid, System.Random random, LandmarkSpec spec, int count)
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
                var offset = RandomOuterChunkOffset(random);
                var cell = RandomCellInChunk(grid, random, offset.x, offset.y, footprint);
                if (!CanUseChunkCell(grid, cell, footprint)) continue;
                CreateLandmark(grid, spec, sprite, cell, footprint);
                placed++;
            }
        }

        static Vector2Int RandomOuterChunkOffset(System.Random random)
        {
            int index = random.Next(0, 8);
            switch (index)
            {
                case 0: return new Vector2Int(-1, -1);
                case 1: return new Vector2Int(0, -1);
                case 2: return new Vector2Int(1, -1);
                case 3: return new Vector2Int(-1, 0);
                case 4: return new Vector2Int(1, 0);
                case 5: return new Vector2Int(-1, 1);
                case 6: return new Vector2Int(0, 1);
                default: return new Vector2Int(1, 1);
            }
        }

        void SpawnCenterChunkResources(TileGrid grid, System.Random random, Dictionary<string, LandmarkSpec> specsByName)
        {
            SpawnCenterBandEntry(grid, random, specsByName, "Tree1", 3, 10, 20);
            SpawnCenterBandEntry(grid, random, specsByName, "Rock1", 3, 10, 20);
        }

        void SpawnCenterBandEntry(TileGrid grid, System.Random random, Dictionary<string, LandmarkSpec> specsByName, string landmarkName, int count, int minDistance, int maxDistance)
        {
            if (grid == null || specsByName == null || !specsByName.TryGetValue(landmarkName, out var spec)) return;
            var sprite = LoadSprite(spec.resourcePath);
            if (sprite == null) return;

            var footprint = NormalizeFootprint(spec.footprint);
            int placed = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(1, count * Mathf.Max(1, maxPlacementAttemptsPerObject));
            while (placed < count && attempts++ < maxAttempts)
            {
                var cell = RandomCellInChunk(grid, random, 0, footprint);
                var band = new PlacementBand { minDistanceCells = minDistance, maxDistanceCells = maxDistance };
                if (!CanUseCell(grid, cell, footprint, band)) continue;
                CreateLandmark(grid, spec, sprite, cell, footprint);
                placed++;
            }
        }

        void SpawnChunkResources(TileGrid grid, System.Random random, Dictionary<string, LandmarkSpec> specsByName, int chunkOffsetY, PlacementEntry[] entries)
        {
            if (grid == null || specsByName == null || entries == null) return;
            var sorted = new List<(PlacementEntry entry, LandmarkSpec spec)>();
            foreach (var entry in entries)
            {
                if (entry == null || entry.count <= 0 || string.IsNullOrEmpty(entry.landmarkName)) continue;
                if (specsByName.TryGetValue(entry.landmarkName, out var spec)) sorted.Add((entry, spec));
            }

            sorted.Sort((a, b) => Mathf.Max(b.spec.footprint.x, b.spec.footprint.y)
                .CompareTo(Mathf.Max(a.spec.footprint.x, a.spec.footprint.y)));
            foreach (var pair in sorted)
            {
                SpawnChunkEntry(grid, random, pair.spec, chunkOffsetY, pair.entry.count);
            }
        }

        void SpawnChunkEntry(TileGrid grid, System.Random random, LandmarkSpec spec, int chunkOffsetY, int count)
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
                var cell = RandomCellInChunk(grid, random, chunkOffsetY, footprint);
                if (!CanUseChunkCell(grid, cell, footprint)) continue;
                CreateLandmark(grid, spec, sprite, cell, footprint);
                placed++;
            }
        }

        Vector3Int RandomCellInChunk(TileGrid grid, System.Random random, int chunkOffsetY, Vector2Int footprint)
        {
            return RandomCellInChunk(grid, random, 0, chunkOffsetY, footprint);
        }

        Vector3Int RandomCellInChunk(TileGrid grid, System.Random random, int chunkOffsetX, int chunkOffsetY, Vector2Int footprint)
        {
            int chunkCells = Mathf.Max(1, grid.groundChunkCells);
            int columns = Mathf.Max(1, Mathf.CeilToInt(grid.width / (float)chunkCells));
            int rows = Mathf.Max(1, Mathf.CeilToInt(grid.height / (float)chunkCells));
            int centerColumn = columns / 2;
            int centerRow = rows / 2;
            int column = Mathf.Clamp(centerColumn + chunkOffsetX, 0, columns - 1);
            int row = Mathf.Clamp(centerRow + chunkOffsetY, 0, rows - 1);
            int pad = Mathf.Max(0, edgePaddingCells) + Mathf.Max(0, Mathf.Max(footprint.x, footprint.y) - 1);
            int minX = Mathf.Clamp(column * chunkCells + pad, 0, grid.width - 1);
            int maxX = Mathf.Clamp(Mathf.Min(grid.width, column * chunkCells + chunkCells) - pad, minX + 1, grid.width);
            int minY = Mathf.Clamp(row * chunkCells + pad, 0, grid.height - 1);
            int maxY = Mathf.Clamp(Mathf.Min(grid.height, row * chunkCells + chunkCells) - pad, minY + 1, grid.height);
            return grid.GridToCell(random.Next(minX, maxX), random.Next(minY, maxY));
        }

        bool CanUseChunkCell(TileGrid grid, Vector3Int originCell, Vector2Int footprint)
        {
            if (!grid.CanPlaceObject(originCell, footprint)) return false;
            if (OverlapsClearRoute(grid, originCell, footprint)) return false;
            if (!HasSeparation(grid, originCell, footprint)) return false;
            return true;
        }

        static PlacementEntry Entry(string landmarkName, int count)
        {
            return new PlacementEntry { landmarkName = landmarkName, count = count };
        }

        bool OverlapsClearRoute(TileGrid grid, Vector3Int originCell, Vector2Int footprint)
        {
            int routeCount = Mathf.Max(0, clearRouteCount);
            float halfWidth = Mathf.Max(0f, clearRouteHalfWidthCells);
            if (routeCount == 0 || halfWidth <= 0f) return false;
            if (!grid.TryCellToGrid(placementCenterCell, out var centerX, out var centerY)) return false;
            if (!grid.TryCellToGrid(originCell, out var x, out var y)) return false;

            var fromCenter = new Vector2(x - centerX, y - centerY);
            float protectedHalfWidth = halfWidth + Mathf.Max(footprint.x, footprint.y) * 0.5f;
            float angleStep = 360f / routeCount;
            for (int i = 0; i < routeCount; i++)
            {
                float radians = (clearRouteAngleOffsetDegrees + angleStep * i) * Mathf.Deg2Rad;
                var routeDirection = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                if (Vector2.Dot(fromCenter, routeDirection) < 0f) continue;
                float perpendicular = Mathf.Abs(routeDirection.x * fromCenter.y - routeDirection.y * fromCenter.x);
                if (perpendicular <= protectedHalfWidth) return true;
            }

            return false;
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
            int separation = Mathf.Max(2, separationCells);
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
            root.transform.position = GridObjectVisual.FootprintBottomCenterToWorld(grid, originCell, footprint);

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
            var gridVisual = root.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureFootprint(footprint);
            gridVisual.fitVisualWidthToFootprint = true;
            gridVisual.resetVisualOffset = true;

            var visualObject = new GameObject("Paper Visual");
            visualObject.transform.SetParent(root.transform, false);
            var visual = visualObject.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, 1000);
            visual.useBottomCenterAnchor = true;
            gridVisual.ApplyToVisual(visual, sprite, sprite.bounds.size);
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
            gridVisual.ConfigureFootprintBox(collider, false);
            collider.isTrigger = true;

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
            if (GeneratedSpriteLoader.IsGeneratedPath(resourcePath))
            {
                var sprite = GeneratedSpriteLoader.Load(resourcePath);
                if (sprite != null) return sprite;
            }

            var texture = GeneratedSpriteLoader.IsGeneratedPath(resourcePath)
                ? GeneratedSpriteLoader.LoadTexture(resourcePath)
                : Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f), 128f);
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
