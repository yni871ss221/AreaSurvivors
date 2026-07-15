using System;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class EvolutionChoicePresentation : MonoBehaviour
    {
        [Serializable]
        public struct EvolutionIconEntry
        {
            public WeaponType weaponType;
            public GameObject icon;
        }

        public RectTransform bounceVisual;
        public GameObject standardWeaponIcon;
        public GameObject evolutionWeaponIcon;
        public EvolutionIconEntry[] evolutionWeaponIcons = Array.Empty<EvolutionIconEntry>();
        public Text[] textTargets = Array.Empty<Text>();
        public Color evolutionTextColor = new Color32(255, 92, 92, 255);
        [Min(0f)] public float bounceScale = 0.055f;
        [Min(0.01f)] public float bounceCyclesPerSecond = 1.8f;

        Color[] defaultTextColors = Array.Empty<Color>();
        Vector3 bounceBaseScale = Vector3.one;
        bool initialized;
        bool evolutionActive;
        WeaponType activeEvolutionType;
        float phase;

        void Awake()
        {
            EnsureInitialized();
            ApplyStaticState();
        }

        void OnDisable()
        {
            ResetBounceVisual();
        }

        void Update()
        {
            if (!evolutionActive || bounceVisual == null) return;
            phase += Time.unscaledDeltaTime * bounceCyclesPerSecond * Mathf.PI * 2f;
            float scale = 1f + (Mathf.Sin(phase) * 0.5f + 0.5f) * bounceScale;
            bounceVisual.localScale = bounceBaseScale * scale;
        }

        public void SetEvolution(bool active, WeaponType evolutionType)
        {
            EnsureInitialized();
            evolutionActive = active;
            activeEvolutionType = evolutionType;
            phase = 0f;
            ApplyStaticState();
            enabled = active;
        }

        void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            bounceBaseScale = bounceVisual != null ? bounceVisual.localScale : Vector3.one;
            defaultTextColors = new Color[textTargets != null ? textTargets.Length : 0];
            for (int i = 0; i < defaultTextColors.Length; i++)
            {
                defaultTextColors[i] = textTargets[i] != null ? textTargets[i].color : Color.white;
            }
        }

        void ApplyStaticState()
        {
            if (bounceVisual != null && bounceVisual.gameObject.activeSelf != evolutionActive)
            {
                bounceVisual.gameObject.SetActive(evolutionActive);
            }
            if (standardWeaponIcon != null && standardWeaponIcon.activeSelf == evolutionActive)
            {
                standardWeaponIcon.SetActive(!evolutionActive);
            }
            bool hasTypedIcons = evolutionWeaponIcons != null && evolutionWeaponIcons.Length > 0;
            if (!hasTypedIcons && evolutionWeaponIcon != null && evolutionWeaponIcon.activeSelf != evolutionActive)
            {
                evolutionWeaponIcon.SetActive(evolutionActive);
            }
            if (hasTypedIcons)
            {
                for (int i = 0; i < evolutionWeaponIcons.Length; i++)
                {
                    var icon = evolutionWeaponIcons[i].icon;
                    bool visible = evolutionActive && evolutionWeaponIcons[i].weaponType == activeEvolutionType;
                    if (icon != null && icon.activeSelf != visible) icon.SetActive(visible);
                }
            }

            for (int i = 0; i < defaultTextColors.Length; i++)
            {
                if (textTargets[i] != null)
                {
                    textTargets[i].color = evolutionActive ? evolutionTextColor : defaultTextColors[i];
                }
            }
            if (!evolutionActive) ResetBounceVisual();
        }

        void ResetBounceVisual()
        {
            if (bounceVisual != null) bounceVisual.localScale = bounceBaseScale;
        }
    }
}
