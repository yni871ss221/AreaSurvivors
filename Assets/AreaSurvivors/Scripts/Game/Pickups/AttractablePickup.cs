using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public abstract class AttractablePickup : MonoBehaviour
    {
        public int value = 1;
        public float attractRange = 3f;
        public float speed = 6f;

        PlayerController attractionTarget;
        bool stageTransitionRewardReserved;
        int stageTransitionRewardValue;
        bool collected;

        public bool IsStageTransitionAttracting =>
            stageTransitionRewardReserved && attractionTarget != null && !collected;

        public bool IsAttracting =>
            attractionTarget != null && !collected;

        public bool IsCollected => collected;

        protected virtual void OnEnable()
        {
            PickupAttractionRegistry.Register(this);
        }

        protected virtual void OnDisable()
        {
            PickupAttractionRegistry.Unregister(this);
        }

        public int BeginStageTransitionAttraction(PlayerController player)
        {
            if (player == null || collected || stageTransitionRewardReserved) return 0;

            int reservedValue = Mathf.Max(0, value);
            stageTransitionRewardValue = reservedValue;
            value = 0;
            attractionTarget = player;
            stageTransitionRewardReserved = true;
            PickupAttractionRegistry.MarkStageTransitionAttracting(this);
            player.RegisterPickupAttraction(this);
            return reservedValue;
        }

        public float EstimateStageTransitionAttractionSeconds(PlayerController player)
        {
            if (player == null) return 0f;
            return PickupAttractionMotion.EstimateWorstCaseTravelSeconds(
                transform.position,
                player.transform.position,
                ResolveAttractionSpeed(player),
                player.CurrentMoveSpeed);
        }

        public float ResolveAttractionSpeed(PlayerController player)
        {
            return PickupAttractionMotion.ResolveSpeed(
                speed,
                player != null ? player.CurrentMoveSpeed : 0f);
        }

        public static Vector3 MoveTowardsTarget(
            Vector3 currentPosition,
            Vector3 targetPosition,
            float moveSpeed,
            float deltaTime)
        {
            return PickupAttractionMotion.MoveTowardsTarget(
                currentPosition,
                targetPosition,
                moveSpeed,
                deltaTime);
        }

        public void CompleteStageTransitionAttraction()
        {
            if (!stageTransitionRewardReserved || collected) return;
            if (attractionTarget != null)
            {
                transform.position = attractionTarget.transform.position;
            }
            int rewardValue = stageTransitionRewardValue;
            stageTransitionRewardValue = 0;
            collected = true;
            attractionTarget = null;
            PickupAttractionRegistry.MarkStageTransitionCompleted(this);
            if (rewardValue > 0) AwardReward(rewardValue);
            Destroy(gameObject);
        }

        internal bool CanBeginProximityAttraction =>
            !collected &&
            !stageTransitionRewardReserved &&
            attractionTarget == null &&
            isActiveAndEnabled;

        internal bool TryBeginProximityAttraction(PlayerController player)
        {
            if (player == null || !CanBeginProximityAttraction) return false;
            attractionTarget = player;
            PickupAttractionRegistry.MarkAttracting(this);
            player.RegisterPickupAttraction(this);
            return true;
        }

        internal bool TickAttraction(
            PlayerController player,
            float scaledDeltaTime,
            float unscaledDeltaTime)
        {
            if (collected || attractionTarget != player || player == null) return false;

            Vector3 targetPosition = player.transform.position;
            float deltaTime = stageTransitionRewardReserved
                ? unscaledDeltaTime
                : scaledDeltaTime;
            transform.position = MoveTowardsTarget(
                transform.position,
                targetPosition,
                ResolveAttractionSpeed(player),
                deltaTime);

            if ((transform.position - targetPosition).sqrMagnitude > 0.000001f)
            {
                return true;
            }

            if (stageTransitionRewardReserved)
            {
                CompleteStageTransitionAttraction();
            }
            else
            {
                Collect();
            }
            return false;
        }

        void Collect()
        {
            if (collected || stageTransitionRewardReserved) return;
            collected = true;
            attractionTarget = null;
            AwardReward(Mathf.Max(0, value));
            Destroy(gameObject);
        }

        protected abstract void AwardReward(int amount);
    }

    public static class PickupAttractionRegistry
    {
        public const float ScanIntervalSeconds = 0.1f;
        public const float ExperienceMergeCellSize = TileGrid.DefaultCellSize;

        static readonly HashSet<AttractablePickup> ActivePickups =
            new HashSet<AttractablePickup>();
        static readonly HashSet<AttractablePickup> IdlePickups =
            new HashSet<AttractablePickup>();
        static readonly HashSet<AttractablePickup> StageTransitionAttractions =
            new HashSet<AttractablePickup>();
        static readonly Dictionary<Vector2Int, ExperienceOrb> IdleExperienceByCell =
            new Dictionary<Vector2Int, ExperienceOrb>();
        static readonly Dictionary<ExperienceOrb, Vector2Int> ExperienceCells =
            new Dictionary<ExperienceOrb, Vector2Int>();
        static readonly List<AttractablePickup> ScanBuffer =
            new List<AttractablePickup>(128);

        public static int StageTransitionAttractionCount =>
            StageTransitionAttractions.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            ActivePickups.Clear();
            IdlePickups.Clear();
            StageTransitionAttractions.Clear();
            IdleExperienceByCell.Clear();
            ExperienceCells.Clear();
            ScanBuffer.Clear();
        }

        internal static void Register(AttractablePickup pickup)
        {
            if (pickup == null) return;
            ActivePickups.Add(pickup);
            if (pickup.CanBeginProximityAttraction) IdlePickups.Add(pickup);
            IndexIdleExperience(pickup);
        }

        internal static void Unregister(AttractablePickup pickup)
        {
            if (ReferenceEquals(pickup, null)) return;
            ActivePickups.Remove(pickup);
            IdlePickups.Remove(pickup);
            StageTransitionAttractions.Remove(pickup);
            RemoveIdleExperienceIndex(pickup);
        }

        internal static void MarkAttracting(AttractablePickup pickup)
        {
            if (ReferenceEquals(pickup, null)) return;
            IdlePickups.Remove(pickup);
            RemoveIdleExperienceIndex(pickup);
        }

        internal static void MarkStageTransitionAttracting(AttractablePickup pickup)
        {
            if (ReferenceEquals(pickup, null)) return;
            MarkAttracting(pickup);
            StageTransitionAttractions.Add(pickup);
        }

        internal static void MarkStageTransitionCompleted(AttractablePickup pickup)
        {
            if (ReferenceEquals(pickup, null)) return;
            StageTransitionAttractions.Remove(pickup);
        }

        public static bool TryMergeExperienceReward(Vector3 position, int amount)
        {
            if (amount <= 0) return true;

            Vector2Int cell = ResolveExperienceCell(position);
            if (!IdleExperienceByCell.TryGetValue(cell, out var experience))
            {
                return false;
            }

            if (experience == null || !experience.CanBeginProximityAttraction)
            {
                RemoveIdleExperienceIndex(experience);
                IdleExperienceByCell.Remove(cell);
                return false;
            }

            return experience.TryMergeReward(amount);
        }

#if UNITY_EDITOR
        public static void RegisterForValidation(AttractablePickup pickup)
        {
            Register(pickup);
        }

        public static void UnregisterForValidation(AttractablePickup pickup)
        {
            Unregister(pickup);
        }
#endif

        public static int BeginNearbyAttraction(
            PlayerController player,
            Vector3 previousPlayerPosition,
            Vector3 currentPlayerPosition)
        {
            if (player == null || IdlePickups.Count == 0) return 0;

            ScanBuffer.Clear();
            foreach (var pickup in IdlePickups)
            {
                ScanBuffer.Add(pickup);
            }

            int attractionCount = 0;
            for (int i = 0; i < ScanBuffer.Count; i++)
            {
                var pickup = ScanBuffer[i];
                if (pickup == null || !pickup.CanBeginProximityAttraction) continue;
                float range = Mathf.Max(0f, pickup.attractRange);
                if (SqrDistanceToSegment(
                        pickup.transform.position,
                        previousPlayerPosition,
                        currentPlayerPosition) >
                    range * range)
                {
                    continue;
                }

                if (pickup.TryBeginProximityAttraction(player)) attractionCount++;
            }
            CombatPerformanceDiagnostics.RecordPickupProximityScan(
                ScanBuffer.Count,
                attractionCount);
            return attractionCount;
        }

        public static void CopyActiveTo(List<AttractablePickup> destination)
        {
            if (destination == null) return;
            destination.Clear();
            foreach (var pickup in ActivePickups)
            {
                if (pickup != null && !pickup.IsCollected) destination.Add(pickup);
            }
        }

        static void IndexIdleExperience(AttractablePickup pickup)
        {
            if (!(pickup is ExperienceOrb experience) ||
                !pickup.CanBeginProximityAttraction)
            {
                return;
            }

            RemoveIdleExperienceIndex(experience);
            Vector2Int cell = ResolveExperienceCell(experience.transform.position);
            if (IdleExperienceByCell.TryGetValue(cell, out var existing) &&
                existing != null &&
                existing.CanBeginProximityAttraction)
            {
                return;
            }

            IdleExperienceByCell[cell] = experience;
            ExperienceCells[experience] = cell;
        }

        static void RemoveIdleExperienceIndex(AttractablePickup pickup)
        {
            if (!(pickup is ExperienceOrb experience) ||
                !ExperienceCells.TryGetValue(experience, out var cell))
            {
                return;
            }

            if (IdleExperienceByCell.TryGetValue(cell, out var indexed) &&
                ReferenceEquals(indexed, experience))
            {
                IdleExperienceByCell.Remove(cell);
            }
            ExperienceCells.Remove(experience);
        }

        static Vector2Int ResolveExperienceCell(Vector3 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / ExperienceMergeCellSize),
                Mathf.FloorToInt(position.y / ExperienceMergeCellSize));
        }

        static float SqrDistanceToSegment(
            Vector2 point,
            Vector2 segmentStart,
            Vector2 segmentEnd)
        {
            Vector2 segment = segmentEnd - segmentStart;
            float segmentLengthSquared = segment.sqrMagnitude;
            if (segmentLengthSquared <= 0.000001f)
            {
                return (point - segmentStart).sqrMagnitude;
            }

            float t = Mathf.Clamp01(
                Vector2.Dot(point - segmentStart, segment) / segmentLengthSquared);
            Vector2 closest = segmentStart + segment * t;
            return (point - closest).sqrMagnitude;
        }
    }
}
