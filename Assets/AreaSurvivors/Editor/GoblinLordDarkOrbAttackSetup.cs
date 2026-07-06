using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class GoblinLordDarkOrbAttackSetup
    {
        const string CatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string SpriteFolder = "Assets/AreaSurvivors/Sprites/Generated/Boss/GoblinLord";
        const string EnemyPrefabPath = "Assets/AreaSurvivors/Prefabs/Enemy.prefab";
        const string DarkOrbPrefabPath = "Assets/AreaSurvivors/Prefabs/BossDarkOrb.prefab";
        const float BossAttackPixelsPerUnit = 256f / 0.4667f;

        static readonly string[] SpriteNames =
        {
            "Down_Cast",
            "Right_Cast",
            "Left_Cast",
            "Up_Cast",
            "DarkOrb"
        };

        [MenuItem("AreaSurvivors/Setup/Apply Goblin Lord Dark Orb Attack")]
        public static void Apply()
        {
            ImportSprites();
            UpdateGeneratedSpriteCatalog();
            var darkOrbPrefab = CreateDarkOrbPrefab();
            WireEnemyPrefab(darkOrbPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Goblin Lord dark orb attack was applied.");
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
                importer.spritePixelsPerUnit = name == "DarkOrb" ? 256f : BossAttackPixelsPerUnit;
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
                string catalogName = $"Boss/GoblinLord/{name}";
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

        static GameObject CreateDarkOrbPrefab()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/DarkOrb.png");
            var root = new GameObject("BossDarkOrb");
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;

            var hitbox = root.AddComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = 1.25f;

            var projectile = root.AddComponent<BossDarkOrbProjectile>();
            projectile.orbSprite = sprite;
            projectile.damage = 4;
            projectile.damageRadius = hitbox.radius;
            projectile.speed = 2.4f;
            projectile.lifetimeSeconds = 8f;
            projectile.damageIntervalSeconds = 0.45f;
            projectile.visualScale = 1f;

            var visualObject = new GameObject("Paper Visual");
            visualObject.transform.SetParent(root.transform, false);
            visualObject.AddComponent<PaperBillboard>();
            var visual = visualObject.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, WeaponSortingOrders.Projectile);
            visual.visible = true;

            AddDarkOrbRangeVisual(root.transform);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DarkOrbPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void AddDarkOrbRangeVisual(Transform root)
        {
            var rangeRoot = new GameObject("Range Visual");
            rangeRoot.transform.SetParent(root, false);
            var fillFilter = rangeRoot.AddComponent<MeshFilter>();
            var fillRenderer = rangeRoot.AddComponent<MeshRenderer>();
            var outlineRenderer = rangeRoot.AddComponent<LineRenderer>();
            var rangeVisual = rangeRoot.AddComponent<ThunderBallRangeVisual>();
            rangeVisual.Initialize(fillFilter, fillRenderer, outlineRenderer);
        }

        static void WireEnemyPrefab(GameObject darkOrbPrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            var special = root.GetComponent<GoblinLordDarkOrbAttackController>();
            if (special == null) special = root.AddComponent<GoblinLordDarkOrbAttackController>();
            special.darkOrbPrefab = darkOrbPrefab != null
                ? darkOrbPrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(DarkOrbPrefabPath);
            special.downCastFrame = LoadSprite("Down_Cast");
            special.rightCastFrame = LoadSprite("Right_Cast");
            special.leftCastFrame = LoadSprite("Left_Cast");
            special.upCastFrame = LoadSprite("Up_Cast");
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
