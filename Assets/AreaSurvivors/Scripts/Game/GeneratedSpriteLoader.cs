using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AreaSurvivors
{
    public static class GeneratedSpriteLoader
    {
        const string GeneratedPrefix = "Generated/";
        const string GeneratedSpriteRoot = "Assets/AreaSurvivors/Sprites/Generated/";
        static GeneratedSpriteCatalog catalog;

        public static Sprite Load(string nameOrResourcePath)
        {
            var name = NormalizeName(nameOrResourcePath);
            if (string.IsNullOrEmpty(name)) return null;

#if UNITY_EDITOR
            var editorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedSpriteRoot + name + ".png");
            if (editorSprite != null) return editorSprite;
#endif

            var catalogSprite = Catalog != null ? Catalog.Find(name) : null;
            if (catalogSprite != null) return catalogSprite;

            return Resources.Load<Sprite>(GeneratedPrefix + name);
        }

        public static Texture2D LoadTexture(string nameOrResourcePath)
        {
            var name = NormalizeName(nameOrResourcePath);
            if (string.IsNullOrEmpty(name)) return null;

#if UNITY_EDITOR
            var editorTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(GeneratedSpriteRoot + name + ".png");
            if (editorTexture != null) return editorTexture;
#endif

            var catalogSprite = Catalog != null ? Catalog.Find(name) : null;
            if (catalogSprite != null) return catalogSprite.texture;

            return Resources.Load<Texture2D>(GeneratedPrefix + name);
        }

        public static Sprite[] LoadAll(string nameOrResourcePath)
        {
            var name = NormalizeName(nameOrResourcePath);
            if (string.IsNullOrEmpty(name)) return System.Array.Empty<Sprite>();

#if UNITY_EDITOR
            var folder = GeneratedSpriteRoot + name.TrimEnd('/');
            var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            if (guids != null && guids.Length > 0)
            {
                var sprites = new System.Collections.Generic.List<Sprite>();
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite != null) sprites.Add(sprite);
                }
                sprites.Sort((a, b) => string.CompareOrdinal(a != null ? a.name : string.Empty, b != null ? b.name : string.Empty));
                return sprites.ToArray();
            }
#endif

            var catalogSprites = Catalog != null ? Catalog.FindAll(name) : null;
            if (catalogSprites != null && catalogSprites.Length > 0) return catalogSprites;
            return Resources.LoadAll<Sprite>(GeneratedPrefix + name);
        }

        public static bool IsGeneratedPath(string resourcePath)
        {
            return !string.IsNullOrEmpty(resourcePath) && resourcePath.StartsWith(GeneratedPrefix);
        }

        static string NormalizeName(string nameOrResourcePath)
        {
            if (string.IsNullOrEmpty(nameOrResourcePath)) return null;
            return nameOrResourcePath.StartsWith(GeneratedPrefix)
                ? nameOrResourcePath.Substring(GeneratedPrefix.Length)
                : nameOrResourcePath;
        }

        static GeneratedSpriteCatalog Catalog
        {
            get
            {
                if (catalog == null) catalog = Resources.Load<GeneratedSpriteCatalog>("GeneratedSpriteCatalog");
                return catalog;
            }
        }
    }
}
