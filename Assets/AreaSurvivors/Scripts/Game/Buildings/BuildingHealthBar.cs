using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class BuildingHealthBar : MonoBehaviour
    {
        public Slider hpBar;
        [Range(0.9f, 1f)] public float fullHideThreshold = 0.999f;

        Health health;

        void Awake()
        {
            health = GetComponent<Health>();
        }

        void OnEnable()
        {
            if (health == null) health = GetComponent<Health>();
            if (health != null)
            {
                health.Damaged += OnHealthChanged;
                health.Healed += OnHealthChanged;
                health.Died += OnDied;
            }

            Refresh();
        }

        void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnHealthChanged;
                health.Healed -= OnHealthChanged;
                health.Died -= OnDied;
            }
        }

        void LateUpdate()
        {
            Refresh();
        }

        void OnHealthChanged(Health _, int __)
        {
            Refresh();
        }

        void OnDied(Health _)
        {
            Refresh();
        }

        public void Refresh()
        {
            if (hpBar == null || health == null) return;
            float normalized = health.Normalized;
            hpBar.value = normalized;
            hpBar.gameObject.SetActive(!health.IsDead && normalized < fullHideThreshold);
        }
    }
}
