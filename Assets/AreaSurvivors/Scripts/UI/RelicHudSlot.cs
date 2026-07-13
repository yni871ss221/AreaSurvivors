using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class RelicHudSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly Color ActiveIconColor = Color.white;
        static readonly Color InactiveIconColor = new Color(0.42f, 0.42f, 0.42f, 0.9f);
        static readonly Color InactiveOutlineColor = new Color(0.12f, 0.14f, 0.12f, 0.88f);

        public RelicType relicType;
        public Image background;
        public Image icon;

        RelicHudPanel owner;
        RelicDefinition definition;
        bool initialized;
        bool active;
        float bounceTime;

        public RelicDefinition Definition => definition != null ? definition : RelicCatalog.Get(relicType);

        public void Initialize(RelicHudPanel panel)
        {
            owner = panel;
            definition = RelicCatalog.Get(relicType);
            if (background == null) background = GetComponent<Image>();
            if (icon == null)
            {
                var iconTransform = transform.Find("Icon");
                icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            }
            if (icon != null) icon.rectTransform.localScale = Vector3.one * RelicCatalog.IconScale(definition);

            initialized = true;
        }

        public void SetOwnedVisible(bool owned)
        {
            if (gameObject.activeSelf == owned) return;
            if (!owned)
            {
                active = false;
                bounceTime = 0f;
                transform.localScale = Vector3.one;
            }
            gameObject.SetActive(owned);
        }

        public void SetActiveState(bool nextActive, bool force)
        {
            if (!initialized) Initialize(owner);
            if (!force && active == nextActive) return;
            if (nextActive && (!active || force)) bounceTime = 0.34f;
            active = nextActive;

            if (icon != null) icon.color = active ? ActiveIconColor : InactiveIconColor;
            var outlineColor = active && definition != null
                ? RelicRarityVisuals.GetColor(definition.rarity)
                : InactiveOutlineColor;
            UiBoxOutline.Apply(background != null ? background.transform : transform, outlineColor, active ? 2.8f : 1.6f);
        }

        void Update()
        {
            if (bounceTime <= 0f)
            {
                if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
                return;
            }

            bounceTime = Mathf.Max(0f, bounceTime - Time.unscaledDeltaTime);
            float normalized = 1f - bounceTime / 0.34f;
            float bounce = Mathf.Sin(normalized * Mathf.PI) * 0.22f;
            transform.localScale = Vector3.one * (1f + bounce);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            owner?.ShowTooltip(Definition);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            owner?.HideTooltip();
        }
    }
}
