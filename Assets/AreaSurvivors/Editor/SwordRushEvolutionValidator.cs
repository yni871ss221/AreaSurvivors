using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class SwordRushEvolutionValidator
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string WeaponBookScenePath = "Assets/AreaSurvivors/Scenes/07_WeaponBook.unity";
        const string TestLauncherScenePath = "Assets/AreaSurvivors/Scenes/08_GameTestLauncher.unity";
        const string SwordRushPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/SwordRushSlash.prefab";
        const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Characters/Player.prefab";

        [MenuItem("Area Survivors/Validate/Sword Rush Evolution")]
        public static void ValidateMenu()
        {
            if (!ValidateAll(true)) throw new InvalidOperationException("Sword Rush evolution validation failed.");
        }

        [MenuItem("Area Survivors/Validate/Weapon Book Evolution Panel")]
        public static void ValidateWeaponBookMenu()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(WeaponBookScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation) scene = EditorSceneManager.OpenScene(WeaponBookScenePath, OpenSceneMode.Additive);

            try
            {
                if (!ValidateWeaponBookSceneOnly(scene, true))
                {
                    throw new InvalidOperationException("Weapon Book evolution panel validation failed.");
                }
            }
            finally
            {
                if (openedForValidation && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static bool ValidateAll(bool logSuccess)
        {
            int errors = 0;
            var swordRushRequirements = WeaponCatalog.EvolutionRequirementSources(WeaponType.SwordRush);
            if (swordRushRequirements == null || swordRushRequirements.Length != 1 || swordRushRequirements[0] != "武器Lv.10")
            {
                Error("Sword Rush evolution requirement must be Weapon Lv.10.", ref errors);
            }
            ValidateAsset<Sprite>("Assets/AreaSurvivors/Sprites/Generated/Weapons/SwordRushIcon.png", ref errors);
            ValidateEffectSprite("Assets/AreaSurvivors/Sprites/Generated/Weapons/SwordRushSlashEffect.png", ref errors);
            ValidateEffectSprite("Assets/AreaSurvivors/Sprites/Generated/Weapons/SwordRushSlashEffectAlt.png", ref errors);

            var swordRushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SwordRushPrefabPath);
            var swordRushView = swordRushPrefab != null ? swordRushPrefab.GetComponent<SlashView>() : null;
            if (swordRushView == null)
            {
                Error("Sword Rush slash prefab or SlashView is missing.", ref errors);
            }
            else
            {
                var serializedView = new SerializedObject(swordRushView);
                var frames = serializedView.FindProperty("animationFrames");
                if (frames == null || frames.arraySize != 2 || frames.GetArrayElementAtIndex(0).objectReferenceValue == null || frames.GetArrayElementAtIndex(1).objectReferenceValue == null)
                {
                    Error("Sword Rush slash prefab must reference exactly two alternating frames.", ref errors);
                }

                ValidateFloat(serializedView, "hitboxWidthMultiplier", 1f, "Sword Rush hitbox width must match its 3.2 range.", ref errors);
                ValidateFloat(serializedView, "visualReferenceRange", 3.2f, "Sword Rush visual reference range must remain 3.2.", ref errors);
                ValidateFloat(serializedView, "visualScaleMultiplier", 1f, "Sword Rush visual must cover the same square as its hitbox.", ref errors);
            }

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var weapon = playerPrefab != null ? playerPrefab.GetComponent<WeaponController>() : null;
            if (weapon == null || weapon.swordRushSlashPrefab != swordRushPrefab)
            {
                Error("Player prefab does not reference SwordRushSlash.prefab.", ref errors);
            }

            ValidateGameScene(ref errors);
            ValidateWeaponBookScene(ref errors);
            ValidateTestLauncherScene(ref errors);

            if (errors == 0 && logSuccess) Debug.Log("Sword Rush evolution validator: passed.");
            return errors == 0;
        }

        static void ValidateGameScene(ref int errors)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(GameScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation) scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

            try
            {
            var presentations = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EvolutionChoicePresentation>(true))
                .ToArray();
            if (presentations.Length != 3 || presentations.Any(item => item.bounceVisual == null || item.evolutionWeaponIcon == null || item.standardWeaponIcon == null))
            {
                Error("05_Game must contain three fully bound EvolutionChoicePresentation components.", ref errors);
            }

            var compactSlots = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WeaponHudCompactIconSlot>(true))
                .ToArray();
            if (compactSlots.Length < 3 || compactSlots.Any(slot => slot.infoPanelBackground == null || slot.slotBackground == null || !slot.icons.Any(entry => entry.weaponType == WeaponType.SwordRush && entry.icon != null)))
            {
                Error("05_Game compact weapon slots are missing Sword Rush icons or background references.", ref errors);
            }

            var slashPanel = FindByName(scene, "Slash Weapon Status");
            if (slashPanel == null || slashPanel.transform.Find("Sword Rush Icon") == null)
            {
                Error("05_Game Slash Weapon Status is missing the static Sword Rush icon.", ref errors);
            }
            }
            finally
            {
                if (openedForValidation && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ValidateWeaponBookScene(ref int errors)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(WeaponBookScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation) scene = EditorSceneManager.OpenScene(WeaponBookScenePath, OpenSceneMode.Additive);

            try
            {
                ValidateWeaponBookScene(scene, ref errors);
            }
            finally
            {
                if (openedForValidation && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static bool ValidateWeaponBookSceneOnly(UnityEngine.SceneManagement.Scene scene, bool logSuccess)
        {
            int errors = 0;
            ValidateWeaponBookScene(scene, ref errors);
            if (errors == 0 && logSuccess) Debug.Log("Weapon Book evolution panel validator: passed.");
            return errors == 0;
        }

        static void ValidateWeaponBookScene(UnityEngine.SceneManagement.Scene scene, ref int errors)
        {
            var screen = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WeaponBookScreen>(true))
                .FirstOrDefault();
            if (screen == null || screen.evolutionPanel == null || screen.evolutionHeadingText == null || screen.evolutionWeaponNameText == null || screen.evolutionDescriptionText == null || screen.evolutionDiscoveredIcon == null || screen.evolutionUndiscoveredIcon == null)
            {
                Error("07_WeaponBook evolution panel references are incomplete.", ref errors);
                return;
            }

            var panelTransform = screen.evolutionPanel.transform;
            if (!screen.evolutionHeadingText.transform.IsChildOf(panelTransform) ||
                !screen.evolutionWeaponNameText.transform.IsChildOf(panelTransform) ||
                !screen.evolutionDescriptionText.transform.IsChildOf(panelTransform))
            {
                Error("07_WeaponBook evolution texts must be children of the Evolution Panel.", ref errors);
            }

            if (!screen.evolutionPanel.activeSelf ||
                screen.evolutionHeadingText.text != "進化武器" ||
                screen.evolutionWeaponNameText.text != "-" ||
                screen.evolutionDescriptionText.text != "-")
            {
                Error("07_WeaponBook evolution panel must be editor-visible with heading '進化武器' and '-' placeholders.", ref errors);
            }

            var discoveredImage = screen.evolutionDiscoveredIcon.GetComponent<Image>();
            var lockedImage = screen.evolutionUndiscoveredIcon.GetComponent<Image>();
            var lockedColor = new Color(0f, 0f, 0f, 0.78f);
            if (discoveredImage == null || lockedImage == null || discoveredImage.sprite == null || discoveredImage.sprite != lockedImage.sprite)
            {
                Error("07_WeaponBook discovered and locked evolution icons must reference the same source Sprite.", ref errors);
            }
            if (lockedImage == null || !ApproximatelyColor(lockedImage.color, lockedColor))
            {
                Error("07_WeaponBook locked evolution icon must use the standard black silhouette tint.", ref errors);
            }
        }

        static bool ApproximatelyColor(Color actual, Color expected)
        {
            return Mathf.Approximately(actual.r, expected.r) &&
                Mathf.Approximately(actual.g, expected.g) &&
                Mathf.Approximately(actual.b, expected.b) &&
                Mathf.Approximately(actual.a, expected.a);
        }

        static void ValidateTestLauncherScene(ref int errors)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(TestLauncherScenePath);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            if (openedForValidation) scene = EditorSceneManager.OpenScene(TestLauncherScenePath, OpenSceneMode.Additive);

            try
            {
            if (FindByName(scene, "Reset Weapon Evolutions Button") == null)
            {
                Error("08_GameTestLauncher is missing Reset Weapon Evolutions Button.", ref errors);
            }
            if (FindByName(scene, GameTestLaunchScreen.WeaponTestButtonName(WeaponType.SwordRush)) == null)
            {
                Error("08_GameTestLauncher is missing the Sword Rush weapon test button.", ref errors);
            }
            }
            finally
            {
                if (openedForValidation && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static GameObject FindByName(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var matches = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < matches.Length; i++)
                {
                    if (matches[i].name == name) return matches[i].gameObject;
                }
            }
            return null;
        }

        static void ValidateAsset<T>(string path, ref int errors) where T : UnityEngine.Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) == null) Error("Missing asset: " + path, ref errors);
        }

        static void ValidateFloat(SerializedObject serializedObject, string propertyName, float expected, string message, ref int errors)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null || !Mathf.Approximately(property.floatValue, expected)) Error(message, ref errors);
        }

        static void ValidateEffectSprite(string path, ref int errors)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (sprite == null || texture == null || importer == null)
            {
                Error("Missing Sword Rush effect sprite: " + path, ref errors);
                return;
            }

            if (texture.width != 320 || texture.height != 320) Error("Sword Rush effect sprite must be 320x320: " + path, ref errors);
            if (importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point || importer.mipmapEnabled || !Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
            {
                Error("Sword Rush effect sprite importer settings are invalid: " + path, ref errors);
            }
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError(message);
        }
    }
}
