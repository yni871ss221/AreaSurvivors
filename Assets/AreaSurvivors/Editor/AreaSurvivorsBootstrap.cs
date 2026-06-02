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
        const float TileCellHeight = TileCellWidth * 0.55f;
        const int HorizontalFenceCellLength = 12;
        const int VerticalFenceCellLength = 16;
        const float ObstacleCenterClearance = 5.8f;

        [MenuItem("Area Survivors/Build Initial Project")]
        public static void BuildAll()
        {
            EnsureFolders();
            CreateSprites();
            CreateTilePalette();
            var config = CreateConfig();
            var prefabs = CreatePrefabs();
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

        static void EnsureFolders()
        {
            foreach (var path in new[] { Root, Scenes, Prefabs, Sprites, GeneratedSprites, ResourcesPath, TilePalette, Root + "/Resources/Config", Root + "/Resources/Generated" })
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
            return config;
        }

        static void CreateTilePalette()
        {
            ImportGeneratedSprites();
            foreach (var name in new[] { "Ground", "Paint", "Tree", "Rock", "Pond", "Tower", "Ballista", "FenceHorizontal", "FenceVertical" })
            {
                CreateTileAsset(name);
            }

            var palette = new GameObject("Environment Palette");
            var grid = palette.AddComponent<Grid>();
            grid.cellSize = new Vector3(TileCellWidth, TileCellHeight, 0f);
            var tilemap = CreateTilemap(palette.transform, "Palette Tiles", 0);
            var tiles = new[] { "Ground", "Paint", "Tree", "Rock", "Pond", "Tower", "Ballista", "FenceHorizontal", "FenceVertical" };
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
            tile.sprite = LoadSprite(name == "Ground" ? "Tile" : name == "Paint" ? "PaintTile" : name);
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

        static PrefabSet CreatePrefabs()
        {
            ImportGeneratedSprites();
            var arrow = SavePrefab(CreateProjectile("Arrow", LoadSprite("Arrow"), new Color(0.85f, 0.72f, 0.35f)), Prefabs + "/Arrow.prefab");
            var fireball = SavePrefab(CreateProjectile("Fireball", LoadSprite("Fireball"), new Color(1f, 0.35f, 0.16f)), Prefabs + "/Fireball.prefab");
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
                tower = SavePrefab(CreateTower(), Prefabs + "/Tower.prefab").GetComponent<TowerController>(),
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
            var go = Actor("Player", LoadWalkFrame("Knight", "Down", 1) ?? LoadSprite("Knight"), Color.white, 0.32f);
            var health = go.AddComponent<Health>();
            health.maxHp = 40;
            var animator = go.AddComponent<DirectionalSpriteAnimator>();
            var player = go.AddComponent<PlayerController>();
            player.directionalAnimator = animator;
            player.knightSprite = LoadWalkFrame("Knight", "Down", 1) ?? LoadSprite("Knight");
            player.archerSprite = LoadWalkFrame("Archer", "Down", 1) ?? LoadSprite("Archer");
            player.mageSprite = LoadWalkFrame("Mage", "Down", 1) ?? LoadSprite("Mage");
            player.knightDownFrames = LoadWalkFrames("Knight", "Down");
            player.knightLeftFrames = LoadWalkFrames("Knight", "Left");
            player.knightRightFrames = LoadWalkFrames("Knight", "Right");
            player.knightUpFrames = LoadWalkFrames("Knight", "Up");
            player.archerDownFrames = LoadWalkFrames("Archer", "Down");
            player.archerLeftFrames = LoadWalkFrames("Archer", "Left");
            player.archerRightFrames = LoadWalkFrames("Archer", "Right");
            player.archerUpFrames = LoadWalkFrames("Archer", "Up");
            player.mageDownFrames = LoadWalkFrames("Mage", "Down");
            player.mageLeftFrames = LoadWalkFrames("Mage", "Left");
            player.mageRightFrames = LoadWalkFrames("Mage", "Right");
            player.mageUpFrames = LoadWalkFrames("Mage", "Up");
            var weapon = go.AddComponent<WeaponController>();
            player.weapon = weapon;
            player.hpBar = AddWorldHpBar(go.transform, new Vector3(0, -0.55f, 0));
            weapon.arrowPrefab = arrowPrefab;
            weapon.fireballPrefab = fireballPrefab;
            return go;
        }

        static GameObject CreateBallista(GameObject arrowPrefab)
        {
            var go = new GameObject("BallistaTower");
            GroundShadow(go.transform, new Vector2(1.4f, 0.72f));
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = TileCellWidth;

            var sprite = LoadSprite("Ballista");
            var ghost = MeshChild(go.transform, "Ghost", sprite, new Color(1f, 1f, 1f, 0.22f), 1000);
            var build = MeshChild(go.transform, "Build Fill", sprite, Color.white, 1001);
            var complete = MeshChild(go.transform, "Complete", sprite, Color.white, 1002);
            var hammer = MeshChild(go.transform, "Hammer", LoadSprite("Hammer"), Color.white, 2200);
            hammer.transform.localPosition = new Vector3(0.28f, -0.12f, 0f);
            var sparkle = MeshChild(go.transform, "Completion Sparkle", LoadSprite("Sparkle"), new Color(1f, 1f, 1f, 0f), 2400);
            sparkle.visible = false;

            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.renderers = new[] { ghost.Renderer, build.Renderer, complete.Renderer };

            var ballista = go.AddComponent(GetRuntimeType("AreaSurvivors.BallistaTower"));
            SetObjectReference(ballista, "arrowPrefab", arrowPrefab);
            SetObjectReference(ballista, "ghostRenderer", ghost);
            SetObjectReference(ballista, "buildRenderer", build);
            SetObjectReference(ballista, "completeRenderer", complete);
            SetObjectReference(ballista, "hammerRenderer", hammer);
            SetObjectReference(ballista, "sparkleRenderer", sparkle);
            SetObjectReference(ballista, "buildGauge", AddWorldBuildGauge(go.transform, new Vector3(0f, -0.75f, 0f)));
            return go;
        }

        static GameObject CreateFence(bool vertical)
        {
            var go = new GameObject(vertical ? "DefensiveFenceVertical" : "DefensiveFenceHorizontal");
            GroundShadow(go.transform, vertical ? new Vector2(0.7f, 5.4f) : new Vector2(8.1f, 0.45f));
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            go.AddComponent<Health>();

            var buildTrigger = go.AddComponent<BoxCollider2D>();
            buildTrigger.isTrigger = true;
            buildTrigger.size = vertical
                ? new Vector2(TileCellWidth, TileCellHeight * VerticalFenceCellLength)
                : new Vector2(TileCellWidth * HorizontalFenceCellLength, TileCellHeight);
            buildTrigger.offset = vertical ? new Vector2(0f, TileCellHeight * VerticalFenceCellLength * 0.5f) : Vector2.zero;
            var blocker = go.AddComponent<BoxCollider2D>();
            blocker.size = buildTrigger.size;
            blocker.offset = buildTrigger.offset;

            var sprite = LoadSprite(vertical ? "FenceVertical" : "FenceHorizontal");
            var ghost = MeshChild(go.transform, "Ghost", sprite, new Color(1f, 1f, 1f, 0.22f), 1000);
            var build = MeshChild(go.transform, "Build Fill", sprite, Color.white, 1001);
            var complete = MeshChild(go.transform, "Complete", sprite, Color.white, 1002);
            var hammer = MeshChild(go.transform, "Hammer", LoadSprite("Hammer"), Color.white, 2200);
            hammer.transform.localPosition = vertical ? new Vector3(0.32f, TileCellHeight * VerticalFenceCellLength * 0.5f, 0f) : new Vector3(0.54f, -0.12f, 0f);
            var sparkle = MeshChild(go.transform, "Completion Sparkle", LoadSprite("Sparkle"), new Color(1f, 1f, 1f, 0f), 2400);
            sparkle.transform.localPosition = vertical ? new Vector3(0f, TileCellHeight * VerticalFenceCellLength * 0.5f, 0f) : Vector3.zero;
            sparkle.visible = false;

            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.renderers = new[] { ghost.Renderer, build.Renderer, complete.Renderer };

            var fence = go.AddComponent(GetRuntimeType("AreaSurvivors.DefensiveFence"));
            SetBool(fence, "vertical", vertical);
            SetObjectReference(fence, "blockingCollider", blocker);
            SetObjectReference(fence, "ghostRenderer", ghost);
            SetObjectReference(fence, "buildRenderer", build);
            SetObjectReference(fence, "completeRenderer", complete);
            SetObjectReference(fence, "hammerRenderer", hammer);
            SetObjectReference(fence, "sparkleRenderer", sparkle);
            SetObjectReference(fence, "buildGauge", AddWorldBuildGauge(go.transform, vertical ? new Vector3(0.62f, TileCellHeight * VerticalFenceCellLength * 0.5f, 0f) : new Vector3(TileCellWidth * HorizontalFenceCellLength * 0.5f + 0.2f, -0.08f, 0f)));
            return go;
        }

        static GameObject CreateEnemy()
        {
            var go = Actor("Enemy", LoadWalkFrame("EnemyBoar", "Down", 1) ?? LoadSprite("EnemyBoar"), Color.white, 0.34f);
            go.AddComponent<Health>();
            var animator = go.AddComponent<DirectionalSpriteAnimator>();
            animator.SetFrames(LoadWalkFrames("EnemyBoar", "Down"), LoadWalkFrames("EnemyBoar", "Left"), LoadWalkFrames("EnemyBoar", "Right"), LoadWalkFrames("EnemyBoar", "Up"));
            var enemy = go.AddComponent<EnemyController>();
            enemy.directionalAnimator = animator;
            return go;
        }

        static GameObject CreateTower()
        {
            var go = Actor("Tower", LoadSprite("Tower"), Color.white, 0.85f);
            go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            go.AddComponent<Health>();
            var tower = go.AddComponent<TowerController>();
            tower.hpBar = AddWorldHpBar(go.transform, new Vector3(0, -0.9f, 0), 1.3f);
            return go;
        }

        static GameObject Actor(string name, Sprite sprite, Color color, float colliderRadius)
        {
            var go = new GameObject(name);
            var visual = MeshChild(go.transform, "Paper Visual", sprite, HasGeneratedSprite(name) ? Color.white : color, 1000);
            GroundShadow(go.transform, new Vector2(colliderRadius * 2.2f, colliderRadius * 1.2f));
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

        static Slider AddWorldHpBar(Transform parent, Vector3 localPos, float width = 0.85f)
        {
            var canvas = new GameObject("HP Bar").AddComponent<Canvas>();
            canvas.transform.SetParent(parent, false);
            canvas.transform.localPosition = localPos;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 3000;
            canvas.gameObject.AddComponent<PaperBillboard>();
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 0.14f);
            var slider = canvas.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = false;

            var bg = StretchImageChild(canvas.transform, "Background", new Color(0.12f, 0.04f, 0.04f), Vector2.zero, Vector2.one);
            var fillArea = new GameObject("Fill Area").AddComponent<RectTransform>();
            fillArea.SetParent(canvas.transform, false);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;
            var fill = StretchImageChild(fillArea, "Fill", new Color(0.25f, 0.88f, 0.35f), Vector2.zero, Vector2.one);
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

        static Slider AddWorldBuildGauge(Transform parent, Vector3 localPos)
        {
            var canvas = new GameObject("Build Gauge").AddComponent<Canvas>();
            canvas.transform.SetParent(parent, false);
            canvas.transform.localPosition = localPos;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 3100;
            canvas.gameObject.AddComponent<PaperBillboard>();
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(0.18f, 0.78f);
            var slider = canvas.gameObject.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.direction = Slider.Direction.BottomToTop;
            var bg = StretchImageChild(canvas.transform, "Background", new Color(0.02f, 0.03f, 0.03f, 0.84f), Vector2.zero, Vector2.one);
            var fillArea = new GameObject("Fill Area").AddComponent<RectTransform>();
            fillArea.SetParent(canvas.transform, false);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;
            var fill = StretchImageChild(fillArea, "Fill", new Color(0.35f, 0.72f, 1f, 0.9f), Vector2.zero, Vector2.one);
            fill.rectTransform.pivot = new Vector2(0.5f, 0f);
            slider.targetGraphic = bg;
            slider.fillRect = fill.rectTransform;
            canvas.gameObject.SetActive(false);
            return slider;
        }

        static PaperMeshVisual MeshChild(Transform parent, string name, Sprite sprite, Color color, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var visual = child.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, color, sortingOrder);
            child.AddComponent<PaperBillboard>();
            return visual;
        }

        static PaperMeshVisual GroundShadow(Transform parent, Vector2 scale)
        {
            var child = new GameObject("Ground Shadow");
            child.transform.SetParent(parent, false);
            child.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            child.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var visual = child.AddComponent<PaperMeshVisual>();
            visual.Configure(LoadSprite("Shadow"), new Color(0f, 0f, 0f, 0.2f), -10);
            return visual;
        }

        static GameObject CreateProjectile(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            MeshChild(go.transform, "Paper Visual", sprite, color, 20);
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.12f;
            go.AddComponent<Projectile>();
            return go;
        }

        static GameObject CreateXpOrb()
        {
            var go = new GameObject("ExperienceOrb");
            go.transform.localScale = Vector3.one * 0.34f;
            MeshChild(go.transform, "Paper Visual", LoadSprite("Orb"), HasGeneratedSprite("Orb") ? Color.white : new Color(0.35f, 0.95f, 1f), 15);
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
            camera.orthographic = false;
            camera.fieldOfView = 50f;
            camera.transform.position = new Vector3(0f, -12f, -14f);
            camera.transform.rotation = Quaternion.Euler(-40f, 0f, 0f);
            camera.backgroundColor = new Color(0.19f, 0.31f, 0.19f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<CameraFollow>();

            var environment = new GameObject("Environment Grid");
            var unityGrid = environment.AddComponent<Grid>();
            unityGrid.cellSize = new Vector3(TileCellWidth, TileCellHeight, 0f);
            var groundTilemap = CreateTilemap(environment.transform, "Ground Tilemap", -20);
            var paintTilemap = CreateTilemap(environment.transform, "Paint Tilemap", -19);
            var objectTilemap = CreateTilemap(environment.transform, "Object Tilemap", 1000);
            objectTilemap.tileAnchor = new Vector3(0.5f, 0f, 0f);
            objectTilemap.GetComponent<TilemapRenderer>().enabled = false;
            var grid = environment.AddComponent<TileGrid>();
            grid.tileSprite = LoadSprite("Tile");
            grid.paintSprite = LoadSprite("PaintTile");
            grid.groundTilemap = groundTilemap;
            grid.paintTilemap = paintTilemap;
            grid.objectTilemap = objectTilemap;
            grid.groundTile = LoadTile("Ground");
            grid.paintTile = LoadTile("Paint");
            grid.Build();

            AddObstacles(grid);
            var spawner = new GameObject("Enemy Spawner").AddComponent<EnemySpawner>();
            spawner.enemyPrefab = prefabs.enemy;
            spawner.xpOrbPrefab = prefabs.xpOrb;
            spawner.damagePopupPrefab = prefabs.damagePopup;

            var manager = new GameObject("Game Manager").AddComponent<GameManager>();
            manager.config = config;
            manager.grid = grid;
            manager.playerPrefab = prefabs.player;
            manager.towerPrefab = prefabs.tower;
            manager.spawner = spawner;
            AddBallistas(prefabs.ballista, config, grid);
            AddFences(prefabs.horizontalFence, prefabs.verticalFence, config, grid);
            BuildHud(manager);

            EditorSceneManager.SaveScene(scene, $"{Scenes}/{SceneNames.Game}.unity");
        }

        static void AddBallistas(GameObject ballistaPrefab, GameConfig config, TileGrid grid)
        {
            if (ballistaPrefab == null) return;
            var cells = new[]
            {
                new Vector2Int(6, 8),
                new Vector2Int(6, -8),
                new Vector2Int(-6, 8),
                new Vector2Int(-6, -8)
            };

            foreach (var cell in cells)
            {
                var instance = PrefabUtility.InstantiatePrefab(ballistaPrefab) as GameObject;
                if (instance == null) continue;
                instance.transform.position = CellToWorld(grid, cell);
                var ballista = instance.GetComponent(GetRuntimeType("AreaSurvivors.BallistaTower"));
                if (ballista != null) SetObjectReference(ballista, "config", config);
            }
        }

        static void AddFences(GameObject horizontalPrefab, GameObject verticalPrefab, GameConfig config, TileGrid grid)
        {
            if (horizontalPrefab == null || verticalPrefab == null) return;
            var placements = new[]
            {
                new FencePlacement(new Vector2Int(0, 8), false),
                new FencePlacement(new Vector2Int(0, -8), false),
                new FencePlacement(new Vector2Int(-6, -8), true),
                new FencePlacement(new Vector2Int(6, -8), true)
            };

            foreach (var placement in placements)
            {
                var fencePrefab = placement.vertical ? verticalPrefab : horizontalPrefab;
                var instance = PrefabUtility.InstantiatePrefab(fencePrefab) as GameObject;
                if (instance == null) continue;
                instance.transform.position = CellToWorld(grid, placement.cell);
                var fence = instance.GetComponent(GetRuntimeType("AreaSurvivors.DefensiveFence"));
                if (fence != null) SetObjectReference(fence, "config", config);
            }
        }

        static void AddObstacles(TileGrid grid)
        {
            var specs = new[]
            {
                new ObstacleSpec("Tree", 18, 0.34f, new Vector2(0f, -0.06f)),
                new ObstacleSpec("Rock", 16, 0.42f, Vector2.zero),
                new ObstacleSpec("Pond", 10, 0.82f, Vector2.zero)
            };

            var availableCells = CreateObstacleGridCells();
            Shuffle(availableCells);
            int nextCell = 0;
            foreach (var spec in specs)
            {
                for (int i = 0; i < spec.count; i++)
                {
                    if (nextCell >= availableCells.Count) return;
                    CreateObstacle(grid, spec, availableCells[nextCell++]);
                }
            }
        }

        static List<Vector2Int> CreateObstacleGridCells()
        {
            var cells = new List<Vector2Int>();
            for (int x = -8; x <= 8; x++)
            {
                for (int y = -4; y <= 4; y++)
                {
                    var cell = new Vector2Int(x * 5, y * 8);
                    if (new Vector2(cell.x * TileCellWidth, cell.y * TileCellHeight).magnitude < ObstacleCenterClearance) continue;
                    cells.Add(cell);
                }
            }

            return cells;
        }

        static void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int swapIndex = Random.Range(0, i + 1);
                (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
            }
        }

        static void CreateObstacle(TileGrid grid, ObstacleSpec spec, Vector2Int cell)
        {
            var root = new GameObject(spec.name);
            root.transform.position = CellToWorld(grid, cell);
            root.AddComponent<Obstacle>();
            grid.objectTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), LoadTile(spec.name));
            GroundShadow(root.transform, new Vector2(spec.colliderRadius * 2.4f, spec.colliderRadius * 1.4f));
            var visual = MeshChild(root.transform, "Paper Visual", LoadSprite(spec.name), Color.white, 1000);
            var ySort = root.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.renderers = new[] { visual.Renderer };

            var col = root.AddComponent<CircleCollider2D>();
            col.radius = spec.colliderRadius;
            col.offset = spec.colliderOffset;
        }

        static Vector3 CellToWorld(TileGrid grid, Vector2Int cell)
        {
            return grid.groundTilemap.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
        }

        static void BuildHud(GameManager manager)
        {
            var canvas = new GameObject("HUD").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvas.gameObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280, 720);
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            manager.xpBar = HudSlider(canvas.transform, new Vector2(0, 334), new Vector2(900, 24), new Color(0.25f, 0.55f, 1f));
            manager.timerText = HudText(canvas.transform, "00:00", 26, new Vector2(-90, 296), new Vector2(160, 36));
            manager.killText = HudText(canvas.transform, "撃破 0", 26, new Vector2(105, 296), new Vector2(180, 36));
            manager.levelText = HudText(canvas.transform, "Lv 1", 22, new Vector2(-515, 334), new Vector2(100, 28));

            var panel = new GameObject("Level Up Panel");
            panel.transform.SetParent(canvas.transform, false);
            var image = panel.AddComponent<Image>();
            image.color = new Color(0.06f, 0.07f, 0.08f, 0.92f);
            image.rectTransform.sizeDelta = new Vector2(560, 310);
            SimpleUi.Label(panel.transform, "レベルアップ", 32, new Vector2(0, 105), new Vector2(420, 56));
            manager.upgradeButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                manager.upgradeButtons[i] = SimpleUi.Button(panel.transform, "Upgrade", new Vector2(0, 35 - i * 74), null, new Vector2(420, 54));
            }
            panel.SetActive(false);
            manager.levelUpPanel = panel;
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

            var bg = StretchImageChild(slider.transform, "Background", new Color(0.08f, 0.08f, 0.09f), Vector2.zero, Vector2.one);
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
            Pixel("Tile", 16, 16, new[] { "................", "..,,,,,,,,,,,,..", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", ".,,,,,,,,,,,,,,.", "..,,,,,,,,,,,,..", "................" }, Palette());
            Pixel("Shadow", 16, 8, new[] { "................", "....ssssssss....", "..ssssssssssss..", ".ssssssssssssss.", ".ssssssssssssss.", "..ssssssssssss..", "....ssssssss....", "................" }, Palette());
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
            Pixel("Slash", 18, 12, new[] { ".............YY...", "..........YYYYY...", ".......YYYYYY.....", ".....YYYYY........", "...YYYY...........", "..YYY.............", ".YY...............", "..................", "..................", "..................", "..................", ".................." }, Palette(), ResourcesPath + "/Slash.png");
            Pixel("Tree", 16, 16, new[] { "......gggg......", "....ggGGGGgg....", "...gGGGGGGGGg...", "...gGGGGGGGGg...", "....ggGGGGgg....", "......bbbb......", "......bbbb......", ".....bbbbbb.....", "................", "................", "................", "................", "................", "................", "................", "................" }, Palette());
            Pixel("Rock", 16, 16, new[] { "................", ".....kkkkkk.....", "...kkKKKKKKk....", "..kKKKKKKKKKk...", "..kKKKKKKKKKk...", "...kKKKKKKKk....", "....kkkkkk......", "................", "................", "................", "................", "................", "................", "................", "................", "................" }, Palette());
            Pixel("Pond", 16, 16, new[] { "................", "....aaaaaaaa....", "..aaAAAAAAAAaa..", ".aAAAAAAAAAAAAa.", ".aAAAAAAAAAAAAa.", "..aaAAAAAAAAaa..", "....aaaaaaaa....", "................", "................", "................", "................", "................", "................", "................", "................", "................" }, Palette());
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

        static Sprite LoadSprite(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>($"{GeneratedSprites}/{name}.png") ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{Sprites}/{name}.png") ??
                   AssetDatabase.LoadAssetAtPath<Sprite>($"{ResourcesPath}/{name}.png");
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
            if (assetPath.EndsWith("/Tree.png")) return 256;
            if (assetPath.EndsWith("/Rock.png")) return 256;
            if (assetPath.EndsWith("/Pond.png")) return 256;
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
                importer.spritePivot = new Vector2(0.5f, 0f);
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

        struct PrefabSet
        {
            public PlayerController player;
            public GameObject enemy;
            public TowerController tower;
            public GameObject xpOrb;
            public GameObject arrow;
            public GameObject fireball;
            public GameObject ballista;
            public GameObject horizontalFence;
            public GameObject verticalFence;
            public GameObject damagePopup;
        }

        readonly struct FencePlacement
        {
            public readonly Vector2Int cell;
            public readonly bool vertical;

            public FencePlacement(Vector2Int cell, bool vertical)
            {
                this.cell = cell;
                this.vertical = vertical;
            }
        }

        readonly struct ObstacleSpec
        {
            public readonly string name;
            public readonly int count;
            public readonly float colliderRadius;
            public readonly Vector2 colliderOffset;

            public ObstacleSpec(string name, int count, float colliderRadius, Vector2 colliderOffset)
            {
                this.name = name;
                this.count = count;
                this.colliderRadius = colliderRadius;
                this.colliderOffset = colliderOffset;
            }
        }

    }
}
