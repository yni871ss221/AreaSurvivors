using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class WeaponBookScreen : MonoBehaviour
    {
        const string LockedMessage = "スキルツリーでアンロックが必要です";

        public GameConfig config;
        public SceneNavigator navigator;
        public Button backButton;
        public Text detailTitleText;
        public Text featureText;
        public Text statsText;
        public Text specialEffectText;
        public Text messageText;
        public WeaponAttributeIconSet detailTypeIcons;
        public WeaponBookEntryView[] entries;

        WeaponBookEntryView selectedEntry;

        void OnEnable()
        {
            LocalizationService.LanguageChanged += RefreshLocalizedContent;
        }

        void OnDisable()
        {
            LocalizationService.LanguageChanged -= RefreshLocalizedContent;
        }

        void Start()
        {
            AudioManager.PlayBgm(BgmTrack.LobbyUpgrades);

            if (config == null) config = Resources.Load<GameConfig>("Config/GameConfig");
            if (navigator == null) navigator = GetComponent<SceneNavigator>();
            if (entries == null || entries.Length == 0) entries = GetComponentsInChildren<WeaponBookEntryView>(true);

            BindBackButton();
            InitializeEntries();
            SelectFirstAvailableEntry();
        }

        void Update()
        {
            var candidates = SelectionCandidates();
            if (UiSelectionUtility.TickControllerSubmit(candidates)) return;
            if (UiSelectionUtility.CancelPressed())
            {
                AudioManager.PlayButtonConfirm();
                if (navigator != null) navigator.LoadLobby();
                return;
            }

            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            UiSelectionUtility.EnsureSelection(candidates);
            SelectFocusedEntry();
        }

        public void Select(WeaponBookEntryView entry)
        {
            if (entry == null) return;

            ClearEntrySelection();
            selectedEntry = entry;
            selectedEntry.SetSelected(true);

            SetText(detailTitleText, entry.LocalizedDisplayName);
            SetText(featureText, string.IsNullOrEmpty(entry.featureDescription) ? "-" : entry.LocalizedFeatureDescription);
            SetText(statsText, LocalizationService.LocalizeSource(InitialStatsText(entry)));
            SetText(specialEffectText, string.IsNullOrEmpty(entry.specialEffectDescription)
                ? LocalizationService.Text("特殊効果は今後追加予定です。", "Special effects will be added in a future update.")
                : entry.LocalizedSpecialEffectDescription);
            SetText(messageText, string.Empty);
            ShowDetailTypeIcon(entry.attributeType);
        }

        public void ShowLockedMessage(WeaponBookEntryView entry)
        {
            ClearEntrySelection();
            selectedEntry = null;

            SetText(detailTitleText, "LOCK");
            SetText(featureText, LocalizationService.Text(LockedMessage, "Unlock this weapon in the skill tree."));
            SetText(statsText, "-");
            SetText(specialEffectText, "-");
            SetText(messageText, string.Empty);
            HideDetailTypeIcon();
        }

        void RefreshLocalizedContent()
        {
            if (!isActiveAndEnabled || entries == null) return;
            foreach (var entry in entries)
            {
                if (entry != null) entry.Refresh();
            }

            if (selectedEntry != null) Select(selectedEntry);
        }

        void ClearEntrySelection()
        {
            if (entries == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null) entries[i].SetSelected(false);
            }
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
                    Debug.LogError("SceneNavigator is not assigned on WeaponBookScreen.");
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

        void SelectFirstAvailableEntry()
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i] != null && entries[i].IsUnlocked)
                    {
                        var candidates = SelectionCandidates();
                        UiSelectionUtility.ConfigureVerticalNavigation(candidates);
                        UiSelectionUtility.SelectFirst(entries[i].button, backButton);
                        Select(entries[i]);
                        return;
                    }
                }
            }

            SetText(detailTitleText, "武器図鑑");
            SetText(featureText, "武器パネルを選択してください。");
            SetText(statsText, "-");
            SetText(specialEffectText, "-");
            SetText(messageText, string.Empty);
            HideDetailTypeIcon();
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

        void SelectFocusedEntry()
        {
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (focused == null || entries == null) return;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.button == null || entry.button.gameObject != focused) continue;
                if (entry == selectedEntry) return;
                if (entry.IsUnlocked) Select(entry);
                else ShowLockedMessage(entry);
                return;
            }
        }

        void ShowDetailTypeIcon(WeaponAttributeType attributeType)
        {
            if (detailTypeIcons == null) return;
            if (attributeType == WeaponAttributeType.None)
            {
                detailTypeIcons.Hide();
                return;
            }

            detailTypeIcons.Show(attributeType);
        }

        void HideDetailTypeIcon()
        {
            if (detailTypeIcons != null) detailTypeIcons.Hide();
        }

        string InitialStatsText(WeaponBookEntryView entry)
        {
            if (entry == null) return "-";
            if (!entry.usesRuntimeStats || config == null)
            {
                return string.IsNullOrEmpty(entry.initialStatsText) ? "-" : entry.LocalizedInitialStatsText;
            }

            var stats = config.GetWeaponStats(entry.weaponType, 1);
            float attackPower = entry.weaponType == WeaponType.Slash
                ? stats.attackPower + config.slashDamageBonus
                : stats.attackPower;

            var builder = new StringBuilder();
            builder.Append("攻撃力: ").Append(Number(attackPower)).AppendLine();
            if (entry.weaponType == WeaponType.Shield)
            {
                builder.Append("シールド数: ").Append(stats.projectileCount).AppendLine();
                builder.Append("ノックバック: ").Append(Number(stats.knockback)).AppendLine();
                builder.Append("回転速度: ").Append(Number(stats.rotationSpeed)).AppendLine();
                builder.Append("回転半径: ").Append(Number(stats.range / TileGrid.DefaultCellSize)).AppendLine("セル");
                return builder.ToString().TrimEnd();
            }

            if (WeaponCatalog.IsAdvanced(entry.weaponType))
            {
                AppendAdvancedStats(builder, entry.weaponType, stats);
                return builder.ToString().TrimEnd();
            }

            builder.Append("攻撃間隔: ").Append(Number(stats.cooldownSeconds)).AppendLine("秒");
            if (stats.range > 0f) builder.Append("射程: ").Append(Number(stats.range / TileGrid.DefaultCellSize)).AppendLine("セル");
            if (stats.projectileSpeed > 0f) builder.Append("弾速: ").Append(Number(stats.projectileSpeed)).AppendLine();
            if (stats.projectileCount > 1) builder.Append("弾数: ").Append(stats.projectileCount).AppendLine();
            if (stats.explosionRadius > 0f) builder.Append("爆発範囲: ").Append(Number(stats.explosionRadius / TileGrid.DefaultCellSize)).AppendLine("セル");
            if (stats.knockback > 0f) builder.Append("ノックバック: ").Append(Number(stats.knockback));
            return builder.ToString().TrimEnd();
        }

        static void SetText(Text text, string value)
        {
            if (text != null) text.text = LocalizationService.LocalizeSource(value);
        }

        static void AppendAdvancedStats(StringBuilder builder, WeaponType weaponType, WeaponStatBlock stats)
        {
            switch (weaponType)
            {
                case WeaponType.Flag:
                    builder.Append("攻撃範囲: ").Append(Number(stats.range / TileGrid.DefaultCellSize)).AppendLine("セル");
                    builder.Append("速度低下: ").Append(Percent(stats.slowAmount)).AppendLine();
                    builder.Append("攻撃間隔: ").Append(Number(stats.damageIntervalSeconds)).AppendLine("秒");
                    break;
                case WeaponType.BoomerangSword:
                    builder.Append("剣本数: ").Append(stats.projectileCount).AppendLine();
                    builder.Append("攻撃範囲: ").Append(Number(stats.range / TileGrid.DefaultCellSize)).AppendLine("セル");
                    builder.Append("攻撃間隔: ").Append(Number(stats.cooldownSeconds)).AppendLine("秒");
                    break;
                case WeaponType.AuraSword:
                    builder.Append("攻撃回数: ").Append(stats.projectileCount).AppendLine();
                    builder.Append("攻撃範囲: ").Append(Number(stats.range / TileGrid.DefaultCellSize)).AppendLine("セル");
                    builder.Append("攻撃距離: ").Append(Number(stats.distance / TileGrid.DefaultCellSize)).AppendLine("セル");
                    break;
                case WeaponType.ArrowRain:
                    builder.Append("攻撃範囲: ").Append(Number(stats.range / TileGrid.DefaultCellSize)).AppendLine("セル");
                    builder.Append("攻撃時間: ").Append(Number(stats.durationSeconds)).AppendLine("秒");
                    builder.Append("攻撃間隔: ").Append(Number(stats.cooldownSeconds)).AppendLine("秒");
                    break;
                case WeaponType.Gun:
                    builder.Append("攻撃間隔: ").Append(Number(stats.cooldownSeconds)).AppendLine("秒");
                    builder.Append("攻撃距離: ").Append(Number(stats.distance / TileGrid.DefaultCellSize)).AppendLine("セル");
                    builder.Append("攻撃回数: ").Append(stats.projectileCount).AppendLine();
                    break;
                case WeaponType.Frost:
                    builder.Append("攻撃範囲: ").Append(Number(stats.range / TileGrid.DefaultCellSize)).AppendLine("セル");
                    builder.Append("速度低下: ").Append(Percent(stats.slowAmount)).AppendLine();
                    builder.Append("攻撃間隔: ").Append(Number(stats.cooldownSeconds)).AppendLine("秒");
                    break;
                case WeaponType.ThunderBall:
                    builder.Append("攻撃範囲: ").Append(Number(stats.range / TileGrid.DefaultCellSize)).AppendLine("セル");
                    builder.Append("弾数: ").Append(stats.projectileCount).AppendLine();
                    builder.Append("持続時間: ").Append(Number(stats.durationSeconds)).AppendLine("秒");
                    break;
            }
        }

        static string Number(float value)
        {
            return value.ToString("0.##");
        }

        static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }
    }
}
