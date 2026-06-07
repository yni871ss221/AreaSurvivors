using System.Collections;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class SlashView : MonoBehaviour
    {
        static Sprite[] frames;

        public static void Flash(Vector3 position, Vector2 direction)
        {
            var go = new GameObject("Knight Slash");
            var dir = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.down;
            go.transform.position = position + (Vector3)(dir * 1.02f);
            go.transform.localScale = Vector3.one * 0.78f;
            var billboard = go.AddComponent<PaperBillboard>();
            billboard.rollDegrees = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            EnsureFrames();
            var visual = go.AddComponent<PaperMeshVisual>();
            visual.Configure(frames.Length > 0 ? frames[0] : Resources.Load<Sprite>("Slash"), new Color(1f, 0.92f, 0.42f, 0.82f), WeaponSortingOrders.Slash);
            PixelBurstEffect.Spawn(visual.sprite, go.transform.position, new Color(1f, 0.94f, 0.45f, 0.46f), 3, 0.22f, 0.16f, WeaponSortingOrders.SlashBurst);
            go.AddComponent<SlashView>().StartCoroutine(go.GetComponent<SlashView>().Life(visual, dir));
        }

        static void EnsureFrames()
        {
            if (frames != null) return;
            frames = new[]
            {
                Resources.Load<Sprite>("Generated/Slash_0"),
                Resources.Load<Sprite>("Generated/Slash_1"),
                Resources.Load<Sprite>("Generated/Slash_2")
            };
        }

        IEnumerator Life(PaperMeshVisual visual, Vector2 direction)
        {
            EnsureFrames();
            float frameSeconds = 0.055f;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null) visual.sprite = frames[i];
                visual.color = new Color(1f, 0.92f, 0.42f, Mathf.Lerp(0.82f, 0.32f, i / Mathf.Max(1f, frames.Length - 1f)));
                transform.position += (Vector3)direction * 0.035f;
                yield return new WaitForSeconds(frameSeconds);
            }
            Destroy(gameObject);
        }
    }
}
