using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class PixelBurstEffect : MonoBehaviour
    {
        const int MaxPoolSize = 160;
        static readonly Queue<PixelBurstEffect> Pool = new Queue<PixelBurstEffect>();

        PaperMeshVisual visual;
        Vector3 velocity;
        Vector3 startScale;
        Color baseColor;
        float lifetime = 0.22f;
        float age;
        float spin;

        public static void Spawn(Sprite sprite, Vector3 position, Color color, int count, float scale, float duration, int sortingOrder = 3350)
        {
            if (sprite == null) return;
            count = Mathf.Clamp(count, 1, 12);
            for (int i = 0; i < count; i++)
            {
                var piece = GetOrCreatePiece();
                var go = piece.gameObject;
                go.transform.position = position + new Vector3(Random.Range(-0.05f, 0.05f), Random.Range(-0.04f, 0.08f), 0f);
                go.transform.localScale = Vector3.one * Random.Range(scale * 0.62f, scale * 1.18f);
                go.transform.localRotation = Quaternion.identity;
                piece.visual.Configure(sprite, color, sortingOrder + i);
                piece.visual.visible = true;
                piece.Configure(piece.visual, color, duration);
                if (!go.activeSelf) go.SetActive(true);
            }
        }

        static PixelBurstEffect GetOrCreatePiece()
        {
            while (Pool.Count > 0)
            {
                var pooled = Pool.Dequeue();
                if (pooled != null && pooled.gameObject != null) return pooled;
            }

            var go = new GameObject("Pixel Burst");
            go.AddComponent<PaperBillboard>();
            var mesh = go.AddComponent<PaperMeshVisual>();
            var piece = go.AddComponent<PixelBurstEffect>();
            piece.visual = mesh;
            return piece;
        }

        void Configure(PaperMeshVisual mesh, Color color, float seconds)
        {
            visual = mesh;
            baseColor = color;
            lifetime = Mathf.Max(0.06f, seconds);
            age = 0f;
            startScale = transform.localScale;
            velocity = new Vector3(Random.Range(-1f, 1f), Random.Range(0.35f, 1.25f), 0f) * Random.Range(0.22f, 0.58f);
            spin = Random.Range(-220f, 220f);
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            transform.position += velocity * Time.deltaTime;
            velocity *= Mathf.Pow(0.18f, Time.deltaTime);
            transform.localRotation = Quaternion.Euler(0f, 0f, spin * age);
            transform.localScale = startScale * Mathf.Lerp(1f, 0.18f, t);
            if (visual != null)
            {
                visual.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(baseColor.a, 0f, t));
            }
            if (age >= lifetime) ReturnToPool();
        }

        void ReturnToPool()
        {
            if (visual != null) visual.visible = false;
            if (Pool.Count >= MaxPoolSize)
            {
                Destroy(gameObject);
                return;
            }

            gameObject.SetActive(false);
            Pool.Enqueue(this);
        }
    }

    public sealed class CompletionSparkleEffect : MonoBehaviour
    {
        PaperMeshVisual visual;
        Vector3 startScale;
        Color baseColor = Color.white;
        float lifetime = 0.75f;
        float age;

        public static void Spawn(Sprite sprite, Vector3 position, float scale = 0.7f, int sortingOrder = 22030)
        {
            if (sprite == null) sprite = GeneratedSpriteLoader.Load("Sparkle");
            if (sprite == null) return;

            var go = new GameObject("Completion Sparkle Effect");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * Mathf.Max(0.05f, scale);
            go.AddComponent<PaperBillboard>();
            var mesh = go.AddComponent<PaperMeshVisual>();
            mesh.Configure(sprite, Color.white, sortingOrder);
            var outline = go.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.022f;
            go.AddComponent<PreserveSortingOrder>();
            go.AddComponent<CompletionSparkleEffect>().Configure(mesh);
        }

        void Configure(PaperMeshVisual target)
        {
            visual = target;
            startScale = transform.localScale;
            baseColor = visual != null ? visual.color : Color.white;
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / lifetime);
            float pulse = Mathf.Sin(t * Mathf.PI);
            transform.localScale = startScale * (0.7f + pulse * 1.35f);
            transform.localRotation = Quaternion.Euler(0f, 0f, t * 230f);
            transform.position += Vector3.up * (0.16f * Time.deltaTime);
            if (visual != null)
            {
                visual.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(1f, 0f, t));
            }
            if (age >= lifetime) Destroy(gameObject);
        }
    }
}
