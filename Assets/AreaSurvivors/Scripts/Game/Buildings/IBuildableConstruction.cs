using UnityEngine;

namespace AreaSurvivors
{
    public interface IBuildableConstruction
    {
        bool IsBuilt { get; }
        TileGrid Grid { get; }
        Vector3Int OriginCell { get; }
        Vector2Int Footprint { get; }
        void CompleteImmediately();
        void RestoreAfterRevive();
    }
}
