using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class DamagePopup : MonoBehaviour
    {
        public Text text;
        public float lifetime = 0.75f;
        float age;

        public static void Show(GameObject prefab, Vector3 position, int amount, Color color)
        {
            if (prefab == null) return;
            var go = Instantiate(prefab, position, Quaternion.identity);
            var popup = go.GetComponent<DamagePopup>();
            if (popup != null)
            {
                popup.text.text = amount.ToString();
                popup.text.color = color;
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            transform.position += Vector3.up * (1.2f * Time.deltaTime);
            if (text != null) text.color = new Color(text.color.r, text.color.g, text.color.b, 1f - age / lifetime);
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
