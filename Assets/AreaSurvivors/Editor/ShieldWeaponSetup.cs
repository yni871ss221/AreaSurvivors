using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using AreaSurvivors.EditorTools;

namespace AreaSurvivors.Editor
{
    public static class ShieldWeaponSetup
    {
        const string ShieldSpritePath = "Assets/AreaSurvivors/Sprites/Generated/Shield.png";
        const string ShieldPrefabPath = "Assets/AreaSurvivors/Prefabs/Shield.prefab";
        const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Player.prefab";
        const string CatalogPath = "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset";
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const string UpgradeScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";

        [MenuItem("AreaSurvivors/Setup/Apply Shield Weapon")]
        public static void Apply()
        {
            ImportShieldSprite();
            ImportShieldAudio();
            UpdateGeneratedSpriteCatalog();
            CreateShieldPrefab();
            WirePlayerPrefab();
            UpdateGameConfigAssets();
            UpdateGameHudScene();
            AddShieldSkillNode();
            WeaponBookSceneSetup.Apply();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void ImportShieldSprite()
        {
            AssetDatabase.ImportAsset(ShieldSpritePath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(ShieldSpritePath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        static void ImportShieldAudio()
        {
            AssetDatabase.ImportAsset("Assets/AreaSurvivors/Resources/Audio/SFX/shield_hit.mp3", ImportAssetOptions.ForceUpdate);
        }

        static void UpdateGeneratedSpriteCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GeneratedSpriteCatalog>(CatalogPath);
            var shield = AssetDatabase.LoadAssetAtPath<Sprite>(ShieldSpritePath);
            if (catalog == null || shield == null) return;

            var entries = catalog.entries != null
                ? catalog.entries.ToList()
                : new List<GeneratedSpriteCatalog.Entry>();
            int index = entries.FindIndex(entry => entry.name == "Shield");
            var next = new GeneratedSpriteCatalog.Entry { name = "Shield", sprite = shield };
            if (index >= 0) entries[index] = next;
            else entries.Add(next);
            catalog.entries = entries.OrderBy(entry => entry.name).ToArray();
            EditorUtility.SetDirty(catalog);
        }

        static void CreateShieldPrefab()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShieldSpritePath);
            if (sprite == null) return;

            var root = new GameObject("Shield");
            root.transform.localScale = Vector3.one * 0.34f;

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.simulated = true;

            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.42f;

            root.AddComponent<ShieldOrbitShield>();

            var visualRoot = new GameObject("Paper Visual");
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.AddComponent<PaperBillboard>();
            var visual = visualRoot.AddComponent<PaperMeshVisual>();
            visual.Configure(sprite, Color.white, WeaponSortingOrders.Projectile);

            PrefabUtility.SaveAsPrefabAsset(root, ShieldPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void WirePlayerPrefab()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var shieldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPrefabPath);
            if (player == null || shieldPrefab == null) return;

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            var orbit = root.GetComponent<ShieldOrbitController>();
            if (orbit == null) orbit = root.AddComponent<ShieldOrbitController>();
            orbit.shieldPrefab = shieldPrefab;
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

        static void UpdateGameHudScene()
        {
            if (!File.Exists(GameScenePath)) return;
            var previousScenePath = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            GameHudSceneBuilder.RestoreWeaponStatusHud();
            RemoveShieldHudPanel();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != GameScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        static void RemoveShieldHudPanel()
        {
            var panel = GameObject.Find("Shield Weapon Status");
            if (panel != null)
            {
                Object.DestroyImmediate(panel);
            }
        }

        static void AddShieldSkillNode()
        {
            if (!File.Exists(UpgradeScenePath)) return;

            var previousScenePath = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(UpgradeScenePath, OpenSceneMode.Single);
            var existing = Object.FindObjectsOfType<SkillNodeView>(true).FirstOrDefault(node => node.type == UpgradeType.UnlockShield);
            if (existing == null)
            {
                var parentNode = Object.FindObjectsOfType<SkillNodeView>(true).FirstOrDefault(node => node.type == UpgradeType.MovePenaltyReduction);
                var siblingNode = Object.FindObjectsOfType<SkillNodeView>(true).FirstOrDefault(node => node.type == UpgradeType.UnlockFireball);
                var parent = siblingNode != null ? siblingNode.transform.parent : parentNode != null ? parentNode.transform.parent : null;
                if (parent != null && parentNode != null)
                {
                    existing = CreateSkillNode(parent, parentNode, siblingNode);
                }
            }

            if (existing != null)
            {
                existing.type = UpgradeType.UnlockShield;
                existing.prerequisites = new[] { UpgradeType.MovePenaltyReduction };
                existing.title = "シールドアンロック";
                existing.description = "レベルアップ時の候補にシールドが登場するようになります。";
                existing.implemented = true;
                EditorUtility.SetDirty(existing);
                UpdateNodeLabel(existing, "11");
                UpdateNodeIcon(existing, "Shield");
                NormalizeWeaponUnlockRow(existing);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != UpgradeScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        static SkillNodeView CreateSkillNode(Transform parent, SkillNodeView parentNode, SkillNodeView siblingNode)
        {
            var template = siblingNode != null ? siblingNode : parentNode;
            var go = Object.Instantiate(template.gameObject, parent);
            go.name = "11 Shield Unlock";
            var rect = go.GetComponent<RectTransform>();
            var sourceRect = template.GetComponent<RectTransform>();
            if (rect != null && sourceRect != null)
            {
                rect.anchorMin = sourceRect.anchorMin;
                rect.anchorMax = sourceRect.anchorMax;
                rect.pivot = sourceRect.pivot;
                rect.sizeDelta = sourceRect.sizeDelta;
                rect.anchoredPosition = parentNode.GetComponent<RectTransform>().anchoredPosition + new Vector2(102.5f, -70f);
            }

            return go.GetComponent<SkillNodeView>();
        }

        static void UpdateNodeLabel(SkillNodeView node, string label)
        {
            if (node == null) return;
            var text = node.transform.Find("Node Button/Node No")?.GetComponent<Text>() ?? node.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
                EditorUtility.SetDirty(text);
            }
        }

        static void UpdateNodeIcon(SkillNodeView node, string spriteName)
        {
            if (node == null) return;
            var image = node.icon != null ? node.icon : node.transform.Find("Node Button/Icon")?.GetComponent<Image>();
            if (image == null) return;
            var sprite = GeneratedSpriteLoader.Load(spriteName);
            if (sprite == null) return;
            image.sprite = sprite;
            node.icon = image;
            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(node);
        }

        static void NormalizeWeaponUnlockRow(SkillNodeView shieldNode)
        {
            if (shieldNode == null || shieldNode.transform.parent == null) return;

            var nodes = Object.FindObjectsOfType<SkillNodeView>(true);
            var parent = nodes.FirstOrDefault(view => view.type == UpgradeType.MovePenaltyReduction);
            if (parent == null || parent.RectTransform == null) return;

            var arrow = nodes.FirstOrDefault(view => view.type == UpgradeType.UnlockArrow);
            var fireball = nodes.FirstOrDefault(view => view.type == UpgradeType.UnlockFireball);
            var linkRoot = FindSkillLinkRoot(shieldNode.transform.parent);

            SetUnlockNodePosition(parent, arrow, new Vector2(-102.5f, -70f));
            SetUnlockNodePosition(parent, fireball, new Vector2(0f, -70f));
            SetUnlockNodePosition(parent, shieldNode, new Vector2(102.5f, -70f));

            DestroyWeaponUnlockLinks(linkRoot, "MovePenaltyReduction to UnlockArrow");
            DestroyWeaponUnlockLinks(linkRoot, "MovePenaltyReduction to UnlockFireball");
            DestroyWeaponUnlockLinks(linkRoot, "MovePenaltyReduction to UnlockShield");

            CreateWeaponUnlockLink(linkRoot, "MovePenaltyReduction to UnlockArrow", parent, arrow);
            CreateWeaponUnlockLink(linkRoot, "MovePenaltyReduction to UnlockFireball", parent, fireball);
            CreateWeaponUnlockLink(linkRoot, "MovePenaltyReduction to UnlockShield", parent, shieldNode);
        }

        static void SetUnlockNodePosition(SkillNodeView parent, SkillNodeView node, Vector2 offsetFromParent)
        {
            if (parent == null || parent.RectTransform == null || node == null || node.RectTransform == null) return;
            node.RectTransform.anchoredPosition = parent.RectTransform.anchoredPosition + offsetFromParent;
            node.prerequisites = new[] { UpgradeType.MovePenaltyReduction };
            EditorUtility.SetDirty(node.RectTransform);
            EditorUtility.SetDirty(node);
        }

        static void DestroyWeaponUnlockLinks(Transform linkRoot, string baseName)
        {
            DestroyChild(linkRoot, baseName + " A");
            DestroyChild(linkRoot, baseName + " B");
            DestroyChild(linkRoot, baseName + " C");

            foreach (var target in Object.FindObjectsOfType<Transform>(true)
                .Where(transform => transform.name == baseName + " A" || transform.name == baseName + " B" || transform.name == baseName + " C")
                .ToArray())
            {
                Object.DestroyImmediate(target.gameObject);
            }
        }

        static Transform FindSkillLinkRoot(Transform nodeRoot)
        {
            if (nodeRoot == null) return null;

            var direct = nodeRoot.Find("Skill Links");
            if (direct != null) return direct;

            return nodeRoot.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == "Skill Links") ?? nodeRoot;
        }

        static void CreateWeaponUnlockLink(Transform linkRoot, string baseName, SkillNodeView parent, SkillNodeView node)
        {
            if (linkRoot == null || parent == null || parent.RectTransform == null || node == null || node.RectTransform == null) return;

            var from = parent.RectTransform.anchoredPosition;
            var to = node.RectTransform.anchoredPosition;
            if (Mathf.Abs(to.x - from.x) > 0.01f)
            {
                var elbow = new Vector2(to.x, from.y);
                CreateLinkSegment(linkRoot, baseName + " A", from, elbow);
                CreateLinkSegment(linkRoot, baseName + " B", elbow, to);
                return;
            }

            CreateLinkSegment(linkRoot, baseName + " A", from, to);
        }

        static void CreateLinkSegment(Transform parent, string name, Vector2 from, Vector2 to)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.01f) return;
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = new Color(0.30f, 0.36f, 0.34f, 0.75f);
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = from + delta * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, 5f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var segment = image.gameObject.AddComponent<SkillLinkSegment>();
            segment.prerequisite = UpgradeType.MovePenaltyReduction;
            segment.image = image;
        }

        static void DestroyChild(Transform parent, string name)
        {
            var child = parent != null ? parent.Find(name) : null;
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
    }
}
