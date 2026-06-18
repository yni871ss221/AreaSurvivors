using System.Collections.Generic;
using System.IO;
using AreaSurvivors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class AreaSurvivorsBootstrap
    {
        const string Root = "Assets/AreaSurvivors";
        const string Scenes = Root + "/Scenes";
        const string Prefabs = Root + "/Prefabs";
        const string Sprites = Root + "/Sprites";
        const string GeneratedSprites = Root + "/Sprites/Generated";
        const string ResourcesPath = Root + "/Resources";
        const string TilePalette = Root + "/TilePalette";
        const float TileCellWidth = 0.7f;
        const float TileCellHeight = 0.5f;

        [MenuItem("Area Survivors/Build Initial Project")]
        public static void BuildAll()
        {
            EnsureFolders();
            CreateSprites();
            CreateTilePalette();
            var config = CreateConfig();
            ApplyPrototypeDefaults(config);
            var prefabs = CreatePrefabs(config);
            CreateMenuScene(SceneNames.Title, typeof(TitleScreen));
            CreateMenuScene(SceneNames.Options, typeof(OptionsScreen));
            CreateMenuScene(SceneNames.Lobby, typeof(LobbyScreen));
            CreateMenuScene(SceneNames.Upgrades, typeof(UpgradeScreen));
            CreateGameScene(config, prefabs);
            CreateMenuScene(SceneNames.GameEnd, typeof(GameOverScreen));
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Area Survivors initial project generated.");
        }

        [MenuItem("Area Survivors/Config/Apply Weapon Level Defaults")]
        public static void ApplyWeaponLevelDefaults()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ResourcesPath + "/Config/GameConfig.asset");
            if (config == null)
            {
                Debug.LogWarning("GameConfig.asset was not found.");
                return;
            }

            config.EnsureWeaponLevelDefaults();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Weapon level defaults were applied to GameConfig.asset.");
        }

        [MenuItem("Area Survivors/Rebuild Build Prefabs")]
        public static void RebuildBuildPrefabs()
        {
            EnsureFolders();
            ImportGeneratedSprites();

            var arrow = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Arrow.prefab");
            if (arrow == null)
            {
                var config = CreateConfig();
                arrow = SavePrefab(CreateProjectile("Arrow", LoadSprite("Arrow"), new Color(0.85f, 0.72f, 0.35f), config), Prefabs + "/Arrow.prefab");
            }

            SavePrefab(CreateBallista(arrow), Prefabs + "/BallistaTower.prefab");
            SavePrefab(CreateWoodenWall(), Prefabs + "/WoodenWall.prefab");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Area Survivors build prefabs rebuilt.");
        }

        [MenuItem("Area Survivors/Rebuild Player Prefab")]
        public static void RebuildPlayerPrefab()
        {
            EnsureFolders();
            ImportGeneratedSprites();

            var config = CreateConfig();
            var arrow = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Arrow.prefab");
            if (arrow == null)
            {
                arrow = SavePrefab(CreateProjectile("Arrow", LoadSprite("Arrow"), new Color(0.85f, 0.72f, 0.35f), config), Prefabs + "/Arrow.prefab");
            }

            var fireball = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/Fireball.prefab");
            if (fireball == null)
            {
                fireball = SavePrefab(CreateProjectile("Fireball", LoadSprite("Fireball"), new Color(1f, 0.35f, 0.16f), config), Prefabs + "/Fireball.prefab");
            }

            SavePrefab(CreatePlayer(arrow, fireball), Prefabs + "/Player.prefab");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Area Survivors player prefab rebuilt.");
        }

        [MenuItem("Area Survivors/Map/Rebuild Map Perimeter")]
        public static void RebuildMapPerimeter()
        {
            var grid = Object.FindObjectOfType<TileGrid>();
            if (grid == null)
            {
                Debug.LogWarning("TileGrid was not found in the active scene.");
                return;
            }

            var perimeterType = GetRuntimeType("AreaSurvivors.MapPerimeterController");
            var perimeter = Object.FindObjectOfType(perimeterType) as Component;
            if (perimeter == null) perimeter = new GameObject("Map Perimeter").AddComponent(perimeterType);
            SetObjectReference(perimeter, "grid", grid);
            perimeter.SendMessage("Rebuild", SendMessageOptions.DontRequireReceiver);
            EditorUtility.SetDirty(perimeter);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = perimeter.gameObject;
            Debug.Log("Map perimeter rebuilt.");
        }

        static void EnsureFolders()
        {
            foreach (var path in new[] { Root, Scenes, Prefabs, Sprites, GeneratedSprites, ResourcesPath, TilePalette, Root + "/Resources/Config" })
            {
                if (!AssetDatabase.IsValidFolder(path))
                {
                    var parent = Path.GetDirectoryName(path).Replace("\\", "/");
                    var name = Path.GetFileName(path);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        static GameConfig CreateConfig()
        {
            var path = Root + "/Resources/Config/GameConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(config, path);
            }
            EditorUtility.SetDirty(config);
            return config;
        }

        static void ApplyPrototypeDefaults(GameConfig config)
        {
            config.cameraOrthographicSize = 12.5f;
            config.cameraOffset = new Vector3(0f, -15.5f, -19f);
            config.cameraPitch = -45f;
            config.cameraZoomedInOrthographicSize = 3.9f;
            config.cameraZoomedInOffset = new Vector3(0f, -8.5f, -9f);
            config.cameraZoomedInPitch = -35f;
            config.cameraDefaultZoom = 0.5f;
            config.cameraZoomScrollSpeed = 0.16f;
            config.cameraPlayerWeight = 0.55f;
            config.playerMoveSpeed = 2.1f;
            config.playerMaxHp = 40;
            config.playerReviveSeconds = 6f;
            config.paintRadius = 1;
            config.playerVisualScale = 1f;
            config.moveSpeedPerUpgradeLevel = 0.18f;
            config.paintRadiusLevelsPerBonus = 2;
            config.maxHpPerUpgradeLevel = 5;
            config.reviveSecondsReductionPerUpgradeLevel = 0.35f;
            config.minReviveSeconds = 1f;
            config.runMoveSpeedMultiplier = 1.08f;
            config.runPaintRadiusBonus = 1;
            config.runMaxHpBonus = 8;
            config.towerMaxHp = 160;
            config.towerMaxHpPerUpgradeLevel = 12;
            config.towerUpgradeWoodCost = 300;
            config.towerUpgradeStoneCost = 300;
            config.towerUpgradeBuildSeconds = 5f;
            config.upgradedTowerMaxHp = 450;
            config.upgradedTowerRegenBonus = 3;
            config.upgradedTowerCannonDamageBonus = 10;
            config.upgradedTowerCannonExplosionRadiusMultiplier = 2f;
            config.upgradedTowerImmediatePaintRadiusCells = 15;
            config.ballistaRange = 9.5f;
            config.ballistaMaxHp = 90;
            config.baseAttackPower = 6;
            config.knightCooldown = 1.05f;
            config.archerCooldown = 0.75f;
            config.mageCooldown = 1.45f;
            config.minAttackCooldownMultiplier = 0.45f;
            config.runAttackPowerBonus = 2;
            config.runAttackCooldownMultiplier = 0.92f;
            config.knightDamageBonus = 2;
            config.knightSlashRange = 1.05f;
            config.knightSlashOffset = 1.05f;
            config.mageExplosionRadius = 1.1f;
            config.baseKnockback = 1f;
            config.knockbackForceUnit = 2.2f;
            config.knockbackDuration = 0.16f;
            config.baseDefense = 0;
            config.baseXpGainMultiplier = 1f;
            config.baseAutoRegen = 0;
            config.autoRegenIntervalSeconds = 2f;
            config.baseWorkSpeedMultiplier = 1f;
            config.baseResourceGainBonus = 0;
            config.defensePerUpgradeLevel = 1;
            config.xpGainMultiplierPerUpgradeLevel = 0.1f;
            config.autoRegenPerUpgradeLevel = 1;
            config.workSpeedMultiplierPerUpgradeLevel = 0.1f;
            config.resourceGainPerUpgradeLevel = 1;
            config.runKnockbackBonus = 1;
            config.runDefenseBonus = 1;
            config.runXpGainMultiplierBonus = 0.1f;
            config.runAutoRegenBonus = 1;
            config.runWorkSpeedMultiplierBonus = 0.1f;
            config.runResourceGainBonus = 1;
            config.projectileSpeed = 11.5f;
            config.projectileLifetime = 4.2f;
            config.projectileVisualScale = 1.35f;
            config.enemyBaseSpeed = 0.9f;
            config.enemyVisualScale = 1f;
            config.enemyDamage = 3;
            config.spawnInterval = 1.8f;
            config.enemySpawnRadius = 28f;
            config.difficultyRampSeconds = 55f;
            config.spawnDirectionChangeSeconds = 30f;
            config.spawnDirectionArcDegrees = 60f;
            config.maxAliveEnemies = 160;
            config.bossTimeSeconds = 300f;
            config.bossAnnouncement = "\u30aa\u30fc\u30af\u30ad\u30f3\u30b0\u51fa\u73fe\uff01";
            config.EnsureEnemySpawnDefaults();
            config.startingBallistaStock = 4;
            config.startingWallStock = 4;
            EditorUtility.SetDirty(config);
        }

        static void CreateTilePalette()
        {
            ImportGeneratedSprites();
            foreach (var name in new[] { "Ground", "Paint", "Tower", "Ballista", "WoodenWall", "WoodenGateClosed" })
            {
                CreateTileAsset(name);
            }

            var palette = new GameObject("Environment Palette");
            var grid = palette.AddComponent<Grid>();
            grid.cellSize = new Vector3(TileCellWidth, TileCellHeight, 0f);
            var tilemap = CreateTilemap(palette.transform, "Palette Tiles", 0);
            var tiles = new[] { "Ground", "Paint", "Tower", "Ballista", "WoodenWall", "WoodenGateClosed" };
            for (int i = 0; i < tiles.Length; i++)
            {
                tilemap.SetTile(new Vector3Int(i, 0, 0), LoadTile(tiles[i]));
            }

            PrefabUtility.SaveAsPrefabAsset(palette, TilePalette + "/EnvironmentPalette.prefab");
            Object.DestroyImmediate(palette);
        }

        static Tile CreateTileAsset(string name)
        {
            var path = TilePalette + "/" + name + ".asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }

            tile.name = name;
            tile.sprite = name == "Ground" ? LoadMapSprite("Tile") : name == "Paint" ? LoadMapSprite("PaintTile") : LoadGeneratedSprite(name) ?? LoadSprite(name);
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        static Tile LoadTile(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Tile>(TilePalette + "/" + name + ".asset");
        }

        static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tilemap = go.AddComponent<Tilemap>();
            var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.mode = TilemapRenderer.Mode.Individual;
            return tilemap;
        }

        static PrefabSet CreatePrefabs(GameConfig config)
        {
            ImportGeneratedSprites();
            var arrow = SavePrefab(CreateProjectile("Arrow", LoadSprite("Arrow"), new Color(0.85f, 0.72f, 0.35f), config), Prefabs + "/Arrow.prefab");
            var fireball = SavePrefab(CreateProjectile("Fireball", LoadSprite("Fireball"), new Color(1f, 0.35f, 0.16f), config), Prefabs + "/Fireball.prefab");
            var ballista = SavePrefab(CreateBallista(arrow), Prefabs + "/BallistaTower.prefab");
            var woodenWall = SavePrefab(CreateWoodenWall(), Prefabs + "/WoodenWall.prefab");
            var woodenGate = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/WoodenGate.prefab") ?? woodenWall;
            var set = new PrefabSet
            {
                arrow = arrow,
                fireball = fireball,
                ballista = ballista,
                woodenWall = woodenWall,
                woodenGate = woodenGate,
                player = SavePrefab(CreatePlayer(arrow, fireball), Prefabs + "/Player.prefab").GetComponent<PlayerController>(),
                enemy = SavePrefab(CreateEnemy(), Prefabs + "/Enemy.prefab"),
                xpOrb = SavePrefab(CreateXpOrb(), Prefabs + "/ExperienceOrb.prefab"),
                damagePopup = SavePrefab(CreateDamagePopup(), Prefabs + "/DamagePopup.prefab")
            };
            return set;
        }

        static GameObject SavePrefab(GameObject go, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject CreatePlayer(GameObject arrowPrefab, GameObject fireballPrefab)
        {
            var knightSprite = LoadCharacterSprite("Knight");
            var archerSprite = LoadCharacterSprite("Archer");
            var mageSprite = LoadCharacterSprite("Mage");
            var go = Actor("Player", knightSprite, Color.white, 0.32f);
            var health = go.AddComponent<Health>();
            health.maxHp = 40;
            var animator = go.AddComponent<DirectionalSpriteAnimator>();
            go.AddComponent<PlayerStats>();
            go.AddComponent<AutoRegeneration>();
            var player = go.AddComponent<PlayerController>();
            player.directionalAnimator = animator;
            player.knightSprite = knightSprite;
            player.archerSprite = archerSprite;
            player.mageSprite = mageSprite;
            player.knightDownFrames = LoadWalkFramesOrStatic("Knight", "Down", knightSprite);
            player.knightLeftFrames = LoadWalkFramesOrStatic("Knight", "Left", knightSprite);
            player.knightRightFrames = LoadWalkFramesOrStatic("Knight", "Right", knightSprite);
            player.knightUpFrames = LoadWalkFramesOrStatic("Knight", "Up", knightSprite);
            player.archerDownFrames = LoadWalkFramesOrStatic("Archer", "Down", archerSprite);
            player.archerLeftFrames = LoadWalkFramesOrStatic("Archer", "Left", archerSprite);
            player.archerRightFrames = LoadWalkFramesOrStatic("Archer", "Right", archerSprite);
            player.archerUpFrames = LoadWalkFramesOrStatic("Archer", "Up", archerSprite);
            player.mageDownFrames = LoadWalkFramesOrStatic("Mage", "Down", mageSprite);
            player.mageLeftFrames = LoadWalkFramesOrStatic("Mage", "Left", mageSprite);
            player.mageRightFrames = LoadWalkFramesOrStatic("Mage", "Right", mageSprite);
            player.mageUpFrames = LoadWalkFramesOrStatic("Mage", "Up", mageSprite);
            var weapon = go.AddComponent<WeaponController>();
            player.weapon = weapon;
            player.hpBar = AddWorldHpBar(go.transform, new Vector3(0, -0.44f, 0));
            weapon.arrowPrefab = arrowPrefab;
            weapon.fireballPrefab = fireballPrefab;
            return go;
        }

        static GameObject CreateBallista(GameObject arrowPrefab)
        {
            var go = new GameObject("BallistaTower");
            ConfigureGridMarker(go, GridObjectType.Ballista, GridCellFlags.BlocksBuilding | GridCellFlags.Defensive, new Vector2Int(2, 2));
            var gridVisual = go.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureFootprint(new Vector2Int(2, 2));
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var trigger = go.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            gridVisual.ConfigureFootprintBox(trigger, true);
            var blocker = go.AddComponent<BoxCollider2D>();
            gridVisual.ConfigureFootprintBox(blocker, false);
            blocker.enabled = false;
            go.AddComponent<Health>();

            var ballistaSprite = LoadGeneratedSprite("Ballista") ?? LoadSprite("Ballista");
            var ghost = CreateSpriteVisual(go.transform, "Ghost Image", ballistaSprite, new Vector2(1.34f, 1.65f), new Color(1f, 1f, 1f, 0.34f), 1000);
            var build = CreateSpriteVisual(go.transform, "Build Fill Image", ballistaSprite, new Vector2(1.34f, 1.65f), Color.white, 1001);
            var complete = CreateSpriteVisual(go.transform, "Complete Image", ballistaSprite, new Vector2(1.34f, 1.65f), Color.white, 1002);
            SetVisualOffset(Vector3.zero, ghost, build, complete);
            var hammer = MeshChild(go.transform, "Hammer", LoadGeneratedSprite("Hammer") ?? LoadSprite("Hammer"), Color.white, 2200);
            hammer.transform.localPosition = new Vector3(0.28f, -0.12f, 0f);
            var sparkle = MeshChild(go.transform, "Completion Sparkle", LoadGeneratedSprite("Sparkle") ?? LoadSprite("Sparkle"), new Color(1f, 1f, 1f, 0f), 2400);
            sparkle.visible = false;

            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            var sortRenderers = new List<Renderer>();
            sortRenderers.AddRange(ghost.GetComponentsInChildren<Renderer>(true));
            sortRenderers.AddRange(build.GetComponentsInChildren<Renderer>(true));
            sortRenderers.AddRange(complete.GetComponentsInChildren<Renderer>(true));
            ySort.renderers = sortRenderers.ToArray();

            var ballista = go.AddComponent(GetRuntimeType("AreaSurvivors.BallistaTower"));
            SetObjectReference(ballista, "arrowPrefab", arrowPrefab);
            SetObjectReference(ballista, "blockingCollider", blocker);
            SetObjectReference(ballista, "ghostRenderer", ghost);
            SetObjectReference(ballista, "buildRenderer", build);
            SetObjectReference(ballista, "completeRenderer", complete);
            SetObjectReference(ballista, "ballistaSprite", ballistaSprite);
            SetVector2(ballista, "spriteVisualSize", new Vector2(1.34f, 1.65f));
            SetObjectReference(ballista, "ghostObject", ghost.gameObject);
            SetObjectReference(ballista, "buildObject", build.gameObject);
            SetObjectReference(ballista, "completeObject", complete.gameObject);
            SetObjectReference(ballista, "hammerRenderer", hammer);
            SetObjectReference(ballista, "sparkleRenderer", sparkle);
            return go;
        }

        static GameObject CreateWoodenWall()
        {
            var go = new GameObject("WoodenWall");
            var footprint = new Vector2Int(3, 1);
            ConfigureGridMarker(go, GridObjectType.WoodenWall, GridCellFlags.BlocksMovement | GridCellFlags.BlocksBuilding | GridCellFlags.Defensive, footprint);
            var gridVisual = go.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureFootprint(footprint);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            go.AddComponent<Health>();

            var buildTrigger = go.AddComponent<BoxCollider2D>();
            gridVisual.ConfigureFootprintBox(buildTrigger, true);
            var blocker = go.AddComponent<BoxCollider2D>();
            gridVisual.ConfigureFootprintBox(blocker, false);

            var barrierSprite = LoadGeneratedSprite("WoodenWall") ?? LoadSprite("WoodenWall");
            var ghost = CreateWoodenBarrierSpriteVisual(go.transform, "Ghost Image", barrierSprite, new Color(1f, 1f, 1f, 0.34f), 1000);
            var build = CreateWoodenBarrierSpriteVisual(go.transform, "Build Fill Image", barrierSprite, Color.white, 1001);
            var complete = CreateWoodenBarrierSpriteVisual(go.transform, "Complete Image", barrierSprite, Color.white, 1002);
            SetVisualOffset(Vector3.zero, ghost, build, complete);
            var hammer = MeshChild(go.transform, "Hammer", LoadGeneratedSprite("Hammer") ?? LoadSprite("Hammer"), Color.white, 2200);
            hammer.transform.localPosition = new Vector3(0.24f, -0.06f, 0f);
            var sparkle = MeshChild(go.transform, "Completion Sparkle", LoadGeneratedSprite("Sparkle") ?? LoadSprite("Sparkle"), new Color(1f, 1f, 1f, 0f), 2400);
            sparkle.transform.localPosition = Vector3.zero;
            sparkle.visible = false;

            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            var renderers = new List<Renderer>();
            renderers.AddRange(ghost.GetComponentsInChildren<Renderer>(true));
            renderers.AddRange(build.GetComponentsInChildren<Renderer>(true));
            renderers.AddRange(complete.GetComponentsInChildren<Renderer>(true));
            ySort.renderers = renderers.ToArray();

            var barrier = go.AddComponent(GetRuntimeType("AreaSurvivors.WoodenBarrier"));
            SetObjectReference(barrier, "blockingCollider", blocker);
            SetObjectReference(barrier, "ghostRenderer", ghost);
            SetObjectReference(barrier, "buildRenderer", build);
            SetObjectReference(barrier, "completeRenderer", complete);
            SetObjectReference(barrier, "ghostObject", ghost.gameObject);
            SetObjectReference(barrier, "completeObject", complete.gameObject);
            SetObjectReference(barrier, "hammerRenderer", hammer);
            SetObjectReference(barrier, "sparkleRenderer", sparkle);
            SetObjectReference(barrier, "barrierSprite", barrierSprite);
            SetVector2(barrier, "spriteVisualSize", new Vector2(1.34f, 0.58f));
            return go;
        }

        static PaperMeshVisual CreateWoodenBarrierSpriteVisual(Transform parent, string name, Sprite sprite, Color color, int sortingOrder)
        {
            return CreateSpriteVisual(parent, name, sprite, new Vector2(1.34f, 0.58f), color, sortingOrder);
        }

        static PaperMeshVisual CreateSpriteVisual(Transform parent, string name, Sprite sprite, Vector2 size, Color color, int sortingOrder)
        {
            var visual = MeshChild(parent, name, sprite, color, sortingOrder);
            visual.useBottomCenterAnchor = true;
            var bounds = sprite != null ? sprite.bounds.size : Vector3.one;
            float x = Mathf.Abs(bounds.x) > 0.001f ? size.x / bounds.x : 1f;
            float y = Mathf.Abs(bounds.y) > 0.001f ? size.y / bounds.y : 1f;
            visual.transform.localScale = new Vector3(x, y, 1f);
            visual.transform.localPosition = Vector3.zero;
            visual.visible = false;
            return visual;
        }

        static void SetVisualOffset(Vector3 offset, params PaperMeshVisual[] visuals)
        {
            foreach (var visual in visuals)
            {
                if (visual != null) visual.transform.localPosition = offset;
            }
        }

        static GameObject CreateEnemy()
        {
            var enemySprite = LoadCharacterSprite("EnemyBoar");
            var go = Actor("Enemy", enemySprite, Color.white, 0.34f);
            var enemyVisual = go.GetComponentInChildren<PaperMeshVisual>();
            if (enemyVisual != null)
            {
                var outline = enemyVisual.gameObject.AddComponent<RuntimeSpriteOutline>();
                outline.outlineColor = Color.black;
                outline.thickness = 0.018f;
            }
            go.AddComponent<Health>();
            var animator = go.AddComponent<DirectionalSpriteAnimator>();
            animator.SetFrames(
                LoadWalkFramesOrStatic("EnemyBoar", "Down", enemySprite),
                LoadWalkFramesOrStatic("EnemyBoar", "Left", enemySprite),
                LoadWalkFramesOrStatic("EnemyBoar", "Right", enemySprite),
                LoadWalkFramesOrStatic("EnemyBoar", "Up", enemySprite));
            var enemy = go.AddComponent<EnemyController>();
            enemy.directionalAnimator = animator;
            return go;
        }

        static GameObject CreateTower()
        {
            var go = new GameObject("Tower");
            ConfigureGridMarker(go, GridObjectType.Tower, GridCellFlags.BlocksMovement | GridCellFlags.BlocksBuilding | GridCellFlags.Defensive, new Vector2Int(3, 3));
            var gridVisual = go.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureFootprint(new Vector2Int(3, 3));
            gridVisual.fitVisualWidthToFootprint = false;
            gridVisual.resetVisualOffset = false;
            var towerSprite = LoadGeneratedSprite("Tower") ?? LoadSprite("Tower");
            var visual = MeshChild(go.transform, "Base Tower Image", towerSprite, Color.white, 1003);
            visual.useBottomCenterAnchor = true;
            if (towerSprite != null && Mathf.Abs(towerSprite.bounds.size.x) > 0.001f)
            {
                float targetWidth = 3 * GridObjectVisual.CellWidth;
                float scale = targetWidth / towerSprite.bounds.size.x;
                visual.transform.localScale = new Vector3(scale, scale, 1f);
            }
            visual.visible = true;
            visual.gameObject.AddComponent<OcclusionMaskSource>();
            var outline = visual.gameObject.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.018f;
            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.sortPivotOffsetY = 0f;
            ySort.renderers = visual.GetComponentsInChildren<Renderer>(true);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.bodyType = RigidbodyType2D.Static;
            var col = go.AddComponent<BoxCollider2D>();
            gridVisual.ConfigureFootprintBox(col, false);
            go.AddComponent<Health>();
            var tower = go.AddComponent<TowerController>();
            tower.hpBar = AddWorldHpBar(go.transform, new Vector3(0, -0.82f, 0), 0.9f);
            return go;
        }

        static GameObject Actor(string name, Sprite sprite, Color color, float colliderRadius)
        {
            var go = new GameObject(name);
            var gridVisual = go.AddComponent<GridObjectVisual>();
            gridVisual.ConfigureCharacter(1f);
            var visual = MeshChild(go.transform, "Paper Visual", sprite, HasGeneratedSprite(name) ? Color.white : color, 1000);
            ScalePaperVisual(visual, Vector2.one * TileCellWidth);
            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.renderers = new[] { visual.Renderer };
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            var col = go.AddComponent<CircleCollider2D>();
            gridVisual.ConfigureCharacterCircle(col);
            return go;
        }

        static Slider AddWorldHpBar(Transform parent, Vector3 localPos, float width = 0.55f)
        {
            var canvas = new GameObject("HP Bar").AddComponent<Canvas>();
            canvas.transform.SetParent(parent, false);
            canvas.transform.localPosition = localPos;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 3000;
            canvas.gameObject.AddComponent<PaperBillboard>();
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 0.07f);
            var slider = canvas.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = false;

            var bg = StretchImageChild(canvas.transform, "Background", new Color(0.12f, 0.04f, 0.04f, 0.74f), Vector2.zero, Vector2.one);
            var fillArea = new GameObject("Fill Area").AddComponent<RectTransform>();
            fillArea.SetParent(canvas.transform, false);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;
            var fill = StretchImageChild(fillArea, "Fill", new Color(0.25f, 0.88f, 0.35f, 0.9f), Vector2.zero, Vector2.one);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            slider.targetGraphic = bg;
            slider.fillRect = fill.rectTransform;
            return slider;
        }

        static Image ImageChild(Transform parent, string name, Color color, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.rectTransform.sizeDelta = size;
            return image;
        }

        static Image StretchImageChild(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            image.rectTransform.anchorMin = anchorMin;
            image.rectTransform.anchorMax = anchorMax;
            image.rectTransform.offsetMin = Vector2.zero;
            image.rectTransform.offsetMax = Vector2.zero;
            return image;
        }

        static PaperMeshVisual MeshChild(Transform parent, string name, Sprite sprite, Color color, int sortingOrder, bool faceCamera = true)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var visual = child.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, color, sortingOrder);
            child.AddComponent<PaperBillboard>().faceCamera = faceCamera;
            return visual;
        }

        static GameObject CreateProjectile(string name, Sprite sprite, Color color, GameConfig config)
        {
            var go = new GameObject(name);
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, config.projectileVisualScale);
            MeshChild(go.transform, "Paper Visual", sprite, color, 20);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.16f;
            var projectile = go.AddComponent<Projectile>();
            projectile.fallbackSprite = sprite;
            projectile.fallbackColor = color;
            projectile.lifetime = config.projectileLifetime;
            projectile.visualScale = config.projectileVisualScale;
            return go;
        }

        static GameObject CreateXpOrb()
        {
            var go = new GameObject("ExperienceOrb");
            go.transform.localScale = Vector3.one * 0.34f;
            var visual = MeshChild(go.transform, "Paper Visual", LoadSprite("Generated/ExperienceOrb"), Color.white, 15);
            var outline = visual.gameObject.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = 0.018f;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.28f;
            go.AddComponent<ExperienceOrb>();
            return go;
        }

        static GameObject CreateDamagePopup()
        {
            var go = new GameObject("DamagePopup");
            go.AddComponent<PaperBillboard>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var text = AddDamageText(go.transform, "Text", font, Color.white, 4000);
            var outline = text.gameObject.AddComponent<RuntimeTextMeshOutline>();
            outline.faceColor = Color.white;
            outline.outlineColor = Color.black;
            outline.outlinePixels = 2f;
            var popup = go.AddComponent<DamagePopup>();
            popup.text = text;
            return go;
        }

        static TextMesh AddDamageText(Transform parent, string name, Font font, Color color, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var text = child.AddComponent<TextMesh>();
            text.font = font;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 64;
            text.characterSize = 0.08f;
            text.color = color;
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sortingOrder = sortingOrder;
            if (font != null) renderer.sharedMaterial = font.material;
            return text;
        }

        static void CreateMenuScene(string sceneName, System.Type screenType)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject(sceneName + " Controller").AddComponent(screenType);
            EditorSceneManager.SaveScene(scene, $"{Scenes}/{sceneName}.unity");
        }

        static void CreateGameScene(GameConfig config, PrefabSet prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = config.cameraOrthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.03f, 1f);
            camera.transform.position = config.cameraOffset;
            camera.transform.rotation = Quaternion.Euler(config.cameraPitch, 0f, 0f);
            camera.gameObject.AddComponent<AudioListener>();
            var follow = camera.gameObject.AddComponent<CameraFollow>();
            follow.offset = config.cameraOffset;
            follow.zoomedInOffset = config.cameraZoomedInOffset;
            follow.orthographicSize = config.cameraOrthographicSize;
            follow.zoomedInOrthographicSize = config.cameraZoomedInOrthographicSize;
            follow.pitch = config.cameraPitch;
            follow.zoomedInPitch = config.cameraZoomedInPitch;
            follow.defaultZoom = config.cameraDefaultZoom;
            follow.scrollSpeed = config.cameraZoomScrollSpeed;
            follow.targetWeight = config.cameraPlayerWeight;

            var environment = new GameObject("Environment Grid");
            var unityGrid = environment.AddComponent<Grid>();
            unityGrid.cellSize = new Vector3(TileCellWidth, TileCellHeight, 0f);
            var groundTilemap = CreateTilemap(environment.transform, "Ground Tilemap", -20);
            var paintTilemap = CreateTilemap(environment.transform, "Paint Tilemap", -19);
            var objectTilemap = CreateTilemap(environment.transform, "Object Tilemap", 1000);
            var buildPreviewTilemap = CreateTilemap(environment.transform, "Build Preview Tilemap", 950);
            buildPreviewTilemap.color = new Color(1f, 1f, 1f, 0.72f);
            objectTilemap.tileAnchor = new Vector3(0.5f, 0f, 0f);
            objectTilemap.GetComponent<TilemapRenderer>().enabled = false;
            var grid = environment.AddComponent<TileGrid>();
            grid.tileSprite = LoadMapSprite("Tile");
            grid.paintSprite = LoadMapSprite("PaintTile");
            grid.groundTilemap = groundTilemap;
            grid.paintTilemap = paintTilemap;
            grid.objectTilemap = objectTilemap;
            grid.groundTile = LoadTile("Ground");
            grid.paintTile = LoadTile("Paint");
            grid.ApplyVerticalMapLayout();
            grid.Build();
            var perimeter = new GameObject("Map Perimeter").AddComponent(GetRuntimeType("AreaSurvivors.MapPerimeterController"));
            SetObjectReference(perimeter, "grid", grid);
            perimeter.SendMessage("Rebuild", SendMessageOptions.DontRequireReceiver);

            var spawner = new GameObject("Enemy Spawner").AddComponent<EnemySpawner>();
            spawner.enemyPrefab = prefabs.enemy;
            spawner.xpOrbPrefab = prefabs.xpOrb;
            spawner.damagePopupPrefab = prefabs.damagePopup;

            var tower = CreateTower().GetComponent<TowerController>();
            tower.transform.position = CellToWorld(grid, Vector2Int.zero);

            var manager = new GameObject("Game Manager").AddComponent<GameManager>();
            var buildPlacement = manager.gameObject.AddComponent<BuildPlacementController>();
            manager.config = config;
            manager.grid = grid;
            manager.playerPrefab = prefabs.player;
            manager.sceneTower = tower;
            manager.spawner = spawner;
            manager.buildPlacement = buildPlacement;
            buildPlacement.grid = grid;
            buildPlacement.buildPreviewTilemap = buildPreviewTilemap;
            buildPlacement.buildPreviewTile = LoadTile("Paint");
            BuildHud(manager, buildPlacement);
            ConfigureBuildPlacement(buildPlacement, prefabs);

            EditorSceneManager.SaveScene(scene, $"{Scenes}/{SceneNames.Game}.unity");
        }

        static void ConfigureBuildPlacement(BuildPlacementController buildPlacement, PrefabSet prefabs)
        {
            if (buildPlacement == null) return;
            buildPlacement.ballistaPrefab = prefabs.ballista;
            buildPlacement.woodenWallPrefab = prefabs.woodenWall;
            buildPlacement.woodenGatePrefab = prefabs.woodenGate != null ? prefabs.woodenGate : prefabs.woodenWall;
            buildPlacement.ballistaPreviewSprite = LoadGeneratedSprite("Ballista") ?? LoadSprite("Ballista");
            buildPlacement.woodenWallPreviewSprite = LoadGeneratedSprite("WoodenWall") ?? LoadSprite("WoodenWall");
            buildPlacement.woodenGatePreviewSprite = LoadGeneratedSprite("WoodenGateClosed");
            buildPlacement.woodenGateOpenSprite = LoadGeneratedSprite("WoodenGateOpen");
            buildPlacement.ballistaTile = LoadTile("Ballista");
            buildPlacement.woodenWallTile = LoadTile("WoodenWall");
            buildPlacement.woodenGateTile = LoadTile("WoodenGateClosed");
        }

        static Vector3 CellToWorld(TileGrid grid, Vector2Int cell)
        {
            return grid.groundTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        }

        static Vector3 CellToWorld(TileGrid grid, Vector2 cell)
        {
            return CellToWorld(grid, Vector2Int.zero) + new Vector3(cell.x * TileCellWidth, cell.y * TileCellHeight, 0f);
        }

        static void BuildHud(GameManager manager, BuildPlacementController buildPlacement)
        {
            var canvas = new GameObject("HUD").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.gameObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            SimpleUi.Panel(canvas.transform, "XP Backplate", new Vector2(0, 338), new Vector2(600, 22), new Color(0.03f, 0.045f, 0.045f, 0.62f));
            SimpleUi.Panel(canvas.transform, "Run Stats Backplate", new Vector2(0, 304), new Vector2(340, 36), new Color(0.03f, 0.045f, 0.045f, 0.62f));
            SimpleUi.Panel(canvas.transform, "Level Backplate", new Vector2(-548, 334), new Vector2(112, 34), new Color(0.03f, 0.045f, 0.045f, 0.62f));

            manager.xpBar = HudSlider(canvas.transform, new Vector2(0, 338), new Vector2(560, 10), new Color(0.25f, 0.55f, 1f, 0.9f));
            manager.timerText = HudText(canvas.transform, "00:00", 22, new Vector2(-86, 304), new Vector2(140, 30));
            manager.killText = HudText(canvas.transform, "\u6483\u7834 0", 22, new Vector2(92, 304), new Vector2(160, 30));
            manager.levelText = HudText(canvas.transform, "Lv 1", 20, new Vector2(-548, 334), new Vector2(92, 26));
            CreateEditableHudWidgets(canvas.transform, buildPlacement);

            var panel = new GameObject("Level Up Panel");
            panel.transform.SetParent(canvas.transform, false);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.06f, 0.07f, 0.08f, 0.92f);
            image.rectTransform.sizeDelta = new Vector2(560, 310);
            SimpleUi.Panel(panel.transform, "Level Up Frame", Vector2.zero, new Vector2(560, 310), new Color(0.06f, 0.07f, 0.08f, 0.16f));
            SimpleUi.Label(panel.transform, "\u30ec\u30d9\u30eb\u30a2\u30c3\u30d7", 32, new Vector2(0, 105), new Vector2(420, 56));
            manager.upgradeButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                manager.upgradeButtons[i] = SimpleUi.Button(panel.transform, "Upgrade", new Vector2(0, 35 - i * 74), null, new Vector2(420, 54));
            }
            panel.SetActive(false);
            manager.levelUpPanel = panel;
        }

        static void CreateEditableHudWidgets(Transform canvas, BuildPlacementController buildPlacement)
        {
            DestroyChild(canvas, "Build Backplate");
            DestroyChild(canvas, "Construction Menu");
            DestroyChild(canvas, "Tower Status");
            DestroyLegacyBuildStatusText(canvas);

            var construction = HudPanel(canvas, "Construction Menu", new Vector2(16, 16), new Vector2(276, 96), Vector2.zero, Vector2.zero, new Color(0.035f, 0.05f, 0.045f, 0.72f));
            CreateEditableBuildSlot(construction, "Build Slot 1", "1", LoadGeneratedSprite("Ballista"), new Vector2(42, 48), new Vector2(46, 44));
            CreateEditableBuildSlot(construction, "Build Slot 2", "2", LoadGeneratedSprite("WoodenWall"), new Vector2(112, 48), new Vector2(46, 44));
            CreateEditableBuildSlot(construction, "Build Slot 3", "3", LoadGeneratedSprite("WoodenGateClosed"), new Vector2(182, 48), new Vector2(46, 44));
            var status = HudText(construction, "1 \u30d0\u30ea\u30b9\u30bf x4", 14, new Vector2(238, 48), new Vector2(64, 58));
            status.name = "Build Status";
            if (buildPlacement != null) buildPlacement.buildText = status;

            var tower = HudPanel(canvas, "Tower Status", new Vector2(-14, -12), new Vector2(110, 314), Vector2.one, Vector2.one, new Color(0.035f, 0.05f, 0.045f, 0.72f));
            var towerImage = new GameObject("Tower Image").AddComponent<Image>();
            towerImage.transform.SetParent(tower, false);
            towerImage.sprite = LoadGeneratedSprite("Tower");
            towerImage.preserveAspect = true;
            towerImage.raycastTarget = false;
            AnchorTopCenter(towerImage.rectTransform);
            towerImage.rectTransform.anchoredPosition = new Vector2(0, -8);
            towerImage.rectTransform.sizeDelta = new Vector2(98, 98);

            var bar = HudPanel(tower, "Tower HP Bar", new Vector2(0, -126), new Vector2(38, 136), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Color(0.02f, 0.025f, 0.025f, 0.86f));
            var fill = new GameObject("Fill").AddComponent<Image>();
            fill.transform.SetParent(bar, false);
            fill.color = new Color(0.22f, 0.62f, 1f, 0.96f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.pivot = new Vector2(0.5f, 0);
            fill.rectTransform.offsetMin = new Vector2(4, 4);
            fill.rectTransform.offsetMax = new Vector2(-4, -4);

            var hp = HudText(tower, "184/184", 13, new Vector2(0, -286), new Vector2(88, 20));
            hp.name = "Tower HP Text";
            AnchorTopCenter(hp.rectTransform);
        }

        static void CreateEditableBuildSlot(Transform parent, string name, string key, Sprite sprite, Vector2 position, Vector2 iconSize)
        {
            var slot = HudPanel(parent, name, position, new Vector2(58, 66), Vector2.zero, Vector2.zero, new Color(0.09f, 0.16f, 0.12f, 0.92f));
            slot.gameObject.AddComponent<Button>();
            if (sprite != null)
            {
                var icon = new GameObject("Icon").AddComponent<Image>();
                icon.transform.SetParent(slot, false);
                icon.sprite = sprite;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.rectTransform.anchoredPosition = new Vector2(0, -2);
                icon.rectTransform.sizeDelta = iconSize;
            }
            var keyText = HudText(slot, key, 16, new Vector2(-18, 22), new Vector2(24, 22));
            keyText.name = "Key";
            var stock = HudText(slot, "x4", 12, new Vector2(17, -22), new Vector2(34, 18));
            stock.name = "Stock";
        }

        static RectTransform HudPanel(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, Color color)
        {
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == anchorMax ? anchorMin : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            AddHudFrame(rect, size);
            return rect;
        }

        static void AddHudFrame(Transform parent, Vector2 size)
        {
            UiBoxOutline.Apply(parent, new Color(0.58f, 0.68f, 0.40f, 0.9f), 2f);
        }

        static void AnchorTopCenter(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0.5f, 1);
            rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 1);
        }

        static void DestroyChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }

        static void DestroyLegacyBuildStatusText(Transform canvas)
        {
            var labels = canvas.GetComponentsInChildren<Text>(true);
            foreach (var label in labels)
            {
                if (label == null || label.name == "Build Status") continue;
                if (label.text.Contains("Ballista") || label.text.Contains("\u30d0\u30ea\u30b9\u30bf"))
                {
                    Object.DestroyImmediate(label.gameObject);
                }
            }
        }

        static Text HudText(Transform parent, string text, int size, Vector2 pos, Vector2 rect)
        {
            return SimpleUi.Label(parent, text, size, pos, rect);
        }

        static Slider HudSlider(Transform parent, Vector2 pos, Vector2 size, Color fillColor)
        {
            var slider = new GameObject("XP Bar").AddComponent<Slider>();
            slider.transform.SetParent(parent, false);
            slider.GetComponent<RectTransform>().anchoredPosition = pos;
            slider.GetComponent<RectTransform>().sizeDelta = size;
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = false;

            var bg = StretchImageChild(slider.transform, "Background", new Color(0.08f, 0.08f, 0.09f, 0.42f), Vector2.zero, Vector2.one);
            var fillArea = new GameObject("Fill Area").AddComponent<RectTransform>();
            fillArea.SetParent(slider.transform, false);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;
            var fill = StretchImageChild(fillArea, "Fill", fillColor, Vector2.zero, Vector2.one);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            slider.targetGraphic = bg;
            slider.fillRect = fill.rectTransform;
            return slider;
        }

        static void SetBuildScenes()
        {
            var entries = new List<EditorBuildSettingsScene>();
            foreach (var sceneName in new[] { SceneNames.Title, SceneNames.Options, SceneNames.Lobby, SceneNames.Upgrades, SceneNames.Game, SceneNames.GameEnd })
            {
                entries.Add(new EditorBuildSettingsScene($"{Scenes}/{sceneName}.unity", true));
            }
            EditorBuildSettings.scenes = entries.ToArray();
        }

        static void CreateSprites()
        {
            ImportGeneratedSprites();
            CreatePaintTileSprite();
            Pixel("Tile", 16, 16, new[] { "................", "..,,,,,,,,,,,,..", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", "..,,,,,,,,,,,,..", "................" }, Palette());
            Pixel("Knight", 16, 16, new[] { "......HHHH......", ".....HhhhhH.....", ".....HhhhhH.....", "......BBBB......", ".....BbbbbB.....", "....BbbbbbbB....", "...SBBBBBBBB....", "..SS..BBBB......", ".SS...B..B......", "......B..B......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette());
            Pixel("Knight", 16, 16, new[] { "......HHHH......", ".....HhhhhH.....", ".....HhhhhH.....", "......BBBB......", ".....BbbbbB.....", "....BbbbbbbB....", "...SBBBBBBBB....", "..SS..BBBB......", ".SS...B..B......", "......B..B......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette(), ResourcesPath + "/Knight.png");
            Pixel("Archer", 16, 16, new[] { "......HHHH......", ".....HhhhhH.....", ".....HhhhhH.....", "......GGGG......", ".....GggggG.....", "....GggggggG....", "...yGggggggG....", "..yy..GGGG......", ".yy...G..G......", "......G..G......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette(), ResourcesPath + "/Archer.png");
            Pixel("Mage", 16, 16, new[] { "......AAAA......", ".....AaaaaA.....", "....AaaaaaaA....", "......hhhh......", ".....OooooO.....", "....OooooooO....", "...YOOOOOOOO....", "..YY..OOOO......", ".YY...O..O......", "......O..O......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette(), ResourcesPath + "/Mage.png");
            Pixel("EnemyBoar", 16, 16, new[] { "................", "................", "....rrrrrr......", "...rRRRRRRr.....", "..rRRRrrRRRr....", ".rRRRoRRoRRr....", ".rRRRRRRRRRr....", "..rRRRRRRRr.....", "...tt....tt.....", "..tt......tt....", "................", "................", "................", "................", "................", "................" }, Palette());
            Pixel("Tower", 24, 24, new[] { ".........CCCCCC.........", "........CccccccC........", "........CccccccC........", ".......CccccccccC.......", ".......CccWWWWccC.......", ".......CccW..WccC.......", ".......CccWWWWccC.......", ".......CccccccccC.......", ".......CccccccccC.......", "......CccccccccccC......", "......CccccccccccC......", "......CcccWWWWcccC......", "......CcccW..WcccC......", "......CcccWWWWcccC......", ".....CccccccccccccC.....", ".....CccccccccccccC.....", "....CccccccccccccccC....", "....CccccccccccccccC....", "...CccccccccccccccccC...", "...CCCCCCCCCCCCCCCCCC...", "....BBBBBBBBBBBBBBBB....", "....BBBBBBBBBBBBBBBB....", ".........................", "........................." }, Palette());
            Pixel("Ballista", 24, 24, new[] { "........................", "........................", "..........kkkk..........", ".........kKKKKk.........", "........kKKKKKKk........", ".......kkKyyyyKkk.......", "......kk..yyyy..kk......", ".....kk....yy....kk.....", "....kk.....yy.....kk....", "...kk......yy......kk...", ".........CCCCCC.........", "........CccccccC........", ".......CccccccccC.......", ".......CCcWWcCC........", ".........cWWc..........", ".........cWWc..........", "........ccWWcc.........", ".......ccWWWWcc........", "......ccWWWWWWcc.......", "......CCCCCCCCCC.......", ".......BBBBBBBB........", ".......BbbbbbbB........", "........................", "........................" }, Palette());
            Pixel("Hammer", 16, 16, new[] { "................", "....kkkkkk......", "...kKKKKKKk.....", "....kkkkkk......", "......WW........", ".....WW.........", ".....WW.........", "....WW..........", "....WW..........", "...WW...........", "...WW...........", "..WW............", "................", "................", "................", "................" }, Palette());
            Pixel("Arrow", 12, 4, new[] { "....yyyyYYYY", "yyyyYYYY>>>>", "yyyyYYYY>>>>", "....yyyyYYYY" }, Palette());
            Pixel("Fireball", 12, 12, new[] { "....oooo....", "...oOOOOo...", "..oOOYYOOo..", ".oOOYYYYOOo.", ".oOYYYYYYOo.", ".oOYYYYYYOo.", ".oOOYYYYOOo.", "..oOOYYOOo..", "...oOOOOo...", "....oooo....", "............", "............" }, Palette());
            Pixel("Orb", 8, 8, new[] { "..AAAA..", ".AaaaaA.", "AaaWWaaA", "AaaWWaaA", ".AaaaaA.", "..AAAA..", "........", "........" }, Palette());
            Pixel("Sparkle", 16, 16, new[] { "................", ".......Y........", ".......Y........", "......YYY.......", ".......Y........", "...Y...Y...Y....", "....YYYYYYY.....", "...YYYYYYYYY....", "....YYYYYYY.....", "...Y...Y...Y....", ".......Y........", "......YYY.......", ".......Y........", ".......Y........", "................", "................" }, Palette());
            Pixel("Slash", 18, 12, new[] { ".............YY...", "..........YYYYY...", ".......YYYYYY.....", ".....YYYYY........", "...YYYY...........", "..YYY.............", ".YY...............", "..................", "..................", "..................", "..................", ".................." }, Palette(), ResourcesPath + "/Slash.png");
        }

        static Dictionary<char, Color32> Palette()
        {
            return new Dictionary<char, Color32>
            {
                ['.'] = new Color32(0, 0, 0, 0), [','] = new Color32(92, 132, 66, 255),
                ['H'] = new Color32(230, 230, 220, 255), ['h'] = new Color32(150, 160, 170, 255),
                ['B'] = new Color32(70, 105, 190, 255), ['b'] = new Color32(45, 70, 135, 255),
                ['S'] = new Color32(210, 215, 225, 255), ['F'] = new Color32(55, 50, 70, 255),
                ['r'] = new Color32(115, 56, 44, 255), ['R'] = new Color32(185, 92, 72, 255),
                ['o'] = new Color32(30, 20, 20, 255), ['t'] = new Color32(52, 34, 28, 255),
                ['C'] = new Color32(110, 110, 120, 255), ['c'] = new Color32(170, 170, 182, 255),
                ['W'] = new Color32(62, 43, 38, 255), ['Y'] = new Color32(255, 232, 89, 255),
                ['y'] = new Color32(178, 132, 52, 255), ['>'] = new Color32(238, 238, 210, 255),
                ['O'] = new Color32(255, 118, 35, 255), ['A'] = new Color32(65, 225, 255, 255),
                ['a'] = new Color32(36, 134, 205, 255), ['g'] = new Color32(30, 95, 40, 255),
                ['G'] = new Color32(54, 150, 58, 255), ['k'] = new Color32(82, 82, 88, 255),
            ['K'] = new Color32(132, 132, 140, 255),
            ['s'] = new Color32(255, 255, 255, 255)
            };
        }

        static void Pixel(string name, int width, int height, string[] rows, Dictionary<char, Color32> palette, string overridePath = null)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            for (int y = 0; y < height; y++)
            {
                var row = rows[Mathf.Min(rows.Length - 1, y)];
                for (int x = 0; x < width; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    texture.SetPixel(x, height - 1 - y, palette.ContainsKey(c) ? palette[c] : palette['.']);
                }
            }
            texture.Apply();
            var path = overridePath ?? $"{Sprites}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void CreatePaintTileSprite()
        {
            var texture = new Texture2D(90, 64, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var color = new Color32(255, 255, 255, 255);
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            var path = $"{GeneratedSprites}/PaintTile.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 128;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}/{name}.png") ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedSprites}/{name}.png") ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{ResourcesPath}/{name}.png");
        }

        static Sprite LoadGeneratedSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedSprites}/{name}.png") ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{ResourcesPath}/Generated/{name}.png");
        }

        static Sprite LoadMapSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedSprites}/{name}.png") ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}/{name}.png") ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{ResourcesPath}/{name}.png");
        }

        static Sprite LoadPixelCharacterSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{ResourcesPath}/{name}.png") ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}/{name}.png") ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedSprites}/{name}.png");
        }

        static Sprite LoadCharacterSprite(string name)
        {
            return LoadWalkFrame(name, "Down", 1) ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{ResourcesPath}/Generated/{name}.png") ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedSprites}/{name}.png") ??
                   LoadPixelCharacterSprite(name);
        }

        static Sprite[] StaticFrames(Sprite sprite)
        {
            return new[] { sprite, sprite, sprite };
        }

        static void ScalePaperVisual(PaperMeshVisual visual, Vector2 size)
        {
            if (visual == null || visual.sprite == null) return;
            var bounds = visual.sprite.bounds.size;
            float x = Mathf.Abs(bounds.x) > 0.001f ? size.x / bounds.x : 1f;
            float y = Mathf.Abs(bounds.y) > 0.001f ? size.y / bounds.y : 1f;
            visual.transform.localScale = new Vector3(x, y, 1f);
        }

        static bool HasGeneratedSprite(string name)
        {
            return File.Exists($"{GeneratedSprites}/{name}.png");
        }

        static void ImportGeneratedSprites()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            if (!AssetDatabase.IsValidFolder(GeneratedSprites)) return;
            foreach (var path in Directory.GetFiles(GeneratedSprites, "*.png"))
            {
                var assetPath = path.Replace("\\", "/");
                ConfigureSpriteImporter(assetPath, GetPixelsPerUnit(assetPath));
            }

            var generatedResources = $"{ResourcesPath}/Generated";
            if (!AssetDatabase.IsValidFolder(generatedResources)) return;
            foreach (var path in Directory.GetFiles(generatedResources, "*.png", SearchOption.AllDirectories))
            {
                var assetPath = path.Replace("\\", "/");
                ConfigureSpriteImporter(assetPath, GetPixelsPerUnit(assetPath));
            }
        }

        static float GetPixelsPerUnit(string assetPath)
        {
            if (assetPath.EndsWith("/Arrow.png")) return 256;
            if (assetPath.EndsWith("/Fireball.png")) return 96;
            if (assetPath.Contains("/Walk/")) return 256;
            if (assetPath.Contains("/Slash_")) return 256;
            return 128;
        }

        static Sprite[] LoadWalkFrames(string character, string direction)
        {
            var frames = new Sprite[3];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = LoadWalkFrame(character, direction, i);
            }

            return frames;
        }

        static Sprite[] LoadWalkFramesOrStatic(string character, string direction, Sprite fallback)
        {
            var frames = LoadWalkFrames(character, direction);
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null) return StaticFrames(fallback);
            }

            return frames;
        }

        static Sprite LoadWalkFrame(string character, string direction, int index)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedSprites}/Walk/{character}/{direction}_{index}.png");
        }

        static void ConfigureSpriteImporter(string assetPath, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static System.Type GetRuntimeType(string typeName)
        {
            var type = System.Type.GetType(typeName + ", Assembly-CSharp");
            if (type == null) throw new System.InvalidOperationException($"Runtime type not found: {typeName}");
            return type;
        }

        static void SetObjectReference(Component component, string propertyName, Object value)
        {
            if (component == null) return;
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetBool(Component component, string propertyName, bool value)
        {
            if (component == null) return;
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVector2(Component component, string propertyName, Vector2 value)
        {
            if (component == null) return;
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(propertyName);
            if (property == null) return;
            property.vector2Value = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureGridMarker(GameObject go, GridObjectType type, GridCellFlags flags, Vector2Int footprint)
        {
            var marker = go.GetComponent<GridObjectMarker>();
            if (marker == null) marker = go.AddComponent<GridObjectMarker>();
            marker.type = type;
            marker.flags = flags;
            marker.footprint = footprint;
        }

        struct PrefabSet
        {
            public PlayerController player;
            public GameObject enemy;
            public GameObject xpOrb;
            public GameObject arrow;
            public GameObject fireball;
            public GameObject ballista;
            public GameObject woodenWall;
            public GameObject woodenGate;
            public GameObject damagePopup;
        }

    }
}
