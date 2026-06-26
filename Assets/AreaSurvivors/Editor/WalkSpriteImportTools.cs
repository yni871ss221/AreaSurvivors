using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class WalkSpriteImportTools
    {
        const string WalkSpriteFolder = "Assets/AreaSurvivors/Sprites/Generated/Walk";
        const float EnemyWalkSpritePixelsPerUnit = 256f / 0.4667f;
        const float PlayerWalkSpritePixelsPerUnit = 256f / 0.46666667f;

        [MenuItem("AreaSurvivors/Enemies/Normalize Enemy Walk Sprite Imports")]
        public static void NormalizeWalkSpriteImports()
        {
            NormalizeEnemyWalkSpriteImports();
        }

        [MenuItem("AreaSurvivors/Characters/Normalize Walk Sprite Imports")]
        public static void NormalizeCharacterWalkSpriteImports()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { WalkSpriteFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsEnemyWalkSprite(path))
                {
                    NormalizeSpriteImport(path, EnemyWalkSpritePixelsPerUnit, SpriteMeshType.FullRect);
                }
                else if (IsPlayerWalkSprite(path))
                {
                    NormalizeSpriteImport(path, PlayerWalkSpritePixelsPerUnit, SpriteMeshType.Tight);
                }
            }
        }

        static void NormalizeEnemyWalkSpriteImports()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { WalkSpriteFolder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsEnemyWalkSprite(path)) continue;
                NormalizeSpriteImport(path, EnemyWalkSpritePixelsPerUnit, SpriteMeshType.FullRect);
            }
        }

        static void NormalizeSpriteImport(string path, float pixelsPerUnit, SpriteMeshType spriteMeshType)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = spriteMeshType;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static bool IsEnemyWalkSprite(string path)
        {
            return path.Contains("/Walk/Enemy");
        }

        static bool IsPlayerWalkSprite(string path)
        {
            return path.Contains("/Walk/Knight/") ||
                path.Contains("/Walk/Archer/") ||
                path.Contains("/Walk/Mage/");
        }

    }
}
