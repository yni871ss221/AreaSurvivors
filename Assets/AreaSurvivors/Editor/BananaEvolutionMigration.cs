using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class BananaEvolutionMigration
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string WeaponBookScenePath = "Assets/AreaSurvivors/Scenes/07_WeaponBook.unity";
        const string TestLauncherScenePath = "Assets/AreaSurvivors/Scenes/08_GameTestLauncher.unity";
        const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Characters/Player.prefab";
        const string BoomerangPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/BoomerangSwordProjectile.prefab";
        const string BananaPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/BananaProjectile.prefab";
        const string CompletionMarkerRelativePath = "Library/AreaSafeUnity/banana-evolution-migration.ok";

        [MenuItem("Area Survivors/Migrations/Apply Banana Evolution")]
        public static void Apply()
        {
            string completionMarkerPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CompletionMarkerRelativePath);
            if (File.Exists(completionMarkerPath)) File.Delete(completionMarkerPath);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            GeneratedSpriteAssetUtility.ImportSprite("Weapons/BananaIcon", 100f);
            GeneratedSpriteAssetUtility.ImportSprite("Weapons/BananaProjectile", 96f);
            var icon = GeneratedSpriteAssetUtility.LoadSprite("Weapons/BananaIcon");
            var projectileSprite = GeneratedSpriteAssetUtility.LoadSprite("Weapons/BananaProjectile");
            if (icon == null || projectileSprite == null) throw new System.InvalidOperationException("Banana sprites could not be imported.");

            PreflightScenes();
            var bananaPrefab = CreateBananaProjectilePrefab(projectileSprite);
            AssignPlayerPrefab(bananaPrefab);
            UpdateGameScene(icon);
            UpdateWeaponBookScene(icon);
            UpdateTestLauncherScene();
            GeneratedSpriteCatalogBuilder.Rebuild();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(completionMarkerPath));
            File.WriteAllText(completionMarkerPath, System.DateTime.UtcNow.ToString("o"));
            Debug.Log("Banana evolution migration: completed.");
        }

        static void PreflightScenes()
        {
            WithScene(GameScenePath, scene =>
            {
                var presentations = ComponentsInScene<EvolutionChoicePresentation>(scene);
                if (presentations.Length != 3 || presentations.Any(item => item.evolutionWeaponIcon == null))
                {
                    throw new System.InvalidOperationException("05_Game requires exactly three level-up presentations with a static evolution icon source.");
                }

                var compactSlots = ComponentsInScene<WeaponHudCompactIconSlot>(scene);
                if (compactSlots.Length < 3 || compactSlots.Any(slot => !slot.icons.Any(entry => entry.weaponType == WeaponType.SwordRush && entry.icon != null)))
                {
                    throw new System.InvalidOperationException("05_Game compact HUD slots require static Sword Rush icon sources.");
                }

                foreach (var panelName in new[] { "Slash Weapon Status", "Arrow Weapon Status", "Fireball Weapon Status" })
                {
                    var panel = FindByName(scene, panelName);
                    if (panel == null || panel.transform.Find("Icon") == null)
                    {
                        throw new System.InvalidOperationException("05_Game detailed HUD is missing the common Icon source under " + panelName + ".");
                    }
                }
            }, false);

            WithScene(WeaponBookScenePath, scene =>
            {
                var screen = ComponentsInScene<WeaponBookScreen>(scene).FirstOrDefault();
                if (screen == null || screen.evolutionDiscoveredIcon == null || screen.evolutionUndiscoveredIcon == null)
                {
                    throw new System.InvalidOperationException("07_WeaponBook requires discovered and undiscovered static evolution icon sources.");
                }
            }, false);

            WithScene(TestLauncherScenePath, scene =>
            {
                if (FindByName(scene, GameTestLaunchScreen.WeaponTestButtonName(WeaponType.SwordRush)) == null)
                {
                    throw new System.InvalidOperationException("08_GameTestLauncher requires the Sword Rush weapon test button source.");
                }
            }, false);
        }

        static GameObject CreateBananaProjectilePrefab(Sprite projectileSprite)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BananaPrefabPath) == null)
            {
                if (!AssetDatabase.CopyAsset(BoomerangPrefabPath, BananaPrefabPath))
                {
                    throw new System.InvalidOperationException("Failed to create BananaProjectile.prefab.");
                }
            }

            var root = PrefabUtility.LoadPrefabContents(BananaPrefabPath);
            try
            {
                root.name = "BananaProjectile";
                root.transform.localScale = Vector3.one;
                var collider = root.GetComponent<CircleCollider2D>();
                if (collider != null) collider.radius = 0.5f;
                var visual = root.GetComponentInChildren<PaperMeshVisual>(true);
                if (visual == null) throw new System.InvalidOperationException("Banana projectile PaperMeshVisual is missing.");
                var serializedVisual = new SerializedObject(visual);
                serializedVisual.FindProperty("sourceSprite").objectReferenceValue = projectileSprite;
                serializedVisual.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BananaPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(BananaPrefabPath);
        }

        static void AssignPlayerPrefab(GameObject bananaPrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var runtime = root.GetComponent<AdvancedWeaponRuntime>();
                if (runtime == null) throw new System.InvalidOperationException("Player AdvancedWeaponRuntime is missing.");
                runtime.bananaPrefab = bananaPrefab;
                EditorUtility.SetDirty(runtime);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void UpdateGameScene(Sprite icon)
        {
            WithScene(GameScenePath, scene =>
            {
                foreach (var presentation in ComponentsInScene<EvolutionChoicePresentation>(scene))
                {
                    var bananaIcon = DuplicateIcon(presentation.evolutionWeaponIcon, "Banana Evolution Icon", icon);
                    presentation.evolutionWeaponIcons = new[]
                    {
                        new EvolutionChoicePresentation.EvolutionIconEntry { weaponType = WeaponType.SwordRush, icon = presentation.evolutionWeaponIcon },
                        new EvolutionChoicePresentation.EvolutionIconEntry { weaponType = WeaponType.Banana, icon = bananaIcon }
                    };
                    EditorUtility.SetDirty(presentation);
                }

                foreach (var slot in ComponentsInScene<WeaponHudCompactIconSlot>(scene))
                {
                    var swordRushEntry = slot.icons.FirstOrDefault(entry => entry.weaponType == WeaponType.SwordRush);
                    var bananaIcon = DuplicateIcon(swordRushEntry.icon, "Banana Icon", icon);
                    var entries = new List<WeaponHudCompactIconSlot.IconEntry>(slot.icons.Where(entry => entry.weaponType != WeaponType.Banana));
                    entries.Add(new WeaponHudCompactIconSlot.IconEntry { weaponType = WeaponType.Banana, icon = bananaIcon });
                    slot.icons = entries.ToArray();
                    EditorUtility.SetDirty(slot);
                }

                foreach (var panelName in new[] { "Slash Weapon Status", "Arrow Weapon Status", "Fireball Weapon Status" })
                {
                    var panel = FindByName(scene, panelName);
                    var source = panel != null ? panel.transform.Find("Icon")?.gameObject : null;
                    DuplicateIcon(source, "Banana Icon", icon);
                }
            });
        }

        static void UpdateWeaponBookScene(Sprite icon)
        {
            WithScene(WeaponBookScenePath, scene =>
            {
                var screen = ComponentsInScene<WeaponBookScreen>(scene).FirstOrDefault();
                if (screen == null) throw new System.InvalidOperationException("WeaponBookScreen is missing.");
                var bananaDiscovered = DuplicateIcon(screen.evolutionDiscoveredIcon, "Banana Evolution Icon", icon);
                var bananaUndiscovered = DuplicateIcon(screen.evolutionUndiscoveredIcon, "Banana Evolution Icon Locked", icon);
                screen.evolutionIcons = new[]
                {
                    new WeaponBookScreen.EvolutionIconEntry
                    {
                        weaponType = WeaponType.SwordRush,
                        discoveredIcon = screen.evolutionDiscoveredIcon,
                        undiscoveredIcon = screen.evolutionUndiscoveredIcon
                    },
                    new WeaponBookScreen.EvolutionIconEntry
                    {
                        weaponType = WeaponType.Banana,
                        discoveredIcon = bananaDiscovered,
                        undiscoveredIcon = bananaUndiscovered
                    }
                };
                EditorUtility.SetDirty(screen);
            });
        }

        static void UpdateTestLauncherScene()
        {
            WithScene(TestLauncherScenePath, scene =>
            {
                var source = FindByName(scene, GameTestLaunchScreen.WeaponTestButtonName(WeaponType.SwordRush));
                if (source == null) throw new System.InvalidOperationException("Sword Rush weapon test button is missing.");
                string targetName = GameTestLaunchScreen.WeaponTestButtonName(WeaponType.Banana);
                var clone = FindByName(scene, targetName);
                if (clone == null)
                {
                    clone = Object.Instantiate(source, source.transform.parent, false);
                    clone.name = targetName;
                    clone.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);
                    PositionNewSiblingAfter(source.GetComponent<RectTransform>(), clone.GetComponent<RectTransform>());
                }

                var label = clone.GetComponentInChildren<Text>(true);
                if (label != null) label.text = "バナナ";
                clone.SetActive(true);
            });
        }

        static void PositionNewSiblingAfter(RectTransform source, RectTransform target)
        {
            if (source == null || target == null || source.parent == null) return;
            if (source.parent.GetComponent<LayoutGroup>() != null) return;
            var previous = source.GetSiblingIndex() > 0 ? source.parent.GetChild(source.GetSiblingIndex() - 1) as RectTransform : null;
            Vector2 delta = previous != null ? source.anchoredPosition - previous.anchoredPosition : new Vector2(0f, -source.rect.height);
            target.anchoredPosition = source.anchoredPosition + delta;
        }

        static GameObject DuplicateIcon(GameObject source, string targetName, Sprite sprite)
        {
            if (source == null) throw new System.InvalidOperationException("Static evolution icon source is missing: " + targetName);
            var existing = source.transform.parent != null ? source.transform.parent.Find(targetName)?.gameObject : null;
            var target = existing != null ? existing : Object.Instantiate(source, source.transform.parent, false);
            target.name = targetName;
            foreach (var image in target.GetComponentsInChildren<Image>(true)) image.sprite = sprite;
            target.SetActive(false);
            return target;
        }

        static void WithScene(string path, System.Action<Scene> apply, bool saveChanges = true)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                apply(scene);
                if (saveChanges)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
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
    }
}
