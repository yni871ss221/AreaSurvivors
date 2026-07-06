using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class DragonBreathAttackSetup
    {
        const string CatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string SpriteFolder = "Assets/AreaSurvivors/Sprites/Generated/Boss/Dragon";
        const string EnemyPrefabPath = "Assets/AreaSurvivors/Prefabs/Enemy.prefab";
        const string BreathProjectilePrefabPath = "Assets/AreaSurvivors/Prefabs/BossDragonBreath.prefab";
        const float DragonPixelsPerUnit = 256f / 0.4667f;
        const float EffectPixelsPerUnit = 128f;

        static readonly string[] SpriteNames =
        {
            "Down_MouthClosed",
            "Down_MouthOpen",
            "Right_MouthClosed",
            "Right_MouthOpen",
            "Left_MouthClosed",
            "Left_MouthOpen",
            "Up_MouthClosed",
            "Up_MouthOpen",
            "BreathFireball",
            "BreathExplosion"
        };

        [MenuItem("AreaSurvivors/Setup/Apply Dragon Breath Attack")]
        public static void Apply()
        {
            ImportSprites();
            UpdateGeneratedSpriteCatalog();
            var projectilePrefab = CreateBreathProjectilePrefab();
            WireEnemyPrefab(projectilePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Dragon breath attack was applied.");
        }

        static void ImportSprites()
        {
            foreach (var name in SpriteNames)
            {
                string path = $"{SpriteFolder}/{name}.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = name.StartsWith("Breath") ? EffectPixelsPerUnit : DragonPixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        static void UpdateGeneratedSpriteCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GeneratedSpriteCatalog>(CatalogPath);
            if (catalog == null) return;
            var entries = catalog.entries != null
                ? catalog.entries.ToList()
                : new List<GeneratedSpriteCatalog.Entry>();

            foreach (var name in SpriteNames)
            {
                string catalogName = $"Boss/Dragon/{name}";
                var sprite = LoadSprite(name);
                if (sprite == null) continue;
                int index = entries.FindIndex(entry => entry.name == catalogName);
                var next = new GeneratedSpriteCatalog.Entry { name = catalogName, sprite = sprite };
                if (index >= 0) entries[index] = next;
                else entries.Add(next);
            }

            catalog.entries = entries.OrderBy(entry => entry.name).ToArray();
            EditorUtility.SetDirty(catalog);
        }

        static GameObject CreateBreathProjectilePrefab()
        {
            var fireballSprite = LoadSprite("BreathFireball");
            var explosionSprite = LoadSprite("BreathExplosion");
            var root = new GameObject("BossDragonBreath");
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            var hitbox = root.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = Vector2.one * (TileGrid.DefaultCellSize * 3f);

            var projectile = root.AddComponent<BossDragonBreathProjectile>();
            projectile.fireballSprite = fireballSprite;
            projectile.explosionSprite = explosionSprite;
            projectile.damage = 1;
            projectile.speed = 4.2f;
            projectile.rangeWorld = TileGrid.DefaultCellSize * 15f;
            projectile.hitboxSizeWorld = hitbox.size;
            projectile.explosionRadiusWorld = TileGrid.DefaultCellSize * 3f;
            projectile.projectileVisualScale = 1f;
            projectile.explosionDurationSeconds = 0.28f;

            var visualObject = new GameObject("Paper Visual");
            visualObject.transform.SetParent(root.transform, false);
            visualObject.AddComponent<PaperBillboard>();
            var visual = visualObject.AddComponent<PaperMeshVisual>();
            visual.Configure(fireballSprite, Color.white, WeaponSortingOrders.Projectile);
            visual.visible = true;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BreathProjectilePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void WireEnemyPrefab(GameObject breathProjectilePrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            var special = root.GetComponent<DragonBreathAttackController>();
            if (special == null) special = root.AddComponent<DragonBreathAttackController>();
            special.breathProjectilePrefab = breathProjectilePrefab != null
                ? breathProjectilePrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(BreathProjectilePrefabPath);
            special.downMouthClosedFrame = LoadSprite("Down_MouthClosed");
            special.downMouthOpenFrame = LoadSprite("Down_MouthOpen");
            special.rightMouthClosedFrame = LoadSprite("Right_MouthClosed");
            special.rightMouthOpenFrame = LoadSprite("Right_MouthOpen");
            special.leftMouthClosedFrame = LoadSprite("Left_MouthClosed");
            special.leftMouthOpenFrame = LoadSprite("Left_MouthOpen");
            special.upMouthClosedFrame = LoadSprite("Up_MouthClosed");
            special.upMouthOpenFrame = LoadSprite("Up_MouthOpen");
            EditorUtility.SetDirty(special);
            PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");
        }
    }
}
