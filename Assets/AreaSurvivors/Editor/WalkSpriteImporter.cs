using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public sealed class WalkSpriteImporter : AssetPostprocessor
    {
        const float EnemyWalkSpritePixelsPerUnit = 256f / 0.4667f;
        const float PlayerWalkSpritePixelsPerUnit = 256f / 0.46666667f;

        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Sprites/Generated/Walk/")) return;
            var isEnemy = IsEnemyWalkSprite(assetPath);
            var isPlayer = IsPlayerWalkSprite(assetPath);
            if (!isEnemy && !isPlayer) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = isEnemy ? EnemyWalkSpritePixelsPerUnit : PlayerWalkSpritePixelsPerUnit;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = isEnemy ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.filterMode = UnityEngine.FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }

        static bool IsEnemyWalkSprite(string path)
        {
            return path.Contains("/Sprites/Generated/Walk/Enemy");
        }

        static bool IsPlayerWalkSprite(string path)
        {
            return path.Contains("/Sprites/Generated/Walk/Knight/") ||
                path.Contains("/Sprites/Generated/Walk/Archer/") ||
                path.Contains("/Sprites/Generated/Walk/Mage/");
        }

    }
}
