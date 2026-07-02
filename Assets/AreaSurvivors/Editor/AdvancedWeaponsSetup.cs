using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AreaSurvivors.EditorTools;

namespace AreaSurvivors.Editor
{
    public static class AdvancedWeaponsSetup
    {
        const string CatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Player.prefab";
        const string PrefabFolder = "Assets/AreaSurvivors/Prefabs/Weapons";
        const string SpriteFolder = "Assets/AreaSurvivors/Sprites/Generated";
        const string UpgradeScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";

        static readonly string[] SpriteNames =
        {
            "Flag",
            "BoomerangSword",
            "AuraSword",
            "ArrowRain",
            "Gun",
            "Frost",
            "ThunderBall",
            "AdvancedWeaponArea",
            "FlagAreaEllipse",
            "ArrowRainFrame_0",
            "ArrowRainFrame_1",
            "ArrowRainFrame_2",
            "ArrowRainFrame_3",
            "ArrowRainFrame_4",
            "ArrowRainFrame_5",
            "ArrowRainFrame_6",
            "ArrowRainFrame_7",
            "FrostAreaEllipse",
            "FrostAreaTexture",
            "FrostAreaTextureAlt",
            "BoomerangSwordBlade",
            "GunBullet",
            "AuraSwordSlash"
        };

        [MenuItem("AreaSurvivors/Setup/Apply Advanced Weapons")]
        public static void Apply()
        {
            ImportSprites();
            ImportAudio();
            UpdateGeneratedSpriteCatalog();
            CreatePrefabs();
            WirePlayerPrefab();
            UpdateGameConfigAssets();
            UpdatePlayerSkillTree();
            WeaponBookSceneSetup.Apply();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Advanced weapons were applied.");
        }

        [MenuItem("AreaSurvivors/Setup/Apply Boomerang Projectile")]
        public static void ApplyBoomerangProjectile()
        {
            ImportSprite("BoomerangSwordBlade");
            CreateProjectilePrefab("BoomerangSwordProjectile", "BoomerangSwordBlade", 0.38f, 0.45f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Boomerang projectile was applied.");
        }

        [MenuItem("AreaSurvivors/Setup/ApplyThunderBallProjectile")]
        public static void ApplyThunderBallProjectile()
        {
            ImportSprite("ThunderBall");
            CreateProjectilePrefab("ThunderBallProjectile", "ThunderBall", 0.4f, 0.42f);
            WirePlayerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Thunder ball projectile was applied.");
        }

        [MenuItem("AreaSurvivors/Setup/ApplyArrowRainArea")]
        public static void ApplyArrowRainArea()
        {
            for (int i = 0; i < 8; i++) ImportSprite($"ArrowRainFrame_{i}");
            UpdateGeneratedSpriteCatalog();
            CreateArrowRainAreaPrefab();
            WirePlayerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Arrow rain area was applied.");
        }

        [MenuItem("AreaSurvivors/Setup/ApplyFrostArea")]
        public static void ApplyFrostArea()
        {
            ImportSprite("FrostAreaEllipse");
            ImportSprite("FrostAreaTexture");
            ImportSprite("FrostAreaTextureAlt");
            UpdateGeneratedSpriteCatalog();
            CreateFrostAreaPrefab();
            WirePlayerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Frost area was applied.");
        }

        static void ImportSprites()
        {
            foreach (var name in SpriteNames)
            {
                ImportSprite(name);
            }
        }

        static void ImportSprite(string name)
        {
            string path = $"{SpriteFolder}/{name}.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 64f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static void ImportAudio()
        {
            string[] paths =
            {
                "Assets/AreaSurvivors/Resources/Audio/SFX/boomerang_sword.mp3",
                "Assets/AreaSurvivors/Resources/Audio/SFX/aura_sword.mp3",
                "Assets/AreaSurvivors/Resources/Audio/SFX/arrow_rain.mp3",
                "Assets/AreaSurvivors/Resources/Audio/SFX/gun_shot.mp3",
                "Assets/AreaSurvivors/Resources/Audio/SFX/frost_cast.mp3",
                "Assets/AreaSurvivors/Resources/Audio/SFX/thunder_ball.mp3"
            };
            foreach (var path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        static void UpdateGeneratedSpriteCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GeneratedSpriteCatalog>(CatalogPath);
            if (catalog == null) return;
            var entries = catalog.entries != null
                ? catalog.entries.ToList()
                : new List<GeneratedSpriteCatalog.Entry>();

            foreach (var name in SpriteNames)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");
                if (sprite == null) continue;
                int index = entries.FindIndex(entry => entry.name == name);
                var next = new GeneratedSpriteCatalog.Entry { name = name, sprite = sprite };
                if (index >= 0) entries[index] = next;
                else entries.Add(next);
            }

            catalog.entries = entries.OrderBy(entry => entry.name).ToArray();
            EditorUtility.SetDirty(catalog);
        }

        static void CreatePrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/AreaSurvivors/Prefabs", "Weapons");
            }

            CreateAreaPrefab();
            CreateArrowRainAreaPrefab();
            CreateFrostAreaPrefab();
            CreateProjectilePrefab("BoomerangSwordProjectile", "BoomerangSwordBlade", 0.38f, 0.45f);
            CreateProjectilePrefab("AuraSwordProjectile", "AuraSwordSlash", 0.48f, 0.52f);
            CreateProjectilePrefab("GunBulletProjectile", "GunBullet", 0.24f, 0.24f);
            CreateProjectilePrefab("ThunderBallProjectile", "ThunderBall", 0.4f, 0.42f);
        }

        static void CreateAreaPrefab()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/AdvancedWeaponArea.png");
            if (sprite == null) return;
            var ellipseSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/FlagAreaEllipse.png");
            var root = new GameObject("Advanced Weapon Area");
            root.AddComponent<AdvancedWeaponArea>();
            var visualRoot = new GameObject("Paper Visual");
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.AddComponent<PaperBillboard>().faceCamera = false;
            var visual = visualRoot.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, WeaponSortingOrders.AreaEffect);
            visual.ConfigureEllipseShape(ellipseSprite, 0.08f);
            if (ellipseSprite != null)
            {
                var outlineRoot = new GameObject("Ellipse Range Outline");
                outlineRoot.transform.SetParent(root.transform, false);
                outlineRoot.AddComponent<PaperBillboard>().faceCamera = false;
                var outline = outlineRoot.AddComponent<PaperMeshVisual>();
                outline.Configure(ellipseSprite, new Color(0.5f, 0.78f, 0.68f, 0.45f), WeaponSortingOrders.AreaEffect + 1);
            }
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/AdvancedWeaponArea.prefab");
            Object.DestroyImmediate(root);
        }

        static void CreateArrowRainAreaPrefab()
        {
            var frames = Enumerable.Range(0, 8)
                .Select(index => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/ArrowRainFrame_{index}.png"))
                .Where(sprite => sprite != null)
                .ToArray();
            if (frames.Length == 0) return;

            var root = new GameObject("Arrow Rain Area");
            root.AddComponent<AdvancedWeaponArea>();

            var circleRoot = new GameObject("Circle Visual");
            circleRoot.transform.SetParent(root.transform, false);
            circleRoot.AddComponent<PaperBillboard>().faceCamera = false;
            var fillFilter = circleRoot.AddComponent<MeshFilter>();
            var fillRenderer = circleRoot.AddComponent<MeshRenderer>();
            var outlineRenderer = circleRoot.AddComponent<LineRenderer>();

            var rainVisuals = CreateArrowRainVisuals(root.transform, frames);

            var visual = root.AddComponent<ArrowRainAreaVisual>();
            visual.Initialize(fillFilter, fillRenderer, outlineRenderer, rainVisuals, frames);

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/ArrowRainArea.prefab");
            Object.DestroyImmediate(root);
        }

        static PaperMeshVisual[] CreateArrowRainVisuals(Transform root, Sprite[] frames)
        {
            var offsets = BuildArrowRainOffsets();

            var visuals = new PaperMeshVisual[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var rainRoot = new GameObject($"Arrow Rain Animation {i + 1:00}");
                rainRoot.transform.SetParent(root, false);
                rainRoot.transform.localPosition = new Vector3(offsets[i].x, offsets[i].y, 0f);
                rainRoot.AddComponent<PaperBillboard>();
                var rainVisual = rainRoot.AddComponent<PaperMeshVisual>();
                rainVisual.Configure(frames[i % frames.Length], Color.white, WeaponSortingOrders.Projectile);
                visuals[i] = rainVisual;
            }

            return visuals;
        }

        static void CreateFrostAreaPrefab()
        {
            var frostSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/FrostAreaTexture.png");
            if (frostSprite == null) return;
            var frostAltSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/FrostAreaTextureAlt.png");
            var ellipseSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/FrostAreaEllipse.png");

            var root = new GameObject("Frost Area");
            root.AddComponent<AdvancedWeaponArea>();

            var visualRoot = new GameObject("Frost Area Visual");
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.AddComponent<PaperBillboard>().faceCamera = false;
            var visual = visualRoot.AddComponent<PaperMeshVisual>();
            visual.Configure(frostSprite, new Color(1f, 1f, 1f, 0.78f), WeaponSortingOrders.AreaEffect);
            visual.ConfigureEllipseShape(ellipseSprite, 0f);
            if (frostAltSprite != null)
            {
                var animator = visualRoot.AddComponent<AreaSurvivors.PaperMeshSpriteAnimator>();
                animator.Initialize(visual, new[] { frostSprite, frostAltSprite }, 3f);
            }
            if (ellipseSprite != null)
            {
                var outlineRoot = new GameObject("Ellipse Range Outline");
                outlineRoot.transform.SetParent(root.transform, false);
                outlineRoot.AddComponent<PaperBillboard>().faceCamera = false;
                var outline = outlineRoot.AddComponent<PaperMeshVisual>();
                outline.Configure(ellipseSprite, new Color(0.58f, 0.88f, 0.95f, 0.4f), WeaponSortingOrders.AreaEffect + 1);
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/FrostArea.prefab");
            Object.DestroyImmediate(root);
        }

        static Vector2[] BuildArrowRainOffsets()
        {
            return new[]
            {
                new Vector2(-0.58f, 0.28f),
                new Vector2(0.10f, 0.51f),
                new Vector2(0.54f, 0.12f),
                new Vector2(-0.22f, -0.08f),
                new Vector2(0.34f, -0.34f),
                new Vector2(-0.64f, -0.42f),
                new Vector2(0.02f, -0.68f)
            };
        }

        static void CreateProjectilePrefab(string prefabName, string spriteName, float scale, float colliderRadius)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{spriteName}.png");
            if (sprite == null) return;
            var root = new GameObject(prefabName);
            root.transform.localScale = Vector3.one * scale;
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.simulated = true;
            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = colliderRadius;
            root.AddComponent<AdvancedWeaponProjectile>();
            var visualRoot = new GameObject("Paper Visual");
            visualRoot.transform.SetParent(root.transform, false);
            var visual = visualRoot.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, WeaponSortingOrders.Projectile);
            if (prefabName == "ThunderBallProjectile") AddThunderBallRangeVisual(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{prefabName}.prefab");
            Object.DestroyImmediate(root);
        }

        static void AddThunderBallRangeVisual(Transform root)
        {
            var rangeRoot = new GameObject("Range Visual");
            rangeRoot.transform.SetParent(root, false);
            var fillFilter = rangeRoot.AddComponent<MeshFilter>();
            var fillRenderer = rangeRoot.AddComponent<MeshRenderer>();
            var outlineRenderer = rangeRoot.AddComponent<LineRenderer>();
            var rangeVisual = rangeRoot.AddComponent<ThunderBallRangeVisual>();
            rangeVisual.Initialize(fillFilter, fillRenderer, outlineRenderer);
        }

        static void WirePlayerPrefab()
        {
            if (!File.Exists(PlayerPrefabPath)) return;
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            var runtime = root.GetComponent<AdvancedWeaponRuntime>();
            if (runtime == null) runtime = root.AddComponent<AdvancedWeaponRuntime>();
            runtime.areaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/AdvancedWeaponArea.prefab");
            runtime.arrowRainAreaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/ArrowRainArea.prefab");
            runtime.frostAreaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/FrostArea.prefab");
            runtime.boomerangPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/BoomerangSwordProjectile.prefab");
            runtime.auraSlashPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/AuraSwordProjectile.prefab");
            runtime.bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/GunBulletProjectile.prefab");
            runtime.thunderBallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/ThunderBallProjectile.prefab");
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void UpdateGameConfigAssets()
        {
            var guids = AssetDatabase.FindAssets("t:GameConfig");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<GameConfig>(path);
                if (config == null) continue;
                config.EnsureWeaponLevelDefaults();
                EditorUtility.SetDirty(config);
            }
        }

        static void UpdatePlayerSkillTree()
        {
            if (!File.Exists(UpgradeScenePath)) return;
            string previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(UpgradeScenePath, OpenSceneMode.Single);
            var nodes = Object.FindObjectsOfType<SkillNodeView>(true).ToList();
            var parent = nodes.FirstOrDefault(node => node.type == UpgradeType.MovePenaltyReduction);
            if (parent == null || parent.RectTransform == null) return;
            var template = nodes.FirstOrDefault(node => node.type == UpgradeType.UnlockArrow) ?? parent;
            Transform nodeRoot = template.transform.parent;
            Transform linkRoot = FindSkillLinkRoot(nodeRoot);
            DestroyAdvancedLinks(linkRoot);

            var specs = new[]
            {
                new SkillSpec(9, UpgradeType.UnlockArrow, "弓アンロック", "レベルアップ時の候補に弓が登場するようになります。", "ArrowHudIcon", UpgradeType.MovePenaltyReduction, new Vector2(-180f, -76f)),
                new SkillSpec(10, UpgradeType.UnlockArrowRain, "アローレインアンロック", "レベルアップ時の候補にアローレインが登場するようになります。", "ArrowRain", UpgradeType.UnlockArrow, new Vector2(-180f, -152f)),
                new SkillSpec(11, UpgradeType.UnlockGun, "銃アンロック", "レベルアップ時の候補に銃が登場するようになります。", "Gun", UpgradeType.UnlockArrowRain, new Vector2(-180f, -228f)),
                new SkillSpec(12, UpgradeType.UnlockFireball, "ファイアボールアンロック", "レベルアップ時の候補にファイアボールが登場するようになります。", "FireballHudIcon", UpgradeType.MovePenaltyReduction, new Vector2(-60f, -76f)),
                new SkillSpec(13, UpgradeType.UnlockFrost, "フロストアンロック", "レベルアップ時の候補にフロストが登場するようになります。", "Frost", UpgradeType.UnlockFireball, new Vector2(-60f, -152f)),
                new SkillSpec(14, UpgradeType.UnlockThunderBall, "サンダーボールアンロック", "レベルアップ時の候補にサンダーボールが登場するようになります。", "ThunderBall", UpgradeType.UnlockFrost, new Vector2(-60f, -228f)),
                new SkillSpec(15, UpgradeType.UnlockShield, "シールドアンロック", "レベルアップ時の候補にシールドが登場するようになります。", "Shield", UpgradeType.MovePenaltyReduction, new Vector2(60f, -76f)),
                new SkillSpec(16, UpgradeType.UnlockFlag, "旗アンロック", "レベルアップ時の候補に旗が登場するようになります。", "Flag", UpgradeType.UnlockShield, new Vector2(60f, -152f)),
                new SkillSpec(17, UpgradeType.UnlockBoomerangSword, "ブーメランソードアンロック", "レベルアップ時の候補にブーメランソードが登場するようになります。", "BoomerangSword", UpgradeType.MovePenaltyReduction, new Vector2(180f, -76f)),
                new SkillSpec(18, UpgradeType.UnlockAuraSword, "オーラソードアンロック", "レベルアップ時の候補にオーラソードが登場するようになります。", "AuraSword", UpgradeType.UnlockBoomerangSword, new Vector2(180f, -152f)),
                new SkillSpec(19, UpgradeType.RemoveStartingSlash, "初期スラッシュ削除", "ゲーム開始時にスラッシュを持たず、武器枠を1つ空けた状態にします。", "Slash_0", UpgradeType.UnlockAuraSword, new Vector2(180f, -228f)),
            };

            var byType = Object.FindObjectsOfType<SkillNodeView>(true).ToDictionary(node => node.type, node => node);
            foreach (var spec in specs)
            {
                var node = GetOrCreateNode(byType, nodeRoot, template, spec);
                ApplySkillSpec(node, spec, parent.RectTransform.anchoredPosition);
                byType[spec.type] = node;
            }

            foreach (var spec in specs)
            {
                if (!byType.TryGetValue(spec.prerequisite, out var from) || !byType.TryGetValue(spec.type, out var to)) continue;
                CreateLink(linkRoot, $"Advanced Weapon Link {spec.number:00}", from, to);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != UpgradeScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        static SkillNodeView GetOrCreateNode(Dictionary<UpgradeType, SkillNodeView> byType, Transform parent, SkillNodeView template, SkillSpec spec)
        {
            if (byType.TryGetValue(spec.type, out var existing) && existing != null) return existing;
            var go = Object.Instantiate(template.gameObject, parent);
            go.name = $"{spec.number:00} {spec.type}";
            return go.GetComponent<SkillNodeView>();
        }

        static void ApplySkillSpec(SkillNodeView node, SkillSpec spec, Vector2 parentPosition)
        {
            if (node == null) return;
            node.type = spec.type;
            node.prerequisites = new[] { spec.prerequisite };
            node.title = spec.title;
            node.description = spec.description;
            node.implemented = true;
            if (node.RectTransform != null) node.RectTransform.anchoredPosition = parentPosition + spec.offsetFromParent;
            var no = node.transform.Find("Node Button/Node No")?.GetComponent<Text>() ?? node.GetComponentInChildren<Text>(true);
            if (no != null) no.text = spec.number.ToString();
            var image = node.icon != null ? node.icon : node.transform.Find("Node Button/Icon")?.GetComponent<Image>();
            var sprite = GeneratedSpriteLoader.Load(spec.icon);
            if (image != null && sprite != null)
            {
                image.sprite = sprite;
                node.icon = image;
                EditorUtility.SetDirty(image);
            }

            EditorUtility.SetDirty(node);
        }

        static Transform FindSkillLinkRoot(Transform nodeRoot)
        {
            if (nodeRoot == null) return null;
            return nodeRoot.Find("Skill Links") ?? nodeRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == "Skill Links") ?? nodeRoot;
        }

        static void DestroyAdvancedLinks(Transform linkRoot)
        {
            if (linkRoot == null) return;
            var targets = linkRoot.GetComponentsInChildren<Transform>(true)
                .Where(child => child != linkRoot && (child.name.StartsWith("Advanced Weapon Link") || child.name.StartsWith("MovePenaltyReduction to Unlock")))
                .ToArray();
            foreach (var target in targets) Object.DestroyImmediate(target.gameObject);
        }

        static void CreateLink(Transform parent, string name, SkillNodeView fromNode, SkillNodeView toNode)
        {
            if (parent == null || fromNode == null || toNode == null || fromNode.RectTransform == null || toNode.RectTransform == null) return;
            var from = fromNode.RectTransform.anchoredPosition;
            var to = toNode.RectTransform.anchoredPosition;
            if (Mathf.Abs(to.x - from.x) > 0.01f)
            {
                var elbow = new Vector2(to.x, from.y);
                CreateSegment(parent, name + " A", from, elbow);
                CreateSegment(parent, name + " B", elbow, to);
                return;
            }

            CreateSegment(parent, name + " A", from, to);
        }

        static void CreateSegment(Transform parent, string name, Vector2 from, Vector2 to)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.01f) return;
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = new Color(0.30f, 0.36f, 0.34f, 0.75f);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(delta.magnitude, 4f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            image.transform.SetAsFirstSibling();
        }

        readonly struct SkillSpec
        {
            public readonly int number;
            public readonly UpgradeType type;
            public readonly string title;
            public readonly string description;
            public readonly string icon;
            public readonly UpgradeType prerequisite;
            public readonly Vector2 offsetFromParent;

            public SkillSpec(int number, UpgradeType type, string title, string description, string icon, UpgradeType prerequisite, Vector2 offsetFromParent)
            {
                this.number = number;
                this.type = type;
                this.title = title;
                this.description = description;
                this.icon = icon;
                this.prerequisite = prerequisite;
                this.offsetFromParent = offsetFromParent;
            }
        }
    }
}
