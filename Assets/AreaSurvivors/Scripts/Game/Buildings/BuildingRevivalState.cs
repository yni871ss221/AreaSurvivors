using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class BuildingRevivalState : MonoBehaviour
    {
        const int DestroyedSortingOffset = -1600;
        const float RevivePlayerCollisionClearance = 0.05f;
        public const int RequiredRecoveryStableFixedUpdates = 2;

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

        Vector3Int originCell;
        bool upgraded;
        bool destroyed;
        bool configured;
        bool hasOriginalSortingOffset;
        int originalSortingOffset;
        readonly List<IgnoredCollisionPair> reviveIgnoredCollisions = new List<IgnoredCollisionPair>();
        Coroutine reviveCollisionRoutine;
        PlayerController revivePlayer;
        TileGrid reviveGrid;
        int reviveStableFixedUpdates;
        public bool IsDestroyed => destroyed;

        public void Configure(Vector3Int buildingOriginCell, bool isUpgraded)
        {
            originCell = buildingOriginCell;
            upgraded = isUpgraded;
            destroyed = false;
            configured = true;
            CaptureOriginalSortingOffset();
            ApplyDestroyedVisual(false);
        }

        public static bool TryHandleDestroyed(GameObject target, TileGrid grid, Vector3Int fallbackOriginCell)
        {
            if (target == null) return false;
            var state = target.GetComponent<BuildingRevivalState>();
            if (state == null || !state.configured) return false;
            state.MarkDestroyed(grid, fallbackOriginCell);
            return true;
        }

        public static int ReviveDestroyedBuildings(TileGrid grid, float healthRatio)
        {
            int revived = 0;
            var revivedStates = new List<BuildingRevivalState>();
            var states = FindObjectsOfType<BuildingRevivalState>(true);
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] == null || !states[i].TryRevive(grid, healthRatio)) continue;
                revived++;
                revivedStates.Add(states[i]);
            }

            if (revived > 0)
            {
                revivedStates[0].BeginRevivePlayerCollisionGrace(grid);
            }
            return revived;
        }

        void MarkDestroyed(TileGrid grid, Vector3Int fallbackOriginCell)
        {
            RestoreRevivePlayerCollisions();
            destroyed = true;
            if (!configured) originCell = fallbackOriginCell;
            if (grid != null) grid.ClearObject(originCell);
            ApplyDestroyedVisual(true);
        }

        void ApplyDestroyedVisual(bool destroyed)
        {
            bool hasDestroyedVisual = false;
            var visualSet = GetComponent<BuildingPrefabVisualSet>();
            if (destroyed)
            {
                hasDestroyedVisual = visualSet != null && visualSet.ApplyDestroyedVisual(upgraded);
            }
            else if (visualSet != null)
            {
                hasDestroyedVisual = visualSet.ApplyCompleteVisual(upgraded);
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
            if (!configured || !destroyed) return false;

            var marker = GetComponent<GridObjectMarker>();
            if (grid != null && marker != null)
            {
                if (!grid.TryRegisterObject(originCell, marker.type, marker.flags, gameObject, marker.footprint))
                {
                    return false;
                }
            }

            destroyed = false;
            var health = GetComponent<Health>();
            if (health != null)
            {
                health.currentHp = Mathf.Clamp(Mathf.RoundToInt(health.maxHp * Mathf.Clamp01(healthRatio)), 1, health.maxHp);
            }

            ApplyDestroyedVisual(false);
            GetComponent<IBuildableConstruction>()?.RestoreAfterRevive();
            return true;
        }

        void BeginRevivePlayerCollisionGrace(TileGrid grid)
        {
            var states = FindObjectsOfType<BuildingRevivalState>(true);
            for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
            {
                var state = states[stateIndex];
                if (state != null) state.RestoreRevivePlayerCollisions();
            }

            var player = GameManager.Instance != null ? GameManager.Instance.Player : FindObjectOfType<PlayerController>();
            if (player == null || grid == null) return;

            revivePlayer = player;
            reviveGrid = grid;
            reviveStableFixedUpdates = 0;
            revivePlayer.SetBuildingRecoveryActive(true);
            Physics2D.SyncTransforms();
            RefreshRevivePlayerCollisionIgnores();
            TryResolvePlayerRecovery(grid, player, out _);
            reviveCollisionRoutine = StartCoroutine(RestoreRevivePlayerCollisionsWhenClear());
        }

        public static bool ShouldIgnoreCollisionDuringReviveGrace(Collider2D buildingCollider, Collider2D playerCollider)
        {
            return IsActiveSolidCollider(buildingCollider) && IsActiveSolidCollider(playerCollider);
        }

        public static bool TryResolvePlayerRecovery(
            TileGrid grid,
            PlayerController player,
            out Vector2 resolvedPosition)
        {
            resolvedPosition = player != null ? (Vector2)player.transform.position : Vector2.zero;
            if (grid == null || player == null) return false;

            var body = player.GetComponent<Rigidbody2D>();
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic) return false;

            var playerColliders = player.GetComponentsInChildren<Collider2D>(true);
            var solidColliders = FindObjectsOfType<Collider2D>(true);
            Physics2D.SyncTransforms();

            Vector2 origin = body.position;
            resolvedPosition = origin;
            if (!HasActiveSolidCollider(playerColliders)) return false;

            Vector3 originSample = player.MovementSamplePosition();
            var candidates = new List<Vector3Int>();
            if (!grid.TryGetRecoveryCandidates(originSample, candidates, out bool originIsReachable))
            {
                return false;
            }

            if (originIsReachable &&
                IsPlayerPositionFreeOfSolidColliders(player.transform, body, playerColliders, solidColliders))
            {
                return true;
            }

            Vector2 sampleOffsetFromBody = (Vector2)originSample - origin;
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                Vector3Int candidateCell = candidates[candidateIndex];
                if (!grid.TryCellToGrid(candidateCell, out int gridX, out int gridY)) continue;
                Vector2 candidateSample = grid.GridToWorld(gridX, gridY);
                Vector2 candidateBodyPosition = candidateSample - sampleOffsetFromBody;
                body.position = candidateBodyPosition;
                Physics2D.SyncTransforms();

                if (!IsPlayerPositionFreeOfSolidColliders(
                        player.transform,
                        body,
                        playerColliders,
                        solidColliders))
                {
                    continue;
                }

                body.velocity = Vector2.zero;
                resolvedPosition = candidateBodyPosition;
                return true;
            }

            body.position = origin;
            Physics2D.SyncTransforms();
            return false;
        }

        static bool HasActiveSolidCollider(Collider2D[] colliders)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (IsActiveSolidCollider(colliders[i])) return true;
            }

            return false;
        }

        static bool IsPlayerPositionFreeOfSolidColliders(
            Transform playerRoot,
            Rigidbody2D playerBody,
            Collider2D[] playerColliders,
            Collider2D[] solidColliders)
        {
            for (int playerIndex = 0; playerIndex < playerColliders.Length; playerIndex++)
            {
                var playerCollider = playerColliders[playerIndex];
                if (!IsActiveSolidCollider(playerCollider)) continue;

                for (int solidIndex = 0; solidIndex < solidColliders.Length; solidIndex++)
                {
                    var solidCollider = solidColliders[solidIndex];
                    if (!IsActiveSolidCollider(solidCollider)) continue;
                    if (solidCollider.attachedRigidbody == playerBody) continue;
                    if (solidCollider.transform == playerRoot || solidCollider.transform.IsChildOf(playerRoot)) continue;

                    var distance = playerCollider.Distance(solidCollider);
                    if (!distance.isValid ||
                        distance.isOverlapped ||
                        distance.distance < RevivePlayerCollisionClearance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static bool IsActiveSolidCollider(Collider2D collider)
        {
            return collider != null &&
                   collider.enabled &&
                   collider.gameObject.activeInHierarchy &&
                   !collider.isTrigger;
        }

        IEnumerator RestoreRevivePlayerCollisionsWhenClear()
        {
            try
            {
                var waitForFixedUpdate = new WaitForFixedUpdate();
                while (revivePlayer != null && reviveGrid != null)
                {
                    yield return waitForFixedUpdate;
                    if (revivePlayer == null || reviveGrid == null) break;

                    Physics2D.SyncTransforms();
                    RefreshRevivePlayerCollisionIgnores();
                    if (!IsRevivePlayerRecoverySafe())
                    {
                        reviveStableFixedUpdates = 0;
                        TryResolvePlayerRecovery(reviveGrid, revivePlayer, out _);
                        continue;
                    }

                    reviveStableFixedUpdates++;
                    if (HasRequiredRecoveryStability(reviveStableFixedUpdates)) break;
                }
            }
            finally
            {
                reviveCollisionRoutine = null;
                ReleaseRevivePlayerCollisionState();
            }
        }

        public static bool HasRequiredRecoveryStability(int consecutiveFixedUpdates)
        {
            return consecutiveFixedUpdates >= RequiredRecoveryStableFixedUpdates;
        }

        void RefreshRevivePlayerCollisionIgnores()
        {
            if (revivePlayer == null) return;
            var playerColliders = revivePlayer.GetComponentsInChildren<Collider2D>(true);
            var states = FindObjectsOfType<BuildingRevivalState>(true);
            for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
            {
                var state = states[stateIndex];
                if (state == null || !state.configured || !state.isActiveAndEnabled) continue;
                var buildingColliders = state.GetComponentsInChildren<Collider2D>(true);
                for (int buildingIndex = 0; buildingIndex < buildingColliders.Length; buildingIndex++)
                {
                    var buildingCollider = buildingColliders[buildingIndex];
                    for (int playerIndex = 0; playerIndex < playerColliders.Length; playerIndex++)
                    {
                        var playerCollider = playerColliders[playerIndex];
                        if (!ShouldIgnoreCollisionDuringReviveGrace(buildingCollider, playerCollider)) continue;
                        Physics2D.IgnoreCollision(buildingCollider, playerCollider, true);
                        if (!ContainsIgnoredCollisionPair(buildingCollider, playerCollider))
                        {
                            reviveIgnoredCollisions.Add(new IgnoredCollisionPair(buildingCollider, playerCollider));
                        }
                    }
                }
            }
        }

        bool ContainsIgnoredCollisionPair(Collider2D buildingCollider, Collider2D playerCollider)
        {
            for (int i = 0; i < reviveIgnoredCollisions.Count; i++)
            {
                var pair = reviveIgnoredCollisions[i];
                if (pair.buildingCollider == buildingCollider && pair.playerCollider == playerCollider) return true;
            }
            return false;
        }

        bool IsRevivePlayerRecoverySafe()
        {
            if (revivePlayer == null || reviveGrid == null) return false;
            Vector3 sample = revivePlayer.MovementSamplePosition();
            if (!reviveGrid.IsRecoveryReachable(sample)) return false;

            var playerBody = revivePlayer.GetComponent<Rigidbody2D>();
            if (playerBody == null || playerBody.bodyType != RigidbodyType2D.Dynamic) return false;
            var playerColliders = revivePlayer.GetComponentsInChildren<Collider2D>(true);
            if (!HasActiveSolidCollider(playerColliders)) return false;
            var states = FindObjectsOfType<BuildingRevivalState>(true);
            for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
            {
                var state = states[stateIndex];
                if (state == null || !state.configured || !state.isActiveAndEnabled) continue;
                var buildingColliders = state.GetComponentsInChildren<Collider2D>(true);
                if (!AreColliderSetsClear(playerColliders, buildingColliders)) return false;
            }
            return true;
        }

        static bool AreColliderSetsClear(Collider2D[] playerColliders, Collider2D[] buildingColliders)
        {
            for (int playerIndex = 0; playerIndex < playerColliders.Length; playerIndex++)
            {
                var playerCollider = playerColliders[playerIndex];
                if (!IsActiveSolidCollider(playerCollider)) continue;
                for (int buildingIndex = 0; buildingIndex < buildingColliders.Length; buildingIndex++)
                {
                    var buildingCollider = buildingColliders[buildingIndex];
                    if (!IsActiveSolidCollider(buildingCollider)) continue;
                    var distance = buildingCollider.Distance(playerCollider);
                    if (!distance.isValid ||
                        distance.isOverlapped ||
                        distance.distance < RevivePlayerCollisionClearance)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        void RestoreRevivePlayerCollisions()
        {
            var routineToStop = reviveCollisionRoutine;
            reviveCollisionRoutine = null;
            if (routineToStop != null)
            {
                StopCoroutine(routineToStop);
            }
            ReleaseRevivePlayerCollisionState();
        }

        void ReleaseRevivePlayerCollisionState()
        {
            for (int i = 0; i < reviveIgnoredCollisions.Count; i++)
            {
                var pair = reviveIgnoredCollisions[i];
                if (pair.buildingCollider != null && pair.playerCollider != null)
                {
                    Physics2D.IgnoreCollision(pair.buildingCollider, pair.playerCollider, false);
                }
            }

            reviveIgnoredCollisions.Clear();
            if (revivePlayer != null) revivePlayer.SetBuildingRecoveryActive(false);
            revivePlayer = null;
            reviveGrid = null;
            reviveStableFixedUpdates = 0;
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
