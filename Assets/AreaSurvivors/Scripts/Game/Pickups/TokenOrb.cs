using UnityEngine;

namespace AreaSurvivors
{
    public sealed class TokenOrb : MonoBehaviour
    {
        public int value = 1;
        public float attractRange = 3f;
        public float speed = 6f;

        public static TokenOrb Spawn(Vector3 position, int amount)
        {
            if (amount <= 0) return null;
            var go = new GameObject("Token Orb");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.34f;

            var visualObject = new GameObject("Paper Visual");
            visualObject.transform.SetParent(go.transform, false);
            var visual = visualObject.AddComponent<PaperMeshVisual>();
            visual.Configure(LoadSprite(), Color.white, 3400);
            var outline = visualObject.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.018f;

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.28f;
            var orb = go.AddComponent<TokenOrb>();
            orb.value = amount;
            return orb;
        }

        void Update()
        {
            var player = GameManager.Instance == null ? null : GameManager.Instance.Player;
            if (player == null) return;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < attractRange)
            {
                transform.position = Vector3.MoveTowards(transform.position, player.transform.position, speed * Time.deltaTime);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            GameManager.Instance?.AddRunTokens(value);
            Destroy(gameObject);
        }

        static Sprite LoadSprite()
        {
            var sprite = GeneratedSpriteLoader.Load("Token");
            if (sprite != null) return sprite;
            var texture = GeneratedSpriteLoader.LoadTexture("Token");
            if (texture == null) return GeneratedSpriteLoader.Load("ExperienceOrb");
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 128f);
        }
    }
}
