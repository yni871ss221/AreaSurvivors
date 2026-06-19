using UnityEngine;

namespace AreaSurvivors
{
    public sealed class BuildingPersistentState : MonoBehaviour
    {
        SavedBuildingData data;
        bool persistent;

        public void Configure(SavedBuildingData savedData)
        {
            data = savedData;
            persistent = data != null;
            ApplyDestroyedVisual(persistent && data.destroyed);
        }

        public void MarkUpgraded()
        {
            if (data == null) return;
            data.upgraded = true;
            ProgressionStore.Save();
        }

        public static void TryMarkUpgraded(GameObject target)
        {
            if (target == null) return;
            var state = target.GetComponent<BuildingPersistentState>();
            if (state == null || !state.persistent) return;
            state.MarkUpgraded();
        }

        public static bool TryMarkDestroyed(GameObject target, TileGrid grid, Vector3Int originCell)
        {
            if (target == null) return false;
            var state = target.GetComponent<BuildingPersistentState>();
            if (state == null || !state.persistent) return false;
            state.MarkDestroyed(grid, originCell);
            return true;
        }

        void MarkDestroyed(TileGrid grid, Vector3Int originCell)
        {
            if (data != null) data.destroyed = true;
            ProgressionStore.Save();
            if (grid != null) grid.ClearObject(originCell);
            ApplyDestroyedVisual(true);
        }

        void ApplyDestroyedVisual(bool destroyed)
        {
            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour == this) continue;
                if (behaviour is BallistaTower ||
                    behaviour is WoodenBarrier ||
                    behaviour is CarpenterHut ||
                    behaviour is WorkerHut ||
                    behaviour is WatchTower ||
                    behaviour is BuildingUpgradeTarget)
                {
                    behaviour.enabled = !destroyed;
                }
            }

            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
            {
                collider.enabled = !destroyed;
            }

            foreach (var renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.color = destroyed ? new Color(0.38f, 0.38f, 0.38f, 0.46f) : Color.white;
            }

            foreach (var visual in GetComponentsInChildren<PaperMeshVisual>(true))
            {
                visual.color = destroyed ? new Color(0.38f, 0.38f, 0.38f, 0.46f) : Color.white;
            }
        }
    }
}
