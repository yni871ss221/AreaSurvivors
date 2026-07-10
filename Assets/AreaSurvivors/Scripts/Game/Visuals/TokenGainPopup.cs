using UnityEngine;

namespace AreaSurvivors
{
    public sealed class TokenGainPopup : MonoBehaviour
    {
        const int PopupSortingOrder = 24100;

        public PaperMeshVisual iconVisual;
        public TextMesh amountText;
        public Sprite tokenSprite;
        public float lifetime = 0.95f;
        public Vector3 drift = new Vector3(0f, 0.52f, 0f);
        public bool destroyOnComplete = true;

        RuntimeTextMeshOutline textOutline;
        Transform followTarget;
        Vector3 followOffset;
        Vector3 baseLocalPosition;
        float age;
        bool initialized;

        public static void Show(GameObject prefab, Transform target, Vector3 offset, int amount)
        {
            if (prefab == null || target == null || amount <= 0) return;
            var go = Instantiate(prefab, target.position + offset, Quaternion.identity);
            var popup = go.GetComponent<TokenGainPopup>();
            if (popup != null) popup.Configure(target, offset, amount);
        }

        void Awake()
        {
            EnsureInitialized();
        }

        void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            baseLocalPosition = transform.localPosition;

            if (amountText != null)
            {
                textOutline = amountText.GetComponent<RuntimeTextMeshOutline>();
                if (textOutline == null) textOutline = amountText.gameObject.AddComponent<RuntimeTextMeshOutline>();
            }

            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sortingOrder = PopupSortingOrder;
                if (renderer.GetComponent<PreserveSortingOrder>() == null)
                    renderer.gameObject.AddComponent<PreserveSortingOrder>();
            }
        }

        public void ShowAmount(int amount)
        {
            if (amount <= 0) return;
            EnsureInitialized();
            followTarget = null;
            followOffset = Vector3.zero;
            age = 0f;
            transform.localPosition = baseLocalPosition;
            ApplyAmount(amount);
            ApplyPresentation(0f);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }

        public void Configure(Transform target, Vector3 offset, int amount)
        {
            EnsureInitialized();
            followTarget = target;
            followOffset = offset;
            age = 0f;
            ApplyAmount(amount);
        }

        void ApplyAmount(int amount)
        {
            if (amountText != null)
            {
                amountText.text = Mathf.Max(0, amount).ToString();
                amountText.color = new Color(1f, 0.9f, 0.28f, 1f);
                textOutline?.SetColors(amountText.color, Color.black);
            }

            if (iconVisual != null && tokenSprite != null)
            {
                iconVisual.Configure(tokenSprite, Color.white, PopupSortingOrder);
            }
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / Mathf.Max(0.001f, lifetime));
            if (followTarget != null)
            {
                transform.position = followTarget.position + followOffset + drift * age;
            }
            else
            {
                transform.localPosition = baseLocalPosition + drift * age;
            }

            ApplyPresentation(t);
            if (age < lifetime) return;
            if (destroyOnComplete)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        void ApplyPresentation(float t)
        {
            float scale = t < 0.18f
                ? Mathf.Lerp(0.58f, 1.08f, Mathf.SmoothStep(0f, 1f, t / 0.18f))
                : Mathf.Lerp(1.08f, 0.92f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.18f) / 0.22f)));
            transform.localScale = Vector3.one * scale;
            float alpha = t < 0.58f ? 1f : 1f - Mathf.InverseLerp(0.58f, 1f, t);
            textOutline?.SetAlpha(alpha);
            if (iconVisual != null) iconVisual.color = new Color(1f, 1f, 1f, alpha);
        }
    }
}
