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
            var editorSprite = LoadEditorSprite(name);
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
            var editorTexture = LoadEditorTexture(name);
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

#if UNITY_EDITOR
        static Sprite LoadEditorSprite(string name)
        {
            var path = FindEditorGeneratedPath(name);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Texture2D LoadEditorTexture(string name)
        {
            var path = FindEditorGeneratedPath(name);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static string FindEditorGeneratedPath(string name)
        {
            var normalized = name.Replace("\\", "/").Trim('/');
            var direct = GeneratedSpriteRoot + normalized + ".png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(direct) != null) return direct;

            string basenameMatch = null;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { GeneratedSpriteRoot.TrimEnd('/') });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png")) continue;

                var relative = path.Substring(GeneratedSpriteRoot.Length, path.Length - GeneratedSpriteRoot.Length - ".png".Length);
                if (relative == normalized) return path;

                var basename = System.IO.Path.GetFileNameWithoutExtension(path);
                if (basename != normalized) continue;
                if (basenameMatch != null) return null;
                basenameMatch = path;
            }

            return basenameMatch;
        }
#endif

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
