using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class CenterTowerSkillTreeSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";
        static readonly Color LinkInactiveColor = new Color(0.30f, 0.36f, 0.34f, 0.75f);

        [MenuItem("AreaSurvivors/Setup/Apply Center Tower Skill Additions")]
        public static void Apply()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyCenterTowerNodes(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != ScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        static void ApplyCenterTowerNodes(Scene scene)
        {
            var nodes = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SkillNodeView>(true))
                .ToArray();
            var towerUpgrade = nodes.FirstOrDefault(node => node.type == UpgradeType.UnlockTowerUpgrade);
            if (towerUpgrade == null || towerUpgrade.RectTransform == null)
            {
                Debug.LogError("UnlockTowerUpgrade node was not found. Center tower skill additions were skipped.");
                return;
            }

            var parent = towerUpgrade.transform.parent;
            var linkRoot = FindSkillLinkRoot(parent);
            var towerUpgradePosition = towerUpgrade.RectTransform.anchoredPosition;
            var towerUpgradePrerequisites = towerUpgrade.prerequisites != null ? towerUpgrade.prerequisites.ToArray() : System.Array.Empty<UpgradeType>();
            var towerUpgradeRoutes = towerUpgrade.linkRoutes != null ? towerUpgrade.linkRoutes.ToArray() : System.Array.Empty<SkillNodeView.SkillLinkRoute>();
            var paintToken = GetOrCreateNode(parent, towerUpgrade, UpgradeType.PaintAreaTokenGain, 6);
            var revive = GetOrCreateNode(parent, towerUpgrade, UpgradeType.ReviveBuildingsOnBossDefeat, 8);
            var chest = GetOrCreateNode(parent, towerUpgrade, UpgradeType.UnlockOpeningRelicChest, 9);

            ApplyNode(
                paintToken,
                UpgradeType.PaintAreaTokenGain,
                6,
                "塗りエリアトークン獲得",
                "塗ったエリアが500に到達する度、スキルレベル分のトークンを獲得します。",
                StatIconCatalog.ForUpgrade(UpgradeType.PaintAreaTokenGain),
                towerUpgradePosition,
                towerUpgradePrerequisites,
                towerUpgradeRoutes);

            ApplyNode(
                towerUpgrade,
                UpgradeType.UnlockTowerUpgrade,
                7,
                "中心塔アップグレード",
                "中心塔をアップグレードし、耐久力と防衛性能を高めます。",
                StatIconCatalog.ForUpgrade(UpgradeType.UnlockTowerUpgrade),
                towerUpgradePosition + new Vector2(0f, -92f),
                UpgradeType.PaintAreaTokenGain);

            ApplyNode(
                revive,
                UpgradeType.ReviveBuildingsOnBossDefeat,
                8,
                "ボス撃破時建造物復活",
                "ボス撃破時、破壊された建造物が最大HP50%で復活します。",
                StatIconCatalog.ForUpgrade(UpgradeType.ReviveBuildingsOnBossDefeat),
                towerUpgradePosition + new Vector2(0f, -184f),
                UpgradeType.UnlockTowerUpgrade);

            ApplyNode(
                chest,
                UpgradeType.UnlockOpeningRelicChest,
                9,
                "開幕宝箱出現",
                "ゲーム開始時、中心塔の下にレリック宝箱が出現します。",
                StatIconCatalog.ForUpgrade(UpgradeType.UnlockOpeningRelicChest),
                towerUpgradePosition + new Vector2(0f, -276f),
                UpgradeType.ReviveBuildingsOnBossDefeat);

            DestroyNamedLinks(linkRoot, "PaintAreaTokenGain to UnlockTowerUpgrade");
            DestroyNamedLinks(linkRoot, "UnlockTowerUpgrade to ReviveBuildingsOnBossDefeat");
            DestroyNamedLinks(linkRoot, "ReviveBuildingsOnBossDefeat to UnlockOpeningRelicChest");
            CreateLink(linkRoot, "PaintAreaTokenGain to UnlockTowerUpgrade", paintToken, towerUpgrade, UpgradeType.PaintAreaTokenGain);
            CreateLink(linkRoot, "UnlockTowerUpgrade to ReviveBuildingsOnBossDefeat", towerUpgrade, revive, UpgradeType.UnlockTowerUpgrade);
            CreateLink(linkRoot, "ReviveBuildingsOnBossDefeat to UnlockOpeningRelicChest", revive, chest, UpgradeType.ReviveBuildingsOnBossDefeat);
        }

        static SkillNodeView GetOrCreateNode(Transform parent, SkillNodeView template, UpgradeType type, int number)
        {
            var existing = Object.FindObjectsOfType<SkillNodeView>(true).FirstOrDefault(node => node.type == type);
            if (existing != null) return existing;
            var go = Object.Instantiate(template.gameObject, parent);
            go.name = $"{number:00} {type}";
            return go.GetComponent<SkillNodeView>();
        }

        static void ApplyNode(
            SkillNodeView node,
            UpgradeType type,
            int number,
            string title,
            string description,
            string iconResource,
            Vector2 position,
            UpgradeType prerequisite)
        {
            ApplyNode(
                node,
                type,
                number,
                title,
                description,
                iconResource,
                position,
                new[] { prerequisite },
                System.Array.Empty<SkillNodeView.SkillLinkRoute>());
        }

        static void ApplyNode(
            SkillNodeView node,
            UpgradeType type,
            int number,
            string title,
            string description,
            string iconResource,
            Vector2 position,
            UpgradeType[] prerequisites,
            SkillNodeView.SkillLinkRoute[] linkRoutes)
        {
            if (node == null) return;
            node.gameObject.name = $"{number:00} {type}";
            node.type = type;
            node.useGridPosition = false;
            node.prerequisites = prerequisites ?? System.Array.Empty<UpgradeType>();
            node.linkRoutes = linkRoutes ?? System.Array.Empty<SkillNodeView.SkillLinkRoute>();
            node.implemented = true;
            node.title = title;
            node.description = description;
            if (node.RectTransform != null) node.RectTransform.anchoredPosition = position;

            node.ResolveReferences();
            var numberText = node.transform.Find("Node Button/Node No")?.GetComponent<Text>();
            if (numberText != null) numberText.text = number.ToString();

            var sprite = GeneratedSpriteLoader.Load(iconResource);
            if (node.icon != null && sprite != null)
            {
                node.icon.sprite = sprite;
                EditorUtility.SetDirty(node.icon);
            }

            EditorUtility.SetDirty(node);
        }

        static Transform FindSkillLinkRoot(Transform nodeParent)
        {
            if (nodeParent == null) return null;
            return nodeParent.Find("Skill Links")
                ?? nodeParent.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == "Skill Links")
                ?? nodeParent;
        }

        static void DestroyNamedLinks(Transform linkRoot, string prefix)
        {
            if (linkRoot == null) return;
            var targets = linkRoot.GetComponentsInChildren<Transform>(true)
                .Where(child => child != linkRoot && child.name.StartsWith(prefix))
                .ToArray();
            foreach (var target in targets) Object.DestroyImmediate(target.gameObject);
        }

        static void CreateLink(Transform parent, string name, SkillNodeView fromNode, SkillNodeView toNode, UpgradeType prerequisite)
        {
            if (parent == null || fromNode?.RectTransform == null || toNode?.RectTransform == null) return;
            CreateSegment(parent, name + " A", fromNode.RectTransform.anchoredPosition, toNode.RectTransform.anchoredPosition, prerequisite);
        }

        static void CreateSegment(Transform parent, string name, Vector2 from, Vector2 to, UpgradeType prerequisite)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.01f) return;
            var image = new GameObject(name).AddComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = LinkInactiveColor;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.sizeDelta = new Vector2(delta.magnitude, 4f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var segment = image.gameObject.AddComponent<SkillLinkSegment>();
            segment.prerequisite = prerequisite;
            segment.image = image;
            image.transform.SetAsFirstSibling();
        }
    }
}
