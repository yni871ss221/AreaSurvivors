using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class LichSummonAttackSetup
    {
        const string CatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string SpriteFolder = "Assets/AreaSurvivors/Sprites/Generated/Boss/Lich";
        const string EnemyPrefabPath = "Assets/AreaSurvivors/Prefabs/Enemy.prefab";
        const string SummonCirclePrefabPath = "Assets/AreaSurvivors/Prefabs/LichSummonCircle.prefab";
        const float BossAttackPixelsPerUnit = 256f / 0.4667f;

        static readonly string[] SpriteNames =
        {
            "Down_Cast",
            "Right_Cast",
            "Left_Cast",
            "Up_Cast",
            "SummonCircle"
        };

        [MenuItem("AreaSurvivors/Setup/Apply Lich Summon Attack")]
        public static void Apply()
        {
            ImportSprites();
            UpdateGeneratedSpriteCatalog();
            var summonCirclePrefab = CreateSummonCirclePrefab();
            WireEnemyPrefab(summonCirclePrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Lich summon attack was applied.");
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
                importer.spritePixelsPerUnit = name == "SummonCircle" ? 256f : BossAttackPixelsPerUnit;
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
                string catalogName = $"Boss/Lich/{name}";
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

        static GameObject CreateSummonCirclePrefab()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/SummonCircle.png");
            var root = new GameObject("LichSummonCircle");
            var effect = root.AddComponent<LichSummonCircleEffect>();
            effect.durationSeconds = 2.2f;

            var visualObject = new GameObject("Summon Circle Visual");
            visualObject.transform.SetParent(root.transform, false);
            visualObject.AddComponent<PaperBillboard>().faceCamera = false;
            var visual = visualObject.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, new Color(1f, 1f, 1f, 0.72f), WeaponSortingOrders.AreaEffect);
            visual.visible = true;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, SummonCirclePrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void WireEnemyPrefab(GameObject summonCirclePrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            var special = root.GetComponent<LichSummonAttackController>();
            if (special == null) special = root.AddComponent<LichSummonAttackController>();
            special.summonCirclePrefab = summonCirclePrefab != null
                ? summonCirclePrefab
                : AssetDatabase.LoadAssetAtPath<GameObject>(SummonCirclePrefabPath);
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
