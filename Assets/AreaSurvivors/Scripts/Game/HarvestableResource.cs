using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class HarvestableResource : MonoBehaviour
    {
        const int HarvestGaugeSortingOrder = 22010;
        const int HarvestToolSortingOrder = 22020;
        static readonly Vector3 HarvestToolScale = Vector3.one * 0.58f;

        public ResourceType resourceType;
        public int maxAmount = 100;
        public int remainingAmount = 100;
        public float harvestInterval = 1f;
        public int amountPerTick = 2;
        public TileGrid grid;
        public Vector3Int originCell;
        public Vector2Int footprint = Vector2Int.one;
        public Sprite pickaxeSprite;
        public Sprite resourceIcon;

        Collider2D resourceCollider;
        PaperMeshVisual pickaxeVisual;
        RectTransform gaugeRoot;
        Image gaugeFill;
        float harvestTimer;
        bool harvesting;

        public void Configure(GameConfig config, TileGrid tileGrid, Vector3Int origin, Vector2Int objectFootprint, ResourceType type, int amount)
        {
            grid = tileGrid;
            originCell = origin;
            footprint = new Vector2Int(Mathf.Max(1, objectFootprint.x), Mathf.Max(1, objectFootprint.y));
            resourceType = type;
            maxAmount = Mathf.Max(1, amount);
            remainingAmount = maxAmount;
            if (config != null)
            {
                harvestInterval = Mathf.Max(0.05f, config.harvestIntervalSeconds);
                amountPerTick = Mathf.Max(1, config.harvestAmountPerTick);
            }
        }

        void Awake()
        {
            resourceCollider = GetComponent<Collider2D>();
        }

        void Start()
        {
            if (pickaxeSprite == null) pickaxeSprite = LoadGeneratedSprite(resourceType == ResourceType.Wood ? "Axe" : "Pickaxe");
            if (resourceIcon == null)
            {
                resourceIcon = LoadGeneratedSprite(resourceType == ResourceType.Wood ? "WoodIcon" : "StoneIcon");
            }
            EnsurePickaxe();
            EnsureGauge();
            SetHarvestUiVisible(false);
        }

        void Update()
        {
            bool canHarvest = CanHarvestNow();
            SetHarvestUiVisible(canHarvest);
            if (!canHarvest)
            {
                harvesting = false;
                harvestTimer = 0f;
                return;
            }

            harvesting = true;
            harvestTimer += Time.deltaTime;
            AnimatePickaxe();
            UpdateGauge();
            if (harvestTimer < harvestInterval) return;

            harvestTimer -= harvestInterval;
            HarvestTick();
        }

        bool CanHarvestNow()
        {
            if (Time.timeScale <= 0f || remainingAmount <= 0 || grid == null) return false;
            var manager = GameManager.Instance;
            var player = manager != null ? manager.Player : null;
            if (player == null) return false;
            if (!IsTouchingPlayer(player)) return false;
            return IsAdjacentToPlayerTerritory();
        }

        bool IsTouchingPlayer(PlayerController player)
        {
            if (resourceCollider == null || player == null) return false;
            var playerCollider = player.GetComponent<Collider2D>();
            if (playerCollider == null) return false;
            var distance = resourceCollider.Distance(playerCollider);
            return distance.distance <= 0.035f;
        }

        bool IsAdjacentToPlayerTerritory()
        {
            var min = MinCell(originCell, footprint);
            int maxX = min.x + footprint.x - 1;
            int maxY = min.y + footprint.y - 1;
            for (int y = min.y - 1; y <= maxY + 1; y++)
            {
                for (int x = min.x - 1; x <= maxX + 1; x++)
                {
                    bool inside = x >= min.x && x <= maxX && y >= min.y && y <= maxY;
                    if (inside) continue;
                    var cell = new Vector3Int(x, y, originCell.z);
                    if (grid.ContainsCell(cell) && grid.GetOwner(cell) == TileOwner.Player) return true;
                }
            }

            return false;
        }

        void HarvestTick()
        {
            int amount = Mathf.Min(Mathf.Max(1, amountPerTick), remainingAmount);
            remainingAmount -= amount;
            var manager = GameManager.Instance;
            if (manager != null) manager.AddResource(resourceType, amount);
            HarvestResourcePopup.Show(transform.position + Vector3.up * PopupHeight(), amount, resourceIcon, ResourceColor());
            UpdateGauge();

            if (remainingAmount <= 0)
            {
                if (grid != null) grid.ClearObject(originCell);
                Destroy(gameObject);
            }
        }

        void EnsurePickaxe()
        {
            if (pickaxeSprite == null || pickaxeVisual != null) return;
            var go = new GameObject(resourceType == ResourceType.Wood ? "Axe" : "Pickaxe");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, PopupHeight() - 0.18f, 0f);
            go.transform.localScale = HarvestToolScale;
            go.AddComponent<PaperBillboard>();
            pickaxeVisual = go.AddComponent<PaperMeshVisual>();
            pickaxeVisual.Configure(pickaxeSprite, Color.white, HarvestToolSortingOrder);
            var outline = go.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.022f;
        }

        void EnsureGauge()
        {
            if (gaugeRoot != null) return;
            var canvas = new GameObject("Harvest Gauge").AddComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = HarvestGaugeSortingOrder;
            canvas.gameObject.AddComponent<PaperBillboard>();
            gaugeRoot = canvas.GetComponent<RectTransform>();
            gaugeRoot.localPosition = new Vector3(0f, PopupHeight() - 0.42f, 0f);
            gaugeRoot.sizeDelta = new Vector2(0.86f, 0.12f);
            gaugeRoot.localScale = Vector3.one;

            var background = new GameObject("Background").AddComponent<Image>();
            background.transform.SetParent(gaugeRoot, false);
            background.color = new Color(0.02f, 0.025f, 0.02f, 0.82f);
            background.raycastTarget = false;
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = Vector2.zero;
            background.rectTransform.offsetMax = Vector2.zero;

            gaugeFill = new GameObject("Fill").AddComponent<Image>();
            gaugeFill.transform.SetParent(gaugeRoot, false);
            gaugeFill.color = ResourceColor();
            gaugeFill.raycastTarget = false;
            gaugeFill.rectTransform.anchorMin = Vector2.zero;
            gaugeFill.rectTransform.anchorMax = Vector2.one;
            gaugeFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            gaugeFill.rectTransform.offsetMin = new Vector2(0.02f, 0.02f);
            gaugeFill.rectTransform.offsetMax = new Vector2(-0.02f, -0.02f);
        }

        void SetHarvestUiVisible(bool visible)
        {
            if (pickaxeVisual != null) pickaxeVisual.visible = visible;
            if (gaugeRoot != null) gaugeRoot.gameObject.SetActive(visible);
            if (visible && !harvesting) UpdateGauge();
        }

        void AnimatePickaxe()
        {
            if (pickaxeVisual == null) return;
            float swing = Mathf.Sin(Time.time * 13f);
            pickaxeVisual.transform.localRotation = Quaternion.Euler(0f, 0f, -22f + swing * 20f);
            pickaxeVisual.transform.localPosition = new Vector3(0f, PopupHeight() - 0.04f + Mathf.Abs(swing) * 0.035f, 0f);
            pickaxeVisual.transform.localScale = HarvestToolScale;
        }

        void UpdateGauge()
        {
            if (gaugeFill == null) return;
            float normalized = maxAmount <= 0 ? 0f : Mathf.Clamp01((float)remainingAmount / maxAmount);
            gaugeFill.rectTransform.anchorMax = new Vector2(normalized, 1f);
        }

        float PopupHeight()
        {
            if (resourceCollider == null) return 1f;
            return Mathf.Max(0.85f, resourceCollider.bounds.size.y + 0.42f);
        }

        Color ResourceColor()
        {
            return resourceType == ResourceType.Wood
                ? new Color(0.92f, 0.62f, 0.25f, 1f)
                : new Color(0.78f, 0.80f, 0.82f, 1f);
        }

        static Vector3Int MinCell(Vector3Int origin, Vector2Int size)
        {
            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            return new Vector3Int(origin.x - (size.x - 1) / 2, origin.y - (size.y - 1) / 2, origin.z);
        }

        static Sprite LoadGeneratedSprite(string name)
        {
            var sprite = Resources.Load<Sprite>("Generated/" + name);
            if (sprite != null) return sprite;
            var texture = Resources.Load<Texture2D>("Generated/" + name);
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 128f);
        }
    }
}
