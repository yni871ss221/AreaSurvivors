using UnityEngine;

namespace AreaSurvivors
{
    public enum GridObjectVisualKind
    {
        FootprintObject,
        Character
    }

    public sealed class GridObjectVisual : MonoBehaviour
    {
        public GridObjectVisualKind kind = GridObjectVisualKind.FootprintObject;
        public Vector2Int footprint = Vector2Int.one;
        public bool fitVisualWidthToFootprint = true;
        public bool resetVisualOffset = true;
        public Vector3 visualOffset = Vector3.zero;
        public float characterFootRadiusScale = 0.42f;
        [Min(0f)] public float blockingColliderBottomInset;
        [Min(0f)] public float blockingColliderEdgeRadius;
        public PhysicsMaterial2D blockingColliderMaterial;
        [SerializeField] bool hasGridOrigin;
        [SerializeField] Vector3Int gridOriginCell;

        public const float CellWidth = 0.7f;
        public const float CellHeight = 0.7f;

        public Vector2 FootprintWorldSize
        {
            get
            {
                var normalized = NormalizeFootprint(footprint);
                return new Vector2(normalized.x * CellWidth, normalized.y * CellHeight);
            }
        }

        public bool HasGridOrigin => hasGridOrigin;
        public Vector3Int GridOriginCell => gridOriginCell;

        public void ConfigureFootprint(Vector2Int objectFootprint)
        {
            kind = GridObjectVisualKind.FootprintObject;
            footprint = NormalizeFootprint(objectFootprint);
            ApplyFootprintYSortPivot();
        }

        public void AlignRootToFootprint(TileGrid grid, Vector3Int originCell)
        {
            if (grid == null || grid.groundTilemap == null) return;
            gridOriginCell = originCell;
            hasGridOrigin = true;
            transform.position = FootprintBottomCenterToWorld(grid, originCell, footprint);
        }

        public void ClearGridOrigin()
        {
            hasGridOrigin = false;
        }

        public void ConfigureCharacter(float cellWidthLimit = 1f)
        {
            kind = GridObjectVisualKind.Character;
            footprint = new Vector2Int(Mathf.Max(1, Mathf.CeilToInt(cellWidthLimit)), 1);
            ApplyCharacterYSortPivot();
        }

        public void ApplyCharacterYSortPivot()
        {
            var ySort = GetComponent<YSort>();
            if (ySort == null) return;

            var footprint = GetComponent<CharacterFootprint>();
            if (footprint != null && footprint.FootCollider != null)
            {
                ySort.sortPivotOffsetY = footprint.BottomCenter.y - transform.position.y;
                ySort.Apply();
                return;
            }

            var footCollider = GetComponent<BoxCollider2D>();
            if (footCollider == null)
            {
                ySort.sortPivotOffsetY = 0f;
                return;
            }

            float scaleY = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.y));
            ySort.sortPivotOffsetY = (footCollider.offset.y - footCollider.size.y * 0.5f) * scaleY;
            ySort.Apply();
        }

        public void ApplyFootprintYSortPivot()
        {
            var ySort = GetComponent<YSort>();
            if (ySort == null) return;
            var normalized = NormalizeFootprint(footprint);
            ySort.sortPivotOffsetY = Mathf.Max(0, normalized.y - 1) * CellHeight * 0.5f;
            ySort.Apply();
        }

        public void ApplyToVisual(PaperMeshVisual visual, Sprite sprite, Vector2 fallbackSize)
        {
            ApplyToVisual(visual, sprite, fallbackSize, true);
        }

        public void ApplyToVisual(PaperMeshVisual visual, Sprite sprite, Vector2 fallbackSize, bool preserveAspect)
        {
            if (visual == null || sprite == null) return;
            visual.useBottomCenterAnchor = true;
            if (resetVisualOffset) visual.transform.localPosition = visualOffset;
            if (!fitVisualWidthToFootprint) return;

            var bounds = sprite.bounds.size;
            if (Mathf.Abs(bounds.x) <= 0.001f || Mathf.Abs(bounds.y) <= 0.001f) return;

            float targetWidth = Mathf.Max(0.01f, FootprintWorldSize.x);
            float scale = targetWidth / bounds.x;
            float fallbackHeight = Mathf.Max(0.01f, fallbackSize.y);
            float targetHeight = preserveAspect ? Mathf.Max(fallbackHeight, bounds.y * scale) : fallbackHeight;
            visual.transform.localScale = new Vector3(scale, targetHeight / bounds.y, 1f);
        }

        public void ApplyFootprintWidthPreserveAspect(PaperMeshVisual visual, Sprite sprite)
        {
            if (visual == null || sprite == null) return;
            visual.useBottomCenterAnchor = true;
            if (resetVisualOffset) visual.transform.localPosition = visualOffset;
            if (!fitVisualWidthToFootprint) return;

            var bounds = sprite.bounds.size;
            if (Mathf.Abs(bounds.x) <= 0.001f || Mathf.Abs(bounds.y) <= 0.001f) return;

            float targetWidth = Mathf.Max(0.01f, FootprintWorldSize.x);
            float scale = targetWidth / bounds.x;
            visual.transform.localScale = new Vector3(scale, scale, 1f);
        }

        public BoxCollider2D ConfigureFootprintBox(BoxCollider2D collider, bool isTrigger)
        {
            if (collider != null) return collider;

            collider = gameObject.AddComponent<BoxCollider2D>();
            var size = FootprintWorldSize;
            float bottomInset = isTrigger ? 0f : Mathf.Clamp(blockingColliderBottomInset, 0f, Mathf.Max(0f, size.y - 0.01f));
            var colliderSize = new Vector2(size.x, size.y - bottomInset);
            collider.isTrigger = isTrigger;
            collider.size = colliderSize;
            collider.offset = new Vector2(0f, bottomInset + colliderSize.y * 0.5f);
            if (!isTrigger)
            {
                collider.edgeRadius = blockingColliderEdgeRadius;
                if (blockingColliderMaterial != null) collider.sharedMaterial = blockingColliderMaterial;
            }
            return collider;
        }

        public CircleCollider2D ConfigureCharacterCircle(CircleCollider2D collider)
        {
            if (collider != null) return collider;

            collider = gameObject.AddComponent<CircleCollider2D>();
            float maxDiameter = Mathf.Max(0.1f, FootprintWorldSize.x);
            float worldRadius = Mathf.Min(maxDiameter * 0.5f, maxDiameter * Mathf.Clamp(characterFootRadiusScale, 0.1f, 0.5f));
            float scale = Mathf.Max(0.001f, Mathf.Abs(transform.lossyScale.x));
            collider.radius = worldRadius / scale;
            collider.offset = Vector2.zero;
            return collider;
        }

        public static Vector2Int NormalizeFootprint(Vector2Int value)
        {
            return new Vector2Int(Mathf.Max(1, value.x), Mathf.Max(1, value.y));
        }

        public static Vector3 FootprintBottomCenterToWorld(TileGrid grid, Vector3Int originCell, Vector2Int objectFootprint)
        {
            if (grid == null || grid.groundTilemap == null) return Vector3.zero;
            objectFootprint = NormalizeFootprint(objectFootprint);
            int minX = originCell.x - (objectFootprint.x - 1) / 2;
            int minY = originCell.y - (objectFootprint.y - 1) / 2;
            var min = new Vector3Int(minX, minY, originCell.z);
            var max = new Vector3Int(minX + objectFootprint.x - 1, minY, originCell.z);
            var bottomCenter = (grid.groundTilemap.GetCellCenterWorld(min) + grid.groundTilemap.GetCellCenterWorld(max)) * 0.5f;
            var up = grid.groundTilemap.GetCellCenterWorld(min + Vector3Int.up) - grid.groundTilemap.GetCellCenterWorld(min);
            return bottomCenter - up * 0.5f;
        }

        public static Vector3 FootprintOriginToWorld(TileGrid grid, Vector3Int originCell)
        {
            if (grid == null || grid.groundTilemap == null) return Vector3.zero;
            return grid.groundTilemap.GetCellCenterWorld(originCell);
        }
    }
}
