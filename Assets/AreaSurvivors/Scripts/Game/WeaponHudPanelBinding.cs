using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    sealed class WeaponHudPanelBinding
    {
        RectTransform slashPanel;
        RectTransform arrowPanel;
        RectTransform fireballPanel;
        Text slashAttackText;
        Text slashCooldownText;
        Text slashKnockbackText;
        Text slashRangeText;
        Text arrowAttackText;
        Text arrowCooldownText;
        Text arrowProjectileCountText;
        Text arrowRangeText;
        Text fireballAttackText;
        Text fireballCooldownText;
        Text fireballExplosionText;
        Text fireballRangeText;

        public bool HasMissingReferences { get; private set; }

        public void Bind(Transform hudRoot, RectTransform statsRoot)
        {
            HasMissingReferences = false;
            slashPanel = BindPanel(hudRoot, statsRoot, "Slash Weapon Status");
            arrowPanel = BindPanel(hudRoot, statsRoot, "Arrow Weapon Status");
            fireballPanel = BindPanel(hudRoot, statsRoot, "Fireball Weapon Status");
            slashAttackText = BindValue(hudRoot, statsRoot, "Slash Weapon Status", "Attack Row");
            slashCooldownText = BindValue(hudRoot, statsRoot, "Slash Weapon Status", "Cooldown Row");
            slashKnockbackText = BindValue(hudRoot, statsRoot, "Slash Weapon Status", "Knockback Row");
            slashRangeText = BindValue(hudRoot, statsRoot, "Slash Weapon Status", "Range Row");
            arrowAttackText = BindValue(hudRoot, statsRoot, "Arrow Weapon Status", "Attack Row");
            arrowCooldownText = BindValue(hudRoot, statsRoot, "Arrow Weapon Status", "Cooldown Row");
            arrowProjectileCountText = BindValue(hudRoot, statsRoot, "Arrow Weapon Status", "Projectile Count Row");
            arrowRangeText = BindValue(hudRoot, statsRoot, "Arrow Weapon Status", "Range Row");
            fireballAttackText = BindValue(hudRoot, statsRoot, "Fireball Weapon Status", "Attack Row");
            fireballCooldownText = BindValue(hudRoot, statsRoot, "Fireball Weapon Status", "Cooldown Row");
            fireballExplosionText = BindValue(hudRoot, statsRoot, "Fireball Weapon Status", "Explosion Row");
            fireballRangeText = BindValue(hudRoot, statsRoot, "Fireball Weapon Status", "Range Row");
        }

        public void Update(WeaponController weapon)
        {
            if (weapon == null)
            {
                SetContentVisible(slashPanel, false);
                SetContentVisible(arrowPanel, false);
                SetContentVisible(fireballPanel, false);
                return;
            }

            var slash = weapon.SlashStats;
            var arrow = weapon.ArrowStats;
            var fireball = weapon.FireballStats;
            SetContentVisible(slashPanel, true);
            SetContentVisible(arrowPanel, weapon.ArrowUnlocked);
            SetContentVisible(fireballPanel, weapon.FireballUnlocked);
            SetText(slashAttackText, weapon.SlashAttackPower.ToString());
            SetText(slashCooldownText, Seconds(slash.cooldownSeconds));
            SetText(slashKnockbackText, Number(slash.knockback));
            SetText(slashRangeText, Number(slash.range));
            SetText(arrowAttackText, arrow.attackPower.ToString());
            SetText(arrowCooldownText, Seconds(arrow.cooldownSeconds));
            SetText(arrowProjectileCountText, arrow.projectileCount.ToString());
            SetText(arrowRangeText, Number(arrow.range));
            SetText(fireballAttackText, fireball.attackPower.ToString());
            SetText(fireballCooldownText, Seconds(fireball.cooldownSeconds));
            SetText(fireballExplosionText, Number(fireball.explosionRadius));
            SetText(fireballRangeText, Number(weapon.FireballRange));
        }

        RectTransform BindPanel(Transform hudRoot, Transform statsRoot, string panelName)
        {
            var panel = FindRect(hudRoot, panelName);
            if (panel == null) panel = FindRect(statsRoot, panelName);
            if (panel == null) HasMissingReferences = true;
            return panel;
        }

        Text BindValue(Transform hudRoot, Transform statsRoot, string panelName, string rowName)
        {
            var value = FindText(hudRoot, panelName + "/" + rowName + "/Value");
            if (value == null) value = FindText(statsRoot, panelName + "/" + rowName + "/Value");
            if (value == null) HasMissingReferences = true;
            return value;
        }

        static RectTransform FindRect(Transform parent, string path)
        {
            if (parent == null || string.IsNullOrEmpty(path)) return null;
            var target = parent.Find(path);
            return target != null ? target.GetComponent<RectTransform>() : null;
        }

        static Text FindText(Transform parent, string path)
        {
            if (parent == null || string.IsNullOrEmpty(path)) return null;
            var target = parent.Find(path);
            return target != null ? target.GetComponent<Text>() : null;
        }

        static void SetContentVisible(RectTransform panel, bool visible)
        {
            if (panel == null) return;
            SetDirectChildActive(panel, "Icon", visible);
            SetDirectChildActive(panel, "Title", visible);
            SetDirectChildActive(panel, "Attack Row", visible);
            SetDirectChildActive(panel, "Cooldown Row", visible);
            SetDirectChildActive(panel, "Knockback Row", visible);
            SetDirectChildActive(panel, "Projectile Count Row", visible);
            SetDirectChildActive(panel, "Explosion Row", visible);
            SetDirectChildActive(panel, "Range Row", visible);
        }

        static void SetDirectChildActive(Transform parent, string path, bool active)
        {
            var child = parent != null ? parent.Find(path) : null;
            if (child != null) child.gameObject.SetActive(active);
        }

        static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }

        static string Number(float value)
        {
            return value.ToString("0.##");
        }

        static string Seconds(float value)
        {
            return value.ToString("0.##") + "s";
        }
    }
}
