using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class BananaEvolutionValidator
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string WeaponBookScenePath = "Assets/AreaSurvivors/Scenes/07_WeaponBook.unity";
        const string TestLauncherScenePath = "Assets/AreaSurvivors/Scenes/08_GameTestLauncher.unity";
        const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Characters/Player.prefab";
        const string BananaPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/BananaProjectile.prefab";
        const string BananaIconPath = "Assets/AreaSurvivors/Sprites/Generated/Weapons/BananaIcon.png";
        const string BananaProjectilePath = "Assets/AreaSurvivors/Sprites/Generated/Weapons/BananaProjectile.png";
        const string SuccessMarkerPath =
            "Library/AreaSafeUnity/banana-evolution-validator.ok";

        [MenuItem("Area Survivors/Validate/Banana Evolution")]
        public static void ValidateMenu()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
            if (!ValidateAll(true))
                throw new InvalidOperationException(
                    "Banana evolution validation failed.");
            string directory = Path.GetDirectoryName(SuccessMarkerPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(
                SuccessMarkerPath,
                DateTime.UtcNow.ToString("O"));
        }

        public static bool ValidateAll(bool logSuccess)
        {
            int errors = 0;
            if (!WeaponCatalog.IsEvolution(WeaponType.Banana) ||
                WeaponCatalog.BaseWeaponOf(WeaponType.Banana) != WeaponType.BoomerangSword ||
                WeaponCatalog.EvolutionOf(WeaponType.BoomerangSword) != WeaponType.Banana)
            {
                Error("Banana evolution catalog mapping is invalid.", ref errors);
            }

            var requirements = WeaponCatalog.EvolutionRequirementSources(WeaponType.Banana);
            if (requirements == null ||
                requirements.Length != 2 ||
                requirements[0] != "武器Lv.10" ||
                requirements[1] != "ゲームプレイ中の撃破数300")
            {
                Error(
                    "Banana evolution requirements must be Weapon Lv.10 and 300 gameplay kills.",
                    ref errors);
            }

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/AreaSurvivors/Resources/Config/GameConfig.asset");
            if (config == null || !Mathf.Approximately(config.bananaBaseRange, 1.4f) || config.bananaBaseProjectileCountBonus != 3)
            {
                Error("Banana evolution base stats must keep range 1.4 and projectile bonus +3.", ref errors);
            }

            var icon = ValidateSprite(BananaIconPath, 96, 96, 100f, ref errors);
            var projectileSprite = ValidateSprite(BananaProjectilePath, 96, 96, 96f, ref errors);
            var bananaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BananaPrefabPath);
            var projectile = bananaPrefab != null ? bananaPrefab.GetComponent<AdvancedWeaponProjectile>() : null;
            var collider = bananaPrefab != null ? bananaPrefab.GetComponent<CircleCollider2D>() : null;
            var visual = bananaPrefab != null ? bananaPrefab.GetComponentInChildren<PaperMeshVisual>(true) : null;
            var visualSprite = visual != null ? new SerializedObject(visual).FindProperty("sourceSprite")?.objectReferenceValue as Sprite : null;
            if (projectile == null || collider == null || !Mathf.Approximately(collider.radius, 0.5f) || visualSprite != projectileSprite)
            {
                Error("Banana projectile prefab references or collider are invalid.", ref errors);
            }

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var runtime = playerPrefab != null ? playerPrefab.GetComponent<AdvancedWeaponRuntime>() : null;
            if (runtime == null || runtime.bananaPrefab != bananaPrefab)
            {
                Error("Player prefab does not reference BananaProjectile.prefab.", ref errors);
            }

            errors += ValidateScene(GameScenePath, scene =>
            {
                int sceneErrors = 0;
                ValidateGameScene(scene, icon, ref sceneErrors);
                return sceneErrors;
            });
            errors += ValidateScene(WeaponBookScenePath, scene =>
            {
                int sceneErrors = 0;
                ValidateWeaponBookScene(scene, icon, ref sceneErrors);
                return sceneErrors;
            });
            errors += ValidateScene(TestLauncherScenePath, scene =>
            {
                int sceneErrors = 0;
                ValidateTestLauncherScene(scene, ref sceneErrors);
                return sceneErrors;
            });

            if (errors == 0 && logSuccess) Debug.Log("Banana evolution validator: passed.");
            return errors == 0;
        }

        static void ValidateGameScene(Scene scene, Sprite icon, ref int errors)
        {
            var presentations = ComponentsInScene<EvolutionChoicePresentation>(scene);
            if (presentations.Length != 3 || presentations.Any(item => !HasEvolutionIcon(item.evolutionWeaponIcons, WeaponType.SwordRush) || !HasEvolutionIcon(item.evolutionWeaponIcons, WeaponType.Banana, icon)))
            {
                Error("05_Game level-up choices are missing static Banana evolution icons.", ref errors);
            }

            var compactSlots = ComponentsInScene<WeaponHudCompactIconSlot>(scene);
            if (compactSlots.Length < 3 || compactSlots.Any(slot => !slot.icons.Any(entry => entry.weaponType == WeaponType.Banana && HasSprite(entry.icon, icon))))
            {
                Error("05_Game compact HUD slots are missing static Banana icons.", ref errors);
            }

            foreach (var panelName in new[] { "Slash Weapon Status", "Arrow Weapon Status", "Fireball Weapon Status" })
            {
                var panel = FindByName(scene, panelName);
                if (panel == null || !HasSprite(panel.transform.Find("Banana Icon")?.gameObject, icon))
                {
                    Error("05_Game detailed HUD is missing Banana Icon under " + panelName + ".", ref errors);
                }
            }
        }

        static void ValidateWeaponBookScene(Scene scene, Sprite icon, ref int errors)
        {
            var screen = ComponentsInScene<WeaponBookScreen>(scene).FirstOrDefault();
            var entry = screen != null
                ? screen.evolutionIcons.FirstOrDefault(item => item.weaponType == WeaponType.Banana)
                : default;
            var discovered = entry.discoveredIcon != null ? entry.discoveredIcon.GetComponent<Image>() : null;
            var undiscovered = entry.undiscoveredIcon != null ? entry.undiscoveredIcon.GetComponent<Image>() : null;
            var lockedColor = new Color(0f, 0f, 0f, 0.78f);
            if (screen == null || discovered == null || undiscovered == null || discovered.sprite != icon || undiscovered.sprite != icon || !ApproximatelyColor(undiscovered.color, lockedColor))
            {
                Error("07_WeaponBook Banana discovered/locked icon references are invalid.", ref errors);
            }
        }

        static void ValidateTestLauncherScene(Scene scene, ref int errors)
        {
            if (FindByName(scene, GameTestLaunchScreen.WeaponTestButtonName(WeaponType.Banana)) == null)
            {
                Error("08_GameTestLauncher is missing the Banana weapon test button.", ref errors);
            }
        }

        static Sprite ValidateSprite(string path, int width, int height, float pixelsPerUnit, ref int errors)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (sprite == null || texture == null || importer == null || texture.width != width || texture.height != height ||
                importer.textureType != TextureImporterType.Sprite || importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit))
            {
                Error("Banana sprite import settings are invalid: " + path, ref errors);
            }
            return sprite;
        }

        static bool HasEvolutionIcon(EvolutionChoicePresentation.EvolutionIconEntry[] entries, WeaponType type, Sprite sprite = null)
        {
            if (entries == null) return false;
            return entries.Any(entry => entry.weaponType == type && entry.icon != null && (sprite == null || HasSprite(entry.icon, sprite)));
        }

        static bool HasSprite(GameObject gameObject, Sprite sprite)
        {
            var image = gameObject != null ? gameObject.GetComponent<Image>() : null;
            return image != null && image.sprite == sprite;
        }

        static bool ApproximatelyColor(Color actual, Color expected)
        {
            return Mathf.Approximately(actual.r, expected.r) && Mathf.Approximately(actual.g, expected.g) &&
                Mathf.Approximately(actual.b, expected.b) && Mathf.Approximately(actual.a, expected.a);
        }

        static int ValidateScene(string path, System.Func<Scene, int> validate)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                return validate(scene);
            }
            finally
            {
                if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static T[] ComponentsInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        static GameObject FindByName(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform.name == name) return transform.gameObject;
                }
            }
            return null;
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError(message);
        }
    }
}
