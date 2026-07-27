using UnityEngine;

namespace AreaSurvivors
{
    public sealed class TokenOrb : AttractablePickup
    {
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

            var orb = go.AddComponent<TokenOrb>();
            orb.value = amount;
            return orb;
        }

        protected override void AwardReward(int amount)
        {
            GameManager.Instance?.AddRunTokens(amount);
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
