using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    sealed class WeaponHudPanelBinding
    {
        readonly Dictionary<Text, Color> defaultTextColors = new Dictionary<Text, Color>();
        WeaponSlotBinding[] slots = System.Array.Empty<WeaponSlotBinding>();
        Vector2[] weaponSlotPositions = System.Array.Empty<Vector2>();
        static readonly Color SpecialActiveColor = new Color(1f, 0.24f, 0.18f, 1f);

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
            weaponSlotPositions = CaptureWeaponSlotPositions(slots);
        }

        public void Update(WeaponController weapon)
        {
            if (weapon == null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    PlaceWeaponPanel(slots[i], false, i);
                }

                return;
            }

            var slotIndex = 0;
            foreach (var type in weapon.AcquiredWeaponOrder)
            {
                if (slotIndex >= slots.Length) break;
                if (!IsWeaponVisible(weapon, type)) continue;
                PlaceWeaponPanel(slots[slotIndex], true, slotIndex);
                ConfigureSlot(slots[slotIndex], weapon, type, weapon.IsSpecialEffectActiveFor(type));
                slotIndex++;
            }

            for (int i = slotIndex; i < slots.Length; i++)
            {
                PlaceWeaponPanel(slots[i], false, i);
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
                    slot.ConfigureHeader("スラッシュ", "Slash_0", WeaponAttributeType.Melee);
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", weapon.SlashAttackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Normal("攻撃間隔", Seconds(slash.cooldownSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Special("ノックバック", Number(slash.knockback), StatIconCatalog.Knockback, specialActive),
                        RowSpec.Normal("攻撃範囲", Number(slash.range), StatIconCatalog.Range));
                    break;
                case WeaponType.Arrow:
                    var arrow = weapon.EffectiveArrowStats;
                    slot.ConfigureHeader("弓", "ArrowHudIcon", WeaponAttributeType.Ranged);
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", arrow.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Normal("攻撃間隔", Seconds(arrow.cooldownSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Normal("矢の本数", arrow.projectileCount.ToString(), StatIconCatalog.Projectile),
                        RowSpec.Special("射程", Number(arrow.range), StatIconCatalog.Range, specialActive));
                    break;
                case WeaponType.Fireball:
                    var fireball = weapon.EffectiveFireballStats;
                    slot.ConfigureHeader("ファイアボール", "FireballHudIcon", WeaponAttributeType.Magic);
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", fireball.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Normal("攻撃間隔", Seconds(fireball.cooldownSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Special("爆発範囲", Number(fireball.explosionRadius), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("射程", Number(weapon.FireballRange), StatIconCatalog.Range));
                    break;
                case WeaponType.Shield:
                    var shield = weapon.EffectiveShieldStats;
                    slot.ConfigureHeader("シールド", "Shield", WeaponAttributeType.Defense);
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
        }

        static void ConfigureAdvancedSlot(WeaponSlotBinding slot, WeaponController weapon, WeaponType type, bool specialActive)
        {
            var stats = weapon.GetEffectiveWeaponStatsFor(type);
            slot.ConfigureHeader(WeaponCatalog.DisplayName(type), WeaponCatalog.IconResource(type), WeaponAttributeCatalog.ForWeapon(type));
            switch (type)
            {
                case WeaponType.Flag:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Special("攻撃範囲", Number(stats.range), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("速度低下", Percent(stats.slowAmount), StatIconCatalog.MoveSpeed),
                        RowSpec.Normal("攻撃間隔", Seconds(stats.damageIntervalSeconds), StatIconCatalog.Cooldown));
                    break;
                case WeaponType.BoomerangSword:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Special("剣本数", stats.projectileCount.ToString(), StatIconCatalog.Projectile, specialActive),
                        RowSpec.Normal("攻撃範囲", Number(stats.range), StatIconCatalog.Range),
                        RowSpec.Normal("攻撃間隔", Seconds(stats.cooldownSeconds), StatIconCatalog.Cooldown));
                    break;
                case WeaponType.AuraSword:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Normal("攻撃回数", stats.projectileCount.ToString(), StatIconCatalog.Projectile),
                        RowSpec.Special("攻撃範囲", Number(stats.range), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("攻撃距離", Number(stats.distance), StatIconCatalog.Range));
                    break;
                case WeaponType.ArrowRain:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Special("攻撃範囲", Number(stats.range), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("攻撃時間", Seconds(stats.durationSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Normal("攻撃間隔", Seconds(stats.cooldownSeconds), StatIconCatalog.Cooldown));
                    break;
                case WeaponType.Gun:
                    slot.ConfigureRows(
                        RowSpec.Special("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack, specialActive),
                        RowSpec.Normal("攻撃間隔", Seconds(stats.cooldownSeconds), StatIconCatalog.Cooldown),
                        RowSpec.Normal("攻撃距離", Number(stats.distance), StatIconCatalog.Range),
                        RowSpec.Normal("攻撃回数", stats.projectileCount.ToString(), StatIconCatalog.Projectile));
                    break;
                case WeaponType.Frost:
                    slot.ConfigureRows(
                        RowSpec.Normal("攻撃力", stats.attackPower.ToString(), StatIconCatalog.Attack),
                        RowSpec.Special("攻撃範囲", Number(stats.range), StatIconCatalog.Range, specialActive),
                        RowSpec.Normal("速度低下", Percent(stats.slowAmount), StatIconCatalog.MoveSpeed),
                        RowSpec.Normal("攻撃間隔", Seconds(stats.cooldownSeconds), StatIconCatalog.Cooldown));
                    break;
                case WeaponType.ThunderBall:
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

        void PlaceWeaponPanel(WeaponSlotBinding slot, bool visible, int slotIndex)
        {
            if (slot == null || slot.Panel == null) return;
            slot.Panel.gameObject.SetActive(true);
            slot.SetContentVisible(visible);
            if (slotIndex < weaponSlotPositions.Length)
            {
                slot.Panel.anchoredPosition = weaponSlotPositions[slotIndex];
            }
        }

        RectTransform BindPanel(Transform hudRoot, Transform statsRoot, string panelName)
        {
            var panel = FindRect(hudRoot, panelName);
            if (panel == null) panel = FindRect(statsRoot, panelName);
            if (panel == null) HasMissingReferences = true;
            return panel;
        }

        static RectTransform FindRect(Transform parent, string path)
        {
            if (parent == null || string.IsNullOrEmpty(path)) return null;
            var target = parent.Find(path);
            return target != null ? target.GetComponent<RectTransform>() : null;
        }

        static Vector2[] CaptureWeaponSlotPositions(WeaponSlotBinding[] bindings)
        {
            if (bindings == null || bindings.Length == 0) return System.Array.Empty<Vector2>();

            var positions = new Vector2[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                positions[i] = bindings[i] != null && bindings[i].Panel != null
                    ? bindings[i].Panel.anchoredPosition
                    : Vector2.zero;
            }

            return positions;
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
            readonly WeaponAttributeIconSet attributeIconSet;
            readonly List<RowBinding> rows = new List<RowBinding>();

            public WeaponSlotBinding(RectTransform panel)
            {
                Panel = panel;
                if (panel == null) return;
                title = panel.Find("Title")?.GetComponent<Text>();
                icon = panel.Find("Icon")?.GetComponent<Image>();
                attributeIconSet = panel.Find("Weapon Type Icons")?.GetComponent<WeaponAttributeIconSet>();

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
                SetActive(title != null ? title.gameObject : null, visible);
                if (attributeIconSet != null)
                {
                    if (visible) attributeIconSet.Show(WeaponAttributeType.Melee);
                    else attributeIconSet.Hide();
                }

                foreach (var row in rows)
                {
                    row.SetVisible(false);
                }
            }

            public void ConfigureHeader(string label, string iconResource, WeaponAttributeType attributeType)
            {
                if (title != null) title.text = label;
                if (icon != null)
                {
                    var sprite = GeneratedSpriteLoader.Load(iconResource);
                    if (sprite != null) icon.sprite = sprite;
                }

                if (attributeIconSet != null) attributeIconSet.Show(attributeType);
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
                if (go != null) go.SetActive(active);
            }
        }

        sealed class RowBinding
        {
            readonly GameObject root;
            readonly Text label;
            public readonly Text Value;
            readonly Image icon;
            readonly Color normalValueColor;

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
                if (label != null) label.text = spec.Label;
                if (Value != null)
                {
                    Value.text = spec.Value;
                    Value.color = spec.SpecialActive ? SpecialActiveColor : normalValueColor;
                }

                if (icon != null)
                {
                    var sprite = StatIconCatalog.Load(spec.IconResource);
                    if (sprite != null) icon.sprite = sprite;
                }
            }

            public void SetVisible(bool visible)
            {
                if (root != null) root.SetActive(visible);
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
