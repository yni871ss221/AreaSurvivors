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
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            go.transform.localScale = Vector3.one;
            var sr = go.AddComponent<SpriteRenderer>();
            EnsureFrames();
            sr.sprite = frames.Length > 0 ? frames[0] : Resources.Load<Sprite>("Slash");
            sr.color = Color.white;
            sr.sortingOrder = 30;
            go.AddComponent<SlashView>().StartCoroutine(go.GetComponent<SlashView>().Life(sr, dir));
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

        IEnumerator Life(SpriteRenderer sr, Vector2 direction)
        {
            EnsureFrames();
            float frameSeconds = 0.055f;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null) sr.sprite = frames[i];
                transform.position += (Vector3)direction * 0.035f;
                yield return new WaitForSeconds(frameSeconds);
            }
            Destroy(gameObject);
        }
    }
}
