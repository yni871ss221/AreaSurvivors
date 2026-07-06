using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class BossSpecialAttackSetup
    {
        const string CatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string SpriteFolder = "Assets/AreaSurvivors/Sprites/Generated/Boss/OrcKing";
        const string EnemyPrefabPath = "Assets/AreaSurvivors/Prefabs/Enemy.prefab";
        const string ShockwavePrefabPath = "Assets/AreaSurvivors/Prefabs/BossShockwave.prefab";
        const float BossAttackPixelsPerUnit = 256f / 0.4667f;

        static readonly string[] SpriteNames =
        {
            "Down_Raise",
            "Down_Slam",
            "Right_Raise",
            "Right_Slam",
            "Up_Raise",
            "Up_Slam",
            "Shockwave"
        };

        [MenuItem("AreaSurvivors/Setup/Apply Boss Special Attacks")]
        public static void Apply()
        {
            ImportSprites();
            UpdateGeneratedSpriteCatalog();
            var shockwavePrefab = CreateShockwavePrefab();
            WireEnemyPrefab(shockwavePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Boss special attacks were applied.");
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
                importer.spritePixelsPerUnit = name == "Shockwave" ? 128f : BossAttackPixelsPerUnit;
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
                string catalogName = $"Boss/OrcKing/{name}";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");
                if (sprite == null) continue;
                int index = entries.FindIndex(entry => entry.name == catalogName);
                var next = new GeneratedSpriteCatalog.Entry { name = catalogName, sprite = sprite };
                if (index >= 0) entries[index] = next;
                else entries.Add(next);
            }

            catalog.entries = entries.OrderBy(entry => entry.name).ToArray();
            EditorUtility.SetDirty(catalog);
        }

        static GameObject CreateShockwavePrefab()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/Shockwave.png");
            var root = new GameObject("BossShockwave");
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            var hitbox = root.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = Vector2.one * (TileGrid.DefaultCellSize * 2f);

            var projectile = root.AddComponent<BossShockwaveProjectile>();
            projectile.shockwaveSprite = sprite;
            projectile.displaySeconds = 1f;
            projectile.hitboxSize = hitbox.size;

            var visualObject = new GameObject("Paper Visual");
            visualObject.transform.SetParent(root.transform, false);
            visualObject.AddComponent<PaperBillboard>();
            var visual = visualObject.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, WeaponSortingOrders.Impact);
            visual.visible = true;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ShockwavePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void WireEnemyPrefab(GameObject shockwavePrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            var special = root.GetComponent<BossSpecialAttackController>();
            if (special == null) special = root.AddComponent<BossSpecialAttackController>();
            special.bossKind = EnemyKind.OrcKing;
            special.shockwavePrefab = shockwavePrefab != null
                ? shockwavePrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(ShockwavePrefabPath);
            special.downAttackFrames = new[]
            {
                LoadSprite("Down_Raise"),
                LoadSprite("Down_Slam")
            };
            special.rightAttackFrames = new[]
            {
                LoadSprite("Right_Raise"),
                LoadSprite("Right_Slam")
            };
            special.upAttackFrames = new[]
            {
                LoadSprite("Up_Raise"),
                LoadSprite("Up_Slam")
            };
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
