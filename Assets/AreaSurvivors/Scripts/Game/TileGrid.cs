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

        TileOwner[,] owners;

        public void Build()
        {
            owners = new TileOwner[width, height];
            groundTilemap.ClearAllTiles();
            paintTilemap.ClearAllTiles();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var cell = GridToCell(x, y);
                    groundTilemap.SetTile(cell, groundTile);
                    groundTilemap.SetColor(cell, Color.white);
                }
            }
        }

        public Vector3 GridToWorld(int x, int y)
        {
            return groundTilemap.GetCellCenterWorld(GridToCell(x, y));
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

        public TileOwner GetOwner(Vector3 world)
        {
            int x, y;
            if (!TryWorldToGrid(world, out x, out y)) return TileOwner.Neutral;
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
                    paintTilemap.SetColor(cell, owner == TileOwner.Player ? player : enemy);
                }
            }
        }
    }
}
