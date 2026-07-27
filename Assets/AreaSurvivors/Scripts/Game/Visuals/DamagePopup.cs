using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class DamagePopup : MonoBehaviour
    {
        const int PopupSortingOrder = 24050;
        const int MaxShowsPerFrame = 32;
        const int MaxPoolSizePerPrefab = 96;
        static readonly List<DamagePopup> Pool = new List<DamagePopup>(MaxPoolSizePerPrefab);
        static int showBudgetFrame = -1;
        static int showsThisFrame;
        static long activationSequence;
        static int activeCount;

        public TextMesh text;
        public TextMesh[] outlines;
        public RuntimeTextMeshOutline textOutline;
        public float lifetime = 0.78f;
        float age;
        float drift;
        GameObject sourcePrefab;
        long lastActivationSequence;
        bool countedActive;

        public static int ActiveCount => activeCount;
        public static int MaxActiveInstances => MaxPoolSizePerPrefab;
        public static int MaxVisualsPerFrame => MaxShowsPerFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPoolState()
        {
            Pool.Clear();
            showBudgetFrame = -1;
            showsThisFrame = 0;
            activationSequence = 0;
            activeCount = 0;
        }

        void Awake()
        {
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sortingOrder = PopupSortingOrder;
            }
            if (textOutline == null && text != null)
                textOutline = text.GetComponent<RuntimeTextMeshOutline>();
            DisableLegacyOutlines();
        }

        public static void Show(GameObject prefab, Vector3 position, int amount, Color color)
        {
            CombatPerformanceDiagnostics.RecordDamagePopupRequest();
            if (CombatPerformanceDiagnostics.SuppressDamagePopups) return;
            if (prefab == null) return;

            if (!ReserveFrameShow())
            {
                CombatPerformanceDiagnostics.RecordDamagePopupDrop();
                return;
            }

            var popup = Acquire(prefab, position);
            if (popup == null)
            {
                CombatPerformanceDiagnostics.RecordDamagePopupDrop();
                return;
            }

            CombatPerformanceDiagnostics.RecordDamagePopupSpawn();
            popup.Activate(position, amount, color);
        }

        static bool ReserveFrameShow()
        {
            int frame = Time.frameCount;
            if (showBudgetFrame != frame)
            {
                showBudgetFrame = frame;
                showsThisFrame = 0;
            }
            if (showsThisFrame >= MaxShowsPerFrame) return false;
            showsThisFrame++;
            return true;
        }

        static DamagePopup Acquire(GameObject prefab, Vector3 position)
        {
            DamagePopup inactive = null;
            DamagePopup oldestActive = null;
            int prefabInstanceCount = 0;
            for (int i = Pool.Count - 1; i >= 0; i--)
            {
                var candidate = Pool[i];
                if (candidate == null)
                {
                    Pool.RemoveAt(i);
                    continue;
                }
                if (candidate.sourcePrefab != prefab) continue;

                prefabInstanceCount++;
                if (!candidate.gameObject.activeSelf && inactive == null)
                {
                    inactive = candidate;
                    continue;
                }
                if (oldestActive == null ||
                    candidate.lastActivationSequence < oldestActive.lastActivationSequence)
                {
                    oldestActive = candidate;
                }
            }

            if (inactive != null)
            {
                CombatPerformanceDiagnostics.RecordDamagePopupReuse();
                return inactive;
            }
            if (prefabInstanceCount >= MaxPoolSizePerPrefab)
            {
                if (oldestActive != null)
                    CombatPerformanceDiagnostics.RecordDamagePopupReuse();
                return oldestActive;
            }

            var go = Instantiate(prefab, position, Quaternion.identity);
            var popup = go.GetComponent<DamagePopup>();
            if (popup == null)
            {
                Destroy(go);
                return null;
            }
            popup.sourcePrefab = prefab;
            Pool.Add(popup);
            CombatPerformanceDiagnostics.RecordDamagePopupInstanceCreate();
            return popup;
        }

        void Activate(Vector3 position, int amount, Color color)
        {
            transform.SetPositionAndRotation(position, Quaternion.identity);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (!countedActive)
            {
                countedActive = true;
                activeCount++;
            }

            age = 0f;
            transform.localScale = Vector3.one * EvaluateScale(0f);
            lastActivationSequence = ++activationSequence;
            if (text != null) text.text = amount.ToString();
            textOutline?.SetColors(color, Color.black);
            drift = Random.Range(-0.12f, 0.12f);
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.position += new Vector3(drift, 0.42f, 0f) * Time.deltaTime;
            transform.localScale = Vector3.one * EvaluateScale(t);
            float alpha = t < 0.48f ? 1f : 1f - Mathf.InverseLerp(0.48f, 1f, t);
            textOutline?.SetAlpha(alpha);
            if (age >= lifetime) gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (!countedActive) return;
            countedActive = false;
            activeCount = Mathf.Max(0, activeCount - 1);
        }

        void OnDestroy()
        {
            Pool.Remove(this);
            if (!countedActive) return;
            countedActive = false;
            activeCount = Mathf.Max(0, activeCount - 1);
        }

        float EvaluateScale(float t)
        {
            if (t < 0.16f) return Mathf.Lerp(0.48f, 0.82f, Mathf.SmoothStep(0f, 1f, t / 0.16f));
            if (t < 0.34f) return Mathf.Lerp(0.82f, 0.68f, Mathf.SmoothStep(0f, 1f, (t - 0.16f) / 0.18f));
            return 0.68f;
        }

        void DisableLegacyOutlines()
        {
            if (outlines == null) return;
            foreach (var outline in outlines)
            {
                if (outline == null) continue;
                var renderer = outline.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.enabled = false;
            }
        }
    }
}
