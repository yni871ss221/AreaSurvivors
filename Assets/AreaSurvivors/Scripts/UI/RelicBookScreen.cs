using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class RelicBookScreen : MonoBehaviour
    {
        const string LockedMessage = "未取得のレリックです。ゲーム中に宝箱を拾うと獲得できます。";

        public SceneNavigator navigator;
        public Button backButton;
        public Text detailTitleText;
        public Image rarityBadgeImage;
        public Text rarityText;
        public Text descriptionText;
        public Text effectText;
        public Text messageText;
        public RelicBookEntryView[] entries;

        RelicBookEntryView selectedEntry;

        void Start()
        {
            AudioManager.PlayBgm(BgmTrack.LobbyUpgrades);

            if (navigator == null) navigator = GetComponent<SceneNavigator>();
            if (entries == null || entries.Length == 0) entries = GetComponentsInChildren<RelicBookEntryView>(true);
            SortEntries();

            BindBackButton();
            InitializeEntries();
            SelectFirstOwnedEntry();
        }

        void Update()
        {
            if (UiSelectionUtility.TickControllerSubmit()) return;
            if (UiSelectionUtility.CancelPressed())
            {
                AudioManager.PlayButtonConfirm();
                if (navigator != null) navigator.LoadLobby();
                return;
            }

            var candidates = SelectionCandidates();
            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            UiSelectionUtility.EnsureSelection(candidates);
        }

        public void Select(RelicBookEntryView entry)
        {
            if (entry == null || entry.Definition == null) return;

            if (selectedEntry != null) selectedEntry.SetSelected(false);
            selectedEntry = entry;
            selectedEntry.SetSelected(true);

            var definition = entry.Definition;
            SetText(detailTitleText, definition.displayName);
            ApplyRarityVisuals(definition);
            SetText(descriptionText, definition.description);
            SetText(effectText, definition.effectText);
            SetText(messageText, string.Empty);
        }

        public void ShowLockedMessage(RelicBookEntryView entry)
        {
            if (selectedEntry != null) selectedEntry.SetSelected(false);
            selectedEntry = null;

            SetText(detailTitleText, "LOCK");
            ClearRarityVisuals();
            SetText(descriptionText, LockedMessage);
            SetText(effectText, "-");
            SetText(messageText, string.Empty);
        }

        void BindBackButton()
        {
            if (backButton == null) return;
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(() =>
            {
                AudioManager.PlayButtonConfirm();
                if (navigator != null)
                {
                    navigator.LoadLobby();
                }
                else
                {
                    Debug.LogError("SceneNavigator is not assigned on RelicBookScreen.");
                }
            });
        }

        void InitializeEntries()
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] == null) continue;
                entries[i].Initialize(this);
            }
        }

        void SortEntries()
        {
            if (entries == null) return;
            Array.Sort(entries, (a, b) => RelicCatalog.CompareDisplayOrder(a != null ? a.Definition : null, b != null ? b.Definition : null));
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null) entries[i].transform.SetSiblingIndex(i);
            }
        }

        void SelectFirstOwnedEntry()
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i] != null && entries[i].IsOwned)
                    {
                        Select(entries[i]);
                        UiSelectionUtility.ConfigureVerticalNavigation(SelectionCandidates());
                        UiSelectionUtility.SelectFirst(entries[i].button, backButton);
                        return;
                    }
                }
            }

            SetText(detailTitleText, "所持レリック");
            ClearRarityVisuals();
            SetText(descriptionText, "取得済みレリックはまだありません。ゲーム中に宝箱を拾うと、ここに追加されます。");
            SetText(effectText, "-");
            SetText(messageText, string.Empty);
            var fallbackCandidates = SelectionCandidates();
            UiSelectionUtility.ConfigureVerticalNavigation(fallbackCandidates);
            UiSelectionUtility.SelectFirst(FirstEntryButton(), backButton);
        }

        Selectable[] SelectionCandidates()
        {
            var candidates = new List<Selectable>();
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i] != null && entries[i].button != null) candidates.Add(entries[i].button);
                }
            }

            candidates.Add(backButton);
            return candidates.ToArray();
        }

        Button SelectedEntryButton()
        {
            return selectedEntry != null ? selectedEntry.button : null;
        }

        Button FirstEntryButton()
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && UiSelectionUtility.IsSelectable(entries[i].button)) return entries[i].button;
            }

            return null;
        }

        void ApplyRarityVisuals(RelicDefinition definition)
        {
            if (definition == null)
            {
                ClearRarityVisuals();
                return;
            }

            Color rarityColor = RelicRarityVisuals.GetColor(definition.rarity);
            if (detailTitleText != null) detailTitleText.color = rarityColor;
            SetText(rarityText, RelicCatalog.GetRarityDisplayName(definition.rarity));
            SetAlpha(rarityBadgeImage, 1f);
            SetAlpha(rarityText, 1f);
            if (rarityBadgeImage != null) rarityBadgeImage.color = rarityColor;
            if (rarityText != null) rarityText.color = RelicRarityVisuals.GetBadgeTextColor(definition.rarity);
        }

        void ClearRarityVisuals()
        {
            if (detailTitleText != null) detailTitleText.color = Color.white;
            SetText(rarityText, string.Empty);
            SetAlpha(rarityBadgeImage, 0f);
            SetAlpha(rarityText, 0f);
        }

        static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null) return;
            var color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }
    }
}
