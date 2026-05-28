using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class TowerController : MonoBehaviour
    {
        public Slider hpBar;
        Health health;

        void Awake()
        {
            health = GetComponent<Health>();
            health.Died += _ => GameManager.Instance?.GameOver();
        }

        public void Configure(int maxHp)
        {
            health.SetMax(maxHp);
        }

        void Update()
        {
            if (hpBar != null) hpBar.value = health.Normalized;
        }
    }
}
