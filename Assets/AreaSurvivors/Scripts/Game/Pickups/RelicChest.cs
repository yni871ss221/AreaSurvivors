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
                int duplicateTokenReward = 0;
                if (ProgressionStore.HasRelic(definition.type))
                {
                    duplicateTokenReward = RelicCatalog.GetDuplicateTokenReward(definition.rarity);
                    ProgressionStore.AddTokens(duplicateTokenReward);
                }
                else if (ProgressionStore.UnlockRelic(definition.type))
                {
                    player.StatsSource?.Refresh();
                    player.ApplyCurrentStats(false);
                }
                else
                {
                    duplicateTokenReward = RelicCatalog.GetDuplicateTokenReward(definition.rarity);
                    ProgressionStore.AddTokens(duplicateTokenReward);
                }

                GameManager.Instance?.ShowRelicAcquisition(definition, duplicateTokenReward);
            }
            else
            {
                GameManager.Instance?.ShowAnnouncement("レリックが見つかりません");
            }

            Destroy(gameObject);
        }
    }
}
