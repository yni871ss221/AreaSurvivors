using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Health))]
    public sealed class AutoRegeneration : MonoBehaviour
    {
        public int amount;
        public float intervalSeconds = 2f;
        public GameObject popupPrefab;
        public Vector3 popupOffset = new Vector3(0f, 0.58f, 0f);

        Health health;
        float timer;

        void Awake()
        {
            health = GetComponent<Health>();
        }

        void Update()
        {
            if (Time.timeScale <= 0f || health == null || health.IsDead || amount <= 0) return;
            timer += Time.deltaTime;
            float interval = Mathf.Max(0.05f, intervalSeconds);
            if (timer < interval) return;
            timer -= interval;
            int healed = health.Heal(amount);
            if (healed <= 0) return;
            DamagePopup.Show(popupPrefab, transform.position + popupOffset, healed, new Color(0.35f, 1f, 0.34f, 1f));
        }
    }
}
