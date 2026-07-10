using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class GeneratedSpriteAssetUtility
    {
        public const string Root = "Assets/AreaSurvivors/Sprites/Generated";

        public static Sprite LoadSprite(string nameOrRelativePath)
        {
            var path = FindSpritePath(nameOrRelativePath);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Texture2D LoadTexture(string nameOrRelativePath)
        {
            var path = FindSpritePath(nameOrRelativePath);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static string FindSpritePath(string nameOrRelativePath)
        {
            var normalized = NormalizeName(nameOrRelativePath);
            if (string.IsNullOrEmpty(normalized)) return null;

            var direct = $"{Root}/{normalized}.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(direct) != null || AssetImporter.GetAtPath(direct) != null)
            {
                return direct;
            }

            var basenameMatch = string.Empty;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { Root });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png")) continue;

                var relative = path.Substring(Root.Length + 1, path.Length - Root.Length - 1 - ".png".Length);
                if (relative == normalized) return path;

                var basename = System.IO.Path.GetFileNameWithoutExtension(path);
                if (basename != normalized) continue;
                if (!string.IsNullOrEmpty(basenameMatch)) return null;
                basenameMatch = path;
            }

            return string.IsNullOrEmpty(basenameMatch) ? null : basenameMatch;
        }

        public static void ImportSprite(string nameOrRelativePath, float pixelsPerUnit, Vector4 spriteBorder = default)
        {
            var path = FindSpritePath(nameOrRelativePath);
            if (string.IsNullOrEmpty(path)) return;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = spriteBorder;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static string NormalizeName(string nameOrRelativePath)
        {
            if (string.IsNullOrEmpty(nameOrRelativePath)) return null;
            var normalized = nameOrRelativePath.Replace("\\", "/").Trim('/');
            if (normalized.StartsWith("Generated/")) normalized = normalized.Substring("Generated/".Length);
            if (normalized.StartsWith(Root + "/")) normalized = normalized.Substring(Root.Length + 1);
            if (normalized.EndsWith(".png")) normalized = normalized.Substring(0, normalized.Length - ".png".Length);
            return normalized;
        }
    }
}
