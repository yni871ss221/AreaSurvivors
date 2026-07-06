using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class RelicBookEntryView : MonoBehaviour
    {
        static readonly Color NormalPanelColor = new Color(0.08f, 0.17f, 0.12f, 0.94f);
        static readonly Color SelectedPanelColor = new Color(0.13f, 0.31f, 0.2f, 0.98f);
        static readonly Color LockedIconColor = new Color(0f, 0f, 0f, 0.78f);

        public RelicType relicType;
        public Button button;
        public Image background;
        public Image icon;
        public Image silhouetteOverlay;
        public Text nameText;

        RelicBookScreen owner;

        public bool IsOwned => ProgressionStore.HasRelic(relicType);
        public RelicDefinition Definition => RelicCatalog.Get(relicType);

        public void Initialize(RelicBookScreen screen)
        {
            owner = screen;
            if (button == null) button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClicked);
            }

            Refresh();
        }

        public void Refresh()
        {
            bool owned = IsOwned;
            var definition = Definition;
            if (nameText != null)
            {
                nameText.text = owned && definition != null ? definition.displayName : "LOCK";
                nameText.color = owned && definition != null ? RelicRarityVisuals.GetColor(definition.rarity) : Color.white;
            }
            if (icon != null && definition != null)
            {
                icon.sprite = LoadIcon(definition);
                icon.rectTransform.localScale = Vector3.one * RelicCatalog.IconScale(definition);
            }
            if (silhouetteOverlay != null) silhouetteOverlay.gameObject.SetActive(!owned);
            if (icon != null) icon.color = owned ? Color.white : LockedIconColor;
            SetSelected(false);
        }

        public void SetSelected(bool selected)
        {
            if (background != null) background.color = selected ? SelectedPanelColor : NormalPanelColor;
        }

        void OnClicked()
        {
            AudioManager.PlayButtonConfirm();
            if (owner == null) return;
            if (IsOwned)
            {
                owner.Select(this);
            }
            else
            {
                owner.ShowLockedMessage(this);
            }
        }

        static Sprite LoadIcon(RelicDefinition definition)
        {
            var sprite = definition != null ? GeneratedSpriteLoader.Load(definition.iconPath) : null;
            return sprite != null ? sprite : GeneratedSpriteLoader.Load("TreasureChest");
        }
    }
}
