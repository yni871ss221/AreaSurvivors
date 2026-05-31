using System.Collections.Generic;
using System.IO;
using AreaSurvivors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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

        [MenuItem("Area Survivors/Build Initial Project")]
        public static void BuildAll()
        {
            EnsureFolders();
            CreateSprites();
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
            foreach (var path in new[] { Root, Scenes, Prefabs, Sprites, GeneratedSprites, ResourcesPath, Root + "/Resources/Config", Root + "/Resources/Generated" })
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

        static PrefabSet CreatePrefabs()
        {
            ImportGeneratedSprites();
            var arrow = SavePrefab(CreateProjectile("Arrow", LoadSprite("Arrow"), new Color(0.85f, 0.72f, 0.35f)), Prefabs + "/Arrow.prefab");
            var fireball = SavePrefab(CreateProjectile("Fireball", LoadSprite("Fireball"), new Color(1f, 0.35f, 0.16f)), Prefabs + "/Fireball.prefab");
            var ballista = SavePrefab(CreateBallista(arrow), Prefabs + "/BallistaTower.prefab");
            var set = new PrefabSet
            {
                arrow = arrow,
                fireball = fireball,
                ballista = ballista,
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
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var trigger = go.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.82f;

            var sprite = LoadSprite("Ballista");
            var ghost = SpriteChild(go.transform, "Ghost", sprite, new Color(1f, 1f, 1f, 0.22f), 1000);
            var build = SpriteChild(go.transform, "Build Fill", sprite, Color.white, 1001);
            var complete = SpriteChild(go.transform, "Complete", sprite, Color.white, 1002);
            var hammer = SpriteChild(go.transform, "Hammer", LoadSprite("Hammer"), Color.white, 2200);
            hammer.transform.localPosition = new Vector3(0.28f, -0.12f, 0f);
            var sparkle = SpriteChild(go.transform, "Completion Sparkle", LoadSprite("Sparkle"), new Color(1f, 1f, 1f, 0f), 2400);
            sparkle.enabled = false;

            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.renderers = new[] { ghost, build, complete };

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
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = HasGeneratedSprite(name) ? Color.white : color;
            sr.sortingOrder = 1000;
            var ySort = go.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.renderers = new[] { sr };
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

        static SpriteRenderer SpriteChild(Transform parent, string name, Sprite sprite, Color color, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        static GameObject CreateProjectile(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 20;
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
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite("Orb");
            sr.color = HasGeneratedSprite("Orb") ? Color.white : new Color(0.35f, 0.95f, 1f);
            sr.sortingOrder = 15;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.28f;
            go.AddComponent<ExperienceOrb>();
            return go;
        }

        static GameObject CreateDamagePopup()
        {
            var go = new GameObject("DamagePopup");
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
            camera.orthographicSize = 7f;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.backgroundColor = new Color(0.19f, 0.31f, 0.19f);
            camera.gameObject.AddComponent<AudioListener>();
            camera.gameObject.AddComponent<CameraFollow>();

            var grid = new GameObject("Paint Tile Grid").AddComponent<TileGrid>();
            grid.tileSprite = LoadSprite("Tile");
            grid.paintSprite = LoadSprite("PaintTile");

            AddObstacles();
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
            AddBallistas(prefabs.ballista, config);
            BuildHud(manager);

            EditorSceneManager.SaveScene(scene, $"{Scenes}/{SceneNames.Game}.unity");
        }

        static void AddBallistas(GameObject ballistaPrefab, GameConfig config)
        {
            if (ballistaPrefab == null) return;
            var positions = new[]
            {
                new Vector3(4.2f, 2.7f, 0f),
                new Vector3(4.2f, -4.1f, 0f),
                new Vector3(-4.2f, 2.7f, 0f),
                new Vector3(-4.2f, -4.1f, 0f)
            };

            foreach (var position in positions)
            {
                var instance = PrefabUtility.InstantiatePrefab(ballistaPrefab) as GameObject;
                if (instance == null) continue;
                instance.transform.position = position;
                var ballista = instance.GetComponent(GetRuntimeType("AreaSurvivors.BallistaTower"));
                if (ballista != null) SetObjectReference(ballista, "config", config);
            }
        }

        static void AddObstacles()
        {
            var specs = new[]
            {
                new ObstacleSpec("Tree", 18, 1.45f, 0.34f, new Vector2(0f, 0.8f), new Vector2(0f, -0.06f)),
                new ObstacleSpec("Rock", 16, 1.15f, 0.42f, new Vector2(0f, 0.33f), new Vector2(0f, 0f)),
                new ObstacleSpec("Pond", 10, 1.65f, 0.82f, new Vector2(0f, 0.22f), new Vector2(0f, 0f))
            };

            var placed = new List<PlacedObstacle>();
            foreach (var spec in specs)
            {
                for (int i = 0; i < spec.count; i++)
                {
                    if (TryFindObstaclePosition(spec, placed, out var position))
                    {
                        placed.Add(new PlacedObstacle(position, spec.spacingRadius));
                        CreateObstacle(spec, position);
                    }
                }
            }
        }

        static bool TryFindObstaclePosition(ObstacleSpec spec, List<PlacedObstacle> placed, out Vector3 position)
        {
            for (int attempt = 0; attempt < 120; attempt++)
            {
                position = new Vector3(Random.Range(-29f, 29f), Random.Range(-15f, 15f), 0f);
                if (Vector3.Distance(position, Vector3.zero) < 4.2f) continue;

                bool overlaps = false;
                foreach (var other in placed)
                {
                    if (Vector2.Distance(position, other.position) < spec.spacingRadius + other.radius)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps) return true;
            }

            position = Vector3.zero;
            return false;
        }

        static void CreateObstacle(ObstacleSpec spec, Vector3 position)
        {
            var root = new GameObject(spec.name);
            root.transform.position = position;
            root.AddComponent<Obstacle>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = spec.visualOffset;
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = LoadSprite(spec.name);
            sr.color = HasGeneratedSprite(spec.name) ? Color.white : Color.gray;

            var ySort = root.AddComponent<YSort>();
            ySort.baseOrder = 1000;
            ySort.renderers = new[] { sr };
            ySort.Apply();

            var col = root.AddComponent<CircleCollider2D>();
            col.radius = spec.colliderRadius;
            col.offset = spec.colliderOffset;
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
                ['K'] = new Color32(132, 132, 140, 255)
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

        struct PrefabSet
        {
            public PlayerController player;
            public GameObject enemy;
            public TowerController tower;
            public GameObject xpOrb;
            public GameObject arrow;
            public GameObject fireball;
            public GameObject ballista;
            public GameObject damagePopup;
        }

        readonly struct ObstacleSpec
        {
            public readonly string name;
            public readonly int count;
            public readonly float spacingRadius;
            public readonly float colliderRadius;
            public readonly Vector2 visualOffset;
            public readonly Vector2 colliderOffset;

            public ObstacleSpec(string name, int count, float spacingRadius, float colliderRadius, Vector2 visualOffset, Vector2 colliderOffset)
            {
                this.name = name;
                this.count = count;
                this.spacingRadius = spacingRadius;
                this.colliderRadius = colliderRadius;
                this.visualOffset = visualOffset;
                this.colliderOffset = colliderOffset;
            }
        }

        readonly struct PlacedObstacle
        {
            public readonly Vector2 position;
            public readonly float radius;

            public PlacedObstacle(Vector2 position, float radius)
            {
                this.position = position;
                this.radius = radius;
            }
        }
    }
}
