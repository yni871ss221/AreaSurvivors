using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    sealed class WeaponHudPanelBinding
    {
        readonly Dictionary<Text, Color> defaultTextColors = new Dictionary<Text, Color>();
        WeaponSlotBinding[] slots = System.Array.Empty<WeaponSlotBinding>();
        WeaponHudCompactIconSlot[] compactSlots = System.Array.Empty<WeaponHudCompactIconSlot>();
        GameObject compactGroup;
        bool detailedMode;
        const string WeaponStatusRootName = "Weapon Status";
        const string WeaponInfoRootName = "Weapon Info";
        const string WeaponPanelGroupName = "Weapon Panel Group";
        static readonly Color SpecialActiveColor = new Color(1f, 0.24f, 0.18f, 1f);
        const string ConditionDetailsName = "Pause Condition Details";

        public bool HasMissingReferences { get; private set; }

        public void Bind(Transform hudRoot, RectTransform statsRoot)
        {
            HasMissingReferences = false;
            defaultTextColors.Clear();
            slots = new[]
            {
                BindSlot(hudRoot, statsRoot, "Slash Weapon Status"),
                BindSlot(hudRoot, statsRoot, "Arrow Weapon Status"),
                BindSlot(hudRoot, statsRoot, "Fireball Weapon Status")
            };
            compactSlots = new[]
            {
                BindCompactSlot(hudRoot, "Weapon Icon Slot 1"),
                BindCompactSlot(hudRoot, "Weapon Icon Slot 2"),
                BindCompactSlot(hudRoot, "Weapon Icon Slot 3")
            };
            var compactGroupRect = FindRect(hudRoot, WeaponInfoRootName + "/" + WeaponPanelGroupName);
            if (compactGroupRect == null) compactGroupRect = FindRect(hudRoot, WeaponInfoRootName);
            compactGroup = compactGroupRect != null ? compactGroupRect.gameObject : null;
            if (compactGroup == null) HasMissingReferences = true;
        }

        public void SetDetailedMode(bool visible)
        {
            detailedMode = visible;
            if (compactGroup != null && compactGroup.activeSelf == visible)
            {
                compactGroup.SetActive(!visible);
            }
        }

        public void Update(WeaponController weapon)
        {
            if (weapon == null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    SetDetailPanelVisible(slots[i], false);
                    SetUnusedCompactSlot(i);
                }

                return;
            }

            var slotIndex = 0;
            foreach (var type in weapon.AcquiredWeaponOrder)
            {
                if (slotIndex >= slots.Length) break;
                if (!IsWeaponVisible(weapon, type)) continue;
                var displayType = weapon.GetDisplayWeaponType(type);
                if (detailedMode)
                {
                    SetDetailPanelVisible(slots[slotIndex], true);
                    ConfigureSlot(slots[slotIndex], weapon, displayType, weapon.IsSpecialEffectActiveFor(type));
                    HideCompactSlot(slotIndex);
                }
                else
                {
                    SetDetailPanelVisible(slots[slotIndex], false);
                    ShowCompactSlot(slotIndex, displayType, weapon.GetRunWeaponDisplayLevel(type));
                }
                slotIndex++;
            }

            for (int i = slotIndex; i < slots.Length; i++)
            {
                SetDetailPanelVisible(slots[i], false);
                SetUnusedCompactSlot(i);
            }
        }

        WeaponSlotBinding BindSlot(Transform hudRoot, Transform statsRoot, string panelName)
        {
            var panel = BindPanel(hudRoot, statsRoot, panelName);
            var slot = new WeaponSlotBinding(panel);
            foreach (var row in slot.Rows)
            {
                RememberDefaultColor(row.Value);
            }

            return slot;
        }

        void ConfigureSlot(WeaponSlotBinding slot, WeaponController weapon, WeaponType type, bool specialActive)
        {
            if (slot == null) return;

            switch (type)
            {
                case WeaponType.Slash:
                    var slash = weapon.EffectiveSlashStats;
                    slot.ConfigureHeader(WeaponCatalog.DisplayNameSource(WeaponType.Slash), "Slash_0", WeaponAttributeType.Melee);
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", weapon.SlashAttackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Normal("攻撃間隔", Seconds(slash.cooldownSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Special("ノックバック", Number(slash.knockback), StatIconCatalog.Knockback, specialActive),
                        RowSpec.Normal("攻撃範囲", Number(slash.range), StatIconCatalog.Range));
                    break;
                case WeaponType.SwordRush:
                    var swordRush = weapon.EffectiveSlashStats;
                    slot.ConfigureHeader(WeaponCatalog.DisplayNameSource(WeaponType.SwordRush), WeaponCatalog.IconResource(WeaponType.SwordRush), WeaponAttributeType.Melee, WeaponType.SwordRush);
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", weapon.SlashAttackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Normal("攻撃間隔", Seconds(swordRush.cooldownSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Special("ノックバック", Number(swordRush.knockback), StatIconCatalog.Knockback, specialActive),
                        RowSpec.Normal("攻撃範囲", Number(swordRush.range), StatIconCatalog.Range));
                    break;
                case WeaponType.Arrow:
                case WeaponType.GoldenBow:
                    var arrow = weapon.EffectiveArrowStats;
                    slot.ConfigureHeader(WeaponCatalog.DisplayNameSource(type), WeaponCatalog.IconResource(type), WeaponAttributeType.Ranged, type);
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", arrow.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Normal("攻撃間隔", Seconds(arrow.cooldownSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Normal("矢の本数", arrow.projectileCount.ToString(), StatIconCatalog.Projectile),
                        RowSpec.Special("射程", Number(arrow.range), StatIconCatalog.Range, specialActive));
                    break;
                case WeaponType.Fireball:
                case WeaponType.FireMissile:
                    var fireball = weapon.EffectiveFireballStats;
                    slot.ConfigureHeader(WeaponCatalog.DisplayNameSource(type), WeaponCatalog.IconResource(type), WeaponAttributeType.Magic, type);
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", fireball.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Normal("攻撃間隔", Seconds(fireball.cooldownSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Special("爆発範囲", Number(fireball.explosionRadius), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("射程", Number(weapon.FireballRange), StatIconCatalog.Range));
                    break;
                case WeaponType.Shield:
                case WeaponType.DualShield:
                    var shield = weapon.EffectiveShieldStats;
                    slot.ConfigureHeader(WeaponCatalog.DisplayNameSource(type), WeaponCatalog.IconResource(type), WeaponAttributeType.Defense, type);
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", shield.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Normal("シールド数", shield.projectileCount.ToString(), StatIconCatalog.Defense),
                        RowSpec.Normal("ノックバック", Number(shield.knockback), StatIconCatalog.Knockback),
                        RowSpec.Special("回転速度", Number(shield.rotationSpeed), StatIconCatalog.MoveSpeed, specialActive));
                    break;
                default:
                    ConfigureAdvancedSlot(slot, weapon, type, specialActive);
                    break;
            }

            slot.ConfigureConditions(weapon, type, specialActive);
        }

        static void ConfigureAdvancedSlot(WeaponSlotBinding slot, WeaponController weapon, WeaponType type, bool specialActive)
        {
            var stats = weapon.GetEffectiveWeaponStatsFor(type);
            slot.ConfigureHeader(WeaponCatalog.DisplayNameSource(type), WeaponCatalog.IconResource(type), WeaponAttributeCatalog.ForWeapon(type), WeaponCatalog.IsEvolution(type) ? type : WeaponType.Slash);
            switch (type)
            {
                case WeaponType.Flag:
                case WeaponType.GoddessBlessing:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Special("攻撃範囲", Number(stats.range), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("速度低下", Percent(stats.slowAmount), StatIconCatalog.MoveSpeed),
                        RowSpec.Normal("攻撃間隔", Seconds(stats.damageIntervalSeconds), StatIconCatalog.Cooldown));
                    break;
                case WeaponType.BoomerangSword:
                case WeaponType.Banana:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Special("剣本数", stats.projectileCount.ToString(), StatIconCatalog.Projectile, specialActive),
                        RowSpec.Normal("攻撃範囲", Number(stats.range), StatIconCatalog.Range),
                        RowSpec.Normal("攻撃間隔", Seconds(stats.cooldownSeconds), StatIconCatalog.Cooldown));
                    break;
                case WeaponType.AuraSword:
                case WeaponType.Excalibur:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Normal("攻撃回数", stats.projectileCount.ToString(), StatIconCatalog.Projectile),
                        RowSpec.Special("攻撃範囲", Number(stats.range), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("攻撃距離", Number(stats.distance), StatIconCatalog.Range));
                    break;
                case WeaponType.ArrowRain:
                case WeaponType.ArrowShower:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Special("攻撃範囲", Number(stats.range), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("攻撃時間", Seconds(stats.durationSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Normal("攻撃間隔", Seconds(stats.cooldownSeconds), StatIconCatalog.Cooldown));
                    break;
                case WeaponType.Gun:
                case WeaponType.MachineGun:
                    slot.ConfigureRows(
                        RowSpec.Special("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack, specialActive),
                        RowSpec.Normal("攻撃間隔", Seconds(stats.cooldownSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Normal("攻撃距離", Number(stats.distance), StatIconCatalog.Range),
                        RowSpec.Normal("攻撃回数", stats.projectileCount.ToString(), StatIconCatalog.Projectile));
                    break;
                case WeaponType.Frost:
                case WeaponType.FrostStorm:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Special("攻撃範囲", Number(stats.range), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("速度低下", Percent(stats.slowAmount), StatIconCatalog.MoveSpeed),
                        RowSpec.Normal("攻撃間隔", Seconds(stats.cooldownSeconds), StatIconCatalog.Cooldown));
                    break;
                case WeaponType.ThunderBall:
                case WeaponType.ThunderStorm:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Special("攻撃範囲", Number(stats.range), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("弾数", stats.projectileCount.ToString(), StatIconCatalog.Projectile),
                        RowSpec.Normal("持続時間", Seconds(stats.durationSeconds), StatIconCatalog.Cooldown));
                    break;
            }
        }

        static bool IsWeaponVisible(WeaponController weapon, WeaponType type)
        {
            switch (type)
            {
                case WeaponType.Slash:
                    return true;
                case WeaponType.Arrow:
                    return weapon.ArrowUnlocked;
                case WeaponType.Fireball:
                    return weapon.FireballUnlocked;
                case WeaponType.Shield:
                    return weapon.ShieldUnlocked;
                default:
                    return weapon.IsWeaponUnlocked(type);
            }
        }

        static void SetDetailPanelVisible(WeaponSlotBinding slot, bool visible)
        {
            if (slot == null || slot.Panel == null) return;
            if (slot.Panel.gameObject.activeSelf != visible) slot.Panel.gameObject.SetActive(visible);
            if (visible) slot.SetContentVisible(true);
        }

        void ShowCompactSlot(int slotIndex, WeaponType weaponType, int level)
        {
            if (slotIndex < compactSlots.Length && compactSlots[slotIndex] != null)
            {
                compactSlots[slotIndex].Show(weaponType, level);
            }
        }

        void HideCompactSlot(int slotIndex)
        {
            if (slotIndex < compactSlots.Length && compactSlots[slotIndex] != null)
            {
                compactSlots[slotIndex].Hide();
            }
        }

        void SetUnusedCompactSlot(int slotIndex)
        {
            if (slotIndex >= compactSlots.Length || compactSlots[slotIndex] == null) return;
            if (detailedMode) compactSlots[slotIndex].Hide();
            else compactSlots[slotIndex].Clear();
        }

        RectTransform BindPanel(Transform hudRoot, Transform statsRoot, string panelName)
        {
            var panel = FindRect(hudRoot, WeaponStatusRootName + "/" + panelName);
            if (panel == null) panel = FindRect(hudRoot, panelName);
            if (panel == null) panel = FindRect(statsRoot, panelName);
            if (panel == null) HasMissingReferences = true;
            return panel;
        }

        WeaponHudCompactIconSlot BindCompactSlot(Transform hudRoot, string panelName)
        {
            var panel = FindRect(hudRoot, WeaponInfoRootName + "/" + WeaponPanelGroupName + "/" + panelName);
            if (panel == null) panel = FindRect(hudRoot, WeaponInfoRootName + "/" + panelName);
            if (panel == null) panel = FindRect(hudRoot, panelName);
            var compactSlot = panel != null ? panel.GetComponent<WeaponHudCompactIconSlot>() : null;
            if (compactSlot == null) HasMissingReferences = true;
            return compactSlot;
        }

        static RectTransform FindRect(Transform parent, string path)
        {
            if (parent == null || string.IsNullOrEmpty(path)) return null;
            var target = parent.Find(path);
            return target != null ? target.GetComponent<RectTransform>() : null;
        }

        void RememberDefaultColor(Text text)
        {
            if (text != null && !defaultTextColors.ContainsKey(text))
            {
                defaultTextColors.Add(text, text.color);
            }
        }

        static string Number(float value)
        {
            return value.ToString("0.##");
        }

        static string Seconds(float value)
        {
            return value.ToString("0.##") + "s";
        }

        static string Percent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        sealed class WeaponSlotBinding
        {
            static readonly string[] RowNames =
            {
                "Attack Row",
                "Cooldown Row",
                "Knockback Row",
                "Projectile Count Row",
                "Explosion Row",
                "Range Row"
            };

            public readonly RectTransform Panel;
            readonly Text title;
            readonly Image icon;
            readonly Dictionary<WeaponType, GameObject> evolutionIcons = new Dictionary<WeaponType, GameObject>();
            readonly WeaponAttributeIconSet attributeIconSet;
            readonly List<RowBinding> rows = new List<RowBinding>();
            readonly GameObject conditionDetails;
            readonly Text specialEffectLabel;
            readonly Text specialEffectText;
            readonly Text evolutionConditionLabel;
            readonly Text evolutionConditionText;
            readonly Color normalSpecialEffectColor;
            string lastIconResource;
            WeaponAttributeType lastAttributeType = WeaponAttributeType.None;
            bool hasHeaderState;

            public WeaponSlotBinding(RectTransform panel)
            {
                Panel = panel;
                if (panel == null) return;
                title = panel.Find("Title")?.GetComponent<Text>();
                icon = panel.Find("Icon")?.GetComponent<Image>();
                AddEvolutionIcon(WeaponType.SwordRush, panel.Find("Sword Rush Icon")?.gameObject);
                AddEvolutionIcon(WeaponType.Banana, panel.Find("Banana Icon")?.gameObject);
                AddEvolutionIcon(WeaponType.Excalibur, panel.Find("Excalibur Icon")?.gameObject);
                AddEvolutionIcon(WeaponType.GoldenBow, panel.Find("Golden Bow Icon")?.gameObject);
                AddEvolutionIcon(WeaponType.ArrowShower, panel.Find("Arrow Shower Icon")?.gameObject);
                AddEvolutionIcon(WeaponType.MachineGun, panel.Find("Machine Gun Icon")?.gameObject);
                AddEvolutionIcon(WeaponType.FireMissile, panel.Find("Fire Missile Icon")?.gameObject);
                AddEvolutionIcon(WeaponType.FrostStorm, panel.Find("Frost Storm Icon")?.gameObject);
                AddEvolutionIcon(WeaponType.ThunderStorm, panel.Find("Thunder Storm Icon")?.gameObject);
                AddEvolutionIcon(WeaponType.DualShield, panel.Find("Dual Shield Icon")?.gameObject);
                AddEvolutionIcon(WeaponType.GoddessBlessing, panel.Find("Goddess Blessing Icon")?.gameObject);
                attributeIconSet = panel.Find("Weapon Type Icons")?.GetComponent<WeaponAttributeIconSet>();
                conditionDetails = panel.Find(ConditionDetailsName)?.gameObject;
                specialEffectLabel = panel.Find(ConditionDetailsName + "/Special Effect Label")?.GetComponent<Text>();
                specialEffectText = panel.Find(ConditionDetailsName + "/Special Effect Text")?.GetComponent<Text>();
                evolutionConditionLabel = panel.Find(ConditionDetailsName + "/Evolution Condition Label")?.GetComponent<Text>();
                evolutionConditionText = panel.Find(ConditionDetailsName + "/Evolution Condition Text")?.GetComponent<Text>();
                normalSpecialEffectColor = specialEffectText != null ? specialEffectText.color : Color.white;

                for (int i = 0; i < panel.childCount; i++)
                {
                    var child = panel.GetChild(i);
                    if (IsRow(child.name))
                    {
                        rows.Add(new RowBinding(child));
                    }
                }
            }

            public IReadOnlyList<RowBinding> Rows => rows;

            public void SetContentVisible(bool visible)
            {
                SetActive(icon != null ? icon.gameObject : null, visible);
                foreach (var pair in evolutionIcons)
                {
                    if (!visible) SetActive(pair.Value, false);
                }
                SetActive(title != null ? title.gameObject : null, visible);
                if (attributeIconSet != null && !visible)
                {
                    attributeIconSet.Hide();
                    hasHeaderState = false;
                }

                if (!visible)
                {
                    SetActive(conditionDetails, false);
                    foreach (var row in rows)
                    {
                        row.SetVisible(false);
                    }
                }
            }

            public void ConfigureConditions(WeaponController weapon, WeaponType displayType, bool specialActive)
            {
                if (conditionDetails == null || weapon == null) return;
                SetActive(conditionDetails, true);

                if (specialEffectLabel != null)
                {
                    specialEffectLabel.text = LocalizationService.LocalizeSource("特殊効果");
                }

                string specialSource = WeaponCatalog.AreaControlSpecialEffectDescriptionSource(displayType);
                bool showSpecial = !string.IsNullOrEmpty(specialSource);
                SetActive(specialEffectLabel != null ? specialEffectLabel.gameObject : null, showSpecial);
                SetActive(specialEffectText != null ? specialEffectText.gameObject : null, showSpecial);
                if (specialEffectText != null && showSpecial)
                {
                    specialEffectText.text = LocalizationService.LocalizeSource(specialSource);
                    specialEffectText.color = specialActive ? SpecialActiveColor : normalSpecialEffectColor;
                    specialEffectText.fontStyle = specialActive ? FontStyle.Bold : FontStyle.Normal;
                }

                bool evolved = WeaponCatalog.IsEvolution(displayType);
                var evolutionType = WeaponCatalog.EvolutionOf(WeaponCatalog.BaseWeaponOf(displayType));
                var requirements = WeaponCatalog.EvolutionRequirementSources(evolutionType);
                bool showEvolution = !evolved && requirements.Length > 0;
                SetActive(evolutionConditionLabel != null ? evolutionConditionLabel.gameObject : null, showEvolution);
                SetActive(evolutionConditionText != null ? evolutionConditionText.gameObject : null, showEvolution);
                if (!showEvolution) return;

                if (evolutionConditionLabel != null)
                {
                    evolutionConditionLabel.text = LocalizationService.LocalizeSource("進化条件");
                }

                if (evolutionConditionText != null)
                {
                    evolutionConditionText.supportRichText = true;
                    evolutionConditionText.text = BuildEvolutionRequirements(weapon, displayType, requirements);
                }
            }

            static string BuildEvolutionRequirements(WeaponController weapon, WeaponType type, IReadOnlyList<string> requirements)
            {
                var text = new StringBuilder(128);
                var baseType = WeaponCatalog.BaseWeaponOf(type);
                for (int i = 0; i < requirements.Count; i++)
                {
                    if (i > 0) text.Append('\n');
                    string requirement = LocalizationService.LocalizeSource(requirements[i]);
                    if (weapon.IsEvolutionRequirementMet(baseType, i))
                    {
                        text.Append("<color=#FF3D2E><b>");
                        text.Append(requirement);
                        text.Append("</b></color>");
                    }
                    else
                    {
                        text.Append(requirement);
                    }
                }

                return text.ToString();
            }

            public void ConfigureHeader(string label, string iconResource, WeaponAttributeType attributeType, WeaponType evolutionType = WeaponType.Slash)
            {
                string localizedLabel = LocalizationService.LocalizeSource(label);
                if (title != null && title.text != localizedLabel) title.text = localizedLabel;
                bool evolved = WeaponCatalog.IsEvolution(evolutionType);
                foreach (var pair in evolutionIcons) SetActive(pair.Value, evolved && pair.Key == evolutionType);
                SetActive(icon != null ? icon.gameObject : null, !evolved);
                if (!evolved && icon != null && lastIconResource != iconResource)
                {
                    var sprite = GeneratedSpriteLoader.Load(iconResource);
                    if (sprite != null) icon.sprite = sprite;
                }

                if (attributeIconSet != null && (!hasHeaderState || lastAttributeType != attributeType)) attributeIconSet.Show(attributeType);
                lastIconResource = iconResource;
                lastAttributeType = attributeType;
                hasHeaderState = true;
            }

            void AddEvolutionIcon(WeaponType type, GameObject iconObject)
            {
                if (iconObject != null) evolutionIcons[type] = iconObject;
            }

            public void ConfigureRows(params RowSpec[] specs)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (i < specs.Length) rows[i].Apply(specs[i]);
                    else rows[i].SetVisible(false);
                }
            }

            static bool IsRow(string name)
            {
                for (int i = 0; i < RowNames.Length; i++)
                {
                    if (name == RowNames[i]) return true;
                }

                return false;
            }

            static void SetActive(GameObject go, bool active)
            {
                if (go != null && go.activeSelf != active) go.SetActive(active);
            }
        }

        sealed class RowBinding
        {
            readonly GameObject root;
            readonly Text label;
            public readonly Text Value;
            readonly Image icon;
            readonly Color normalValueColor;
            string lastLabelSource;
            GameLanguage lastLanguage;
            string lastValue;
            string lastIconResource;
            bool lastSpecialActive;
            bool hasState;

            public RowBinding(Transform row)
            {
                root = row != null ? row.gameObject : null;
                label = row != null ? row.Find("Name")?.GetComponent<Text>() : null;
                Value = row != null ? row.Find("Value")?.GetComponent<Text>() : null;
                icon = row != null ? row.Find("Icon")?.GetComponent<Image>() : null;
                normalValueColor = Value != null ? Value.color : Color.white;
            }

            public void Apply(RowSpec spec)
            {
                SetVisible(true);
                var language = LocalizationService.CurrentLanguage;
                if (label != null && (!hasState || lastLabelSource != spec.Label || lastLanguage != language))
                {
                    label.text = LocalizationService.LocalizeSource(spec.Label);
                }
                if (Value != null)
                {
                    if (!hasState || lastValue != spec.Value) Value.text = spec.Value;
                    if (!hasState || lastSpecialActive != spec.SpecialActive)
                    {
                        Value.color = spec.SpecialActive ? SpecialActiveColor : normalValueColor;
                    }
                }

                if (icon != null && (!hasState || lastIconResource != spec.IconResource))
                {
                    var sprite = StatIconCatalog.Load(spec.IconResource);
                    if (sprite != null) icon.sprite = sprite;
                }

                lastLabelSource = spec.Label;
                lastLanguage = language;
                lastValue = spec.Value;
                lastIconResource = spec.IconResource;
                lastSpecialActive = spec.SpecialActive;
                hasState = true;
            }

            public void SetVisible(bool visible)
            {
                if (root != null && root.activeSelf != visible) root.SetActive(visible);
            }
        }

        readonly struct RowSpec
        {
            public readonly string Label;
            public readonly string Value;
            public readonly string IconResource;
            public readonly bool SpecialActive;

            RowSpec(string label, string value, string iconResource, bool specialActive)
            {
                Label = label;
                Value = value;
                IconResource = iconResource;
                SpecialActive = specialActive;
            }

            public static RowSpec Normal(string label, string value, string iconResource)
            {
                return new RowSpec(label, value, iconResource, false);
            }

            public static RowSpec Special(string label, string value, string iconResource, bool active)
            {
                return new RowSpec(label, value, iconResource, active);
            }
        }
    }
}
