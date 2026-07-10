using UnityEngine;

namespace AreaSurvivors
{
    public sealed class GridObjectMarker : MonoBehaviour
    {
        public GridObjectType type = GridObjectType.Empty;
        public GridCellFlags flags = GridCellFlags.BlocksBuilding;
        public Vector2Int footprint = Vector2Int.one;

        public bool Register(TileGrid grid)
        {
            if (grid == null || grid.groundTilemap == null) return false;
            var visual = GetComponent<GridObjectVisual>();
            var cell = visual != null && visual.HasGridOrigin
                ? visual.GridOriginCell
                : grid.groundTilemap.WorldToCell(transform.position);
            return grid.TryRegisterObject(cell, type, flags, gameObject, footprint);
        }
    }
}
