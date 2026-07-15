using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class BuildingPersistentState : MonoBehaviour
    {
        const int DestroyedSortingOffset = -1600;
        const float RevivePlayerCollisionClearance = 0.05f;

        readonly struct IgnoredCollisionPair
        {
            public readonly Collider2D buildingCollider;
            public readonly Collider2D playerCollider;

            public IgnoredCollisionPair(Collider2D buildingCollider, Collider2D playerCollider)
            {
                this.buildingCollider = buildingCollider;
                this.playerCollider = playerCollider;
            }
        }

        SavedBuildingData data;
        bool persistent;
        bool hasOriginalSortingOffset;
        int originalSortingOffset;
        readonly List<IgnoredCollisionPair> reviveIgnoredCollisions = new List<IgnoredCollisionPair>();
        Coroutine reviveCollisionRoutine;

        public void Configure(SavedBuildingData savedData)
        {
            data = savedData;
            persistent = data != null;
            CaptureOriginalSortingOffset();
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

        public static int ReviveDestroyedBuildings(TileGrid grid, float healthRatio)
        {
            int revived = 0;
            var states = FindObjectsOfType<BuildingPersistentState>(true);
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] != null && states[i].TryRevive(grid, healthRatio)) revived++;
            }

            if (revived > 0) ProgressionStore.Save();
            return revived;
        }

        void MarkDestroyed(TileGrid grid, Vector3Int originCell)
        {
            RestoreRevivePlayerCollisions();
            if (data != null) data.destroyed = true;
            ProgressionStore.Save();
            if (grid != null) grid.ClearObject(originCell);
            ApplyDestroyedVisual(true);
        }

        void ApplyDestroyedVisual(bool destroyed)
        {
            bool hasDestroyedVisual = false;
            var visualSet = GetComponent<BuildingPrefabVisualSet>();
            if (destroyed)
            {
                hasDestroyedVisual = visualSet != null && visualSet.ApplyDestroyedVisual(data != null && data.upgraded);
            }
            else if (visualSet != null)
            {
                hasDestroyedVisual = visualSet.ApplyCompleteVisual(data != null && data.upgraded);
            }

            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour == this) continue;
                if (behaviour is BallistaTower ||
                    behaviour is WoodenBarrier ||
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
                renderer.color = destroyed && !hasDestroyedVisual ? new Color(0.38f, 0.38f, 0.38f, 0.46f) : Color.white;
            }

            foreach (var visual in GetComponentsInChildren<PaperMeshVisual>(true))
            {
                visual.color = destroyed && !hasDestroyedVisual ? new Color(0.38f, 0.38f, 0.38f, 0.46f) : Color.white;
            }

            ApplyDestroyedSorting(destroyed);
        }

        bool TryRevive(TileGrid grid, float healthRatio)
        {
            if (!persistent || data == null || !data.destroyed) return false;

            var marker = GetComponent<GridObjectMarker>();
            if (grid != null && marker != null)
            {
                var originCell = new Vector3Int(data.x, data.y, 0);
                if (!grid.TryRegisterObject(originCell, marker.type, marker.flags, gameObject, marker.footprint))
                {
                    return false;
                }
            }

            data.destroyed = false;
            ApplyDestroyedVisual(false);
            BeginRevivePlayerCollisionGrace();

            var health = GetComponent<Health>();
            if (health != null)
            {
                health.currentHp = Mathf.Clamp(Mathf.RoundToInt(health.maxHp * Mathf.Clamp01(healthRatio)), 1, health.maxHp);
            }

            return true;
        }

        void BeginRevivePlayerCollisionGrace()
        {
            RestoreRevivePlayerCollisions();

            var player = GameManager.Instance != null ? GameManager.Instance.Player : FindObjectOfType<PlayerController>();
            if (player == null) return;

            var buildingColliders = GetComponentsInChildren<Collider2D>(true);
            var playerColliders = player.GetComponentsInChildren<Collider2D>(true);
            Physics2D.SyncTransforms();

            for (int buildingIndex = 0; buildingIndex < buildingColliders.Length; buildingIndex++)
            {
                var buildingCollider = buildingColliders[buildingIndex];
                if (buildingCollider == null || buildingCollider.isTrigger || !buildingCollider.enabled) continue;

                for (int playerIndex = 0; playerIndex < playerColliders.Length; playerIndex++)
                {
                    var playerCollider = playerColliders[playerIndex];
                    if (playerCollider == null || playerCollider.isTrigger) continue;

                    Physics2D.IgnoreCollision(buildingCollider, playerCollider, true);
                    if (playerCollider.enabled && IsCollisionPairClear(buildingCollider, playerCollider))
                    {
                        Physics2D.IgnoreCollision(buildingCollider, playerCollider, false);
                        continue;
                    }

                    reviveIgnoredCollisions.Add(new IgnoredCollisionPair(buildingCollider, playerCollider));
                }
            }

            if (reviveIgnoredCollisions.Count > 0)
            {
                reviveCollisionRoutine = StartCoroutine(RestoreRevivePlayerCollisionsWhenClear());
            }
        }

        IEnumerator RestoreRevivePlayerCollisionsWhenClear()
        {
            while (reviveIgnoredCollisions.Count > 0)
            {
                for (int i = reviveIgnoredCollisions.Count - 1; i >= 0; i--)
                {
                    var pair = reviveIgnoredCollisions[i];
                    if (pair.buildingCollider == null || pair.playerCollider == null)
                    {
                        reviveIgnoredCollisions.RemoveAt(i);
                        continue;
                    }

                    if (!pair.buildingCollider.enabled || !pair.playerCollider.enabled) continue;
                    if (!IsCollisionPairClear(pair.buildingCollider, pair.playerCollider)) continue;

                    Physics2D.IgnoreCollision(pair.buildingCollider, pair.playerCollider, false);
                    reviveIgnoredCollisions.RemoveAt(i);
                }

                yield return null;
            }

            reviveCollisionRoutine = null;
        }

        static bool IsCollisionPairClear(Collider2D buildingCollider, Collider2D playerCollider)
        {
            var distance = buildingCollider.Distance(playerCollider);
            return !distance.isOverlapped && distance.distance >= RevivePlayerCollisionClearance;
        }

        void RestoreRevivePlayerCollisions()
        {
            if (reviveCollisionRoutine != null)
            {
                StopCoroutine(reviveCollisionRoutine);
                reviveCollisionRoutine = null;
            }

            for (int i = 0; i < reviveIgnoredCollisions.Count; i++)
            {
                var pair = reviveIgnoredCollisions[i];
                if (pair.buildingCollider != null && pair.playerCollider != null)
                {
                    Physics2D.IgnoreCollision(pair.buildingCollider, pair.playerCollider, false);
                }
            }

            reviveIgnoredCollisions.Clear();
        }

        void OnDisable()
        {
            RestoreRevivePlayerCollisions();
        }

        void CaptureOriginalSortingOffset()
        {
            if (hasOriginalSortingOffset) return;
            var ySort = GetComponent<YSort>();
            if (ySort == null) return;
            originalSortingOffset = ySort.orderOffset;
            hasOriginalSortingOffset = true;
        }

        void ApplyDestroyedSorting(bool destroyed)
        {
            var ySort = GetComponent<YSort>();
            if (ySort == null) return;
            CaptureOriginalSortingOffset();
            ySort.orderOffset = destroyed ? originalSortingOffset + DestroyedSortingOffset : originalSortingOffset;
            ySort.Apply();
        }
    }
}
