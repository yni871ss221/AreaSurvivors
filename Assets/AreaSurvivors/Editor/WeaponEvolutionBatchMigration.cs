using System;
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
    public static class WeaponEvolutionBatchMigration
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string WeaponBookScenePath = "Assets/AreaSurvivors/Scenes/07_WeaponBook.unity";
        const string TestLauncherScenePath = "Assets/AreaSurvivors/Scenes/08_GameTestLauncher.unity";
        const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Characters/Player.prefab";
        const string GameConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const string PrefabRoot = "Assets/AreaSurvivors/Prefabs/Weapons/";
        const string GeneratedWeaponRoot = "Assets/AreaSurvivors/Sprites/Generated/Weapons/";
        const string CompletionMarkerRelativePath = "Library/AreaSafeUnity/weapon-evolution-batch-migration.ok";

        static readonly Color LockedIconColor = new Color(0f, 0f, 0f, 0.78f);
        static readonly Color GoldenArrowTint = new Color(1f, 0.75f, 0.1f, 1f);

        sealed class EvolutionSpec
        {
            public WeaponType type;
            public WeaponType sourceType;
            public string assetName;
            public string effectAssetName;
            public string detailIconName;
            public string sourcePrefab;
            public string targetPrefab;
            public Type requiredComponent;
            public float effectPixelsPerUnit;
        }

        static readonly EvolutionSpec[] NewEvolutionSpecs =
        {
            Spec(WeaponType.Excalibur, WeaponType.AuraSword, "Excalibur", "Excalibur Icon", "AuraSwordProjectile.prefab", "ExcaliburSlash.prefab", typeof(AdvancedWeaponProjectile)),
            Spec(WeaponType.GoldenBow, WeaponType.Arrow, "GoldenBow", "Golden Bow Icon", "PlayerArrow.prefab", "GoldenArrow.prefab", typeof(Projectile)),
            Spec(WeaponType.ArrowShower, WeaponType.ArrowRain, "ArrowShower", "Arrow Shower Icon", "AdvancedWeaponArea.prefab", "ArrowShowerStrike.prefab", typeof(AdvancedWeaponArea)),
            Spec(WeaponType.MachineGun, WeaponType.Gun, "MachineGun", "Machine Gun Icon", "GunBulletProjectile.prefab", "MachineGunBullet.prefab", typeof(AdvancedWeaponProjectile), 64f, "MachineGunBullet"),
            Spec(WeaponType.FireMissile, WeaponType.Fireball, "FireMissile", "Fire Missile Icon", "Fireball.prefab", "FireMissile.prefab", typeof(Projectile)),
            Spec(WeaponType.FrostStorm, WeaponType.Frost, "FrostStorm", "Frost Storm Icon", "FrostArea.prefab", "FrostStormSpike.prefab", typeof(AdvancedWeaponArea), 128f, "FrostAreaTexture"),
            Spec(WeaponType.ThunderStorm, WeaponType.ThunderBall, "ThunderStorm", "Thunder Storm Icon", "ThunderBallProjectile.prefab", "ThunderStormOrb.prefab", typeof(AdvancedWeaponProjectile)),
            Spec(WeaponType.DualShield, WeaponType.Shield, "DualShield", "Dual Shield Icon", "Shield.prefab", "DualShield.prefab", typeof(ShieldOrbitShield)),
            Spec(WeaponType.GoddessBlessing, WeaponType.Flag, "GoddessBlessing", "Goddess Blessing Icon", "AdvancedWeaponArea.prefab", "GoddessBlessingArea.prefab", typeof(AdvancedWeaponArea))
        };

        static readonly WeaponType[] AllEvolutionTypes =
        {
            WeaponType.SwordRush,
            WeaponType.Banana,
            WeaponType.Excalibur,
            WeaponType.GoldenBow,
            WeaponType.ArrowShower,
            WeaponType.MachineGun,
            WeaponType.FireMissile,
            WeaponType.FrostStorm,
            WeaponType.ThunderStorm,
            WeaponType.DualShield,
            WeaponType.GoddessBlessing
        };

        static readonly WeaponType[] BaseWeaponTypes =
        {
            WeaponType.Slash,
            WeaponType.Arrow,
            WeaponType.Fireball,
            WeaponType.Shield,
            WeaponType.Flag,
            WeaponType.BoomerangSword,
            WeaponType.AuraSword,
            WeaponType.ArrowRain,
            WeaponType.Gun,
            WeaponType.Frost,
            WeaponType.ThunderBall
        };

        static readonly Dictionary<WeaponType, string> DetailIconNames = new Dictionary<WeaponType, string>
        {
            { WeaponType.SwordRush, "Sword Rush Icon" },
            { WeaponType.Banana, "Banana Icon" },
            { WeaponType.Excalibur, "Excalibur Icon" },
            { WeaponType.GoldenBow, "Golden Bow Icon" },
            { WeaponType.ArrowShower, "Arrow Shower Icon" },
            { WeaponType.MachineGun, "Machine Gun Icon" },
            { WeaponType.FireMissile, "Fire Missile Icon" },
            { WeaponType.FrostStorm, "Frost Storm Icon" },
            { WeaponType.ThunderStorm, "Thunder Storm Icon" },
            { WeaponType.DualShield, "Dual Shield Icon" },
            { WeaponType.GoddessBlessing, "Goddess Blessing Icon" }
        };

        [MenuItem("Area Survivors/Migrations/Apply Weapon Evolution Batch")]
        public static void Apply()
        {
            string markerPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CompletionMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            PreflightAllInputs();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var icons = ImportAndLoadSprites();
            var effects = LoadEvolutionEffects();
            var prefabs = CreateEvolutionPrefabs(effects);
            AssignPlayerPrefab(prefabs);
            UpdateGameScene(icons);
            UpdateWeaponBookScene(icons);
            UpdateTestLauncherScene(icons);
            GeneratedSpriteCatalogBuilder.Rebuild();
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Weapon evolution batch migration: completed.");
        }

        [MenuItem("Area Survivors/Migrations/Fix Game Test Launcher Evolution Icons")]
        public static void FixGameTestLauncherEvolutionIcons()
        {
            var icons = LoadAllEvolutionIcons();
            WithScene(TestLauncherScenePath, scene => ApplyTestLauncherIcons(scene, icons));
            AssetDatabase.SaveAssets();
            Debug.Log("Game Test Launcher evolution icons: fixed.");
        }

        [MenuItem("Area Survivors/Migrations/Repair Weapon UI Icon Bindings")]
        public static void RepairWeaponUiIconBindings()
        {
            WithScene(GameScenePath, scene =>
            {
                foreach (var slot in ComponentsInScene<WeaponHudCompactIconSlot>(scene))
                {
                    slot.icons = BuildCompleteCompactIconEntries(slot);
                    EditorUtility.SetDirty(slot);
                }
            });

            WithScene(WeaponBookScenePath, scene =>
            {
                var screen = ComponentsInScene<WeaponBookScreen>(scene).FirstOrDefault();
                if (screen == null) throw new InvalidOperationException("07_WeaponBook is missing WeaponBookScreen.");
                SetActive(screen.evolutionDiscoveredIcon, false);
                SetActive(screen.evolutionUndiscoveredIcon, false);
                EditorUtility.SetDirty(screen);
            });

            AssetDatabase.SaveAssets();
            Debug.Log("Weapon UI icon bindings: repaired without changing layout or Source Image references.");
        }

        [MenuItem("Area Survivors/Migrations/Apply Golden Bow Projectile Visual Settings")]
        public static void ApplyGoldenBowProjectileVisualSettings()
        {
            GeneratedSpriteAssetUtility.ImportSprite("Weapons/GoldenBowEffect", 96f);
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "PlayerArrow.prefab");
            var sourceSprite = GetStaticPrefabSprite(sourcePrefab);
            if (sourceSprite == null) throw new InvalidOperationException("PlayerArrow prefab sprite is missing.");

            var root = PrefabUtility.LoadPrefabContents(PrefabRoot + "GoldenArrow.prefab");
            try
            {
                SetStaticPrefabSprite(root, sourceSprite);
                SetGoldenArrowTint(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabRoot + "GoldenArrow.prefab");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Golden Bow projectile visual settings: applied.");
        }

        [MenuItem("Area Survivors/Migrations/Apply Machine Gun Bullet Visual Settings")]
        public static void ApplyMachineGunBulletVisualSettings()
        {
            const string spriteName = "Weapons/MachineGunBullet";
            const string targetPrefabPath = PrefabRoot + "MachineGunBullet.prefab";
            GeneratedSpriteAssetUtility.ImportSprite(spriteName, 64f);
            var sprite = GeneratedSpriteAssetUtility.LoadSprite(spriteName);
            if (sprite == null) throw new InvalidOperationException("MachineGunBullet sprite import failed.");

            var root = PrefabUtility.LoadPrefabContents(targetPrefabPath);
            try
            {
                if (root.GetComponent<AdvancedWeaponProjectile>() == null)
                {
                    throw new InvalidOperationException("MachineGunBullet prefab requires AdvancedWeaponProjectile.");
                }
                SetStaticPrefabSprite(root, sprite);
                if (PrefabUtility.SaveAsPrefabAsset(root, targetPrefabPath) == null)
                {
                    throw new InvalidOperationException("Failed to save MachineGunBullet prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Machine Gun bullet visual settings: applied without runtime scale overrides.");
        }

        [MenuItem("Area Survivors/Migrations/Apply Fire Missile Cooldown Correction")]
        public static void ApplyFireMissileCooldownCorrection()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null) throw new InvalidOperationException("GameConfig is missing: " + GameConfigPath);
            config.fireMissileBaseCooldownMultiplier = 0.5f;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log("Fire Missile base cooldown correction: applied at 0.5x Fireball Lv.1 cooldown.");
        }

        [MenuItem("Area Survivors/Migrations/Apply Fire Missile Homing")]
        public static void ApplyFireMissileHoming()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null) throw new InvalidOperationException("GameConfig is missing: " + GameConfigPath);
            config.fireMissileHomingTurnSpeedDegrees = 180f;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log("Fire Missile homing: applied with random launch direction and 180 degrees/second turn speed.");
        }

        static EvolutionSpec Spec(WeaponType type, WeaponType sourceType, string assetName, string detailIconName,
            string sourcePrefab, string targetPrefab, Type requiredComponent, float effectPixelsPerUnit = 96f,
            string effectAssetName = null)
        {
            return new EvolutionSpec
            {
                type = type,
                sourceType = sourceType,
                assetName = assetName,
                effectAssetName = string.IsNullOrEmpty(effectAssetName) ? assetName + "Effect" : effectAssetName,
                detailIconName = detailIconName,
                sourcePrefab = PrefabRoot + sourcePrefab,
                targetPrefab = PrefabRoot + targetPrefab,
                requiredComponent = requiredComponent,
                effectPixelsPerUnit = effectPixelsPerUnit
            };
        }

        static void PreflightAllInputs()
        {
            foreach (var spec in NewEvolutionSpecs)
            {
                RequireFile(GeneratedWeaponRoot + spec.assetName + "Icon.png");
                RequireFile(GeneratedWeaponRoot + spec.effectAssetName + ".png");
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.sourcePrefab);
                if (source == null) throw new InvalidOperationException("Source prefab is missing: " + spec.sourcePrefab);
                if (!HasRequiredComponent(source, spec.requiredComponent))
                {
                    throw new InvalidOperationException(spec.sourcePrefab + " requires " + spec.requiredComponent.Name + ".");
                }
                if (!HasStaticSpriteTarget(source)) throw new InvalidOperationException("Prefab has no static sprite target: " + spec.sourcePrefab);
            }

            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (player == null || player.GetComponent<WeaponController>() == null || player.GetComponent<AdvancedWeaponRuntime>() == null || player.GetComponent<ShieldOrbitController>() == null)
            {
                throw new InvalidOperationException("Player prefab is missing weapon runtime components.");
            }

            var weapon = player.GetComponent<WeaponController>();
            RequireSerializedProperty(weapon, "goldenArrowPrefab");
            RequireSerializedProperty(weapon, "fireMissilePrefab");
            var runtime = player.GetComponent<AdvancedWeaponRuntime>();
            RequireSerializedProperty(runtime, "excaliburSlashPrefab");
            RequireSerializedProperty(runtime, "arrowShowerStrikePrefab");
            RequireSerializedProperty(runtime, "machineGunBulletPrefab");
            RequireSerializedProperty(runtime, "frostStormSpikePrefab");
            RequireSerializedProperty(runtime, "thunderStormOrbPrefab");
            RequireSerializedProperty(runtime, "goddessBlessingAreaPrefab");
            RequireSerializedProperty(player.GetComponent<ShieldOrbitController>(), "dualShieldPrefab");

            PreflightScenes();
        }

        static void RequireFile(string assetPath)
        {
            string absolute = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
            if (!File.Exists(absolute)) throw new InvalidOperationException("Generated sprite source is missing: " + assetPath);
        }

        static void RequireSerializedProperty(UnityEngine.Object target, string propertyName)
        {
            if (target == null || new SerializedObject(target).FindProperty(propertyName) == null)
            {
                throw new InvalidOperationException("Required serialized property is missing: " + propertyName);
            }
        }

        static void PreflightScenes()
        {
            WithScene(GameScenePath, scene =>
            {
                var presentations = ComponentsInScene<EvolutionChoicePresentation>(scene);
                if (presentations.Length != 3 || presentations.Any(item => item.evolutionWeaponIcon == null))
                {
                    throw new InvalidOperationException("05_Game requires exactly three level-up presentations with a static evolution icon source.");
                }

                var slots = ComponentsInScene<WeaponHudCompactIconSlot>(scene);
                if (slots.Length < 3 || slots.Any(slot =>
                        FindIcon(slot.icons, WeaponType.SwordRush) == null ||
                        BaseWeaponTypes.Any(type => FindIcon(slot.icons, type) == null)))
                {
                    throw new InvalidOperationException("05_Game compact HUD slots require all base weapon icons and a static Sword Rush icon source.");
                }

                foreach (var panelName in DetailPanelNames())
                {
                    var panel = FindByName(scene, panelName);
                    if (panel == null || panel.transform.Find("Icon") == null)
                    {
                        throw new InvalidOperationException("05_Game detailed HUD is missing Icon under " + panelName + ".");
                    }
                }
            }, false);

            WithScene(WeaponBookScenePath, scene =>
            {
                var screen = ComponentsInScene<WeaponBookScreen>(scene).FirstOrDefault();
                if (screen == null || screen.evolutionDiscoveredIcon == null || screen.evolutionUndiscoveredIcon == null)
                {
                    throw new InvalidOperationException("07_WeaponBook requires discovered and undiscovered static icon sources.");
                }
            }, false);

            WithScene(TestLauncherScenePath, scene =>
            {
                if (FindByName(scene, GameTestLaunchScreen.WeaponTestButtonName(WeaponType.SwordRush)) == null)
                {
                    throw new InvalidOperationException("08_GameTestLauncher requires the Sword Rush button source.");
                }
            }, false);
        }

        static Dictionary<WeaponType, Sprite> ImportAndLoadSprites()
        {
            foreach (var spec in NewEvolutionSpecs)
            {
                GeneratedSpriteAssetUtility.ImportSprite("Weapons/" + spec.assetName + "Icon", 100f);
                GeneratedSpriteAssetUtility.ImportSprite("Weapons/" + spec.effectAssetName, spec.effectPixelsPerUnit);
            }

            return LoadAllEvolutionIcons();
        }

        static Dictionary<WeaponType, Sprite> LoadAllEvolutionIcons()
        {
            var result = new Dictionary<WeaponType, Sprite>
            {
                { WeaponType.SwordRush, GeneratedSpriteAssetUtility.LoadSprite("Weapons/SwordRushIcon") },
                { WeaponType.Banana, GeneratedSpriteAssetUtility.LoadSprite("Weapons/BananaIcon") }
            };
            foreach (var spec in NewEvolutionSpecs)
            {
                result[spec.type] = GeneratedSpriteAssetUtility.LoadSprite("Weapons/" + spec.assetName + "Icon");
            }
            if (result.Any(pair => pair.Value == null)) throw new InvalidOperationException("One or more evolution icons could not be loaded.");
            return result;
        }

        static Dictionary<WeaponType, Sprite> LoadEvolutionEffects()
        {
            var result = new Dictionary<WeaponType, Sprite>();
            foreach (var spec in NewEvolutionSpecs)
            {
                result[spec.type] = GeneratedSpriteAssetUtility.LoadSprite("Weapons/" + spec.effectAssetName);
            }
            if (result.Any(pair => pair.Value == null)) throw new InvalidOperationException("One or more evolution effects could not be loaded.");
            return result;
        }

        static Dictionary<WeaponType, GameObject> CreateEvolutionPrefabs(IReadOnlyDictionary<WeaponType, Sprite> effects)
        {
            var result = new Dictionary<WeaponType, GameObject>();
            foreach (var spec in NewEvolutionSpecs)
            {
                var effect = effects[spec.type];
                var expectedPrefabSprite = spec.type == WeaponType.GoldenBow
                    ? GetStaticPrefabSprite(AssetDatabase.LoadAssetAtPath<GameObject>(spec.sourcePrefab))
                    : spec.type == WeaponType.ArrowShower
                        ? AssetDatabase.LoadAssetAtPath<Sprite>(GeneratedWeaponRoot + "ArrowShowerImpactFrame01.png")
                        : effect;
                bool created = AssetDatabase.LoadAssetAtPath<GameObject>(spec.targetPrefab) == null;
                if (created && !AssetDatabase.CopyAsset(spec.sourcePrefab, spec.targetPrefab))
                {
                    throw new InvalidOperationException("Failed to copy evolution prefab: " + spec.targetPrefab);
                }

                if (created)
                {
                    var root = PrefabUtility.LoadPrefabContents(spec.targetPrefab);
                    try
                    {
                        root.name = Path.GetFileNameWithoutExtension(spec.targetPrefab);
                        if (!HasRequiredComponent(root, spec.requiredComponent))
                        {
                            throw new InvalidOperationException(spec.targetPrefab + " requires " + spec.requiredComponent.Name + ".");
                        }
                        SetStaticPrefabSprite(root, expectedPrefabSprite);
                        if (spec.type == WeaponType.GoldenBow) SetGoldenArrowTint(root);
                        PrefabUtility.SaveAsPrefabAsset(root, spec.targetPrefab);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
                else
                {
                    var existing = AssetDatabase.LoadAssetAtPath<GameObject>(spec.targetPrefab);
                    bool visualsMatch = spec.type == WeaponType.FrostStorm
                        ? HasStaticSpriteTarget(existing)
                        : StaticSpriteTargetsMatch(existing, expectedPrefabSprite);
                    if (!HasRequiredComponent(existing, spec.requiredComponent) || !visualsMatch)
                    {
                        throw new InvalidOperationException("Existing evolution prefab differs from the expected static setup; migration will not overwrite it: " + spec.targetPrefab);
                    }
                }
                result[spec.type] = AssetDatabase.LoadAssetAtPath<GameObject>(spec.targetPrefab);
            }
            return result;
        }

        static bool HasStaticSpriteTarget(GameObject root)
        {
            return root.GetComponentsInChildren<PaperMeshVisual>(true).Length > 0 || root.GetComponentsInChildren<SpriteRenderer>(true).Length > 0;
        }

        static bool HasRequiredComponent(GameObject root, Type componentType)
        {
            return root != null && root.GetComponent(componentType) != null;
        }

        static bool StaticSpriteTargetsMatch(GameObject root, Sprite expected)
        {
            if (root == null || expected == null) return false;
            int count = 0;
            foreach (var visual in root.GetComponentsInChildren<PaperMeshVisual>(true))
            {
                var property = new SerializedObject(visual).FindProperty("sourceSprite");
                if (property == null || property.objectReferenceValue != expected) return false;
                count++;
            }
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite != expected) return false;
                count++;
            }
            return count > 0;
        }

        static void SetStaticPrefabSprite(GameObject root, Sprite sprite)
        {
            int changed = 0;
            foreach (var visual in root.GetComponentsInChildren<PaperMeshVisual>(true))
            {
                var serialized = new SerializedObject(visual);
                var property = serialized.FindProperty("sourceSprite");
                if (property == null) continue;
                property.objectReferenceValue = sprite;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed++;
            }
            foreach (var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                renderer.sprite = sprite;
                EditorUtility.SetDirty(renderer);
                changed++;
            }
            if (changed == 0) throw new InvalidOperationException("No static sprite target was found in " + root.name + ".");
        }

        static Sprite GetStaticPrefabSprite(GameObject root)
        {
            if (root == null) return null;
            var visual = root.GetComponentInChildren<PaperMeshVisual>(true);
            if (visual != null)
            {
                return new SerializedObject(visual).FindProperty("sourceSprite")?.objectReferenceValue as Sprite;
            }
            var renderer = root.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        static void SetGoldenArrowTint(GameObject root)
        {
            foreach (var visual in root.GetComponentsInChildren<PaperMeshVisual>(true))
            {
                var serialized = new SerializedObject(visual);
                var tint = serialized.FindProperty("tint");
                if (tint != null) tint.colorValue = GoldenArrowTint;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var projectile = root.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.fallbackColor = GoldenArrowTint;
                EditorUtility.SetDirty(projectile);
            }
        }

        static void AssignPlayerPrefab(IReadOnlyDictionary<WeaponType, GameObject> prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                SetObjectReference(root.GetComponent<WeaponController>(), "goldenArrowPrefab", prefabs[WeaponType.GoldenBow]);
                SetObjectReference(root.GetComponent<WeaponController>(), "fireMissilePrefab", prefabs[WeaponType.FireMissile]);

                var runtime = root.GetComponent<AdvancedWeaponRuntime>();
                SetObjectReference(runtime, "excaliburSlashPrefab", prefabs[WeaponType.Excalibur]);
                SetObjectReference(runtime, "arrowShowerStrikePrefab", prefabs[WeaponType.ArrowShower]);
                SetObjectReference(runtime, "machineGunBulletPrefab", prefabs[WeaponType.MachineGun]);
                SetObjectReference(runtime, "frostStormSpikePrefab", prefabs[WeaponType.FrostStorm]);
                SetObjectReference(runtime, "thunderStormOrbPrefab", prefabs[WeaponType.ThunderStorm]);
                SetObjectReference(runtime, "goddessBlessingAreaPrefab", prefabs[WeaponType.GoddessBlessing]);
                SetObjectReference(root.GetComponent<ShieldOrbitController>(), "dualShieldPrefab", prefabs[WeaponType.DualShield]);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            if (target == null) throw new InvalidOperationException("Player component is missing for " + propertyName + ".");
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException("Serialized property is missing: " + propertyName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void UpdateGameScene(IReadOnlyDictionary<WeaponType, Sprite> icons)
        {
            WithScene(GameScenePath, scene =>
            {
                foreach (var presentation in ComponentsInScene<EvolutionChoicePresentation>(scene))
                {
                    var entries = new List<EvolutionChoicePresentation.EvolutionIconEntry>();
                    foreach (var type in AllEvolutionTypes)
                    {
                        var existing = presentation.evolutionWeaponIcons != null
                            ? presentation.evolutionWeaponIcons.FirstOrDefault(entry => entry.weaponType == type).icon
                            : null;
                        GameObject iconObject;
                        if (type == WeaponType.SwordRush) iconObject = presentation.evolutionWeaponIcon;
                        else iconObject = EnsureStaticIcon(existing ?? presentation.evolutionWeaponIcon, DetailIconNames[type] + " Evolution Icon", icons[type]);
                        entries.Add(new EvolutionChoicePresentation.EvolutionIconEntry { weaponType = type, icon = iconObject });
                    }
                    presentation.evolutionWeaponIcons = entries.ToArray();
                    EditorUtility.SetDirty(presentation);
                }

                foreach (var slot in ComponentsInScene<WeaponHudCompactIconSlot>(scene))
                {
                    var source = FindIcon(slot.icons, WeaponType.SwordRush);
                    var entries = new List<WeaponHudCompactIconSlot.IconEntry>(
                        slot.icons.Where(entry => entry.icon != null && !WeaponCatalog.IsEvolution(entry.weaponType)));
                    foreach (var type in AllEvolutionTypes)
                    {
                        var existing = FindIcon(slot.icons, type);
                        var iconObject = type == WeaponType.SwordRush ? source : EnsureStaticIcon(existing ?? source, DetailIconNames[type], icons[type]);
                        entries.Add(new WeaponHudCompactIconSlot.IconEntry { weaponType = type, icon = iconObject });
                    }
                    slot.icons = entries.ToArray();
                    EditorUtility.SetDirty(slot);
                }

                foreach (var panelName in DetailPanelNames())
                {
                    var panel = FindByName(scene, panelName);
                    var source = panel.transform.Find("Icon").gameObject;
                    foreach (var type in AllEvolutionTypes)
                    {
                        EnsureStaticIcon(source, DetailIconNames[type], icons[type]);
                    }
                }
            });
        }

        static void UpdateWeaponBookScene(IReadOnlyDictionary<WeaponType, Sprite> icons)
        {
            WithScene(WeaponBookScenePath, scene =>
            {
                var screen = ComponentsInScene<WeaponBookScreen>(scene).First();
                var entries = new List<WeaponBookScreen.EvolutionIconEntry>();
                foreach (var type in AllEvolutionTypes)
                {
                    var existing = screen.evolutionIcons != null
                        ? screen.evolutionIcons.FirstOrDefault(entry => entry.weaponType == type)
                        : default;
                    var discovered = EnsureStaticIcon(existing.discoveredIcon ?? screen.evolutionDiscoveredIcon, DetailIconNames[type] + " Book Icon", icons[type]);
                    var locked = EnsureLockedStaticIcon(existing.undiscoveredIcon ?? screen.evolutionUndiscoveredIcon, DetailIconNames[type] + " Book Icon Locked", icons[type]);
                    entries.Add(new WeaponBookScreen.EvolutionIconEntry { weaponType = type, discoveredIcon = discovered, undiscoveredIcon = locked });
                }
                screen.evolutionIcons = entries.ToArray();
                EditorUtility.SetDirty(screen);
            });
        }

        static void UpdateTestLauncherScene(IReadOnlyDictionary<WeaponType, Sprite> icons)
        {
            WithScene(TestLauncherScenePath, scene =>
            {
                var source = FindByName(scene, GameTestLaunchScreen.WeaponTestButtonName(WeaponType.SwordRush));
                var previous = source;
                Vector2 delta = DeriveSiblingDelta(source.GetComponent<RectTransform>());
                foreach (var spec in NewEvolutionSpecs)
                {
                    string targetName = GameTestLaunchScreen.WeaponTestButtonName(spec.type);
                    var existing = FindByName(scene, targetName);
                    if (existing != null)
                    {
                        if (existing.transform.parent != source.transform.parent)
                        {
                            throw new InvalidOperationException("Existing weapon test button has an unexpected parent and was not moved: " + targetName);
                        }
                        previous = existing;
                        continue;
                    }

                    var clone = UnityEngine.Object.Instantiate(source, source.transform.parent, false);
                    clone.name = targetName;
                    clone.transform.SetSiblingIndex(previous.transform.GetSiblingIndex() + 1);
                    if (clone.transform.parent.GetComponent<LayoutGroup>() == null)
                    {
                        var previousRect = previous.GetComponent<RectTransform>();
                        var cloneRect = clone.GetComponent<RectTransform>();
                        if (previousRect != null && cloneRect != null) cloneRect.anchoredPosition = previousRect.anchoredPosition + delta;
                    }
                    var label = clone.GetComponentInChildren<Text>(true);
                    if (label != null) label.text = WeaponCatalog.DisplayNameSource(spec.type);
                    clone.SetActive(true);
                    previous = clone;
                }

                ApplyTestLauncherIcons(scene, icons);
            });
        }

        static void ApplyTestLauncherIcons(Scene scene, IReadOnlyDictionary<WeaponType, Sprite> icons)
        {
            foreach (var type in AllEvolutionTypes)
            {
                var button = FindByName(scene, GameTestLaunchScreen.WeaponTestButtonName(type));
                var icon = button != null ? button.transform.Find("Icon")?.GetComponent<Image>() : null;
                if (icon == null || !icons.TryGetValue(type, out var sprite) || sprite == null)
                {
                    throw new InvalidOperationException("Game Test Launcher evolution button or icon is missing: " + type);
                }

                icon.sprite = sprite;
                EditorUtility.SetDirty(icon);
            }
        }

        static Vector2 DeriveSiblingDelta(RectTransform source)
        {
            if (source == null || source.parent == null) return Vector2.zero;
            var previous = source.GetSiblingIndex() > 0 ? source.parent.GetChild(source.GetSiblingIndex() - 1) as RectTransform : null;
            return previous != null ? source.anchoredPosition - previous.anchoredPosition : new Vector2(0f, -source.rect.height);
        }

        static GameObject EnsureStaticIcon(GameObject sourceOrExisting, string targetName, Sprite sprite)
        {
            if (sourceOrExisting == null) throw new InvalidOperationException("Static icon source is missing: " + targetName);
            var parent = sourceOrExisting.transform.parent;
            var existing = parent != null ? parent.Find(targetName)?.gameObject : null;
            if (existing != null)
            {
                if (!HasSprite(existing, sprite)) throw new InvalidOperationException("Existing Scene icon differs from the requested sprite and was not overwritten: " + targetName);
                return existing;
            }

            if (sourceOrExisting.name == targetName)
            {
                if (!HasSprite(sourceOrExisting, sprite)) throw new InvalidOperationException("Existing Scene icon differs from the requested sprite and was not overwritten: " + targetName);
                return sourceOrExisting;
            }

            var target = UnityEngine.Object.Instantiate(sourceOrExisting, parent, false);
            target.name = targetName;
            var images = target.GetComponentsInChildren<Image>(true);
            if (images.Length == 0) throw new InvalidOperationException("Static icon source has no Image: " + targetName);
            foreach (var image in images) image.sprite = sprite;
            target.SetActive(false);
            return target;
        }

        static GameObject EnsureLockedStaticIcon(GameObject sourceOrExisting, string targetName, Sprite sprite)
        {
            if (sourceOrExisting == null) throw new InvalidOperationException("Locked icon source is missing: " + targetName);
            var parent = sourceOrExisting.transform.parent;
            var existing = parent != null ? parent.Find(targetName)?.gameObject : null;
            if (existing == null && sourceOrExisting.name == targetName) existing = sourceOrExisting;
            if (existing != null)
            {
                var existingImages = existing.GetComponentsInChildren<Image>(true);
                if (!HasSprite(existing, sprite) || existingImages.Length == 0 || existingImages.Any(image => !ApproximatelyColor(image.color, LockedIconColor)))
                {
                    throw new InvalidOperationException("Existing locked icon differs and was not overwritten: " + targetName);
                }
                return existing;
            }

            var target = UnityEngine.Object.Instantiate(sourceOrExisting, parent, false);
            target.name = targetName;
            var images = target.GetComponentsInChildren<Image>(true);
            if (images.Length == 0) throw new InvalidOperationException("Locked icon has no Image: " + targetName);
            foreach (var image in images)
            {
                image.sprite = sprite;
                image.color = LockedIconColor;
            }
            target.SetActive(false);
            return target;
        }

        static bool HasSprite(GameObject target, Sprite sprite)
        {
            return target != null && target.GetComponentsInChildren<Image>(true).Any(image => image.sprite == sprite);
        }

        static bool ApproximatelyColor(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
        }

        static GameObject FindIcon(WeaponHudCompactIconSlot.IconEntry[] entries, WeaponType type)
        {
            return entries != null ? entries.FirstOrDefault(entry => entry.weaponType == type).icon : null;
        }

        static WeaponHudCompactIconSlot.IconEntry[] BuildCompleteCompactIconEntries(WeaponHudCompactIconSlot slot)
        {
            var entries = new List<WeaponHudCompactIconSlot.IconEntry>();
            foreach (var type in BaseWeaponTypes)
            {
                var expectedSprite = GeneratedSpriteLoader.Load(WeaponCatalog.IconResource(type));
                var icon = FindIcon(slot.icons, type);
                if (!HasSprite(icon, expectedSprite)) icon = FindDirectChildIconBySprite(slot, expectedSprite);
                if (icon == null)
                {
                    throw new InvalidOperationException("05_Game compact HUD is missing the Scene-authored base icon: " + type);
                }
                entries.Add(new WeaponHudCompactIconSlot.IconEntry { weaponType = type, icon = icon });
            }

            foreach (var type in AllEvolutionTypes)
            {
                var icon = FindIcon(slot.icons, type);
                if (icon == null)
                {
                    throw new InvalidOperationException("05_Game compact HUD is missing the Scene-authored evolution icon: " + type);
                }
                entries.Add(new WeaponHudCompactIconSlot.IconEntry { weaponType = type, icon = icon });
            }
            return entries.ToArray();
        }

        static GameObject FindDirectChildIconBySprite(WeaponHudCompactIconSlot slot, Sprite sprite)
        {
            if (slot == null || sprite == null) return null;
            var matches = slot.GetComponentsInChildren<Image>(true)
                .Where(image => image.transform.parent == slot.transform && image.sprite == sprite)
                .Select(image => image.gameObject)
                .Distinct()
                .ToArray();
            if (matches.Length > 1)
            {
                throw new InvalidOperationException("05_Game compact HUD has duplicate direct-child icons for sprite: " + sprite.name);
            }
            return matches.FirstOrDefault();
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        static string[] DetailPanelNames()
        {
            return new[] { "Slash Weapon Status", "Arrow Weapon Status", "Fireball Weapon Status" };
        }

        static void WithScene(string path, Action<Scene> action, bool save = true)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (save && scene.isDirty)
            {
                if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                throw new InvalidOperationException(path + " has unsaved changes. Existing Scene-authored layout was not overwritten.");
            }
            try
            {
                action(scene);
                if (save)
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
