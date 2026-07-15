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
    public static class WeaponEvolutionBatchValidator
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string WeaponBookScenePath = "Assets/AreaSurvivors/Scenes/07_WeaponBook.unity";
        const string TestLauncherScenePath = "Assets/AreaSurvivors/Scenes/08_GameTestLauncher.unity";
        const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Characters/Player.prefab";
        const string PrefabRoot = "Assets/AreaSurvivors/Prefabs/Weapons/";
        const string SpriteRoot = "Assets/AreaSurvivors/Sprites/Generated/Weapons/";
        const string MachineGunVisualMarkerRelativePath = "Library/AreaSafeUnity/machine-gun-bullet-visual-validator.ok";
        const string FireMissileCooldownMarkerRelativePath = "Library/AreaSafeUnity/fire-missile-cooldown-validator.ok";
        const string FireMissileHomingMarkerRelativePath = "Library/AreaSafeUnity/fire-missile-homing-validator.ok";

        static readonly Color LockedIconColor = new Color(0f, 0f, 0f, 0.78f);

        sealed class Spec
        {
            public WeaponType type;
            public WeaponType sourceType;
            public string assetName;
            public string effectAssetName;
            public string detailIconName;
            public string prefabName;
            public Type requiredComponent;
            public int effectSize;
            public float effectPixelsPerUnit;
        }

        static readonly Spec[] NewSpecs =
        {
            Item(WeaponType.Excalibur, WeaponType.AuraSword, "Excalibur", "Excalibur Icon", "ExcaliburSlash.prefab", typeof(AdvancedWeaponProjectile), 192),
            Item(WeaponType.GoldenBow, WeaponType.Arrow, "GoldenBow", "Golden Bow Icon", "GoldenArrow.prefab", typeof(Projectile), 128),
            Item(WeaponType.ArrowShower, WeaponType.ArrowRain, "ArrowShower", "Arrow Shower Icon", "ArrowShowerStrike.prefab", typeof(AdvancedWeaponArea), 192),
            Item(WeaponType.MachineGun, WeaponType.Gun, "MachineGun", "Machine Gun Icon", "MachineGunBullet.prefab", typeof(AdvancedWeaponProjectile), 128, 64f, "MachineGunBullet"),
            Item(WeaponType.FireMissile, WeaponType.Fireball, "FireMissile", "Fire Missile Icon", "FireMissile.prefab", typeof(Projectile), 128),
            Item(WeaponType.FrostStorm, WeaponType.Frost, "FrostStorm", "Frost Storm Icon", "FrostStormSpike.prefab", typeof(AdvancedWeaponArea), 256, 128f, "FrostAreaTexture"),
            Item(WeaponType.ThunderStorm, WeaponType.ThunderBall, "ThunderStorm", "Thunder Storm Icon", "ThunderStormOrb.prefab", typeof(AdvancedWeaponProjectile), 128),
            Item(WeaponType.DualShield, WeaponType.Shield, "DualShield", "Dual Shield Icon", "DualShield.prefab", typeof(ShieldOrbitShield), 128),
            Item(WeaponType.GoddessBlessing, WeaponType.Flag, "GoddessBlessing", "Goddess Blessing Icon", "GoddessBlessingArea.prefab", typeof(AdvancedWeaponArea), 192)
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

        static readonly Dictionary<WeaponType, string> AdditionalRequirements = new Dictionary<WeaponType, string>
        {
            { WeaponType.SwordRush, "ゲームプレイ回数5回以上" },
            { WeaponType.Banana, "ゲームプレイ中の撃破数300" },
            { WeaponType.Excalibur, "レリックを10個以上所持" },
            { WeaponType.GoldenBow, "ゲームプレイ中の獲得トークン数50" },
            { WeaponType.ArrowShower, "塗り自陣エリア50%以上" },
            { WeaponType.MachineGun, "プレイヤーLv.30" },
            { WeaponType.FireMissile, "ボス出現中" },
            { WeaponType.FrostStorm, "進化武器を3つ以上アンロック" },
            { WeaponType.ThunderStorm, "累計討伐数10000" },
            { WeaponType.DualShield, "プレイヤーのHPが満タンではない" },
            { WeaponType.GoddessBlessing, "中心塔のHPが半分以下" }
        };

        [MenuItem("Area Survivors/Validate/Weapon Evolution Batch")]
        public static void ValidateMenu()
        {
            if (!ValidateAll(true)) throw new InvalidOperationException("Weapon evolution batch validation failed.");
        }

        public static bool ValidateAll(bool logSuccess)
        {
            int errors = 0;
            ValidateCatalog(ref errors);
            ValidateConfig(ref errors);

            var icons = new Dictionary<WeaponType, Sprite>
            {
                { WeaponType.SwordRush, AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "SwordRushIcon.png") },
                { WeaponType.Banana, AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "BananaIcon.png") }
            };
            var prefabs = new Dictionary<WeaponType, GameObject>();
            foreach (var spec in NewSpecs)
            {
                icons[spec.type] = ValidateSprite(SpriteRoot + spec.assetName + "Icon.png", 100f, 96, 96, ref errors);
                int effectHeight = spec.type == WeaponType.MachineGun
                    ? 40
                    : spec.type == WeaponType.FrostStorm
                        ? 166
                        : spec.effectSize;
                var effect = ValidateSprite(SpriteRoot + spec.effectAssetName + ".png", spec.effectPixelsPerUnit,
                    spec.effectSize, effectHeight, ref errors);
                string prefabPath = PrefabRoot + spec.prefabName;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var expectedPrefabSprite = spec.type == WeaponType.GoldenBow
                    ? GetPrefabSprite(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "PlayerArrow.prefab"))
                    : spec.type == WeaponType.ArrowShower
                        ? AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "ArrowShowerImpactFrame01.png")
                        : effect;
                prefabs[spec.type] = prefab;
                if (prefab == null || !HasRequiredComponent(prefab, spec.requiredComponent) || !HasPrefabSprite(prefab, expectedPrefabSprite) ||
                    (spec.type == WeaponType.ArrowShower && prefab.GetComponent<ArrowRainAreaVisual>() == null))
                {
                    Error("Evolution prefab component or static sprite is invalid: " + prefabPath, ref errors);
                }
            }
            if (icons.Any(pair => pair.Value == null)) Error("One or more evolution HUD icons are missing.", ref errors);

            ValidatePlayerPrefab(prefabs, ref errors);
            errors += ValidateScene(GameScenePath, scene => ValidateGameScene(scene, icons));
            errors += ValidateScene(WeaponBookScenePath, scene => ValidateWeaponBookScene(scene, icons));
            errors += ValidateScene(TestLauncherScenePath, scene => ValidateTestLauncherScene(scene, icons));

            if (errors == 0 && logSuccess) Debug.Log("Weapon evolution batch validator: passed.");
            return errors == 0;
        }

        [MenuItem("Area Survivors/Validate/Game Test Launcher Evolution Icons")]
        public static void ValidateGameTestLauncherEvolutionIconsMenu()
        {
            int errors = 0;
            var icons = LoadEvolutionIcons(ref errors);
            errors += ValidateScene(TestLauncherScenePath, scene => ValidateTestLauncherScene(scene, icons));
            if (errors != 0) throw new InvalidOperationException("Game Test Launcher evolution icon validation failed.");
            Debug.Log("Game Test Launcher evolution icon validator: passed. icons=" + AllEvolutionTypes.Length + ".");
        }

        [MenuItem("Area Survivors/Validate/Golden Bow Evolution")]
        public static void ValidateGoldenBowEvolutionMenu()
        {
            int errors = 0;
            ValidateSprite(SpriteRoot + "GoldenBowEffect.png", 96f, 128, 128, ref errors);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "GoldenArrow.prefab");
            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "PlayerArrow.prefab");
            var baseSprite = GetPrefabSprite(basePrefab);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var playerControllers = player != null ? player.GetComponents<PlayerController>() : Array.Empty<PlayerController>();
            var weaponControllers = player != null ? player.GetComponents<WeaponController>() : Array.Empty<WeaponController>();
            var weapon = weaponControllers.Length == 1 ? weaponControllers[0] : null;
            var goldenVisual = prefab != null ? prefab.GetComponentInChildren<PaperMeshVisual>(true) : null;
            var baseVisual = basePrefab != null ? basePrefab.GetComponentInChildren<PaperMeshVisual>(true) : null;
            if (prefab == null || prefab.GetComponent<Projectile>() == null || baseSprite == null || !HasPrefabSprite(prefab, baseSprite) ||
                goldenVisual == null || baseVisual == null || goldenVisual.transform.localScale != baseVisual.transform.localScale ||
                playerControllers.Length != 1 || weapon == null || playerControllers[0].weapon != weapon || weapon.goldenArrowPrefab != prefab)
            {
                Error("Golden Bow projectile prefab or player reference is invalid.", ref errors);
            }

            var scheduleMethod = typeof(WeaponController).GetMethod(
                "TryConsumeArrowSchedule",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (WeaponController.ResolveArrowVolleyProjectileCount(4, 4) != 4 ||
                WeaponController.ResolveArrowVolleyProjectileCount(4, 1) != 1 ||
                WeaponController.ResolveTestStatLevel(GameConfig.MaxWeaponLevel, null) != GameConfig.MaxWeaponLevel ||
                WeaponController.ResolveTestStatLevel(GameConfig.MaxWeaponLevel, 1) != 1 ||
                WeaponController.EvolutionTestUpgradeCount != 2 ||
                typeof(WeaponController).GetField("nextArrowVolleyAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) == null ||
                typeof(WeaponController).GetField("testStatLevelOverrides", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) == null ||
                scheduleMethod == null)
            {
                Error("Bow projectile volley count, evolution test profile, or cooldown gate is invalid.", ref errors);
            }
            else
            {
                ValidateArrowScheduleGate(scheduleMethod, ref errors);
            }

            var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/AreaSurvivors/Resources/Config/GameConfig.asset");
            ValidateEvolutionTestProfiles(gameConfig, ref errors);

            errors += ValidateScene(GameScenePath, ValidateGoldenBowRuntimeMultiplicity);

            if (errors != 0) throw new InvalidOperationException("Golden Bow evolution validation failed.");
            Debug.Log("Golden Bow evolution validator: passed.");
        }

        [MenuItem("Area Survivors/Validate/Machine Gun Bullet Visual")]
        public static void ValidateMachineGunBulletVisualMenu()
        {
            string markerPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                MachineGunVisualMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            int errors = 0;
            var machineSprite = ValidateSprite(SpriteRoot + "MachineGunBullet.png", 64f, 128, 40, ref errors);
            var gunSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "GunBullet.png");
            var machinePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "MachineGunBullet.prefab");
            var gunPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "GunBulletProjectile.prefab");
            var machineVisuals = machinePrefab != null
                ? machinePrefab.GetComponentsInChildren<PaperMeshVisual>(true)
                : Array.Empty<PaperMeshVisual>();
            var gunVisual = gunPrefab != null ? gunPrefab.GetComponentInChildren<PaperMeshVisual>(true) : null;
            var machineCollider = machinePrefab != null ? machinePrefab.GetComponent<CircleCollider2D>() : null;
            var gunCollider = gunPrefab != null ? gunPrefab.GetComponent<CircleCollider2D>() : null;

            if (machineSprite == null || gunSprite == null || machineSprite == gunSprite ||
                machineSprite.rect.size != gunSprite.rect.size || machineSprite.bounds.size != gunSprite.bounds.size ||
                machinePrefab == null || gunPrefab == null || machinePrefab.GetComponent<AdvancedWeaponProjectile>() == null ||
                machineVisuals.Length != 1 || gunVisual == null || !HasPrefabSprite(machinePrefab, machineSprite) ||
                machinePrefab.transform.localScale != gunPrefab.transform.localScale ||
                machineVisuals.Length == 1 && machineVisuals[0].transform.localScale != gunVisual.transform.localScale ||
                machineCollider == null || gunCollider == null ||
                !Mathf.Approximately(machineCollider.radius, gunCollider.radius) || machineCollider.offset != gunCollider.offset ||
                machineCollider.isTrigger != gunCollider.isTrigger ||
                !HasZeroPitch(machinePrefab.transform) || machineVisuals.Length == 1 && !HasZeroPitch(machineVisuals[0].transform))
            {
                Error("Machine Gun bullet must use its black 128x40 sprite while preserving Gun bullet scale, collider, and zero pitch.", ref errors);
            }

            if (errors != 0) throw new InvalidOperationException("Machine Gun bullet visual validation failed. errors=" + errors);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Machine Gun bullet visual validator: passed. Black sprite and Gun bullet dimensions match.");
        }

        [MenuItem("Area Survivors/Validate/Fire Missile Cooldown")]
        public static void ValidateFireMissileCooldownMenu()
        {
            string markerPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                FireMissileCooldownMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            int errors = 0;
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/AreaSurvivors/Resources/Config/GameConfig.asset");
            if (config == null || !Mathf.Approximately(config.fireMissileBaseCooldownMultiplier, 0.5f) ||
                !Mathf.Approximately(WeaponController.ResolveEvolutionBaseCooldown(4f, 4f, 0.5f), 2f) ||
                !Mathf.Approximately(WeaponController.ResolveEvolutionBaseCooldown(3.6f, 4f, 0.5f), 1.6f))
            {
                Error("Fire Missile cooldown must use half the Fireball Lv.1 base while preserving prior cooldown deltas.",
                    ref errors);
            }

            if (errors != 0) throw new InvalidOperationException("Fire Missile cooldown validation failed. errors=" + errors);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Fire Missile cooldown validator: passed. base=0.5x and prior upgrades are preserved.");
        }

        [MenuItem("Area Survivors/Validate/Fire Missile Homing")]
        public static void ValidateFireMissileHomingMenu()
        {
            string markerPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                FireMissileHomingMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            int errors = 0;
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/AreaSurvivors/Resources/Config/GameConfig.asset");
            var launchRight = WeaponController.ResolveFireMissileLaunchDirection(0f);
            var launchUp = WeaponController.ResolveFireMissileLaunchDirection(90f);
            var quarterTurn = Projectile.ResolveHomingDirection(Vector2.right, Vector2.up, 180f, 0.25f);
            float expectedComponent = Mathf.Sqrt(0.5f);

            if (config == null || !Mathf.Approximately(config.fireMissileHomingTurnSpeedDegrees, 180f) ||
                Vector2.Distance(launchRight, Vector2.right) > 0.0001f ||
                Vector2.Distance(launchUp, Vector2.up) > 0.0001f ||
                Mathf.Abs(quarterTurn.x - expectedComponent) > 0.0001f ||
                Mathf.Abs(quarterTurn.y - expectedComponent) > 0.0001f ||
                Vector2.Distance(quarterTurn, Vector2.up) < 0.1f)
            {
                Error("Fire Missile must launch by angle and turn gradually toward its target at the configured rate.",
                    ref errors);
            }

            if (errors != 0) throw new InvalidOperationException("Fire Missile homing validation failed. errors=" + errors);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Fire Missile homing validator: passed. random launch direction and gradual turn are configured.");
        }

        static void ValidateArrowScheduleGate(System.Reflection.MethodInfo scheduleMethod, ref int errors)
        {
            var root = new GameObject("Golden Bow Schedule Validator") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var controller = root.AddComponent<WeaponController>();
                bool first = (bool)scheduleMethod.Invoke(controller, new object[] { 10f, 0.5f });
                bool sameTimeSecond = (bool)scheduleMethod.Invoke(controller, new object[] { 10f, 0.5f });
                bool nextCooldown = (bool)scheduleMethod.Invoke(controller, new object[] { 10.5f, 0.5f });
                if (!first || sameTimeSecond || !nextCooldown)
                {
                    Error("Arrow cooldown gate does not reject a second schedule at the same time.", ref errors);
                }
            }
            catch (Exception exception)
            {
                Error("Arrow cooldown gate execution failed: " + exception.GetBaseException().Message, ref errors);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void ValidateEvolutionTestProfiles(GameConfig gameConfig, ref int errors)
        {
            const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var configField = typeof(WeaponController).GetField("config", Flags);
            var overrideField = typeof(WeaponController).GetField("testStatLevelOverrides", Flags);
            var applyMethod = typeof(WeaponController).GetMethod("ApplyTestStartingWeaponProfile", Flags);
            if (gameConfig == null || configField == null || overrideField == null || applyMethod == null)
            {
                Error("Evolution test profile validation dependencies are missing.", ref errors);
                return;
            }

            foreach (var evolutionType in AllEvolutionTypes)
            {
                var root = new GameObject(evolutionType + " Test Profile Validator") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    var controller = root.AddComponent<WeaponController>();
                    configField.SetValue(controller, gameConfig);
                    applyMethod.Invoke(controller, new object[] { evolutionType });

                    WeaponType sourceType = WeaponCatalog.BaseWeaponOf(evolutionType);
                    var overrides = overrideField.GetValue(controller) as IDictionary<WeaponType, int>;
                    if (overrides == null || !overrides.TryGetValue(sourceType, out int statLevel) || statLevel != 1 ||
                        !ValidateEvolutionTestUpgradeBonuses(controller, sourceType, gameConfig))
                    {
                        Error(evolutionType + " test profile is not Lv.1 base stats plus two upgrades per parameter.", ref errors);
                    }
                }
                catch (Exception exception)
                {
                    Error(evolutionType + " test profile execution failed: " + exception.GetBaseException().Message, ref errors);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        static bool ValidateEvolutionTestUpgradeBonuses(WeaponController controller, WeaponType type, GameConfig gameConfig)
        {
            int count = WeaponController.EvolutionTestUpgradeCount;
            int attackBonus = Mathf.Max(1, gameConfig.runAttackPowerBonus) * count;
            float cooldownMultiplier = Mathf.Pow(Mathf.Clamp(gameConfig.runAttackCooldownMultiplier, 0.05f, 1f), count);

            switch (type)
            {
                case WeaponType.Slash:
                    return IntFieldEquals(controller, "slashAttackBonus", attackBonus) &&
                        FloatFieldEquals(controller, "slashCooldownMultiplier", cooldownMultiplier) &&
                        FloatFieldEquals(controller, "slashKnockbackBonus", WeaponController.SlashKnockbackUpgradeAmount * count) &&
                        FloatFieldEquals(controller, "slashRangeBonus", WeaponController.SlashRangeUpgradeAmount * count);
                case WeaponType.Arrow:
                    return IntFieldEquals(controller, "arrowAttackBonus", attackBonus) &&
                        FloatFieldEquals(controller, "arrowCooldownMultiplier", cooldownMultiplier) &&
                        IntFieldEquals(controller, "arrowProjectileCountBonus", count) &&
                        FloatFieldEquals(controller, "arrowRangeBonus", WeaponController.ProjectileRangeUpgradeAmount * count);
                case WeaponType.Fireball:
                    return IntFieldEquals(controller, "fireballAttackBonus", attackBonus) &&
                        FloatFieldEquals(controller, "fireballCooldownMultiplier", cooldownMultiplier) &&
                        FloatFieldEquals(controller, "fireballExplosionRadiusBonus", WeaponController.FireballExplosionUpgradeAmount * count) &&
                        FloatFieldEquals(controller, "fireballRangeBonus", WeaponController.ProjectileRangeUpgradeAmount * count);
                case WeaponType.Shield:
                    return IntFieldEquals(controller, "shieldAttackBonus", attackBonus) &&
                        IntFieldEquals(controller, "shieldCountBonus", count) &&
                        FloatFieldEquals(controller, "shieldKnockbackBonus", WeaponController.ShieldKnockbackUpgradeAmount * count) &&
                        FloatFieldEquals(controller, "shieldRotationSpeedBonus", WeaponController.ShieldRotationSpeedUpgradeAmount * count);
            }

            const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var upgradesField = typeof(WeaponController).GetField("advancedWeaponUpgrades", Flags);
            var upgrades = upgradesField != null ? upgradesField.GetValue(controller) as System.Collections.IDictionary : null;
            object state = upgrades != null && upgrades.Contains(type) ? upgrades[type] : null;
            if (state == null || !IntFieldEquals(state, "attackBonus", attackBonus)) return false;

            switch (type)
            {
                case WeaponType.Flag:
                    return FloatFieldEquals(state, "rangeBonus", WeaponController.ProjectileRangeUpgradeAmount * count) &&
                        FloatFieldEquals(state, "slowBonus", 0.05f * count) &&
                        FloatFieldEquals(state, "damageIntervalMultiplier", cooldownMultiplier);
                case WeaponType.BoomerangSword:
                    return IntFieldEquals(state, "projectileCountBonus", count) &&
                        FloatFieldEquals(state, "rangeBonus", WeaponController.SlashRangeUpgradeAmount * count) &&
                        FloatFieldEquals(state, "cooldownMultiplier", cooldownMultiplier);
                case WeaponType.AuraSword:
                    return IntFieldEquals(state, "projectileCountBonus", count) &&
                        FloatFieldEquals(state, "rangeBonus", WeaponController.ProjectileRangeUpgradeAmount * count) &&
                        FloatFieldEquals(state, "distanceBonus", WeaponController.ProjectileRangeUpgradeAmount * count);
                case WeaponType.ArrowRain:
                    return FloatFieldEquals(state, "rangeBonus", WeaponController.ProjectileRangeUpgradeAmount * count) &&
                        FloatFieldEquals(state, "durationBonus", 0.4f * count) &&
                        FloatFieldEquals(state, "cooldownMultiplier", cooldownMultiplier);
                case WeaponType.Gun:
                    return FloatFieldEquals(state, "cooldownMultiplier", cooldownMultiplier) &&
                        FloatFieldEquals(state, "distanceBonus", WeaponController.ProjectileRangeUpgradeAmount * count) &&
                        IntFieldEquals(state, "projectileCountBonus", count);
                case WeaponType.Frost:
                    return FloatFieldEquals(state, "rangeBonus", WeaponController.ProjectileRangeUpgradeAmount * count) &&
                        FloatFieldEquals(state, "slowBonus", 0.05f * count) &&
                        FloatFieldEquals(state, "cooldownMultiplier", cooldownMultiplier);
                case WeaponType.ThunderBall:
                    return FloatFieldEquals(state, "rangeBonus", WeaponController.ProjectileRangeUpgradeAmount * count) &&
                        IntFieldEquals(state, "projectileCountBonus", count) &&
                        FloatFieldEquals(state, "durationBonus", 0.5f * count);
                default:
                    return false;
            }
        }

        static bool IntFieldEquals(object target, string fieldName, int expected)
        {
            var field = target?.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            return field != null && field.GetValue(target) is int actual && actual == expected;
        }

        static bool FloatFieldEquals(object target, string fieldName, float expected)
        {
            var field = target?.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            return field != null && field.GetValue(target) is float actual && Mathf.Approximately(actual, expected);
        }

        static int ValidateGoldenBowRuntimeMultiplicity(Scene scene)
        {
            int errors = 0;
            int gameManagers = ComponentsInScene<GameManager>(scene).Length;
            int staticPlayers = ComponentsInScene<PlayerController>(scene).Length;
            int staticWeapons = ComponentsInScene<WeaponController>(scene).Length;
            if (gameManagers != 1 || staticPlayers != 0 || staticWeapons != 0)
            {
                Error(
                    $"05_Game runtime multiplicity is invalid. GameManager={gameManagers}, static PlayerController={staticPlayers}, static WeaponController={staticWeapons}.",
                    ref errors);
            }
            return errors;
        }

        static Dictionary<WeaponType, Sprite> LoadEvolutionIcons(ref int errors)
        {
            var icons = new Dictionary<WeaponType, Sprite>
            {
                { WeaponType.SwordRush, AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "SwordRushIcon.png") },
                { WeaponType.Banana, AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "BananaIcon.png") }
            };
            foreach (var spec in NewSpecs)
            {
                icons[spec.type] = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + spec.assetName + "Icon.png");
            }
            if (icons.Any(pair => pair.Value == null)) Error("One or more evolution HUD icons are missing.", ref errors);
            return icons;
        }

        static Spec Item(WeaponType type, WeaponType sourceType, string assetName, string detailIconName,
            string prefabName, Type component, int effectSize, float effectPixelsPerUnit = 96f,
            string effectAssetName = null)
        {
            return new Spec
            {
                type = type,
                sourceType = sourceType,
                assetName = assetName,
                effectAssetName = string.IsNullOrEmpty(effectAssetName) ? assetName + "Effect" : effectAssetName,
                detailIconName = detailIconName,
                prefabName = prefabName,
                requiredComponent = component,
                effectSize = effectSize,
                effectPixelsPerUnit = effectPixelsPerUnit
            };
        }

        static bool HasZeroPitch(Transform target)
        {
            if (target == null) return false;
            Vector3 euler = target.localEulerAngles;
            return Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)) <= 0.01f &&
                Mathf.Abs(Mathf.DeltaAngle(0f, euler.y)) <= 0.01f;
        }

        static void ValidateCatalog(ref int errors)
        {
            foreach (var type in AllEvolutionTypes)
            {
                var source = type == WeaponType.SwordRush ? WeaponType.Slash :
                    type == WeaponType.Banana ? WeaponType.BoomerangSword : NewSpecs.First(spec => spec.type == type).sourceType;
                var requirements = WeaponCatalog.EvolutionRequirementSources(type);
                if (!WeaponCatalog.IsEvolution(type) || WeaponCatalog.BaseWeaponOf(type) != source || WeaponCatalog.EvolutionOf(source) != type ||
                    requirements == null || requirements.Length != 2 || requirements[0] != "武器Lv.10" || requirements[1] != AdditionalRequirements[type] ||
                    string.IsNullOrWhiteSpace(WeaponCatalog.DisplayNameSource(type)) || string.IsNullOrWhiteSpace(WeaponCatalog.EvolutionDescriptionSource(type)) ||
                    string.IsNullOrWhiteSpace(WeaponCatalog.EvolutionChoiceDescriptionSource(type)))
                {
                    Error("Evolution catalog mapping, text, or requirements are invalid: " + type, ref errors);
                }
            }
        }

        static void ValidateConfig(ref int errors)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/AreaSurvivors/Resources/Config/GameConfig.asset");
            if (config == null || config.swordRushBaseAttackPower != 16 || !Mathf.Approximately(config.swordRushBaseRange, 3.2f) ||
                config.swordRushStrikeCount != 5 || !Mathf.Approximately(config.bananaBaseRange, 1.4f) || config.bananaBaseProjectileCountBonus != 3 ||
                !Mathf.Approximately(config.excaliburTravelSpeedCellsPerSecond, 5f) || !Mathf.Approximately(config.excaliburDamageIntervalSeconds, 0.2f) ||
                !Mathf.Approximately(config.arrowShowerStrikeIntervalSeconds, 0.25f) || !Mathf.Approximately(config.evolvedGroundStrikeRadius, 0.7f) ||
                !Mathf.Approximately(config.machineGunShotIntervalSeconds, 0.2f) || config.machineGunBaseAttackCountBonus != 10 ||
                !Mathf.Approximately(config.fireMissileBaseCooldownMultiplier, 0.5f) ||
                config.frostStormTargetCount != 5 || config.thunderStormOrbitCount != 3 || config.goddessBlessingHealAmount != 5)
            {
                Error("GameConfig weapon evolution base values are invalid.", ref errors);
            }
        }

        static Sprite ValidateSprite(string path, float pixelsPerUnit, int expectedWidth, int expectedHeight, ref int errors)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (sprite == null || texture == null || texture.width != expectedWidth || texture.height != expectedHeight || importer == null ||
                importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                !importer.alphaIsTransparency || !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit))
            {
                Error("Evolution sprite import settings are invalid: " + path, ref errors);
            }
            return sprite;
        }

        static bool HasPrefabSprite(GameObject prefab, Sprite expected)
        {
            if (prefab == null || expected == null) return false;
            foreach (var visual in prefab.GetComponentsInChildren<PaperMeshVisual>(true))
            {
                var property = new SerializedObject(visual).FindProperty("sourceSprite");
                if (property != null && property.objectReferenceValue == expected) return true;
            }
            return prefab.GetComponentsInChildren<SpriteRenderer>(true).Any(renderer => renderer.sprite == expected);
        }

        static Sprite GetPrefabSprite(GameObject prefab)
        {
            if (prefab == null) return null;
            var visual = prefab.GetComponentInChildren<PaperMeshVisual>(true);
            if (visual != null)
            {
                return new SerializedObject(visual).FindProperty("sourceSprite")?.objectReferenceValue as Sprite;
            }
            var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            return renderer != null ? renderer.sprite : null;
        }

        static bool HasRequiredComponent(GameObject root, Type componentType)
        {
            return root != null && root.GetComponent(componentType) != null;
        }

        static void ValidatePlayerPrefab(IReadOnlyDictionary<WeaponType, GameObject> prefabs, ref int errors)
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var weapon = player != null ? player.GetComponent<WeaponController>() : null;
            var runtime = player != null ? player.GetComponent<AdvancedWeaponRuntime>() : null;
            var shield = player != null ? player.GetComponent<ShieldOrbitController>() : null;
            if (weapon == null || runtime == null || shield == null ||
                weapon.goldenArrowPrefab != prefabs[WeaponType.GoldenBow] || weapon.fireMissilePrefab != prefabs[WeaponType.FireMissile] ||
                runtime.excaliburSlashPrefab != prefabs[WeaponType.Excalibur] || runtime.arrowShowerStrikePrefab != prefabs[WeaponType.ArrowShower] ||
                runtime.machineGunBulletPrefab != prefabs[WeaponType.MachineGun] || runtime.frostStormSpikePrefab != prefabs[WeaponType.FrostStorm] ||
                runtime.thunderStormOrbPrefab != prefabs[WeaponType.ThunderStorm] || runtime.goddessBlessingAreaPrefab != prefabs[WeaponType.GoddessBlessing] ||
                shield.dualShieldPrefab != prefabs[WeaponType.DualShield])
            {
                Error("Player prefab evolution weapon references are invalid.", ref errors);
            }
        }

        static int ValidateGameScene(Scene scene, IReadOnlyDictionary<WeaponType, Sprite> icons)
        {
            int errors = 0;
            var presentations = ComponentsInScene<EvolutionChoicePresentation>(scene);
            if (presentations.Length != 3 || presentations.Any(presentation => AllEvolutionTypes.Any(type => !HasEvolutionIcon(presentation.evolutionWeaponIcons, type, icons[type]))))
            {
                Error("05_Game level-up presentations are missing static evolution icons.", ref errors);
            }

            var slots = ComponentsInScene<WeaponHudCompactIconSlot>(scene);
            if (slots.Length < 3 || slots.Any(slot =>
                    BaseWeaponTypes.Any(type => !HasCompactIcon(slot.icons, type, GeneratedSpriteLoader.Load(WeaponCatalog.IconResource(type)))) ||
                    AllEvolutionTypes.Any(type => !HasCompactIcon(slot.icons, type, icons[type]))))
            {
                Error("05_Game compact HUD slots are missing base or evolution Scene-authored icon bindings.", ref errors);
            }

            foreach (var panelName in DetailPanelNames())
            {
                var panel = FindByName(scene, panelName);
                foreach (var type in AllEvolutionTypes)
                {
                    if (panel == null || !HasSprite(panel.transform.Find(DetailIconNames[type])?.gameObject, icons[type]))
                    {
                        Error("05_Game detailed HUD is missing " + DetailIconNames[type] + " under " + panelName + ".", ref errors);
                    }
                }
            }
            return errors;
        }

        static int ValidateWeaponBookScene(Scene scene, IReadOnlyDictionary<WeaponType, Sprite> icons)
        {
            int errors = 0;
            var screen = ComponentsInScene<WeaponBookScreen>(scene).FirstOrDefault();
            if (screen != null && screen.evolutionIcons != null && screen.evolutionIcons.Length > 0 &&
                ((screen.evolutionDiscoveredIcon != null && screen.evolutionDiscoveredIcon.activeSelf) ||
                 (screen.evolutionUndiscoveredIcon != null && screen.evolutionUndiscoveredIcon.activeSelf)))
            {
                Error("07_WeaponBook legacy evolution fallback icons must be inactive when typed icons are configured.", ref errors);
            }
            foreach (var type in AllEvolutionTypes)
            {
                var entry = screen != null && screen.evolutionIcons != null
                    ? screen.evolutionIcons.FirstOrDefault(item => item.weaponType == type)
                    : default;
                var discovered = entry.discoveredIcon != null ? entry.discoveredIcon.GetComponentInChildren<Image>(true) : null;
                var locked = entry.undiscoveredIcon != null ? entry.undiscoveredIcon.GetComponentInChildren<Image>(true) : null;
                if (discovered == null || locked == null || discovered.sprite != icons[type] || locked.sprite != icons[type] || !ApproximatelyColor(locked.color, LockedIconColor))
                {
                    Error("07_WeaponBook discovered/locked icon is invalid: " + type, ref errors);
                }
            }
            return errors;
        }

        static int ValidateTestLauncherScene(Scene scene, IReadOnlyDictionary<WeaponType, Sprite> icons)
        {
            int errors = 0;
            foreach (var type in AllEvolutionTypes)
            {
                var button = FindByName(scene, GameTestLaunchScreen.WeaponTestButtonName(type));
                var icon = button != null ? button.transform.Find("Icon")?.GetComponent<Image>() : null;
                if (button == null || button.GetComponent<Button>() == null || icon == null || !icons.TryGetValue(type, out var expected) || icon.sprite != expected)
                {
                    Error("08_GameTestLauncher weapon test button icon is invalid: " + type, ref errors);
                }
            }
            return errors;
        }

        static bool HasEvolutionIcon(EvolutionChoicePresentation.EvolutionIconEntry[] entries, WeaponType type, Sprite sprite)
        {
            return entries != null && entries.Any(entry => entry.weaponType == type && HasSprite(entry.icon, sprite));
        }

        static bool HasCompactIcon(WeaponHudCompactIconSlot.IconEntry[] entries, WeaponType type, Sprite sprite)
        {
            return entries != null && entries.Any(entry => entry.weaponType == type && HasSprite(entry.icon, sprite));
        }

        static bool HasSprite(GameObject target, Sprite sprite)
        {
            return target != null && sprite != null && target.GetComponentsInChildren<Image>(true).Any(image => image.sprite == sprite);
        }

        static bool ApproximatelyColor(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
        }

        static int ValidateScene(string path, Func<Scene, int> validate)
        {
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { return validate(scene); }
            finally { if (opened && scene.IsValid()) EditorSceneManager.CloseScene(scene, true); }
        }

        static T[] ComponentsInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        static GameObject FindByName(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name) return transform.gameObject;
            return null;
        }

        static string[] DetailPanelNames()
        {
            return new[] { "Slash Weapon Status", "Arrow Weapon Status", "Fireball Weapon Status" };
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError(message);
        }
    }
}
