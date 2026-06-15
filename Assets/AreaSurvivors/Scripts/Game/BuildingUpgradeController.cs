using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class BuildingUpgradeController : MonoBehaviour
    {
        GameManager owner;
        GameConfig config;
        TileGrid grid;
        TowerController tower;
        Camera mainCamera;
        Image cursorIcon;
        Sprite upgradeIcon;
        Sprite cancelIcon;
        Sprite upgradedTowerSprite;
        bool active;
        TowerController hoverTower;

        public bool IsActive => active;

        public void Initialize(GameManager gameManager, GameConfig gameConfig, TileGrid tileGrid, TowerController centerTower, Canvas hudCanvas)
        {
            owner = gameManager;
            config = gameConfig;
            grid = tileGrid;
            tower = centerTower;
            mainCamera = Camera.main;
            upgradeIcon = LoadGeneratedSprite("UpgradeBuildingIcon");
            cancelIcon = LoadGeneratedSprite("CancelUpgradeIcon");
            upgradedTowerSprite = LoadGeneratedSprite("TowerUpgrade");
            EnsureCursorIcon(hudCanvas);
            SetActive(false);
        }

        void Update()
        {
            if (!active) return;
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                SetActive(false);
                return;
            }

            FollowCursor();
            UpdateHover();
            if (Input.GetMouseButtonDown(0) && !IsPointerOverUi()) ConfirmHover();
        }

        public void Toggle()
        {
            SetActive(!active);
        }

        public void SetActive(bool enabled)
        {
            active = enabled && IsUnlocked();
            if (active && owner != null && owner.buildPlacement != null) owner.buildPlacement.CancelActiveSelection();
            if (!active) ClearHover();
            if (cursorIcon != null) cursorIcon.gameObject.SetActive(active);
        }

        void UpdateHover()
        {
            var nextTower = FindTowerUnderCursor();
            if (nextTower != hoverTower)
            {
                ClearHover();
                hoverTower = nextTower;
            }

            if (hoverTower == null)
            {
                SetCursorSprite(upgradeIcon);
                return;
            }

            if (hoverTower.HasPendingUpgrade)
            {
                hoverTower.HideUpgradePreview();
                SetCursorSprite(cancelIcon != null ? cancelIcon : upgradeIcon);
                return;
            }

            bool canUpgrade = CanReserve(hoverTower);
            hoverTower.ShowUpgradePreview(upgradedTowerSprite, canUpgrade);
            SetCursorSprite(upgradeIcon);
        }

        void ConfirmHover()
        {
            if (hoverTower == null) return;
            if (hoverTower.HasPendingUpgrade)
            {
                hoverTower.CancelUpgradeReservation();
                SetActive(false);
                return;
            }
            if (!CanReserve(hoverTower)) return;
            if (owner == null || config == null) return;
            if (!owner.TrySpendResources(config.towerUpgradeWoodCost, config.towerUpgradeStoneCost)) return;
            if (!hoverTower.BeginUpgradeReservation(config, grid, owner, upgradedTowerSprite))
            {
                owner.AddResource(ResourceType.Wood, config.towerUpgradeWoodCost);
                owner.AddResource(ResourceType.Stone, config.towerUpgradeStoneCost);
                return;
            }
            SetActive(false);
        }

        bool CanReserve(TowerController target)
        {
            if (target == null || config == null || owner == null) return false;
            return target.CanStartUpgrade() &&
                owner.HasResources(config.towerUpgradeWoodCost, config.towerUpgradeStoneCost) &&
                IsUnlocked();
        }

        TowerController FindTowerUnderCursor()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return null;
            if (!TryGetPointerWorld(out var world)) return null;
            var hits = Physics2D.OverlapPointAll(world);
            foreach (var hit in hits)
            {
                if (hit == null) continue;
                var target = hit.GetComponentInParent<TowerController>();
                if (target != null) return target;
            }

            if (grid != null)
            {
                var cell = grid.WorldToCell(world);
                var record = grid.GetObject(cell);
                var target = record != null && record.instance != null ? record.instance.GetComponentInParent<TowerController>() : null;
                if (target != null) return target;
            }

            if (tower != null && IsInsideTowerUpgradeArea(world, tower)) return tower;
            return null;
        }

        bool TryGetPointerWorld(out Vector3 world)
        {
            world = Vector3.zero;
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return false;
            var ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            var ground = new Plane(Vector3.forward, Vector3.zero);
            if (!ground.Raycast(ray, out float distance)) return false;
            world = ray.GetPoint(distance);
            return true;
        }

        static bool IsInsideTowerUpgradeArea(Vector3 world, TowerController target)
        {
            if (target == null) return false;
            var colliders = target.GetComponentsInChildren<Collider2D>(true);
            foreach (var collider in colliders)
            {
                if (collider == null) continue;
                var bounds = collider.bounds;
                bounds.Expand(new Vector3(0.8f, 2.8f, 0f));
                if (bounds.Contains(new Vector3(world.x, world.y, bounds.center.z))) return true;
            }

            return Vector2.Distance(world, target.transform.position) <= 3.25f;
        }

        void ClearHover()
        {
            if (hoverTower != null) hoverTower.HideUpgradePreview();
            hoverTower = null;
        }

        bool IsUnlocked()
        {
            return ProgressionStore.IsUnlocked(UpgradeType.UnlockTowerUpgrade);
        }

        void EnsureCursorIcon(Canvas hudCanvas)
        {
            if (hudCanvas == null) return;
            var existing = hudCanvas.transform.Find("Upgrade Cursor Icon");
            cursorIcon = existing != null ? existing.GetComponent<Image>() : null;
            if (cursorIcon == null)
            {
                cursorIcon = new GameObject("Upgrade Cursor Icon").AddComponent<Image>();
                cursorIcon.transform.SetParent(hudCanvas.transform, false);
            }
            cursorIcon.sprite = upgradeIcon;
            cursorIcon.preserveAspect = true;
            cursorIcon.raycastTarget = false;
            cursorIcon.rectTransform.sizeDelta = new Vector2(44f, 44f);
            cursorIcon.transform.SetAsLastSibling();
        }

        void FollowCursor()
        {
            if (cursorIcon == null) return;
            cursorIcon.rectTransform.position = Input.mousePosition + new Vector3(18f, -18f, 0f);
        }

        void SetCursorSprite(Sprite sprite)
        {
            if (cursorIcon == null || sprite == null) return;
            cursorIcon.sprite = sprite;
        }

        static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
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
