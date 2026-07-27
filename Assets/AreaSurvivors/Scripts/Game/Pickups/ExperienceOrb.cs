using UnityEngine;

namespace AreaSurvivors
{
    public sealed class ExperienceOrb : AttractablePickup
    {
        protected override void AwardReward(int amount)
        {
            AudioManager.PlaySfx(SfxTrack.ExperiencePickup);
            GameManager.Instance?.AddExperience(amount);
        }
    }
}
