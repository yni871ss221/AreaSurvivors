using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class July25GameplayBugFixValidator
    {
        public const string MenuPath = "Area Survivors/Validate/July 25 Gameplay Bug Fixes";
        const string ConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const string EnemyControllerPath = "Assets/AreaSurvivors/Scripts/Game/Characters/EnemyController.cs";
        const string EnemySpawnerPath = "Assets/AreaSurvivors/Scripts/Game/Characters/EnemySpawner.cs";
        const string MarkerRelativePath = "Library/AreaSafeUnity/july-25-gameplay-bug-fixes.success";

        static readonly EnemyKind[] BossKinds =
        {
            EnemyKind.OrcKing,
            EnemyKind.GoblinLord,
            EnemyKind.Lich,
            EnemyKind.Dragon
        };

        [MenuItem(MenuPath)]
        public static void Validate()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string markerPath = Path.Combine(projectRoot, MarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) throw new InvalidOperationException("GameConfig asset was not found.");

            ValidateBossCollisionMass(config);
            ValidateBossFixedHp(config, projectRoot);
            ValidateLockedContactDamageOrdering(projectRoot);
            ValidateRevivedBuildingCollisionGrace();
            ValidateThunderBurstContract(config);

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, "passed");
            Debug.Log("July 25 gameplay bug-fix validation passed.");
        }

        static void ValidateBossCollisionMass(GameConfig config)
        {
            if (!Mathf.Approximately(config.bossEnemyCollisionMass, 1000000f))
            {
                throw new InvalidOperationException(
                    $"Boss collision mass must use Rigidbody2D's maximum-scale value. Actual={config.bossEnemyCollisionMass}");
            }

            for (int i = 0; i < BossKinds.Length; i++)
            {
                var definition = config.GetEnemyDefinition(BossKinds[i]);
                if (definition == null || !definition.boss)
                {
                    throw new InvalidOperationException($"{BossKinds[i]} must remain a boss definition.");
                }

                float mass = EnemyController.ResolveCollisionMass(
                    definition.boss,
                    1f,
                    config.bossEnemyCollisionMass);
                if (!Mathf.Approximately(mass, config.bossEnemyCollisionMass))
                {
                    throw new InvalidOperationException($"{BossKinds[i]} did not receive the configured boss collision mass.");
                }
            }

            float normalMass = EnemyController.ResolveCollisionMass(false, 1f, config.bossEnemyCollisionMass);
            if (!Mathf.Approximately(normalMass, 1f))
            {
                throw new InvalidOperationException("Non-boss collision mass must return to the prefab baseline.");
            }
        }

        static void ValidateBossFixedHp(GameConfig config, string projectRoot)
        {
            ValidateBossHp(config, EnemyKind.OrcKing, 1120);
            ValidateBossHp(config, EnemyKind.GoblinLord, 4480);
            ValidateBossHp(config, EnemyKind.Lich, 8960);
            ValidateBossHp(config, EnemyKind.Dragon, 17920);

            foreach (var definition in config.enemyDefinitions)
            {
                if (definition == null) continue;

                int expectedHp = Mathf.Max(
                    1,
                    Mathf.RoundToInt(config.enemyBaseHp * Mathf.Max(0.01f, definition.hpMultiplier)));
                int actualHp = EnemySpawner.CalculateEnemyHp(config.enemyBaseHp, definition);
                if (actualHp != expectedHp)
                {
                    throw new InvalidOperationException(
                        $"{definition.kind} fixed HP mismatch. Expected={expectedHp}, Actual={actualHp}");
                }
            }

            string sourcePath = Path.Combine(projectRoot, EnemySpawnerPath);
            string source = File.ReadAllText(sourcePath);
            int spawnOneIndex = source.IndexOf("void SpawnOne(EnemyKind kind)", StringComparison.Ordinal);
            int summonedSpawnIndex = source.IndexOf("public EnemyController SpawnSummonedEnemy", spawnOneIndex, StringComparison.Ordinal);
            if (spawnOneIndex < 0 || summonedSpawnIndex <= spawnOneIndex)
            {
                throw new InvalidOperationException("EnemySpawner.SpawnOne source block was not found.");
            }

            string spawnOneSource = source.Substring(spawnOneIndex, summonedSpawnIndex - spawnOneIndex);
            if (!spawnOneSource.Contains("int hp = EnemyHp(definition);"))
            {
                throw new InvalidOperationException("Normal enemy and boss spawns must use the shared fixed HP calculation.");
            }
        }

        static void ValidateBossHp(GameConfig config, EnemyKind kind, int expectedDifficultyOneHp)
        {
            var definition = config.GetEnemyDefinition(kind);
            if (definition == null || !definition.boss)
            {
                throw new InvalidOperationException($"{kind} must remain a boss definition.");
            }

            int actualFixedHp = EnemySpawner.CalculateEnemyHp(config.enemyBaseHp, definition);
            if (actualFixedHp != expectedDifficultyOneHp)
            {
                throw new InvalidOperationException(
                    $"{kind} fixed HP mismatch. Expected={expectedDifficultyOneHp}, Actual={actualFixedHp}");
            }

            int expectedXp = Mathf.RoundToInt((float)expectedDifficultyOneHp / config.enemyBaseHp);
            if (definition.xpValue != expectedXp)
            {
                throw new InvalidOperationException(
                    $"{kind} XP must remain HP/14. Expected={expectedXp}, Actual={definition.xpValue}");
            }
        }

        static void ValidateLockedContactDamageOrdering(string projectRoot)
        {
            string sourcePath = Path.Combine(projectRoot, EnemyControllerPath);
            string source = File.ReadAllText(sourcePath);
            int updateIndex = source.IndexOf("void Update()", StringComparison.Ordinal);
            if (updateIndex < 0)
            {
                throw new InvalidOperationException("EnemyController.Update was not found.");
            }

            int lockIndex = source.IndexOf("if (actionLocked)", updateIndex, StringComparison.Ordinal);
            if (lockIndex < 0)
            {
                throw new InvalidOperationException("EnemyController action-lock branch was not found.");
            }

            int lockedContactIndex = source.IndexOf(
                "TryHandleGridObjectContact(FacingDirection)",
                lockIndex,
                StringComparison.Ordinal);
            int knockbackIndex = source.IndexOf("knockback != null && knockback.Active", lockIndex, StringComparison.Ordinal);
            if (knockbackIndex < 0)
            {
                throw new InvalidOperationException("EnemyController knockback branch was not found.");
            }

            int normalContactIndex = source.IndexOf("bool blockingGridObject", knockbackIndex, StringComparison.Ordinal);
            if (lockedContactIndex < lockIndex ||
                knockbackIndex < lockedContactIndex ||
                normalContactIndex < knockbackIndex)
            {
                throw new InvalidOperationException(
                    "Action-locked contact must run before its return, while normal contact must remain after the knockback return.");
            }
        }

        static void ValidateRevivedBuildingCollisionGrace()
        {
            var building = new GameObject("Revive Grace Building Probe");
            var player = new GameObject("Revive Grace Player Probe");
            try
            {
                var buildingCollider = building.AddComponent<BoxCollider2D>();
                var playerCollider = player.AddComponent<BoxCollider2D>();
                building.transform.position = Vector3.zero;
                player.transform.position = new Vector3(0.25f, 0f, 0f);
                Physics2D.SyncTransforms();

                if (!BuildingRevivalState.ShouldIgnoreCollisionDuringReviveGrace(buildingCollider, playerCollider))
                {
                    throw new InvalidOperationException("Active solid building/player colliders must receive collision recovery.");
                }

                player.transform.position = new Vector3(3f, 0f, 0f);
                Physics2D.SyncTransforms();
                if (!BuildingRevivalState.ShouldIgnoreCollisionDuringReviveGrace(buildingCollider, playerCollider))
                {
                    throw new InvalidOperationException(
                        "Recovery must include separated persistent-building pairs, not only initially overlapping pairs.");
                }

                buildingCollider.isTrigger = true;
                if (BuildingRevivalState.ShouldIgnoreCollisionDuringReviveGrace(buildingCollider, playerCollider))
                {
                    throw new InvalidOperationException("Trigger colliders must not receive revive collision grace.");
                }

                buildingCollider.isTrigger = false;
                buildingCollider.enabled = false;
                if (BuildingRevivalState.ShouldIgnoreCollisionDuringReviveGrace(buildingCollider, playerCollider))
                {
                    throw new InvalidOperationException("Disabled colliders must wait for the recovery refresh.");
                }
                buildingCollider.enabled = true;
                if (!BuildingRevivalState.ShouldIgnoreCollisionDuringReviveGrace(buildingCollider, playerCollider))
                {
                    throw new InvalidOperationException("A re-enabled solid collider must become a recovery pair.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(building);
                UnityEngine.Object.DestroyImmediate(player);
            }

            ValidateRecoveryReachability();
            ValidateRecoveryMovementPath();
            ValidateDeterministicRecoveryCandidateOrder();
            ValidateNonOverlappingPlayerRemainsInPlace();
            if (BuildingRevivalState.HasRequiredRecoveryStability(1) ||
                !BuildingRevivalState.HasRequiredRecoveryStability(2))
            {
                throw new InvalidOperationException(
                    "Recovery collision ignores must require two consecutive FixedUpdate-safe samples.");
            }
        }

        static void ValidateRecoveryReachability()
        {
            var walkable = new bool[7, 7];
            for (int x = 0; x < 7; x++)
            {
                walkable[x, 0] = true;
                walkable[x, 6] = true;
            }
            for (int y = 0; y < 7; y++)
            {
                walkable[0, y] = true;
                walkable[6, y] = true;
            }
            walkable[1, 1] = true;
            walkable[3, 3] = true;

            bool[,] reachable = TileGrid.BuildRecoveryReachableMask(walkable);
            if (reachable[2, 2])
            {
                throw new InvalidOperationException("A BlocksMovement cell must never be a recovery candidate.");
            }
            if (reachable[3, 3])
            {
                throw new InvalidOperationException("A closed interior component must not be a recovery candidate.");
            }
            if (!reachable[1, 1])
            {
                throw new InvalidOperationException("An exterior-connected walkable cell must remain recoverable.");
            }

            var noExteriorSeed = new bool[7, 7];
            noExteriorSeed[1, 1] = true;
            noExteriorSeed[1, 2] = true;
            noExteriorSeed[3, 2] = true;
            noExteriorSeed[3, 3] = true;
            noExteriorSeed[4, 3] = true;
            bool[,] fallback = TileGrid.BuildRecoveryReachableMask(noExteriorSeed);
            if (fallback[1, 1] || !fallback[3, 2] || !fallback[4, 3])
            {
                throw new InvalidOperationException(
                    "When no exterior seed exists, recovery must deterministically use only the largest component.");
            }
        }

        static void ValidateRecoveryMovementPath()
        {
            var gridRoot = CreateRecoveryGridProbe("Recovery Movement Grid", out TileGrid grid);
            var playerObject = new GameObject("Recovery Movement Player Probe");
            var candidateBlocker = new GameObject("Recovery Secondary Collider Blocker");
            GameObject allCandidatesBlocker = null;
            try
            {
                var objects = new GridObjectRecord[grid.width, grid.height];
                objects[2, 2] = new GridObjectRecord
                {
                    flags = GridCellFlags.BlocksMovement
                };
                SetTileGridObjects(grid, objects);

                var primaryCollider = playerObject.AddComponent<BoxCollider2D>();
                primaryCollider.size = new Vector2(0.24f, 0.24f);
                primaryCollider.offset = new Vector2(0.2f, -0.25f);

                var secondaryObject = new GameObject("Secondary Solid Collider");
                secondaryObject.transform.SetParent(playerObject.transform, false);
                secondaryObject.transform.localPosition = new Vector3(0.45f, 0.3f, 0f);
                var secondaryCollider = secondaryObject.AddComponent<BoxCollider2D>();
                secondaryCollider.size = new Vector2(0.24f, 0.24f);

                var player = playerObject.AddComponent<PlayerController>();
                var body = playerObject.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                SetPlayerHitCollider(player, primaryCollider);

                Vector2 originSample = grid.GridToWorld(2, 2);
                body.position = originSample - primaryCollider.offset;
                Physics2D.SyncTransforms();
                Vector2 sampleOffset = (Vector2)player.MovementSamplePosition() - body.position;
                Vector2 expectedSampleOffset = (Vector2)primaryCollider.bounds.center - body.position;
                if (sampleOffset.sqrMagnitude <= 0.000001f ||
                    (sampleOffset - expectedSampleOffset).sqrMagnitude > 0.000001f)
                {
                    throw new InvalidOperationException(
                        $"MovementSamplePosition must use the configured primary collider offset. Expected={expectedSampleOffset}, Actual={sampleOffset}");
                }
                Vector2 originBodyPosition = originSample - sampleOffset;
                body.position = originBodyPosition;

                Vector2 firstCandidateSample = grid.GridToWorld(2, 1);
                Vector2 firstCandidateBody = firstCandidateSample - sampleOffset;
                candidateBlocker.transform.position =
                    firstCandidateBody + (Vector2)secondaryObject.transform.localPosition;
                var candidateBlockerCollider = candidateBlocker.AddComponent<BoxCollider2D>();
                candidateBlockerCollider.size = new Vector2(0.3f, 0.3f);
                Physics2D.SyncTransforms();

                if (!BuildingRevivalState.TryResolvePlayerRecovery(grid, player, out Vector2 resolvedPosition))
                {
                    throw new InvalidOperationException(
                        "A Dynamic player inside a BlocksMovement cell must resolve to the exterior component.");
                }

                Vector2 expectedSample = grid.GridToWorld(1, 2);
                Vector2 resolvedSample = player.MovementSamplePosition();
                if ((resolvedSample - expectedSample).sqrMagnitude > 0.000001f)
                {
                    throw new InvalidOperationException(
                        $"MovementSamplePosition must land on the selected cell center. Expected={expectedSample}, Actual={resolvedSample}");
                }
                if ((resolvedPosition - body.position).sqrMagnitude > 0.000001f ||
                    !grid.IsRecoveryReachable(resolvedSample))
                {
                    throw new InvalidOperationException(
                        "Resolved body position must be reported exactly and belong to the exterior component.");
                }

                var playerColliders = playerObject.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < playerColliders.Length; i++)
                {
                    var distance = playerColliders[i].Distance(candidateBlockerCollider);
                    if (!distance.isValid || distance.isOverlapped || distance.distance < 0.05f)
                    {
                        throw new InvalidOperationException(
                            "A candidate touching either player solid collider must be rejected for the next candidate.");
                    }
                }

                body.position = originBodyPosition;
                allCandidatesBlocker = new GameObject("Recovery All Candidates Blocker");
                allCandidatesBlocker.transform.position = grid.GetWorldBounds().center;
                var allCandidatesCollider = allCandidatesBlocker.AddComponent<BoxCollider2D>();
                allCandidatesCollider.size = new Vector2(20f, 20f);
                Physics2D.SyncTransforms();

                if (BuildingRevivalState.TryResolvePlayerRecovery(grid, player, out resolvedPosition))
                {
                    throw new InvalidOperationException("A fully blocked recovery component must report no candidate.");
                }
                if ((body.position - originBodyPosition).sqrMagnitude > 0.000001f ||
                    (resolvedPosition - originBodyPosition).sqrMagnitude > 0.000001f)
                {
                    throw new InvalidOperationException(
                        "A failed full-component search must restore both Rigidbody2D.position and resolvedPosition.");
                }
            }
            finally
            {
                if (allCandidatesBlocker != null) UnityEngine.Object.DestroyImmediate(allCandidatesBlocker);
                UnityEngine.Object.DestroyImmediate(candidateBlocker);
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(gridRoot);
            }
        }

        static void SetTileGridObjects(TileGrid grid, GridObjectRecord[,] objects)
        {
            var field = typeof(TileGrid).GetField(
                "objects",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException("TileGrid.objects reflection probe could not find the private field.");
            }
            field.SetValue(grid, objects);
        }

        static void SetPlayerHitCollider(PlayerController player, Collider2D collider)
        {
            var field = typeof(PlayerController).GetField(
                "hitCollider",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException(
                    "PlayerController.hitCollider reflection probe could not find the private field.");
            }
            field.SetValue(player, collider);
        }

        static void ValidateDeterministicRecoveryCandidateOrder()
        {
            var gridRoot = CreateRecoveryGridProbe("Recovery Candidate Order Grid", out TileGrid grid);
            try
            {
                Vector3Int leftCell = grid.GridToCell(2, 2);
                Vector3Int rightCell = grid.GridToCell(3, 2);
                Vector3 origin = (grid.GridToWorld(2, 2) + grid.GridToWorld(3, 2)) * 0.5f;
                var candidates = new System.Collections.Generic.List<Vector3Int>();
                if (!grid.TryGetRecoveryCandidates(origin, candidates, out _) || candidates.Count < 2)
                {
                    throw new InvalidOperationException("Recovery candidate ordering did not produce the open exterior component.");
                }
                if (candidates[0] != leftCell || candidates[1] != rightCell)
                {
                    throw new InvalidOperationException(
                        "Equidistant recovery candidates must use the fixed Y-then-X tie-break.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gridRoot);
            }
        }

        static void ValidateNonOverlappingPlayerRemainsInPlace()
        {
            var gridRoot = CreateRecoveryGridProbe("Recovery Clear Player Grid", out TileGrid grid);
            var playerObject = new GameObject("Recovery Clear Player Probe");
            try
            {
                playerObject.transform.position = grid.GridToWorld(2, 2);
                playerObject.AddComponent<BoxCollider2D>();
                var player = playerObject.AddComponent<PlayerController>();
                var body = playerObject.GetComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                Vector2 origin = body.position;
                Physics2D.SyncTransforms();
                if (!BuildingRevivalState.TryResolvePlayerRecovery(grid, player, out Vector2 resolvedPosition))
                {
                    throw new InvalidOperationException("An already safe player position must be accepted.");
                }
                if ((resolvedPosition - origin).sqrMagnitude > 0.0001f ||
                    (body.position - origin).sqrMagnitude > 0.0001f)
                {
                    throw new InvalidOperationException("A normally safe player must remain unmoved.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playerObject);
                UnityEngine.Object.DestroyImmediate(gridRoot);
            }
        }

        static GameObject CreateRecoveryGridProbe(string name, out TileGrid tileGrid)
        {
            var root = new GameObject(name);
            root.transform.position = new Vector3(20000f, 20000f, 0f);
            root.AddComponent<Grid>();
            var tilemapObject = new GameObject("Ground Tilemap");
            tilemapObject.transform.SetParent(root.transform, false);
            var tilemap = tilemapObject.AddComponent<UnityEngine.Tilemaps.Tilemap>();
            tileGrid = root.AddComponent<TileGrid>();
            tileGrid.width = 5;
            tileGrid.height = 5;
            tileGrid.groundTilemap = tilemap;
            return root;
        }

        static void ValidateThunderBurstContract(GameConfig config)
        {
            AssertApproximately(
                0.5f,
                AdvancedWeaponRuntime.BurstProjectileIntervalSeconds(WeaponType.ThunderBall, config),
                "Thunder Ball interval");
            AssertApproximately(
                0.5f,
                AdvancedWeaponRuntime.BurstProjectileIntervalSeconds(WeaponType.ThunderStorm, config),
                "Thunder Storm interval");

            for (int projectileIndex = 0; projectileIndex < 3; projectileIndex++)
            {
                bool expectedThunderSfx = projectileIndex == 0;
                AssertEqual(
                    expectedThunderSfx,
                    AdvancedWeaponRuntime.ShouldPlayBurstProjectileSfx(WeaponType.ThunderBall, projectileIndex),
                    $"Thunder Ball SFX index {projectileIndex}");
                AssertEqual(
                    expectedThunderSfx,
                    AdvancedWeaponRuntime.ShouldPlayBurstProjectileSfx(WeaponType.ThunderStorm, projectileIndex),
                    $"Thunder Storm SFX index {projectileIndex}");
                AssertEqual(
                    true,
                    AdvancedWeaponRuntime.ShouldPlayBurstProjectileSfx(WeaponType.MachineGun, projectileIndex),
                    $"Machine Gun SFX index {projectileIndex}");
            }
        }

        static void AssertApproximately(float expected, float actual, string label)
        {
            if (!Mathf.Approximately(expected, actual))
            {
                throw new InvalidOperationException($"{label} failed. Expected={expected}, Actual={actual}");
            }
        }

        static void AssertEqual(bool expected, bool actual, string label)
        {
            if (expected != actual)
            {
                throw new InvalidOperationException($"{label} failed. Expected={expected}, Actual={actual}");
            }
        }
    }
}
