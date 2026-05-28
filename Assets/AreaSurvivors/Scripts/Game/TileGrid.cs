using UnityEngine;

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
        public int width = 48;
        public int height = 48;
        public float cellSize = 0.7f;
        public Sprite tileSprite;
        public Sprite paintSprite;
        public Color neutral = new Color(0.43f, 0.58f, 0.31f);
        public Color player = new Color(0.24f, 0.55f, 0.95f, 0.52f);
        public Color enemy = new Color(0.85f, 0.25f, 0.22f, 0.50f);

        TileOwner[,] owners;
        SpriteRenderer[,] paintRenderers;

        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }
            owners = new TileOwner[width, height];
            paintRenderers = new SpriteRenderer[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var tile = new GameObject($"Tile_{x}_{y}");
                    tile.transform.SetParent(transform);
                    tile.transform.localPosition = GridToWorld(x, y);
                    var sr = tile.AddComponent<SpriteRenderer>();
                    sr.sprite = tileSprite;
                    sr.color = Color.white;
                    sr.sortingOrder = -20;

                    var paint = new GameObject("Paint");
                    paint.transform.SetParent(tile.transform, false);
                    paint.transform.localPosition = Vector3.zero;
                    var paintSr = paint.AddComponent<SpriteRenderer>();
                    paintSr.sprite = paintSprite != null ? paintSprite : tileSprite;
                    paintSr.color = new Color(1f, 1f, 1f, 0f);
                    paintSr.sortingOrder = -19;
                    paintRenderers[x, y] = paintSr;
                }
            }
        }

        public Vector3 GridToWorld(int x, int y)
        {
            return new Vector3((x - width * 0.5f) * cellSize, (y - height * 0.5f) * cellSize * 0.55f, 0f);
        }

        public bool TryWorldToGrid(Vector3 world, out int x, out int y)
        {
            x = Mathf.RoundToInt(world.x / cellSize + width * 0.5f);
            y = Mathf.RoundToInt(world.y / (cellSize * 0.55f) + height * 0.5f);
            return x >= 0 && y >= 0 && x < width && y < height;
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
                    paintRenderers[x, y].color = owner == TileOwner.Player ? player : owner == TileOwner.Enemy ? enemy : new Color(1f, 1f, 1f, 0f);
                }
            }
        }
    }
}
