using UnityEngine;

namespace AreaSurvivors
{
    public sealed class MapPerimeterController : MonoBehaviour
    {
        public TileGrid grid;

        [Header("Boundary")]
        [Min(0.5f)]
        public float wallThicknessCells = 4f;

        [Header("Provisional Visuals")]
        public bool showVisuals = true;
        public string perimeterChunkResourcePath = "Generated/MapChunks/PerimeterChunk";
        public string forestResourcePath = "Generated/Landmarks/Forest8";
        public string mountainResourcePath = "Generated/Landmarks/Rock8";
        [Min(1f)]
        public float visualSpacingCells = 7f;
        [Min(0f)]
        public float visualOutsideOffsetCells = 3f;
        [Min(0.1f)]
        public float visualScale = 0.9f;
        [Range(0.5f, 1f)]
        public float visualOverlap = 0.78f;
        [Min(1)]
        public int visualRows = 2;
        [Min(0f)]
        public float rowSpacingCells = 4f;
        public int visualSortingOrder = 800;

        const string GeneratedRootName = "Perimeter Content";
        const string LegacyGeneratedRootName = "Generated Perimeter";

        [ContextMenu("Rebuild Perimeter")]
        public void Rebuild()
        {
            if (grid == null) grid = FindObjectOfType<TileGrid>();
            if (grid == null || grid.groundTilemap == null || grid.width <= 0 || grid.height <= 0)
            {
                Debug.LogWarning("Map perimeter requires a configured TileGrid.", this);
                return;
            }

            ClearGenerated();

            var generated = new GameObject(GeneratedRootName);
            generated.transform.SetParent(transform, false);

            CalculateMapBounds(out var center, out var size, out var rightStep, out var upStep);
            CreateBoundary(generated.transform, center, size, rightStep, upStep);
            if (showVisuals) CreateVisuals(generated.transform, center, size, rightStep, upStep);
        }

        [ContextMenu("Clear Generated Perimeter")]
        public void ClearGenerated()
        {
            var existing = transform.Find(GeneratedRootName);
            if (existing != null) DestroyUnityObject(existing.gameObject);
            var legacy = transform.Find(LegacyGeneratedRootName);
            if (legacy != null) DestroyUnityObject(legacy.gameObject);
        }

        public void CalculateMapBounds(out Vector3 center, out Vector2 size, out Vector3 rightStep, out Vector3 upStep)
        {
            Vector3 firstCenter = grid.groundTilemap.GetCellCenterWorld(grid.GridToCell(0, 0));
            rightStep = grid.width > 1
                ? grid.groundTilemap.GetCellCenterWorld(grid.GridToCell(1, 0)) - firstCenter
                : new Vector3(grid.cellSize, 0f, 0f);
            upStep = grid.height > 1
                ? grid.groundTilemap.GetCellCenterWorld(grid.GridToCell(0, 1)) - firstCenter
                : new Vector3(0f, grid.cellSize, 0f);
            Vector3 lastCenter = grid.groundTilemap.GetCellCenterWorld(grid.GridToCell(grid.width - 1, grid.height - 1));
            center = (firstCenter + lastCenter) * 0.5f;
            size = new Vector2(Mathf.Abs(rightStep.x) * grid.width, Mathf.Abs(upStep.y) * grid.height);
        }

        void CreateBoundary(Transform parent, Vector3 center, Vector2 mapSize, Vector3 rightStep, Vector3 upStep)
        {
            var boundaryRoot = new GameObject("Boundary Colliders");
            boundaryRoot.transform.SetParent(parent, false);

            float thicknessX = Mathf.Max(0.1f, Mathf.Abs(rightStep.x) * wallThicknessCells);
            float thicknessY = Mathf.Max(0.1f, Mathf.Abs(upStep.y) * wallThicknessCells);
            CreateWall(boundaryRoot.transform, "North", center + Vector3.up * (mapSize.y + thicknessY) * 0.5f,
                new Vector2(mapSize.x + thicknessX * 2f, thicknessY));
            CreateWall(boundaryRoot.transform, "South", center + Vector3.down * (mapSize.y + thicknessY) * 0.5f,
                new Vector2(mapSize.x + thicknessX * 2f, thicknessY));
            CreateWall(boundaryRoot.transform, "East", center + Vector3.right * (mapSize.x + thicknessX) * 0.5f,
                new Vector2(thicknessX, mapSize.y + thicknessY * 2f));
            CreateWall(boundaryRoot.transform, "West", center + Vector3.left * (mapSize.x + thicknessX) * 0.5f,
                new Vector2(thicknessX, mapSize.y + thicknessY * 2f));
        }

        void CreateWall(Transform parent, string wallName, Vector3 position, Vector2 size)
        {
            var wall = new GameObject(wallName);
            wall.layer = gameObject.layer;
            wall.transform.SetParent(parent, true);
            wall.transform.position = position;
            var collider = wall.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        void CreateVisuals(Transform parent, Vector3 center, Vector2 mapSize, Vector3 rightStep, Vector3 upStep)
        {
            var perimeterChunk = LoadSprite(perimeterChunkResourcePath);
            if (perimeterChunk != null)
            {
                CreatePerimeterChunkVisuals(parent, perimeterChunk, center, mapSize, rightStep, upStep);
                return;
            }

            var forest = LoadSprite(forestResourcePath);
            var mountain = LoadSprite(mountainResourcePath);
            if (forest == null && mountain == null) return;

            var visualRoot = new GameObject("Visuals");
            visualRoot.transform.SetParent(parent, false);

            float spacingX = VisualStep(forest, mountain, true, Mathf.Abs(rightStep.x) * visualSpacingCells);
            float spacingY = VisualStep(forest, mountain, false, Mathf.Abs(upStep.y) * visualSpacingCells);
            float offsetX = mapSize.x * 0.5f + Mathf.Abs(rightStep.x) * visualOutsideOffsetCells;
            float offsetY = mapSize.y * 0.5f + Mathf.Abs(upStep.y) * visualOutsideOffsetCells;

            int horizontalCount = Mathf.CeilToInt((mapSize.x + spacingX * 2f) / spacingX) + 2;
            int verticalCount = Mathf.CeilToInt((mapSize.y + spacingY * 2f) / spacingY) + 2;
            int rows = Mathf.Max(1, visualRows);
            for (int row = 0; row < rows; row++)
            {
                float rowX = row % 2 == 0 ? 0f : spacingX * 0.5f;
                float rowY = row % 2 == 0 ? 0f : spacingY * 0.5f;
                float outsideX = offsetX + row * Mathf.Abs(rightStep.x) * rowSpacingCells;
                float outsideY = offsetY + row * Mathf.Abs(upStep.y) * rowSpacingCells;

                for (int i = 0; i < horizontalCount; i++)
                {
                    float x = center.x + (i - (horizontalCount - 1) * 0.5f) * spacingX + rowX;
                    CreateVisual(visualRoot.transform, $"North {row} {i}", PickSprite(forest, mountain, i + row), new Vector3(x, center.y + outsideY, center.z));
                    CreateVisual(visualRoot.transform, $"South {row} {i}", PickSprite(forest, mountain, i + row + 1), new Vector3(x, center.y - outsideY, center.z));
                }

                for (int i = 0; i < verticalCount; i++)
                {
                    float y = center.y + (i - (verticalCount - 1) * 0.5f) * spacingY + rowY;
                    CreateVisual(visualRoot.transform, $"East {row} {i}", PickSprite(forest, mountain, i + row + 2), new Vector3(center.x + outsideX, y, center.z));
                    CreateVisual(visualRoot.transform, $"West {row} {i}", PickSprite(forest, mountain, i + row + 3), new Vector3(center.x - outsideX, y, center.z));
                }
            }
        }

        void CreatePerimeterChunkVisuals(Transform parent, Sprite sprite, Vector3 center, Vector2 mapSize, Vector3 rightStep, Vector3 upStep)
        {
            if (sprite == null || grid == null) return;

            var visualRoot = new GameObject("Visuals");
            visualRoot.transform.SetParent(parent, false);

            int chunkCells = Mathf.Max(1, grid.groundChunkCells);
            float chunkWidth = Mathf.Abs(rightStep.x) * chunkCells;
            float chunkHeight = Mathf.Abs(upStep.y) * chunkCells;
            int mapColumns = Mathf.Max(1, Mathf.CeilToInt(grid.width / (float)chunkCells));
            int mapRows = Mathf.Max(1, Mathf.CeilToInt(grid.height / (float)chunkCells));
            int rows = Mathf.Max(1, visualRows);

            int horizontalCount = mapColumns + rows * 2;
            for (int layer = 0; layer < rows; layer++)
            {
                float yNorth = center.y + mapSize.y * 0.5f + chunkHeight * (0.5f + layer);
                float ySouth = center.y - mapSize.y * 0.5f - chunkHeight * (0.5f + layer);
                for (int i = 0; i < horizontalCount; i++)
                {
                    float x = center.x + (i - (horizontalCount - 1) * 0.5f) * chunkWidth;
                    CreateChunkVisual(visualRoot.transform, $"North Chunk {layer} {i}", sprite, new Vector3(x, yNorth, center.z), chunkWidth, chunkHeight);
                    CreateChunkVisual(visualRoot.transform, $"South Chunk {layer} {i}", sprite, new Vector3(x, ySouth, center.z), chunkWidth, chunkHeight);
                }

                float xEast = center.x + mapSize.x * 0.5f + chunkWidth * (0.5f + layer);
                float xWest = center.x - mapSize.x * 0.5f - chunkWidth * (0.5f + layer);
                for (int i = 0; i < mapRows; i++)
                {
                    float y = center.y + (i - (mapRows - 1) * 0.5f) * chunkHeight;
                    CreateChunkVisual(visualRoot.transform, $"East Chunk {layer} {i}", sprite, new Vector3(xEast, y, center.z), chunkWidth, chunkHeight);
                    CreateChunkVisual(visualRoot.transform, $"West Chunk {layer} {i}", sprite, new Vector3(xWest, y, center.z), chunkWidth, chunkHeight);
                }
            }
        }

        float VisualStep(Sprite forest, Sprite mountain, bool horizontal, float fallback)
        {
            float forestSize = SpriteSize(forest, horizontal);
            float mountainSize = SpriteSize(mountain, horizontal);
            float size = forestSize > 0f && mountainSize > 0f
                ? Mathf.Min(forestSize, mountainSize)
                : Mathf.Max(forestSize, mountainSize);
            return size > 0f ? Mathf.Max(0.1f, size * visualScale * visualOverlap) : Mathf.Max(0.1f, fallback);
        }

        static float SpriteSize(Sprite sprite, bool horizontal)
        {
            if (sprite == null) return 0f;
            return horizontal ? sprite.bounds.size.x : sprite.bounds.size.y;
        }

        void CreateVisual(Transform parent, string visualName, Sprite sprite, Vector3 position)
        {
            if (sprite == null) return;
            var root = new GameObject(visualName);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            root.transform.localScale = Vector3.one * visualScale;

            var visual = root.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, visualSortingOrder);
            visual.useBottomCenterAnchor = true;
            var billboard = root.AddComponent<PaperBillboard>();
            billboard.faceCamera = true;
        }

        void CreateChunkVisual(Transform parent, string visualName, Sprite sprite, Vector3 position, float width, float height)
        {
            if (sprite == null) return;
            var root = new GameObject(visualName);
            root.transform.SetParent(parent, true);
            root.transform.position = position;

            var visual = root.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, visualSortingOrder);
            var bounds = sprite.bounds.size;
            if (bounds.x > 0.001f && bounds.y > 0.001f)
            {
                root.transform.localScale = new Vector3(width / bounds.x, height / bounds.y, 1f);
            }
        }

        static Sprite PickSprite(Sprite forest, Sprite mountain, int index)
        {
            if (forest == null) return mountain;
            if (mountain == null) return forest;
            return index % 3 == 0 ? mountain : forest;
        }

        static Sprite LoadSprite(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (GeneratedSpriteLoader.IsGeneratedPath(resourcePath))
            {
                var generatedSprite = GeneratedSpriteLoader.Load(resourcePath);
                if (generatedSprite != null) return generatedSprite;
            }

            var importedSprite = Resources.Load<Sprite>(resourcePath);
            if (importedSprite != null) return importedSprite;

            var texture = GeneratedSpriteLoader.IsGeneratedPath(resourcePath)
                ? GeneratedSpriteLoader.LoadTexture(resourcePath)
                : Resources.Load<Texture2D>(resourcePath);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0f), 128f);
        }

        static void DestroyUnityObject(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
