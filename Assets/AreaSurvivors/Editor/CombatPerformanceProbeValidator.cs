using System;
using System.Collections.Generic;
using System.IO;
using AreaSurvivors.Testing;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class CombatPerformanceProbeValidator
    {
        const string MenuPath = "Area Survivors/Validate/Combat Performance Probe";
        const string MarkerRelativePath = "Library/AreaSafeUnity/combat-performance-probe-validator.ok";

        [MenuItem(MenuPath)]
        public static void Validate()
        {
            var errors = new List<string>();

            RequireMode(RuntimePerformanceProbeMode.DisableDamagePopups, 10, errors);
            RequireMode(RuntimePerformanceProbeMode.DisableHitFlash, 11, errors);
            RequireMode(RuntimePerformanceProbeMode.DisableDamageFeedback, 12, errors);
            RequireMode(RuntimePerformanceProbeMode.DisableEnemyController, 4, errors);
            RequireMode(RuntimePerformanceProbeMode.DisableEnemyContactCheck, 5, errors);
            RequireMode(RuntimePerformanceProbeMode.DisableEnemyMoveMultiplier, 6, errors);
            RequireMode(RuntimePerformanceProbeMode.DisableEnemyPaint, 7, errors);
            RequireMode(RuntimePerformanceProbeMode.DisableEnemyAnimation, 8, errors);
            RequireMode(RuntimePerformanceProbeMode.DisableEnemyYSort, 9, errors);
            RequireMode(RuntimePerformanceProbeMode.DisableEnemyEnemyCollision, 13, errors);
            RequireMode(RuntimePerformanceProbeMode.EnablePhysicsMultithreading, 14, errors);
            RequireApproximately(
                AdvancedWeaponArea.CalculateEnemyScanInterval(1f, 0f),
                1f,
                "Damage-only Area scan interval",
                errors);
            RequireApproximately(
                AdvancedWeaponArea.CalculateEnemyScanInterval(1f, 0.4f),
                0.2f,
                "Slow Area refresh interval",
                errors);
            RequireApproximately(
                AdvancedWeaponArea.CalculateEnemyScanInterval(0.08f, 0.4f),
                0.08f,
                "Fast slow Area refresh interval",
                errors);
            RequireApproximately(
                AdvancedWeaponProjectile.CalculateExcaliburScanInterval(0.25f),
                0.25f,
                "Excalibur scan interval",
                errors);
            RequireApproximately(
                AdvancedWeaponProjectile.BananaDamageScanIntervalSeconds,
                0.25f,
                "Banana damage scan interval",
                errors);
            if (EnemySpawner.PerformanceSafeAbsoluteMaxAliveEnemies != 200 ||
                EnemySpawner.CalculateMaxAliveEnemies(160, 1) != 160 ||
                EnemySpawner.CalculateMaxAliveEnemies(160, 2) != 200 ||
                EnemySpawner.CalculateMaxAliveEnemies(160, 5) != 200 ||
                EnemySpawner.CalculateRemainingEnemyCapacity(160, 3, 195) != 5 ||
                EnemySpawner.CalculateRemainingEnemyCapacity(160, 3, 200) != 0 ||
                EnemySpawner.CalculateRemainingEnemyCapacity(160, 3, 600) != 0)
            {
                errors.Add(
                    "Difficulty-adjusted enemy capacity must clamp normal and summoned spawns at the 200-enemy performance limit.");
            }
            if (!Mathf.Approximately(
                    EnemyController.CalculateNextContactDamageAt(10f),
                    10.75f))
            {
                errors.Add(
                    "Enemy contact damage cooldown must use a 0.75-second absolute timestamp.");
            }
            if (!AdvancedWeaponProjectile.ContainsExcaliburPoint(
                    new Vector2(2f, 0f),
                    Vector2.zero,
                    Vector2.right,
                    1f,
                    3f,
                    45f))
            {
                errors.Add("Excalibur sector must contain a point inside its annular forward arc.");
            }
            if (AdvancedWeaponProjectile.ContainsExcaliburPoint(
                    new Vector2(0f, 2f),
                    Vector2.zero,
                    Vector2.right,
                    1f,
                    3f,
                    45f))
            {
                errors.Add("Excalibur sector must reject a point outside its forward arc.");
            }
            if (AdvancedWeaponProjectile.ContainsExcaliburPoint(
                    new Vector2(0.5f, 0f),
                    Vector2.zero,
                    Vector2.right,
                    1f,
                    3f,
                    45f))
            {
                errors.Add("Excalibur sector must reject a point inside its inner radius.");
            }

            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/AdvancedWeaponArea.cs",
                errors,
                "RecordAreaOverlapQuery",
                "RecordAreaDamageAttempt",
                "RecordAreaDamageHit",
                "OverlapCircleNonAlloc",
                "CalculateEnemyScanInterval");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/AdvancedWeaponProjectile.cs",
                errors,
                "RecordProjectileTriggerCallback",
                "RecordProjectileTargetScan",
                "RecordProjectileOverlapQuery",
                "RecordBananaOverlapQuery",
                "RecordAttackPaint",
                "OverlapCircleNonAlloc",
                "OverlapCapsuleNonAlloc",
                "CalculateExcaliburScanInterval",
                "IsHitCoolingDown",
                "colliderEnemyCache",
                "ThunderTargetScanIntervalSeconds",
                "BananaDamageScanIntervalSeconds",
                "projectileCollider.enabled = false",
                "type == WeaponType.Excalibur || type == WeaponType.Banana",
                "ContainsExcaliburPoint",
                "sweptInnerRadius",
                "UpdateExcaliburShape();",
                "SetRuntimeCombatColliderEnabled(false)");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/ExcaliburSectorVisual.cs",
                errors,
                "SetRuntimeCombatColliderEnabled",
                "runtimeCombatColliderEnabled");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Characters/CharacterOcclusionReveal.cs",
                errors,
                "AttachedNormalEnemyCheckInterval",
                "SyncSilhouetteMaterialIfNeeded",
                "SourceTransformChanged",
                "EffectiveRefreshInterval");
            RequireTokens(
                "Assets/AreaSurvivors/Shaders/OcclusionStencilMask.shader",
                errors,
                "ColorMask 0",
                "Blend Zero One",
                "Pass Replace");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Visuals/RuntimeSpriteOutline.cs",
                errors,
                "outlineMaterial",
                "ConfigureCrowdPerformance",
                "RequestSync",
                "safetySyncFrameInterval",
                "AREA_OUTLINE_CROWD_OPTIMIZED",
                "outlineRenderer.sortingOrder != desiredSortingOrder",
                "if (!changed) return;",
                "syncInitialized");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Visuals/PaperMeshVisual.cs",
                errors,
                "RequestOutlineSync",
                "outline?.RequestSync()");
            RequireTokens(
                "Assets/AreaSurvivors/Shaders/SpriteSilhouette.shader",
                errors,
                "#pragma multi_compile_local __ AREA_OUTLINE_CROWD_OPTIMIZED",
                "#if !defined(AREA_OUTLINE_CROWD_OPTIMIZED)");
            RejectTokens(
                "Assets/AreaSurvivors/Scripts/Game/Visuals/RuntimeSpriteOutline.cs",
                errors,
                "MaterialPropertyBlock",
                "sharedOutlineMaterial");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Visuals/YSort.cs",
                errors,
                "IsRuntimeOutlineRenderer",
                "if (visual.order != order || renderer.sortingOrder != order)",
                "renderer.gameObject.name != \"Runtime Outline\"");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/AdvancedWeaponRuntime.cs",
                errors,
                "RecordGroundTargetScan",
                "RecordGroundStrikeSpawn");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/ExcaliburSectorVisual.cs",
                errors,
                "RecordExcaliburShapeRebuild");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/ExcaliburSectorVisual.cs",
                errors,
                "ConfigureIfChanged",
                "if (!changed) return false;");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/FrostStormSpikeImpact.cs",
                errors,
                "RecordAreaOverlapQuery",
                "RecordAreaDamageHit");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/ProjectileExplosionHitbox.cs",
                errors,
                "RecordAreaOverlapQuery",
                "RecordAreaDamageHit");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/SlashView.cs",
                errors,
                "RecordAreaOverlapQuery",
                "RecordAreaDamageHit");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Characters/EnemyController.cs",
                errors,
                "ActiveEnemyInstances",
                "public static IReadOnlyList<EnemyController> ActiveEnemies",
                "ResetActiveEnemyRegistry",
                "public bool IsAlive",
                "EnemyLayerName",
                "IsEnemyLayer(otherCollider.gameObject.layer)",
                "Time.time < nextContactDamageAt",
                "CalculateNextContactDamageAt(Time.time)",
                "RecordDamageFeedbackEvent",
                "RecordEnemyDeath",
                "RecordXpOrbSpawn");
            RejectTokens(
                "Assets/AreaSurvivors/Scripts/Game/Characters/EnemyController.cs",
                errors,
                "contactTimer",
                "contactTimer -= Time.deltaTime");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Characters/EnemySpawner.cs",
                errors,
                "RemainingAliveEnemyCapacity",
                "CalculateRemainingEnemyCapacity",
                "RecordSummonedEnemySpawnAttempt",
                "RecordSummonedEnemySpawned",
                "RecordSummonedEnemyCapBlocked");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/LichSummonAttackController.cs",
                errors,
                "enemySpawner.RemainingAliveEnemyCapacity",
                "RecordSummonedEnemyCapBlocked",
                "requestedCount - allowedSkeletons - allowedSkeletonKnights");
            RejectTokens(
                "Assets/AreaSurvivors/Scripts/Game/Characters/EnemyController.cs",
                errors,
                "AddComponent<EnemyHitFlash>");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/AdvancedWeaponProjectile.cs",
                errors,
                "EnemyController.ActiveEnemies",
                "enemies.Count");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/AdvancedWeaponRuntime.cs",
                errors,
                "EnemyController.ActiveEnemies",
                "enemies.Count");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/Projectile.cs",
                errors,
                "EnemyController.ActiveEnemies",
                "enemy.IsAlive");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Weapons/WeaponController.cs",
                errors,
                "EnemyController.ActiveEnemies",
                "enemy.IsAlive");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Buildings/BallistaTower.cs",
                errors,
                "EnemyController.ActiveEnemies");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Buildings/TowerCannonController.cs",
                errors,
                "EnemyController.ActiveEnemies");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Visuals/DamagePopup.cs",
                errors,
                "SuppressDamagePopups",
                "RecordDamagePopupSpawn",
                "RecordDamagePopupDrop",
                "MaxPoolSizePerPrefab",
                "MaxShowsPerFrame",
                "gameObject.SetActive(false)");
            RejectTokens(
                "Assets/AreaSurvivors/Scripts/Game/Visuals/DamagePopup.cs",
                errors,
                "Destroy(gameObject)");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Characters/EnemyHitFlash.cs",
                errors,
                "SuppressHitFlash",
                "sharedOverlayMaterial",
                "MaterialPropertyBlock",
                "RecordHitFlashCoalescedRequest",
                "HasPrefabReferences");
            RejectTokens(
                "Assets/AreaSurvivors/Scripts/Game/Characters/EnemyHitFlash.cs",
                errors,
                "new GameObject",
                "new Material(",
                "AddComponent<");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Visuals/RuntimeTextMeshOutline.cs",
                errors,
                "sharedOutlineMaterial",
                "MaterialPropertyBlock",
                "SetPropertyBlock");
            RejectTokens(
                "Assets/AreaSurvivors/Scripts/Game/Visuals/RuntimeTextMeshOutline.cs",
                errors,
                "void LateUpdate",
                "new Material(");
            RequireTokens(
                "Assets/AreaSurvivors/Editor/CombatFeedbackPerformanceMigration.cs",
                errors,
                "Combat Feedback Performance",
                "Enemy Hit Flash",
                "DamagePopupOutline.mat",
                "EnemyHitFlash.mat",
                "WriteSuccessMarker");
            RequireTokens(
                "Assets/AreaSurvivors/Editor/EnemyCollisionLayerMigration.cs",
                errors,
                "Enemy Collision Layer",
                "EnsureEnemyLayer",
                "Physics2D.GetIgnoreLayerCollision",
                "Enemy-to-Enemy collision must remain enabled",
                "transforms[i].gameObject.layer = enemyLayer",
                "SaveAsPrefabAsset");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Testing/RuntimePerformanceProbe.cs",
                errors,
                "combat-performance-probe-last.txt",
                "warmupSecondsRemaining",
                "BeginRecording",
                "ApplyModeOverrides",
                "MeasureEnemyQueryPaths",
                "enemyQueryLegacyUs",
                "enemyQueryRegistryUs",
                "managedDelta",
                "Physics2D.IgnoreLayerCollision",
                "EnemyController.EnemyLayerName",
                "Completed?.Invoke",
                "ToCompactString");
            RejectTokens(
                "Assets/AreaSurvivors/Scripts/Testing/RuntimePerformanceProbe.cs",
                errors,
                "Physics2D.IgnoreCollision(",
                "ignoredEnemyCollisionPairs");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Testing/RuntimePerformanceProbeMatrix.cs",
                errors,
                "combat-performance-matrix-last.txt",
                "CaptureEnemyStates",
                "RestoreEnemyStates",
                "Physics2D.SyncTransforms",
                "RuntimePerformanceProbe.Completed",
                "AreaSurvivorsPerformanceMatrixV1",
                "combat-performance-matrix-");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Game/Characters/EnemySpawner.cs",
                errors,
                "PerformanceSafeAbsoluteMaxAliveEnemies = 200",
                "CurrentMaxAliveEnemies",
                "bool forceBossSpawn = IsBossTimedSpawn(timed)",
                "if (spawned <= 0 && !forceBossSpawn) continue;",
                "StopSpawning();",
                "if (!isActiveAndEnabled) return;");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Testing/RuntimePerformanceSentinel.cs",
                errors,
                "public int stageDifficulty;",
                "public int maxAliveEnemies;",
                "spawner.CurrentStageDifficulty",
                "spawner.CurrentMaxAliveEnemies",
                "Stage difficulty / max alive enemies",
                "maxIncidentsPerStage = 5",
                "maxIncidentsPerReasonPerStage = 2",
                "BuildStageCoverage",
                "stage = activeIncidentStage");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Testing/GameplayTestRunner.cs",
                errors,
                "scenario.performanceProbeWarmupSeconds",
                "RuntimePerformanceProbeMatrix.Begin",
                "scenario.performanceProbeTransitionSeconds",
                "enemySpawner.StopSpawning();");
            RequireTokens(
                "Tools/TokenUsage/combat-performance-probe.ps1",
                errors,
                "Start Baseline (10s)",
                "Start Without Damage Feedback (10s)",
                "Prepare Excalibur Sustained Baseline",
                "Prepare Frost Sustained Without Damage Feedback",
                "Prepare Excalibur Sustained Without Enemy Controller",
                "Prepare Excalibur Sustained Without Enemy Contact Check",
                "Prepare Excalibur Sustained Without Enemy Move Multiplier",
                "Prepare Excalibur Sustained Without Enemy Paint",
                "Prepare Excalibur Sustained Without Enemy Animation",
                "Prepare Excalibur Sustained Without Enemy Y Sort",
                "Prepare Excalibur Sustained Without Enemy-Enemy Collision",
                "Prepare Enemy Crowd Baseline",
                "Prepare Enemy Crowd Without Enemy-Enemy Collision",
                "Prepare Enemy Crowd Without Occlusion",
                "Prepare Enemy Crowd Without Outline",
                "Prepare Enemy Crowd Without Occlusion And Outline",
                "Prepare Enemy Crowd Without Enemy Controller",
                "Prepare Enemy Crowd With Physics Multithreading",
                "RebuildPerformanceLoadMatrix",
                "PrepareEnemyLoad200Matrix",
                "PrepareEnemyLoad400Matrix",
                "PrepareEnemyLoad800Matrix",
                "LastMatrixResult",
                "Prepare Excalibur Kill Burst Baseline",
                "Play Mode exit cooldown",
                "LastResult");
            RequireTokens(
                "Assets/AreaSurvivors/Editor/CombatPerformanceProbeCommands.cs",
                errors,
                "Gameplay_Combat_Performance_Excalibur_Sustained.asset",
                "Gameplay_Combat_Performance_Excalibur_KillBurst.asset",
                "Gameplay_Combat_Performance_Frost_Sustained.asset",
                "Gameplay_Combat_Performance_Enemy_Crowd.asset",
                "SustainedEnemyBaseHp = 5000",
                "KillBurstEnemyBaseHp = 1",
                "EnemiesPerCluster = 8",
                "LichCrowdClusterOffsets",
                "RunState.SetNextWeaponTest(WeaponType.Excalibur)",
                "enableEnemySpawner = false",
                "IntegerOverride(\"enemyDamage\", 0)",
                "FloatOverride(\"enemyBaseSpeed\", 0f)",
                "testDurationSeconds = 90f",
                "autoExitPlayModeOnComplete = false",
                "overrideStartingWeapon = true",
                "GameplayTestActionType.MoveObjectToCell",
                "objectName = \"Player\"",
                "performanceProbeWarmupSeconds = 0f",
                "performanceProbeWarmupSeconds = EnemyCrowdProbeWarmupSeconds",
                "performanceProbeDurationSeconds = ProbeDurationSeconds");
            RequireTokens(
                "Assets/AreaSurvivors/Editor/GameplayTestTools.cs",
                errors,
                "Gameplay_Enemy_Load_200.asset",
                "Gameplay_Enemy_Load_400.asset",
                "Gameplay_Enemy_Load_800.asset",
                "Rebuild 200-400-800 Matrix",
                "GetPerformanceLoadProbeModes",
                "runPerformanceProbeMatrix = usePerformanceMatrix",
                "performanceProbeTransitionSeconds = 0.5f",
                "randomSeed = 20260727");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Testing/GameplayTestScenario.cs",
                errors,
                "public float performanceProbeWarmupSeconds;",
                "public bool runPerformanceProbeMatrix;",
                "public RuntimePerformanceProbeMode[] performanceProbeMatrixModes",
                "public float performanceProbeTransitionSeconds",
                "public bool overrideStartingWeapon",
                "public WeaponType startingWeapon");
            RequireTokens(
                "Assets/AreaSurvivors/Scripts/Testing/GameplayTestBootstrap.cs",
                errors,
                "if (scenario.overrideStartingWeapon)",
                "RunState.SetNextWeaponTest(scenario.startingWeapon)");
            ValidateCombatFeedbackPrefabs(errors);
            ValidateEnemyCollisionLayer(errors);
            ValidatePerformanceLoadScenarios(errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Combat performance probe validation failed:\n- " + string.Join("\n- ", errors));
            }

            string markerPath = ProjectPath(MarkerRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log("Combat performance probe validation passed.");
        }

        static void RequireMode(RuntimePerformanceProbeMode mode, int expectedValue, List<string> errors)
        {
            if ((int)mode != expectedValue) errors.Add($"{mode} enum value must remain {expectedValue}.");
        }

        static void RequireApproximately(float actual, float expected, string label, List<string> errors)
        {
            if (!Mathf.Approximately(actual, expected))
            {
                errors.Add($"{label} expected {expected:0.###} but was {actual:0.###}.");
            }
        }

        static void RequireTokens(string relativePath, List<string> errors, params string[] tokens)
        {
            string path = ProjectPath(relativePath);
            if (!File.Exists(path))
            {
                errors.Add($"Missing file: {relativePath}");
                return;
            }

            string text = File.ReadAllText(path);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!text.Contains(tokens[i])) errors.Add($"{relativePath} is missing token: {tokens[i]}");
            }
        }

        static void RejectTokens(string relativePath, List<string> errors, params string[] tokens)
        {
            string path = ProjectPath(relativePath);
            if (!File.Exists(path))
            {
                errors.Add($"Missing file: {relativePath}");
                return;
            }

            string text = File.ReadAllText(path);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (text.Contains(tokens[i])) errors.Add($"{relativePath} contains forbidden token: {tokens[i]}");
            }
        }

        static void ValidateCombatFeedbackPrefabs(List<string> errors)
        {
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(
                CombatFeedbackPerformanceMigration.EnemyPrefabPath);
            var flash = enemy != null ? enemy.GetComponent<EnemyHitFlash>() : null;
            if (flash == null || !flash.HasPrefabReferences || flash.SharedOverlayMaterial == null)
                errors.Add("Enemy prefab must contain prefab-authored EnemyHitFlash references and a shared material.");

            var popupObject = AssetDatabase.LoadAssetAtPath<GameObject>(
                CombatFeedbackPerformanceMigration.DamagePopupPrefabPath);
            var popup = popupObject != null ? popupObject.GetComponent<DamagePopup>() : null;
            if (popup == null ||
                popup.text == null ||
                popup.textOutline == null ||
                popup.textOutline.SharedOutlineMaterial == null ||
                popup.text.GetComponent<PreserveSortingOrder>() == null)
            {
                errors.Add("DamagePopup prefab must contain pooled visual references and a shared outline material.");
            }
        }

        static void ValidateEnemyCollisionLayer(List<string> errors)
        {
            int enemyLayer = LayerMask.NameToLayer(EnemyController.EnemyLayerName);
            if (enemyLayer < 8)
            {
                errors.Add("Enemy must use a dedicated user layer.");
                return;
            }
            if (!EnemyController.IsEnemyLayer(enemyLayer))
            {
                errors.Add("EnemyController must recognize the dedicated Enemy layer.");
            }
            if (Physics2D.GetIgnoreLayerCollision(enemyLayer, enemyLayer))
            {
                errors.Add("Enemy-to-Enemy collision must remain enabled.");
            }

            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(
                EnemyCollisionLayerMigration.EnemyPrefabPath);
            if (enemy == null)
            {
                errors.Add("Enemy prefab is missing.");
                return;
            }

            var transforms = enemy.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].gameObject.layer != enemyLayer)
                {
                    errors.Add(
                        $"Enemy prefab object must use Enemy layer: {transforms[i].name}");
                }
            }

            var body = enemy.GetComponent<Rigidbody2D>();
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic)
            {
                errors.Add("Enemy prefab must retain its Dynamic Rigidbody2D.");
            }

            var colliders = enemy.GetComponentsInChildren<Collider2D>(true);
            if (colliders.Length == 0)
            {
                errors.Add("Enemy prefab must retain a physical Collider2D.");
            }
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].isTrigger)
                {
                    errors.Add(
                        $"Enemy collider must remain non-trigger: {colliders[i].name}");
                }
            }
        }

        static void ValidatePerformanceLoadScenarios(List<string> errors)
        {
            string[] paths =
            {
                "Assets/AreaSurvivors/Testing/Gameplay_Enemy_Load_200.asset",
                "Assets/AreaSurvivors/Testing/Gameplay_Enemy_Load_400.asset",
                "Assets/AreaSurvivors/Testing/Gameplay_Enemy_Load_800.asset"
            };
            int[] expectedEnemyCounts = { 200, 400, 800 };
            var expectedModes = GameplayTestTools.GetPerformanceLoadProbeModes();

            for (int scenarioIndex = 0; scenarioIndex < paths.Length; scenarioIndex++)
            {
                string path = paths[scenarioIndex];
                int expectedEnemyCount = expectedEnemyCounts[scenarioIndex];
                var scenario = AssetDatabase.LoadAssetAtPath<GameplayTestScenario>(path);
                if (scenario == null)
                {
                    errors.Add($"Performance load scenario is missing: {path}");
                    continue;
                }

                int actualEnemyCount = 0;
                var placements = scenario.enemies ?? Array.Empty<GameplayTestScenario.EnemyPlacement>();
                for (int i = 0; i < placements.Length; i++)
                {
                    actualEnemyCount += Mathf.Max(0, placements[i].count);
                }

                if (actualEnemyCount != expectedEnemyCount)
                {
                    errors.Add(
                        $"{scenario.name} must spawn exactly {expectedEnemyCount} enemies, " +
                        $"but spawns {actualEnemyCount}.");
                }
                if (placements.Length == 0 || placements[0].count != expectedEnemyCount / 10 ||
                    placements[0].spacing != Vector2Int.zero)
                {
                    errors.Add(
                        $"{scenario.name} must keep a deterministic 10% zero-spacing stress cluster.");
                }
                if (!scenario.systems.buildGrid ||
                    scenario.systems.enableGameManager ||
                    scenario.systems.enableEnemySpawner ||
                    scenario.systems.enableScenePlayer ||
                    scenario.systems.enableSceneTower ||
                    !scenario.systems.clearExistingEnemies)
                {
                    errors.Add($"{scenario.name} system settings must isolate the fixed enemy load.");
                }
                if (!scenario.useFixedRandomSeed || scenario.randomSeed != 20260727 ||
                    !Mathf.Approximately(scenario.simulationTimeScale, 1f))
                {
                    errors.Add($"{scenario.name} must use the shared deterministic execution settings.");
                }
                if (scenario.runPerformanceProbe || !scenario.runPerformanceProbeMatrix)
                {
                    errors.Add($"{scenario.name} must run only the performance probe matrix.");
                }
                if (!ModesEqual(scenario.performanceProbeMatrixModes, expectedModes))
                {
                    errors.Add($"{scenario.name} performance probe mode order does not match the shared matrix.");
                }

                float requiredDuration =
                    expectedModes.Length *
                    (scenario.performanceProbeWarmupSeconds + scenario.performanceProbeDurationSeconds) +
                    Mathf.Max(0, expectedModes.Length - 1) *
                    scenario.performanceProbeTransitionSeconds +
                    5f;
                if (scenario.testDurationSeconds < requiredDuration)
                {
                    errors.Add(
                        $"{scenario.name} test duration must cover the complete matrix " +
                        $"({requiredDuration:0.0}s required).");
                }
                if (scenario.assertions == null ||
                    scenario.assertions.Length != 1 ||
                    scenario.assertions[0].type != GameplayTestAssertionType.EnemyCountAtLeast ||
                    scenario.assertions[0].expectedCount != expectedEnemyCount)
                {
                    errors.Add($"{scenario.name} must assert its fixed enemy count.");
                }
            }
        }

        static bool ModesEqual(
            RuntimePerformanceProbeMode[] actual,
            RuntimePerformanceProbeMode[] expected)
        {
            if (actual == null || expected == null || actual.Length != expected.Length) return false;
            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i] != expected[i]) return false;
            }

            return true;
        }

        static string ProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
