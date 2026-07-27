using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.EditorTools
{
    public static class RerollUpgradeSkillMigration
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";
        const string SpritePath = "Assets/AreaSurvivors/Sprites/Generated/SkillIcons/SkillReroll.png";
        const string SpriteResource = "SkillIcons/SkillReroll";
        const string CatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string MigrationMarkerPath = "Library/AreaSafeUnity/reroll-upgrade-skill-migration.success";
        const string ValidatorMarkerPath = "Library/AreaSafeUnity/reroll-upgrade-skill-validator.success";
        const string Title = "リロール回数追加";
        const string Description = "ゲーム開始時のリロール回数が1回増えます。";

        [MenuItem("Area Survivors/Migrate/Reroll Upgrade Skill")]
        public static void Migrate()
        {
            DeleteMarker(MigrationMarkerPath);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            GeneratedSpriteAssetUtility.ImportSprite(SpriteResource, 32f);
            GeneratedSpriteCatalogBuilder.Rebuild();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null) throw new InvalidOperationException("Reroll sprite is missing: " + SpritePath);

            bool openedHere;
            var scene = OpenScene(out openedHere);
            try
            {
                var node = RequireSingleNode(FindNodes(scene));
                node.ResolveReferences();
                if (node.icon == null) throw new InvalidOperationException("Reroll skill node is missing its icon reference.");
                node.title = Title;
                node.description = Description;
                node.icon.sprite = sprite;
                EditorUtility.SetDirty(node);
                EditorUtility.SetDirty(node.icon);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save 04_Upgrades.");
                WriteMarker(MigrationMarkerPath);
                Debug.Log("Reroll upgrade skill migration completed.");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Area Survivors/Validate/Reroll Upgrade Skill")]
        public static void Validate()
        {
            DeleteMarker(ValidatorMarkerPath);
            int errors = 0;
            if ((int)UpgradeType.LevelUpRerollCount != 65) Error("LevelUpRerollCount must preserve save ID 65.", ref errors);
            if (ProgressionStore.GetMaxLevel(UpgradeType.LevelUpRerollCount) != 5) Error("Reroll skill must have max level 5.", ref errors);

            int[] costs = { 80, 90, 100, 115, 130 };
            for (int level = 0; level < costs.Length; level++)
            {
                if (ProgressionStore.GetCost(UpgradeType.LevelUpRerollCount, level) != costs[level])
                {
                    Error("Reroll skill cost is incorrect at level " + level + ".", ref errors);
                }
            }

            if (ProgressionStore.CalculateInitialLevelUpRerollCount(0) != 3) Error("Base reroll count must be 3.", ref errors);
            if (ProgressionStore.CalculateInitialLevelUpRerollCount(1) != 4) Error("Each skill level must add one reroll.", ref errors);
            if (ProgressionStore.CalculateInitialLevelUpRerollCount(5) != 8) Error("Level 5 must start with 8 rerolls.", ref errors);
            if (StatIconCatalog.ForUpgrade(UpgradeType.LevelUpRerollCount) != StatIconCatalog.SkillReroll) Error("Reroll icon mapping is incorrect.", ref errors);
            ValidateTranslation(Title, "Additional Reroll", ref errors);
            ValidateTranslation(Description, "Adds 1 reroll at the start of each run.", ref errors);
            ValidateSpriteImport(ref errors);
            ValidateCatalog(ref errors);

            bool openedHere;
            var scene = OpenScene(out openedHere);
            try
            {
                var node = RequireSingleNode(FindNodes(scene));
                node.ResolveReferences();
                if (node.title != Title) Error("Reroll node title is incorrect.", ref errors);
                if (node.description != Description) Error("Reroll node description is incorrect.", ref errors);
                string actualSpritePath = node.icon != null && node.icon.sprite != null
                    ? AssetDatabase.GetAssetPath(node.icon.sprite)
                    : string.Empty;
                if (actualSpritePath != SpritePath) Error("Reroll node icon is incorrect.", ref errors);
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }

            if (errors > 0) throw new InvalidOperationException($"Reroll upgrade skill validation failed with {errors} error(s).");
            WriteMarker(ValidatorMarkerPath);
            Debug.Log("Reroll upgrade skill validation passed.");
        }

        static void ValidateSpriteImport(ref int errors)
        {
            var importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null)
            {
                Error("Reroll sprite importer is missing.", ref errors);
                return;
            }

            if (importer.textureType != TextureImporterType.Sprite) Error("Reroll texture type must be Sprite.", ref errors);
            if (importer.spriteImportMode != SpriteImportMode.Single) Error("Reroll sprite mode must be Single.", ref errors);
            if (importer.mipmapEnabled) Error("Reroll sprite mipmaps must be disabled.", ref errors);
            if (importer.filterMode != FilterMode.Point) Error("Reroll sprite filter mode must be Point.", ref errors);
            if (!importer.alphaIsTransparency) Error("Reroll alpha transparency must be enabled.", ref errors);
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 32f)) Error("Reroll sprite PPU must be 32.", ref errors);
        }

        static void ValidateCatalog(ref int errors)
        {
            var expected = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            var catalog = AssetDatabase.LoadAssetAtPath<GeneratedSpriteCatalog>(CatalogPath);
            if (expected == null || catalog == null || catalog.entries == null)
            {
                Error("Reroll sprite or generated sprite catalog is missing.", ref errors);
                return;
            }

            for (int i = 0; i < catalog.entries.Length; i++)
            {
                if (catalog.entries[i].name == SpriteResource && catalog.entries[i].sprite == expected) return;
            }
            Error("Reroll sprite is missing from GeneratedSpriteCatalog.", ref errors);
        }

        static void ValidateTranslation(string source, string expectedEnglish, ref int errors)
        {
            if (LocalizationTextCatalog.Translate(source, GameLanguage.English) != expectedEnglish)
            {
                Error("Localization is incorrect for: " + source, ref errors);
            }
        }

        static SkillNodeView RequireSingleNode(List<SkillNodeView> nodes)
        {
            SkillNodeView found = null;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null || nodes[i].type != UpgradeType.LevelUpRerollCount) continue;
                if (found != null) throw new InvalidOperationException("Multiple reroll skill nodes found.");
                found = nodes[i];
            }
            if (found == null) throw new InvalidOperationException("Reroll skill node is missing.");
            return found;
        }

        static List<SkillNodeView> FindNodes(Scene scene)
        {
            var nodes = new List<SkillNodeView>();
            foreach (var root in scene.GetRootGameObjects()) nodes.AddRange(root.GetComponentsInChildren<SkillNodeView>(true));
            return nodes;
        }

        static Scene OpenScene(out bool openedHere)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                openedHere = false;
                return scene;
            }
            openedHere = true;
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError("[Reroll Upgrade Skill] " + message);
        }

        static void DeleteMarker(string relativePath)
        {
            if (File.Exists(relativePath)) File.Delete(relativePath);
        }

        static void WriteMarker(string relativePath)
        {
            var directory = Path.GetDirectoryName(relativePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(relativePath, DateTime.UtcNow.ToString("o"));
        }
    }
}
