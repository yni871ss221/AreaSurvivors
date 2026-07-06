using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class RelicHudPanel : MonoBehaviour
    {
        public RelicHudSlot[] slots;
        public GameObject tooltipRoot;
        public Text tooltipTitleText;
        public Image tooltipRarityBadgeImage;
        public Text tooltipRarityText;
        public Text tooltipDescriptionText;
        public Text tooltipEffectText;

        GameManager gameManager;
        PlayerController player;
        float refreshTimer;

        public void Initialize(GameManager owner)
        {
            gameManager = owner;
            player = owner != null ? owner.Player : null;
            if (slots == null || slots.Length == 0) slots = GetComponentsInChildren<RelicHudSlot>(true);
            SortSlotsByDisplayOrder();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].Initialize(this);
            }

            HideTooltip();
            Refresh(true);
        }

        void Update()
        {
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer > 0f) return;
            refreshTimer = 0.15f;
            Refresh(false);
        }

        public void Refresh(bool force = false)
        {
            if (gameManager != null && player == null) player = gameManager.Player;
            if (slots == null) return;
            var weapon = player != null ? player.weapon : null;
            var grid = gameManager != null ? gameManager.grid : null;
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot == null) continue;
                var definition = slot.Definition;
                bool owned = definition != null && ProgressionStore.HasRelic(definition.type);
                slot.SetOwnedVisible(owned);
                if (!owned) continue;
                slot.SetActiveState(RelicEffects.IsActive(definition, weapon, player, grid, gameManager), force);
            }
        }

        public void ShowTooltip(RelicDefinition definition)
        {
            if (definition == null || tooltipRoot == null) return;
            if (tooltipTitleText != null) tooltipTitleText.text = definition.displayName;
            if (tooltipDescriptionText != null) tooltipDescriptionText.text = definition.description;
            if (tooltipEffectText != null) tooltipEffectText.text = definition.effectText;
            if (tooltipRarityText != null)
            {
                tooltipRarityText.text = RelicCatalog.GetRarityDisplayName(definition.rarity);
                tooltipRarityText.color = RelicRarityVisuals.GetBadgeTextColor(definition.rarity);
            }

            Color rarityColor = RelicRarityVisuals.GetColor(definition.rarity);
            if (tooltipTitleText != null) tooltipTitleText.color = rarityColor;
            if (tooltipRarityBadgeImage != null) tooltipRarityBadgeImage.color = rarityColor;
            tooltipRoot.SetActive(true);
        }

        public void HideTooltip()
        {
            if (tooltipRoot != null) tooltipRoot.SetActive(false);
        }

        void SortSlotsByDisplayOrder()
        {
            if (slots == null) return;
            System.Array.Sort(slots, (a, b) => RelicCatalog.CompareDisplayOrder(a != null ? a.Definition : null, b != null ? b.Definition : null));
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) slots[i].transform.SetSiblingIndex(i);
            }
        }
    }
}
