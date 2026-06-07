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
        const string Materials = Root + "/Materials";
        const string Meshes = Root + "/Meshes";
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
            CreateMenuScene(SceneNames.GameOver, typeof(GameOverScreen));
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Area Survivors initial project generated.");
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
            SavePrefab(CreateFence(false), Prefabs + "/DefensiveFenceHorizontal.prefab");
            SavePrefab(CreateFence(true), Prefabs + "/DefensiveFenceVertical.prefab");
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

        [MenuItem("Area Survivors/Rebuild HUD Layout")]
        public static void RebuildHudLayout()
        {
            var manager = Object.FindObjectOfType<GameManager>();
            var buildPlacement = Object.FindObjectOfType<BuildPlacementController>();
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                canvas = new GameObject("HUD").AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            }

            CreateEditableHudWidgets(canvas.transform, buildPlacement);
            if (manager != null) EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Area Survivors HUD layout rebuilt.");
        }

        static void EnsureFolders()
        {
            foreach (var path in new[] { Root, Scenes, Prefabs, Materials, Meshes, Sprites, GeneratedSprites, ResourcesPath, TilePalette, Root + "/Resources/Config", Root + "/Resources/Generated" })
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
            config.playerVisualScale = 1f;
            config.ballistaRange = 9.5f;
            config.ballistaMaxHp = 90;
            config.projectileSpeed = 11.5f;
            config.projectileLifetime = 4.2f;
            config.projectileVisualScale = 1.35f;
            config.enemyBaseSpeed = 0.9f;
            config.enemyVisualScale = 1f;
            config.enemyDamage = 3;
            config.spawnInterval = 1.8f;
            config.enemySpawnRadius = 28f;
            config.difficultyRampSeconds = 55f;
            config.startingBallistaStock = 4;
            config.startingFenceStock = 4;
            EditorUtility.SetDirty(config);
        }

        static void CreateTilePalette()
        {
            ImportGeneratedSprites();
            foreach (var name in new[] { "Ground", "Paint", "Tower", "Ballista", "FenceHorizontal", "FenceVertical" })
            {
                CreateTileAsset(name);
            }

            var palette = new GameObject("Environment Palette");
            var grid = palette.AddComponent<Grid>();
            grid.cellSize = new Vector3(TileCellWidth, TileCellHeight, 0f);
            var tilemap = CreateTilemap(palette.transform, "Palette Tiles", 0);
            var tiles = new[] { "Ground", "Paint", "Tower", "Ballista", "FenceHorizontal", "FenceVertical" };
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
            var horizontalFence = SavePrefab(CreateFence(false), Prefabs + "/DefensiveFenceHorizontal.prefab");
            var verticalFence = SavePrefab(CreateFence(true), Prefabs + "/DefensiveFenceVertical.prefab");
            var set = new PrefabSet
            {
                arrow = arrow,
                fireball = fireball,
                ballista = ballista,
                horizontalFence = horizontalFence,
                verticalFence = verticalFence,
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
            var go = Actor("Player", knightSprite, Color.white, 0.32f, new Vector3(0f, -0.28f, 0.01f));
            var health = go.AddComponent<Health>();
            health.maxHp = 40;
            var animator = go.AddComponent<DirectionalSpriteAnimator>();
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
            GroundShadow(go.transform, new Vector2(1.34f, 0.95f));
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = TileCellWidth * 1.5f;
            var blocker = go.AddComponent<BoxCollider2D>();
            blocker.size = new Vector2(1.28f, 1f);
            blocker.offset = new Vector2(0f, -0.1f);
            blocker.enabled = false;
            go.AddComponent<Health>();

            var ballistaSprite = LoadGeneratedSprite("Ballista") ?? LoadSprite("Ballista");
            var ghost = CreateSpriteVisual(go.transform, "Ghost Image", ballistaSprite, new Vector2(1.34f, 1.65f), new Color(1f, 1f, 1f, 0.34f), 1000);
            var build = CreateSpriteVisual(go.transform, "Build Fill Image", ballistaSprite, new Vector2(1.34f, 1.65f), Color.white, 1001);
            var complete = CreateSpriteVisual(go.transform, "Complete Image", ballistaSprite, new Vector2(1.34f, 1.65f), Color.white, 1002);
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
            SetObjectReference(ballista, "buildGauge", AddWorldBuildGauge(go.transform, new Vector3(0f, -0.82f, 0f), new Vector2(0.88f, 0.09f)));
            return go;
        }

        static GameObject CreateFence(bool vertical)
        {
            var go = new GameObject(vertical ? "DefensiveFenceVertical" : "DefensiveFenceHorizontal");
            if (vertical) go.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            ConfigureGridMarker(go, GridObjectType.Fence, GridCellFlags.BlocksMovement | GridCellFlags.BlocksBuilding | GridCellFlags.Defensive, vertical ? new Vector2Int(1, 2) : new Vector2Int(2, 1));
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            go.AddComponent<Health>();

            var buildTrigger = go.AddComponent<BoxCollider2D>();
            buildTrigger.isTrigger = true;
            buildTrigger.size = vertical
                ? new Vector2(0.56f, 1.55f)
                : new Vector2(1.34f, 0.56f);
            buildTrigger.offset = Vector2.zero;
            var blocker = go.AddComponent<BoxCollider2D>();
            blocker.size = buildTrigger.size;
            blocker.offset = buildTrigger.offset;

            var fenceSprite = LoadGeneratedSprite(vertical ? "FenceVertical" : "FenceHorizontal") ?? LoadSprite(vertical ? "FenceVertical" : "FenceHorizontal");
            var ghost = CreateFenceSpriteVisual(go.transform, "Ghost Image", fenceSprite, vertical, new Color(1f, 1f, 1f, 0.34f), 1000);
            var build = CreateFenceSpriteVisual(go.transform, "Build Fill Image", fenceSprite, vertical, Color.white, 1001);
            var complete = CreateFenceSpriteVisual(go.transform, "Complete Image", fenceSprite, vertical, Color.white, 1002);
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

            var fence = go.AddComponent(GetRuntimeType("AreaSurvivors.DefensiveFence"));
            SetBool(fence, "vertical", vertical);
            SetObjectReference(fence, "blockingCollider", blocker);
            SetObjectReference(fence, "ghostRenderer", ghost);
            SetObjectReference(fence, "buildRenderer", build);
            SetObjectReference(fence, "completeRenderer", complete);
            SetObjectReference(fence, "ghostObject", ghost.gameObject);
            SetObjectReference(fence, "completeObject", complete.gameObject);
            SetObjectReference(fence, "hammerRenderer", hammer);
            SetObjectReference(fence, "sparkleRenderer", sparkle);
            SetObjectReference(fence, "fenceSprite", fenceSprite);
            SetVector2(fence, "spriteVisualSize", vertical ? new Vector2(0.36f, 1.55f) : new Vector2(1.34f, 0.58f));
            SetObjectReference(fence, "buildGauge", AddWorldBuildGauge(go.transform, new Vector3(vertical ? 0.42f : 0f, vertical ? 0f : 0.42f, 0f), vertical ? new Vector2(0.12f, 1.2f) : new Vector2(1.2f, 0.12f)));
            return go;
        }

        static PaperMeshVisual CreateFenceSpriteVisual(Transform parent, string name, Sprite sprite, bool vertical, Color color, int sortingOrder)
        {
            var size = vertical ? new Vector2(0.36f, 1.55f) : new Vector2(1.34f, 0.58f);
            return CreateSpriteVisual(parent, name, sprite, size, color, sortingOrder);
        }

        static PaperMeshVisual CreateSpriteVisual(Transform parent, string name, Sprite sprite, Vector2 size, Color color, int sortingOrder)
        {
            var visual = MeshChild(parent, name, sprite, color, sortingOrder);
            var bounds = sprite != null ? sprite.bounds.size : Vector3.one;
            float x = Mathf.Abs(bounds.x) > 0.001f ? size.x / bounds.x : 1f;
            float y = Mathf.Abs(bounds.y) > 0.001f ? size.y / bounds.y : 1f;
            visual.transform.localScale = new Vector3(x, y, 1f);
            visual.visible = false;
            return visual;
        }

        static GameObject CreateFenceModel(Transform parent, string name, bool vertical, Vector2 footprint, FencePalette palette)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            float length = vertical ? footprint.y : footprint.x;
            float thickness = Mathf.Max(0.18f, (vertical ? footprint.x : footprint.y) * 0.58f);
            const float height = 0.88f;
            const float postWidth = 0.16f;
            const float postDepth = 0.16f;
            const int postCount = 10;
            for (int i = 0; i < postCount; i++)
            {
                float t = postCount == 1 ? 0f : i / (float)(postCount - 1);
                float along = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
                var position = vertical ? new Vector3(0f, along, height * 0.5f) : new Vector3(along, 0f, height * 0.5f);
                var scale = vertical ? new Vector3(postDepth, postWidth, height) : new Vector3(postWidth, postDepth, height);
                Box(root.transform, "Post", position, scale, palette.Post);
                var postEdgeScale = vertical ? new Vector3(0.03f, postWidth * 1.12f, height * 0.72f) : new Vector3(postWidth * 1.12f, 0.03f, height * 0.72f);
                var postEdgeOffset = vertical ? new Vector3(-postDepth * 0.58f, 0f, 0.02f) : new Vector3(0f, -postDepth * 0.58f, 0.02f);
                Cube(root.transform, "Post Edge", position + postEdgeOffset, postEdgeScale, palette.Edge);
                if (i % 2 == 0)
                {
                    var grainOffset = vertical ? new Vector3(postDepth * 0.58f, 0f, 0.06f) : new Vector3(0f, postDepth * 0.58f, 0.06f);
                    var grainScale = vertical ? new Vector3(0.018f, postWidth * 0.72f, height * 0.36f) : new Vector3(postWidth * 0.72f, 0.018f, height * 0.36f);
                    Cube(root.transform, "Post Grain", position + grainOffset, grainScale, palette.Grain);
                }
                Box(root.transform, "Post Cap", position + new Vector3(0f, 0f, height * 0.55f), new Vector3(scale.x * 1.35f, scale.y * 1.35f, 0.12f), palette.Cap);
                var bracePosition = vertical ? new Vector3(0f, along, 0.52f) : new Vector3(along, 0f, 0.52f);
                var braceScale = vertical ? new Vector3(thickness * 1.1f, 0.07f, 0.1f) : new Vector3(0.07f, thickness * 1.1f, 0.1f);
                Box(root.transform, "Post Brace", bracePosition, braceScale, palette.Brace);
                Cube(root.transform, "Nail", bracePosition + new Vector3(0f, 0f, 0.075f), new Vector3(0.055f, 0.055f, 0.035f), palette.Metal);
            }

            for (int i = 0; i < 2; i++)
            {
                float z = i == 0 ? 0.36f : 0.64f;
                float offset = i == 0 ? -thickness * 0.52f : thickness * 0.52f;
                var position = vertical ? new Vector3(offset, 0f, z) : new Vector3(0f, offset, z);
                var scale = vertical ? new Vector3(0.06f, length, 0.1f) : new Vector3(length, 0.06f, 0.1f);
                Box(root.transform, "Rail", position, scale, palette.Rail);
                var edgeScale = vertical ? new Vector3(0.075f, length, 0.025f) : new Vector3(length, 0.075f, 0.025f);
                Cube(root.transform, "Rail Top Edge", position + new Vector3(0f, 0f, 0.064f), edgeScale, palette.Edge);
                Cube(root.transform, "Rail Bottom Edge", position + new Vector3(0f, 0f, -0.064f), edgeScale, palette.Edge);
                for (int g = 0; g < 4; g++)
                {
                    float t = (g + 0.5f) / 4f;
                    float along = Mathf.Lerp(-length * 0.38f, length * 0.38f, t);
                    var grainPosition = vertical ? new Vector3(offset, along, z + 0.075f) : new Vector3(along, offset, z + 0.075f);
                    var grainScale = vertical ? new Vector3(0.082f, length * 0.055f, 0.018f) : new Vector3(length * 0.055f, 0.082f, 0.018f);
                    Cube(root.transform, "Rail Grain", grainPosition, grainScale, palette.Grain);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                float along = Mathf.Lerp(-length * 0.36f, length * 0.36f, i / 3f);
                float tilt = i % 2 == 0 ? 22f : -22f;
                var position = vertical ? new Vector3(0f, along, 0.52f) : new Vector3(along, 0f, 0.52f);
                var scale = vertical ? new Vector3(0.07f, length * 0.13f, 0.1f) : new Vector3(length * 0.13f, 0.07f, 0.1f);
                var rotation = vertical ? Quaternion.Euler(tilt, 0f, 0f) : Quaternion.Euler(0f, -tilt, 0f);
                Box(root.transform, "Diagonal Brace", position, scale, palette.Brace, rotation);
            }

            return root;
        }

        static GameObject Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            return Cube(parent, name, localPosition, localScale, material, Quaternion.identity);
        }

        static GameObject Cube(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, Quaternion localRotation)
        {
            var cube = new GameObject(name);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = localRotation;
            cube.transform.localScale = localScale;
            var filter = cube.AddComponent<MeshFilter>();
            filter.sharedMesh = FenceCubeMesh();
            var renderer = cube.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return cube;
        }

        static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 localScale, FencePartMaterials materials)
        {
            return Box(parent, name, localPosition, localScale, materials, Quaternion.identity);
        }

        static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 localScale, FencePartMaterials materials, Quaternion localRotation)
        {
            var box = new GameObject(name);
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localRotation = localRotation;
            box.transform.localScale = localScale;
            var filter = box.AddComponent<MeshFilter>();
            filter.sharedMesh = FenceShadedBoxMesh();
            var renderer = box.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { materials.Top, materials.Side, materials.Bottom };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return box;
        }

        static Mesh FenceCubeMesh()
        {
            const string path = Meshes + "/FenceCube.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            mesh = new Mesh { name = "FenceCube" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                1, 2, 6, 1, 6, 5,
                2, 3, 7, 2, 7, 6,
                3, 0, 4, 3, 4, 7
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Mesh FenceShadedBoxMesh()
        {
            const string path = Meshes + "/FenceShadedBox.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            mesh = new Mesh { name = "FenceShadedBox" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };
            mesh.subMeshCount = 3;
            mesh.SetTriangles(new[] { 4, 5, 6, 4, 6, 7 }, 0);
            mesh.SetTriangles(new[] { 0, 1, 5, 0, 5, 4, 1, 2, 6, 1, 6, 5, 2, 3, 7, 2, 7, 6, 3, 0, 4, 3, 4, 7 }, 1);
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Mesh TexturedQuadMesh()
        {
            const string path = Meshes + "/TexturedQuad.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;

            mesh = new Mesh { name = "TexturedQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material TexturedMaterial(string name, Sprite sprite, Color color)
        {
            var safeName = name.Replace("/", "_").Replace("\\", "_");
            var path = $"{Materials}/{safeName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Sprites/Default");
            material.mainTexture = sprite != null ? sprite.texture : null;
            material.color = color;
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material FenceMaterial(string name, Color color, bool transparent)
        {
            var path = $"{Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = Shader.Find("Sprites/Default");
            material.color = color;
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        static FencePartMaterials FencePart(string name, Color baseColor, bool transparent)
        {
            return new FencePartMaterials(
                FenceMaterial($"{name} Top", Shade(baseColor, 1.18f), transparent),
                FenceMaterial($"{name} Side", Shade(baseColor, 0.88f), transparent),
                FenceMaterial($"{name} Bottom", Shade(baseColor, 0.64f), transparent));
        }

        static Color Shade(Color color, float multiplier)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                color.a);
        }

        readonly struct FencePartMaterials
        {
            public readonly Material Top;
            public readonly Material Side;
            public readonly Material Bottom;

            public FencePartMaterials(Material top, Material side, Material bottom)
            {
                Top = top;
                Side = side;
                Bottom = bottom;
            }
        }

        readonly struct FencePalette
        {
            public readonly FencePartMaterials Post;
            public readonly FencePartMaterials Rail;
            public readonly FencePartMaterials Brace;
            public readonly FencePartMaterials Cap;
            public readonly Material Edge;
            public readonly Material Grain;
            public readonly Material Metal;

            FencePalette(string prefix, Color post, Color rail, Color brace, Color cap, Color edge, Color grain, Color metal, bool transparent)
            {
                Post = FencePart($"{prefix} Post", post, transparent);
                Rail = FencePart($"{prefix} Rail", rail, transparent);
                Brace = FencePart($"{prefix} Brace", brace, transparent);
                Cap = FencePart($"{prefix} Cap", cap, transparent);
                Edge = FenceMaterial($"{prefix} Edge", edge, transparent);
                Grain = FenceMaterial($"{prefix} Grain", grain, transparent);
                Metal = FenceMaterial($"{prefix} Nail", metal, transparent);
            }

            public static FencePalette Ghost()
            {
                return new FencePalette(
                    "Fence Ghost",
                    new Color(0.72f, 0.58f, 0.28f, 0.32f),
                    new Color(0.92f, 0.70f, 0.34f, 0.32f),
                    new Color(0.62f, 0.45f, 0.22f, 0.32f),
                    new Color(1f, 0.82f, 0.42f, 0.34f),
                    new Color(0.34f, 0.25f, 0.12f, 0.28f),
                    new Color(0.25f, 0.18f, 0.09f, 0.26f),
                    new Color(0.16f, 0.13f, 0.10f, 0.26f),
                    true);
            }

            public static FencePalette Build()
            {
                return new FencePalette(
                    "Fence Build",
                    new Color(0.78f, 0.48f, 0.20f, 1f),
                    new Color(0.92f, 0.62f, 0.28f, 1f),
                    new Color(0.62f, 0.35f, 0.16f, 1f),
                    new Color(1.0f, 0.74f, 0.36f, 1f),
                    new Color(0.36f, 0.20f, 0.10f, 1f),
                    new Color(0.46f, 0.26f, 0.12f, 1f),
                    new Color(0.13f, 0.11f, 0.09f, 1f),
                    false);
            }

            public static FencePalette Complete()
            {
                return new FencePalette(
                    "Fence Wood",
                    new Color(0.58f, 0.34f, 0.15f, 1f),
                    new Color(0.72f, 0.43f, 0.18f, 1f),
                    new Color(0.43f, 0.25f, 0.12f, 1f),
                    new Color(0.83f, 0.55f, 0.25f, 1f),
                    new Color(0.24f, 0.13f, 0.07f, 1f),
                    new Color(0.34f, 0.19f, 0.09f, 1f),
                    new Color(0.08f, 0.07f, 0.06f, 1f),
                    false);
            }
        }

        static GameObject CreateEnemy()
        {
            var enemySprite = LoadCharacterSprite("EnemyBoar");
            var go = Actor("Enemy", enemySprite, Color.white, 0.34f);
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
            var visual = CreateTexturedSpriteModel(go.transform, "Textured Model", "Tower", 2.05f, 0.24f, 2.9f, Color.white, 1000);
            GroundShadow(go.transform, new Vector2(2.05f, 1.35f));
            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.sortPivotOffsetY = -1.2f;
            ySort.renderers = visual.GetComponentsInChildren<Renderer>(true);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.bodyType = RigidbodyType2D.Static;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 1.05f;
            go.AddComponent<Health>();
            var tower = go.AddComponent<TowerController>();
            tower.hpBar = AddWorldHpBar(go.transform, new Vector3(0, -0.82f, 0), 0.9f);
            return go;
        }

        static GameObject CreateTexturedTowerModel(Transform parent, string name, string spritePrefix, float width, float depth, float height, Color color, int sortingOrder)
        {
            return CreateTexturedSpriteModel(parent, name, $"{spritePrefix}Front", width, depth, height, color, sortingOrder);
        }

        static GameObject CreateTexturedSpriteModel(Transform parent, string name, string spriteName, float width, float depth, float height, Color color, int sortingOrder)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var front = LoadGeneratedSprite(spriteName) ?? LoadSprite(spriteName);
            var material = TexturedMaterial($"{name} {spriteName} Front", front, color);
            var shadowColor = new Color(0f, 0f, 0f, Mathf.Clamp01(color.a * 0.24f));
            var shadowMaterial = TexturedMaterial($"{name} {spriteName} Thin Shadow", front, shadowColor);
            float z = height * 0.5f;
            AddTexturedPanel(root.transform, "Thin Shadow", shadowMaterial, new Vector3(width * 0.06f, depth * 0.32f, z * 0.98f), new Vector2(width * 1.03f, height * 0.98f), Quaternion.identity, sortingOrder - 1, true);
            AddTexturedPanel(root.transform, "Front", material, new Vector3(0f, -depth * 0.18f, z), new Vector2(width, height), Quaternion.identity, sortingOrder + 3, true);
            return root;
        }

        static GameObject AddTexturedPanel(Transform parent, string name, Material material, Vector3 localPosition, Vector2 size, Quaternion localRotation, int sortingOrder, bool billboard = false)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = localPosition;
            panel.transform.localRotation = localRotation;
            panel.transform.localScale = new Vector3(size.x, size.y, 1f);
            var filter = panel.AddComponent<MeshFilter>();
            filter.sharedMesh = TexturedQuadMesh();
            var renderer = panel.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (billboard) panel.AddComponent<PaperBillboard>();
            return panel;
        }

        static GameObject Actor(string name, Sprite sprite, Color color, float colliderRadius)
        {
            return Actor(name, sprite, color, colliderRadius, new Vector3(0f, 0f, 0.01f));
        }

        static GameObject Actor(string name, Sprite sprite, Color color, float colliderRadius, Vector3 shadowLocalPosition)
        {
            var go = new GameObject(name);
            var visual = MeshChild(go.transform, "Paper Visual", sprite, HasGeneratedSprite(name) ? Color.white : color, 1000);
            ScalePaperVisual(visual, Vector2.one * TileCellWidth);
            GroundShadow(go.transform, new Vector2(colliderRadius * 2.2f, colliderRadius * 1.2f), shadowLocalPosition);
            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.renderers = new[] { visual.Renderer };
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = colliderRadius;
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

        static Slider AddWorldBuildGauge(Transform parent, Vector3 localPos, Vector2 size)
        {
            var canvas = new GameObject("Build Gauge").AddComponent<Canvas>();
            canvas.transform.SetParent(parent, false);
            canvas.transform.localPosition = localPos;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 3100;
            canvas.gameObject.AddComponent<PaperBillboard>();
            canvas.GetComponent<RectTransform>().sizeDelta = size;
            var slider = canvas.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.direction = size.x >= size.y ? Slider.Direction.LeftToRight : Slider.Direction.BottomToTop;
            var bg = StretchImageChild(canvas.transform, "Background", new Color(0.02f, 0.03f, 0.03f, 0.72f), Vector2.zero, Vector2.one);
            var fillArea = new GameObject("Fill Area").AddComponent<RectTransform>();
            fillArea.SetParent(canvas.transform, false);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;
            var fill = StretchImageChild(fillArea, "Fill", new Color(0.35f, 0.78f, 1f, 0.92f), Vector2.zero, Vector2.one);
            fill.rectTransform.pivot = size.x >= size.y ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0f);
            slider.targetGraphic = bg;
            slider.fillRect = fill.rectTransform;
            canvas.gameObject.SetActive(false);
            return slider;
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

        static PaperMeshVisual GroundShadow(Transform parent, Vector2 scale)
        {
            return GroundShadow(parent, scale, new Vector3(0f, 0f, 0.01f));
        }

        static PaperMeshVisual GroundShadow(Transform parent, Vector2 scale, Vector3 localPosition)
        {
            var child = new GameObject("Ground Shadow");
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var visual = child.AddComponent<PaperMeshVisual>();
            visual.Configure(LoadSprite("Shadow"), new Color(0f, 0f, 0f, 0.2f), -10);
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
            var outlines = new TextMesh[4];
            var offsets = new[]
            {
                new Vector3(0.025f, 0f, 0f),
                new Vector3(-0.025f, 0f, 0f),
                new Vector3(0f, 0.025f, 0f),
                new Vector3(0f, -0.025f, 0f)
            };
            for (int i = 0; i < outlines.Length; i++)
            {
                outlines[i] = AddDamageText(go.transform, "Outline", font, Color.black, 3999);
                outlines[i].transform.localPosition = offsets[i];
            }
            var text = AddDamageText(go.transform, "Text", font, Color.white, 4000);
            var popup = go.AddComponent<DamagePopup>();
            popup.text = text;
            popup.outlines = outlines;
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
            camera.transform.position = config.cameraOffset;
            camera.transform.rotation = Quaternion.Euler(config.cameraPitch, 0f, 0f);
            camera.backgroundColor = new Color(0.19f, 0.31f, 0.19f);
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
            grid.Build();

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
            buildPlacement.horizontalFencePrefab = prefabs.horizontalFence;
            buildPlacement.verticalFencePrefab = prefabs.verticalFence;
            buildPlacement.ballistaPreviewSprite = LoadGeneratedSprite("Ballista") ?? LoadSprite("Ballista");
            buildPlacement.horizontalFencePreviewSprite = LoadGeneratedSprite("FenceHorizontal") ?? LoadSprite("FenceHorizontal");
            buildPlacement.verticalFencePreviewSprite = LoadGeneratedSprite("FenceVertical") ?? LoadSprite("FenceVertical");
            buildPlacement.ballistaTile = LoadTile("Ballista");
            buildPlacement.horizontalFenceTile = LoadTile("FenceHorizontal");
            buildPlacement.verticalFenceTile = LoadTile("FenceVertical");
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
            manager.killText = HudText(canvas.transform, "撃破 0", 22, new Vector2(92, 304), new Vector2(160, 30));
            manager.levelText = HudText(canvas.transform, "Lv 1", 20, new Vector2(-548, 334), new Vector2(92, 26));
            CreateEditableHudWidgets(canvas.transform, buildPlacement);

            var panel = new GameObject("Level Up Panel");
            panel.transform.SetParent(canvas.transform, false);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.06f, 0.07f, 0.08f, 0.92f);
            image.rectTransform.sizeDelta = new Vector2(560, 310);
            SimpleUi.Panel(panel.transform, "Level Up Frame", Vector2.zero, new Vector2(560, 310), new Color(0.06f, 0.07f, 0.08f, 0.16f));
            SimpleUi.Label(panel.transform, "レベルアップ", 32, new Vector2(0, 105), new Vector2(420, 56));
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
            CreateEditableBuildSlot(construction, "Build Slot 2", "2", LoadGeneratedSprite("FenceHorizontal"), new Vector2(112, 48), new Vector2(46, 44));
            CreateEditableBuildSlot(construction, "Build Slot 3", "3", LoadGeneratedSprite("FenceVertical"), new Vector2(182, 48), new Vector2(30, 48));
            var status = HudText(construction, "1 バリスタ x4", 14, new Vector2(238, 48), new Vector2(64, 58));
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
                if (label.text.Contains("Ballista") || label.text.Contains("バリスタ"))
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
            foreach (var sceneName in new[] { SceneNames.Title, SceneNames.Options, SceneNames.Lobby, SceneNames.Upgrades, SceneNames.Game, SceneNames.GameOver })
            {
                entries.Add(new EditorBuildSettingsScene($"{Scenes}/{sceneName}.unity", true));
            }
            EditorBuildSettings.scenes = entries.ToArray();
        }

        static void CreateSprites()
        {
            ImportGeneratedSprites();
            ConfigureSpriteImporter($"{Sprites}/FenceTwentyHorizontal.png", 128);
            ConfigureSpriteImporter($"{Sprites}/FenceTwentyVertical.png", 128);
            CreatePaintTileSprite();
            Pixel("Tile", 16, 16, new[] { "................", "..,,,,,,,,,,,,..", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", "..,,,,,,,,,,,,..", "................" }, Palette());
            Pixel("Shadow", 16, 8, new[] { "................", "....ssssssss....", "..ssssssssssss..", ".ssssssssssssss.", ".ssssssssssssss.", "..ssssssssssss..", "....ssssssss....", "................" }, Palette());
            Pixel("Knight", 16, 16, new[] { "......HHHH......", ".....HhhhhH.....", ".....HhhhhH.....", "......BBBB......", ".....BbbbbB.....", "....BbbbbbbB....", "...SBBBBBBBB....", "..SS..BBBB......", ".SS...B..B......", "......B..B......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette());
            Pixel("Knight", 16, 16, new[] { "......HHHH......", ".....HhhhhH.....", ".....HhhhhH.....", "......BBBB......", ".....BbbbbB.....", "....BbbbbbbB....", "...SBBBBBBBB....", "..SS..BBBB......", ".SS...B..B......", "......B..B......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette(), ResourcesPath + "/Knight.png");
            Pixel("Archer", 16, 16, new[] { "......HHHH......", ".....HhhhhH.....", ".....HhhhhH.....", "......GGGG......", ".....GggggG.....", "....GggggggG....", "...yGggggggG....", "..yy..GGGG......", ".yy...G..G......", "......G..G......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette(), ResourcesPath + "/Archer.png");
            Pixel("Mage", 16, 16, new[] { "......AAAA......", ".....AaaaaA.....", "....AaaaaaaA....", "......hhhh......", ".....OooooO.....", "....OooooooO....", "...YOOOOOOOO....", "..YY..OOOO......", ".YY...O..O......", "......O..O......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette(), ResourcesPath + "/Mage.png");
            Pixel("EnemyBoar", 16, 16, new[] { "................", "................", "....rrrrrr......", "...rRRRRRRr.....", "..rRRRrrRRRr....", ".rRRRoRRoRRr....", ".rRRRRRRRRRr....", "..rRRRRRRRr.....", "...tt....tt.....", "..tt......tt....", "................", "................", "................", "................", "................", "................" }, Palette());
            Pixel("Tower", 24, 24, new[] { ".........CCCCCC.........", "........CccccccC........", "........CccccccC........", ".......CccccccccC.......", ".......CccWWWWccC.......", ".......CccW..WccC.......", ".......CccWWWWccC.......", ".......CccccccccC.......", ".......CccccccccC.......", "......CccccccccccC......", "......CccccccccccC......", "......CcccWWWWcccC......", "......CcccW..WcccC......", "......CcccWWWWcccC......", ".....CccccccccccccC.....", ".....CccccccccccccC.....", "....CccccccccccccccC....", "....CccccccccccccccC....", "...CccccccccccccccccC...", "...CCCCCCCCCCCCCCCCCC...", "....BBBBBBBBBBBBBBBB....", "....BBBBBBBBBBBBBBBB....", ".........................", "........................." }, Palette());
            Pixel("Ballista", 24, 24, new[] { "........................", "........................", "..........kkkk..........", ".........kKKKKk.........", "........kKKKKKKk........", ".......kkKyyyyKkk.......", "......kk..yyyy..kk......", ".....kk....yy....kk.....", "....kk.....yy.....kk....", "...kk......yy......kk...", ".........CCCCCC.........", "........CccccccC........", ".......CccccccccC.......", ".......CCcWWcCC........", ".........cWWc..........", ".........cWWc..........", "........ccWWcc.........", ".......ccWWWWcc........", "......ccWWWWWWcc.......", "......CCCCCCCCCC.......", ".......BBBBBBBB........", ".......BbbbbbbB........", "........................", "........................" }, Palette());
            Pixel("Tower3DFront", 24, 28, new[] { "........................", ".........CCCCCC.........", "........CccccccC........", ".......CccccccccC.......", "......CccWWWWccC........", "......CccW..WccC........", "......CccWWWWccC........", "......CccccccccC........", ".....CccccccccccC.......", ".....CccccccccccC.......", ".....CccWWWWcccC........", ".....CccW..WcccC........", ".....CccWWWWcccC........", "....CccccccccccccC......", "....CccccccccccccC......", "...CccccccccccccccC.....", "...CccccccccccccccC.....", "..CccccccccccccccccC....", "..CCCCCCCCCCCCCCCCCC....", "...BBBBBBBBBBBBBBBB.....", "...BbbbbbbbbbbbbbbB.....", "....BbbbbbbbbbbbbB......", "........................", "........................", "........................", "........................", "........................", "........................" }, Palette());
            Pixel("Ballista3DFront", 28, 24, new[] { "............................", "............................", "...........kkkkkk...........", ".........kkKKKKKKkk.........", ".......kkKKyyyyKKkk.........", ".....kkKK..yyyy..KKkk.......", "...kkKK....yyyy....KKkk.....", "..kkK......yyyy......Kkk....", "...........yyyy.............", ".........CCCCCCCC...........", "........CccccccccC..........", ".......CCcWWWWcCC..........", ".........cWWWWc............", ".........cWWWWc............", "........ccWWWWcc...........", ".......ccWWWWWWcc..........", "......CCCCCCCCCCCC.........", "......BbbbbbbbbbbB.........", ".......Bbbbbbbbbb..........", "............................", "............................", "............................", "............................", "............................" }, Palette());
            Pixel("Hammer", 16, 16, new[] { "................", "....kkkkkk......", "...kKKKKKKk.....", "....kkkkkk......", "......WW........", ".....WW.........", ".....WW.........", "....WW..........", "....WW..........", "...WW...........", "...WW...........", "..WW............", "................", "................", "................", "................" }, Palette());
            Pixel("Arrow", 12, 4, new[] { "....yyyyYYYY", "yyyyYYYY>>>>", "yyyyYYYY>>>>", "....yyyyYYYY" }, Palette());
            Pixel("Fireball", 12, 12, new[] { "....oooo....", "...oOOOOo...", "..oOOYYOOo..", ".oOOYYYYOOo.", ".oOYYYYYYOo.", ".oOYYYYYYOo.", ".oOOYYYYOOo.", "..oOOYYOOo..", "...oOOOOo...", "....oooo....", "............", "............" }, Palette());
            Pixel("Orb", 8, 8, new[] { "..AAAA..", ".AaaaaA.", "AaaWWaaA", "AaaWWaaA", ".AaaaaA.", "..AAAA..", "........", "........" }, Palette());
            Pixel("Sparkle", 16, 16, new[] { "................", ".......Y........", ".......Y........", "......YYY.......", ".......Y........", "...Y...Y...Y....", "....YYYYYYY.....", "...YYYYYYYYY....", "....YYYYYYY.....", "...Y...Y...Y....", ".......Y........", "......YYY.......", ".......Y........", ".......Y........", "................", "................" }, Palette());
            Pixel("FenceHorizontal", 32, 8, new[] { "................................", "..WWWWWWWWWWWWWWWWWWWWWWWWWW..", ".WccccWccccWccccWccccWccccWcW.", ".WbbbbWbbbbWbbbbWbbbbWbbbbWbW.", ".WccccWccccWccccWccccWccccWcW.", "..WWWWWWWWWWWWWWWWWWWWWWWWWW..", "................................", "................................" }, Palette());
            Pixel("FenceVertical", 8, 32, new[] { "........", "..WWWW..", ".WcccbW.", ".WcccbW.", ".WbbbbW.", ".WcccbW.", ".WcccbW.", "..WWWW..", "..WWWW..", ".WcccbW.", ".WcccbW.", ".WbbbbW.", ".WcccbW.", ".WcccbW.", "..WWWW..", "..WWWW..", ".WcccbW.", ".WcccbW.", ".WbbbbW.", ".WcccbW.", ".WcccbW.", "..WWWW..", "..WWWW..", ".WcccbW.", ".WcccbW.", ".WbbbbW.", ".WcccbW.", ".WcccbW.", "..WWWW..", "........", "........", "........" }, Palette());
            Pixel("Knight", 16, 16, new[] { "......HHHH......", ".....HhhhhH.....", ".....HhhhhH.....", "......BBBB......", ".....BbbbbB.....", "....BbbbbbbB....", "...SBBBBBBBB....", "..SS..BBBB......", ".SS...B..B......", "......B..B......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette(), ResourcesPath + "/Generated/Knight.png");
            Pixel("Archer", 16, 16, new[] { "......HHHH......", ".....HhhhhH.....", ".....HhhhhH.....", "......GGGG......", ".....GggggG.....", "....GggggggG....", "...yGggggggG....", "..yy..GGGG......", ".yy...G..G......", "......G..G......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette(), ResourcesPath + "/Generated/Archer.png");
            Pixel("Mage", 16, 16, new[] { "......AAAA......", ".....AaaaaA.....", "....AaaaaaaA....", "......hhhh......", ".....OooooO.....", "....OooooooO....", "...YOOOOOOOO....", "..YY..OOOO......", ".YY...O..O......", "......O..O......", ".....FF..FF.....", "....FF....FF....", "................", "................", "................", "................" }, Palette(), ResourcesPath + "/Generated/Mage.png");
            Pixel("Tower", 24, 24, new[] { ".........CCCCCC.........", "........CccccccC........", "........CccccccC........", ".......CccccccccC.......", ".......CccWWWWccC.......", ".......CccW..WccC.......", ".......CccWWWWccC.......", ".......CccccccccC.......", ".......CccccccccC.......", "......CccccccccccC......", "......CccccccccccC......", "......CcccWWWWcccC......", "......CcccW..WcccC......", "......CcccWWWWcccC......", ".....CccccccccccccC.....", ".....CccccccccccccC.....", "....CccccccccccccccC....", "....CccccccccccccccC....", "...CccccccccccccccccC...", "...CCCCCCCCCCCCCCCCCC...", "....BBBBBBBBBBBBBBBB....", "....BBBBBBBBBBBBBBBB....", ".........................", "........................." }, Palette(), ResourcesPath + "/Generated/Tower.png");
            Pixel("Arrow", 12, 4, new[] { "....yyyyYYYY", "yyyyYYYY>>>>", "yyyyYYYY>>>>", "....yyyyYYYY" }, Palette(), ResourcesPath + "/Generated/Arrow.png");
            Pixel("Orb", 8, 8, new[] { "..AAAA..", ".AaaaaA.", "AaaWWaaA", "AaaWWaaA", ".AaaaaA.", "..AAAA..", "........", "........" }, Palette(), ResourcesPath + "/Generated/Orb.png");
            Pixel("Sparkle", 16, 16, new[] { "................", ".......Y........", ".......Y........", "......YYY.......", ".......Y........", "...Y...Y...Y....", "....YYYYYYY.....", "...YYYYYYYYY....", "....YYYYYYY.....", "...Y...Y...Y....", ".......Y........", "......YYY.......", ".......Y........", ".......Y........", "................", "................" }, Palette(), ResourcesPath + "/Generated/Sparkle.png");
            Pixel("Slash_0", 18, 12, new[] { ".............YY...", "..........YYYYY...", ".......YYYYYY.....", ".....YYYYY........", "...YYYY...........", "..YYY.............", ".YY...............", "..................", "..................", "..................", "..................", ".................." }, Palette(), ResourcesPath + "/Generated/Slash_0.png");
            Pixel("Slash_1", 18, 12, new[] { "...............Y..", "............YYYY..", ".........YYYYYY...", "......YYYYYY......", "....YYYYY.........", "..YYYY............", ".YY...............", "..................", "..................", "..................", "..................", ".................." }, Palette(), ResourcesPath + "/Generated/Slash_1.png");
            Pixel("Slash_2", 18, 12, new[] { "..................", "...............Y..", "............YYYY..", ".........YYYYY....", "......YYYYY.......", "...YYYYY..........", ".YYY..............", ".Y................", "..................", "..................", "..................", ".................." }, Palette(), ResourcesPath + "/Generated/Slash_2.png");
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
            if (assetPath.EndsWith("/FenceHorizontal.png")) return 256;
            if (assetPath.EndsWith("/FenceVertical.png")) return 256;
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
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{ResourcesPath}/Generated/Walk/{character}/{direction}_{index}.png");
        }

        static void ConfigureSpriteImporter(string assetPath, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            if (assetPath.EndsWith("/FenceHorizontal.png"))
            {
                importer.spritePivot = new Vector2(0.5f, 0.5f);
            }
            else if (assetPath.EndsWith("/FenceVertical.png"))
            {
                importer.spritePivot = new Vector2(0.5f, 0.5f);
            }
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
            public GameObject horizontalFence;
            public GameObject verticalFence;
            public GameObject damagePopup;
        }

    }
}
