using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class RelicChest : MonoBehaviour
    {
        bool collected;

        void Reset()
        {
            var trigger = GetComponent<Collider2D>();
            if (trigger != null) trigger.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || other == null) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            collected = true;
            AudioManager.PlaySfx(SfxTrack.RelicChestPickup);
            if (RelicCatalog.TryPickRandom(out var definition))
            {
                if (!RelicCatalog.TryAcquireReward(
                        definition,
                        out bool newlyUnlocked,
                        out int duplicateTokenReward))
                {
                    GameManager.Instance?.ShowAnnouncement(
                        LocalizationService.Text("レリックが見つかりません", "No relic found"));
                    Destroy(gameObject);
                    return;
                }

                if (newlyUnlocked)
                {
                    player.StatsSource?.Refresh();
                    player.ApplyCurrentStats(false);
                }
                GameManager.Instance?.ShowRelicAcquisition(
                    definition,
                    duplicateTokenReward);
            }
            else
            {
                GameManager.Instance?.ShowAnnouncement(
                    LocalizationService.Text("レリックが見つかりません", "No relic found"));
            }

            Destroy(gameObject);
        }
    }
}
