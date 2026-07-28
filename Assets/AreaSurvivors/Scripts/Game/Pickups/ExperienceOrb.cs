using UnityEngine;

namespace AreaSurvivors
{
    public sealed class ExperienceOrb : AttractablePickup
    {
        public static ExperienceOrb SpawnOrMerge(
            GameObject prefab,
            Vector3 position,
            int amount)
        {
            if (amount <= 0) return null;
            if (PickupAttractionRegistry.TryMergeExperienceReward(position, amount))
            {
                CombatPerformanceDiagnostics.RecordXpOrbMerge();
                return null;
            }
            if (prefab == null) return null;

            var orbObject = Instantiate(prefab, position, Quaternion.identity);
            CombatPerformanceDiagnostics.RecordXpOrbSpawn();
            var experience = orbObject.GetComponent<ExperienceOrb>();
            if (experience != null) experience.value = amount;
            return experience;
        }

        internal bool TryMergeReward(int amount)
        {
            if (amount <= 0 || !CanBeginProximityAttraction) return false;
            long mergedValue = (long)Mathf.Max(0, value) + amount;
            value = mergedValue > int.MaxValue
                ? int.MaxValue
                : (int)mergedValue;
            return true;
        }

        protected override void AwardReward(int amount)
        {
            AudioManager.PlaySfx(SfxTrack.ExperiencePickup);
            GameManager.Instance?.AddExperience(amount);
        }
    }
}
