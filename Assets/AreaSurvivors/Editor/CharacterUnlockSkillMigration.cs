using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.EditorTools
{
    public static class CharacterUnlockSkillMigration
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";
        const string ArcherSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Characters/Archer.png";
        const string MageSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Characters/Mage.png";
        const string MigrationMarkerPath = "Library/AreaSafeUnity/character-unlock-skill-migration.success";
        const string ValidatorMarkerPath = "Library/AreaSafeUnity/character-unlock-skill-validator.success";
        const string ArcherTitle = "アーチャーアンロック";
        const string ArcherDescription = "アーチャーと弓をアンロックします。";
        const string MageTitle = "メイジアンロック";
        const string MageDescription = "メイジとファイアボールをアンロックします。";

        [MenuItem("Area Survivors/Migrate/Character Unlock Skills")]
        public static void Migrate()
        {
            DeleteMarker(MigrationMarkerPath);
            bool openedHere;
            var scene = OpenScene(out openedHere);
            try
            {
                var nodes = FindNodes(scene);
                ApplyNode(nodes, UpgradeType.UnlockArcher, ArcherTitle, ArcherDescription, ArcherSpritePath);
                ApplyNode(nodes, UpgradeType.UnlockMage, MageTitle, MageDescription, MageSpritePath);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Failed to save 04_Upgrades.");
                LobbyCharacterSelectionMigration.Migrate();
                WriteMarker(MigrationMarkerPath);
                Debug.Log("Character unlock skill migration completed.");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        [MenuItem("Area Survivors/Validate/Character Unlock Skills")]
        public static void Validate()
        {
            DeleteMarker(ValidatorMarkerPath);
            bool openedHere;
            var scene = OpenScene(out openedHere);
            try
            {
                int errors = 0;
                if ((int)UpgradeType.UnlockArcher != 53) Error("UnlockArcher must preserve save ID 53.", ref errors);
                if ((int)UpgradeType.UnlockMage != 54) Error("UnlockMage must preserve save ID 54.", ref errors);
                if (ProgressionStore.GetMaxLevel(UpgradeType.UnlockArcher) != 1) Error("UnlockArcher must have max level 1.", ref errors);
                if (ProgressionStore.GetMaxLevel(UpgradeType.UnlockMage) != 1) Error("UnlockMage must have max level 1.", ref errors);
                if (WeaponCatalog.UnlockUpgrade(WeaponType.Arrow) != UpgradeType.UnlockArcher)
                {
                    Error("Bow must be unlocked by UnlockArcher.", ref errors);
                }
                if (WeaponCatalog.UnlockUpgrade(WeaponType.Fireball) != UpgradeType.UnlockMage)
                {
                    Error("Fireball must be unlocked by UnlockMage.", ref errors);
                }
                ValidateCharacterMapping(CharacterType.Archer, UpgradeType.UnlockArcher, ref errors);
                ValidateCharacterMapping(CharacterType.Mage, UpgradeType.UnlockMage, ref errors);
                ValidateTranslation(ArcherTitle, "Unlock Archer", ref errors);
                ValidateTranslation(ArcherDescription, "Unlocks the Archer and Bow.", ref errors);
                ValidateTranslation(MageTitle, "Unlock Mage", ref errors);
                ValidateTranslation(MageDescription, "Unlocks the Mage and Fireball.", ref errors);

                var nodes = FindNodes(scene);
                ValidateNode(nodes, UpgradeType.UnlockArcher, ArcherTitle, ArcherDescription, ArcherSpritePath, ref errors);
                ValidateNode(nodes, UpgradeType.UnlockMage, MageTitle, MageDescription, MageSpritePath, ref errors);

                if (errors > 0) throw new InvalidOperationException($"Character unlock skill validation failed with {errors} error(s).");
                WriteMarker(ValidatorMarkerPath);
                Debug.Log("Character unlock skill validation passed.");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ApplyNode(List<SkillNodeView> nodes, UpgradeType type, string title, string description, string spritePath)
        {
            var node = RequireSingleNode(nodes, type);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null) throw new InvalidOperationException("Character unlock sprite is missing: " + spritePath);
            node.ResolveReferences();
            if (node.icon == null) throw new InvalidOperationException(type + " node is missing its icon reference.");
            node.title = title;
            node.description = description;
            node.icon.sprite = sprite;
            EditorUtility.SetDirty(node);
            EditorUtility.SetDirty(node.icon);
        }

        static void ValidateNode(
            List<SkillNodeView> nodes,
            UpgradeType type,
            string expectedTitle,
            string expectedDescription,
            string expectedSpritePath,
            ref int errors)
        {
            SkillNodeView node;
            try
            {
                node = RequireSingleNode(nodes, type);
            }
            catch (Exception exception)
            {
                Error(exception.Message, ref errors);
                return;
            }

            node.ResolveReferences();
            if (node.title != expectedTitle) Error(type + " title is incorrect.", ref errors);
            if (node.description != expectedDescription) Error(type + " description is incorrect.", ref errors);
            var actualSpritePath = node.icon != null && node.icon.sprite != null
                ? AssetDatabase.GetAssetPath(node.icon.sprite)
                : string.Empty;
            if (actualSpritePath != expectedSpritePath) Error(type + " icon is incorrect.", ref errors);
        }

        static void ValidateCharacterMapping(CharacterType type, UpgradeType expected, ref int errors)
        {
            if (!CharacterUnlockCatalog.TryGetUnlockUpgrade(type, out var actual) || actual != expected)
            {
                Error(type + " character unlock mapping is incorrect.", ref errors);
            }
        }

        static void ValidateTranslation(string source, string expectedEnglish, ref int errors)
        {
            var actual = LocalizationTextCatalog.Translate(source, GameLanguage.English);
            if (actual != expectedEnglish)
            {
                Error("Localization is incorrect for: " + source, ref errors);
            }
        }

        static SkillNodeView RequireSingleNode(List<SkillNodeView> nodes, UpgradeType type)
        {
            SkillNodeView found = null;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] == null || nodes[i].type != type) continue;
                if (found != null) throw new InvalidOperationException("Multiple skill nodes found for " + type + ".");
                found = nodes[i];
            }
            if (found == null) throw new InvalidOperationException("Skill node is missing for " + type + ".");
            return found;
        }

        static List<SkillNodeView> FindNodes(Scene scene)
        {
            var nodes = new List<SkillNodeView>();
            foreach (var root in scene.GetRootGameObjects())
            {
                nodes.AddRange(root.GetComponentsInChildren<SkillNodeView>(true));
            }
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
            Debug.LogError("[Character Unlock Skill] " + message);
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
