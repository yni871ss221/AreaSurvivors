using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class PlayerSkillTreeRevisionSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/04_Upgrades.unity";
        static readonly Color LinkInactiveColor = new Color(0.30f, 0.36f, 0.34f, 0.75f);

        [MenuItem("Area Survivors/Setup/Apply Player Skill Tree Revision")]
        public static void Apply()
        {
            string previousScenePath = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ApplyPlayerNodes(scene);
            ApplyCenterTowerNodes(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previousScenePath) && previousScenePath != ScenePath)
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }

        static void ApplyPlayerNodes(Scene scene)
        {
            var nodes = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SkillNodeView>(true))
                .ToArray();
            var playerRoot = nodes.FirstOrDefault(node => node.type == UpgradeType.MaxHp)?.transform.parent;
            if (playerRoot == null)
            {
                Debug.LogError("Player Skills root was not found. Player skill tree revision was skipped.");
                return;
            }

            var byType = nodes.Where(node => node.transform.parent == playerRoot)
                .GroupBy(node => node.type)
                .ToDictionary(group => group.Key, group => group.First());

            var moveTemplate = byType.TryGetValue(UpgradeType.MoveSpeed, out var moveSpeed) ? moveSpeed : byType[UpgradeType.MaxHp];
            var paintTemplate = byType.TryGetValue(UpgradeType.PaintRadius, out var paintRadius) ? paintRadius : moveTemplate;

            var specs = new[]
            {
                new SkillSpec(1, UpgradeType.MaxHp, "プレイヤーHP", "プレイヤーの最大HPが上昇します。", StatIconCatalog.ForUpgrade(UpgradeType.MaxHp), new Vector2(0f, 141f)),
                new SkillSpec(2, UpgradeType.Defense, "防御力", "受けるダメージを軽減します。", StatIconCatalog.ForUpgrade(UpgradeType.Defense), new Vector2(-102.5f, 71f), UpgradeType.MaxHp),
                new SkillSpec(3, UpgradeType.AutoRegen, "体力自動回復", "一定時間ごとにHPを自動回復します。", StatIconCatalog.ForUpgrade(UpgradeType.AutoRegen), new Vector2(-102.5f, 1f), UpgradeType.Defense),
                new SkillSpec(4, UpgradeType.ReviveSpeed, "復活時間短縮", "復活時間を -0.7秒 / Lv 短縮します。", StatIconCatalog.ForUpgrade(UpgradeType.ReviveSpeed), new Vector2(-102.5f, -69f), UpgradeType.AutoRegen),
                new SkillSpec(5, UpgradeType.MoveSpeed, "移動速度", "移動速度が上昇します。", StatIconCatalog.ForUpgrade(UpgradeType.MoveSpeed), new Vector2(102.5f, 71f), UpgradeType.MaxHp),
                new SkillSpec(6, UpgradeType.PaintRadius, "塗り範囲", "2レベルごとに塗り範囲が1セル広がります。", StatIconCatalog.ForUpgrade(UpgradeType.PaintRadius), new Vector2(102.5f, 1f), UpgradeType.MoveSpeed),
                new SkillSpec(7, UpgradeType.XpGain, "経験値獲得量", "獲得経験値が上昇します。", StatIconCatalog.ForUpgrade(UpgradeType.XpGain), new Vector2(102.5f, -69f), UpgradeType.PaintRadius),
                new SkillSpec(8, UpgradeType.MovePenaltyReduction, "移動ペナルティ低下", "敵陣地上の移動速度低下を +0.1 / Lv 軽減します。", StatIconCatalog.ForUpgrade(UpgradeType.MovePenaltyReduction), new Vector2(0f, -139f), UpgradeType.ReviveSpeed, UpgradeType.XpGain),
                new SkillSpec(9, UpgradeType.UnlockArrow, "弓アンロック", "レベルアップ時の候補に弓が登場するようになります。", "ArrowHudIcon", new Vector2(-205f, 1f), UpgradeType.Defense),
                new SkillSpec(10, UpgradeType.UnlockArrowRain, "アローレインアンロック", "レベルアップ時の候補にアローレインが登場するようになります。", "ArrowRain", new Vector2(-180f, -215f), UpgradeType.MovePenaltyReduction),
                new SkillSpec(11, UpgradeType.UnlockGun, "銃アンロック", "レベルアップ時の候補に銃が登場するようになります。", "Gun", new Vector2(-180f, -367f), UpgradeType.MoveSpeedAdvanced),
                new SkillSpec(12, UpgradeType.UnlockFireball, "ファイアボールアンロック", "レベルアップ時の候補にファイアボールが登場するようになります。", "FireballHudIcon", new Vector2(205f, 1f), UpgradeType.MoveSpeed),
                new SkillSpec(13, UpgradeType.UnlockFrost, "フロストアンロック", "レベルアップ時の候補にフロストが登場するようになります。", "Frost", new Vector2(0f, -215f), UpgradeType.MovePenaltyReduction),
                new SkillSpec(14, UpgradeType.UnlockThunderBall, "サンダーボールアンロック", "レベルアップ時の候補にサンダーボールが登場するようになります。", "ThunderBall", new Vector2(0f, -367f), UpgradeType.PaintRadiusAdvanced),
                new SkillSpec(15, UpgradeType.UnlockBoomerangSword, "ブーメランソードアンロック", "レベルアップ時の候補にブーメランソードが登場するようになります。", "BoomerangSword", new Vector2(180f, -215f), UpgradeType.MovePenaltyReduction),
                new SkillSpec(16, UpgradeType.UnlockAuraSword, "オーラソードアンロック", "レベルアップ時の候補にオーラソードが登場するようになります。", "AuraSword", new Vector2(180f, -367f), UpgradeType.RemoveStartingSlash),
                new SkillSpec(17, UpgradeType.RemoveStartingSlash, "初期スラッシュ削除", "ゲーム開始時にスラッシュを持たず、武器枠を1つ空けた状態にします。", "Slash_0", new Vector2(180f, -291f), UpgradeType.UnlockBoomerangSword),
                new SkillSpec(18, UpgradeType.MoveSpeedAdvanced, "移動速度", "移動速度がさらに上昇します。", StatIconCatalog.ForUpgrade(UpgradeType.MoveSpeedAdvanced), new Vector2(-180f, -291f), UpgradeType.UnlockArrowRain),
                new SkillSpec(19, UpgradeType.PaintRadiusAdvanced, "塗り範囲", "2レベルごとに塗り範囲がさらに1セル広がります。", StatIconCatalog.ForUpgrade(UpgradeType.PaintRadiusAdvanced), new Vector2(0f, -291f), UpgradeType.UnlockFrost),
            };

            foreach (var spec in specs)
            {
                var template = spec.type == UpgradeType.PaintRadiusAdvanced ? paintTemplate : moveTemplate;
                var node = GetOrCreateNode(byType, playerRoot, template, spec);
                ApplyNode(node, spec);
                byType[spec.type] = node;
            }

            var linkRoot = FindSkillLinkRoot(playerRoot);
            ClearLinks(linkRoot);
            foreach (var spec in specs)
            {
                foreach (var prerequisite in spec.prerequisites)
                {
                    if (!byType.TryGetValue(prerequisite, out var from) || !byType.TryGetValue(spec.type, out var to)) continue;
                    CreateLink(linkRoot, $"{prerequisite} to {spec.type}", from, to, prerequisite);
                }
            }
        }

        static void ApplyCenterTowerNodes(Scene scene)
        {
            var nodes = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SkillNodeView>(true))
                .ToArray();
            var centerRoot = nodes.FirstOrDefault(node => node.type == UpgradeType.TowerMaxHp)?.transform.parent;
            if (centerRoot == null)
            {
                Debug.LogError("Center Tower Skills root was not found. Center tower skill tree revision was skipped.");
                return;
            }

            var globalByType = nodes
                .GroupBy(node => node.type)
                .ToDictionary(group => group.Key, group => group.First());
            var centerByType = nodes.Where(node => node.transform.parent == centerRoot)
                .GroupBy(node => node.type)
                .ToDictionary(group => group.Key, group => group.First());

            var template = centerByType.TryGetValue(UpgradeType.UnlockOpeningRelicChest, out var chest)
                ? chest
                : centerByType[UpgradeType.TowerMaxHp];
            var specs = new[]
            {
                new SkillSpec(1, UpgradeType.TowerMaxHp, "中心塔HP", "中心塔の最大HPが上昇します。", StatIconCatalog.ForUpgrade(UpgradeType.TowerMaxHp), new Vector2(0f, 92f)),
                new SkillSpec(2, UpgradeType.TowerAutoRegen, "中心塔自動回復", "中心塔の自動回復量が上昇します。", StatIconCatalog.ForUpgrade(UpgradeType.TowerAutoRegen), new Vector2(-110.7f, 22f), UpgradeType.TowerMaxHp),
                new SkillSpec(3, UpgradeType.UnlockTowerCannon, "中心塔大砲", "中心塔が砲撃できるようになります。", StatIconCatalog.ForUpgrade(UpgradeType.UnlockTowerCannon), new Vector2(-110.7f, -48f), UpgradeType.TowerAutoRegen),
                new SkillSpec(4, UpgradeType.EndTokenGain, "終了時トークン", "ラン終了時の固定獲得トークンが +1 増えます。", StatIconCatalog.ForUpgrade(UpgradeType.EndTokenGain), new Vector2(110.7f, 22f), UpgradeType.TowerMaxHp),
                new SkillSpec(5, UpgradeType.EliteSpawnCount, "エリート出現数UP", "エリート敵の出現数が増えます。", StatIconCatalog.ForUpgrade(UpgradeType.EliteSpawnCount), new Vector2(110.7f, -48f), UpgradeType.EndTokenGain),
                new SkillSpec(10, UpgradeType.UnlockShield, "シールドアンロック", "レベルアップ時の候補にシールドが登場するようになります。", "Shield", new Vector2(0f, -118f), UpgradeType.UnlockTowerCannon, UpgradeType.EliteSpawnCount),
                new SkillSpec(6, UpgradeType.PaintAreaTokenGain, "塗りエリアトークン獲得", "塗ったエリアが500に到達する度、スキルレベル分のトークンを獲得します。", StatIconCatalog.ForUpgrade(UpgradeType.PaintAreaTokenGain), new Vector2(-110.7f, -210f), UpgradeType.UnlockShield),
                new SkillSpec(7, UpgradeType.UnlockTowerUpgrade, "中心塔アップグレード", "中心塔をアップグレードし、耐久力と防衛性能を高めます。", StatIconCatalog.ForUpgrade(UpgradeType.UnlockTowerUpgrade), new Vector2(-110.7f, -302f), UpgradeType.PaintAreaTokenGain),
                new SkillSpec(8, UpgradeType.ReviveBuildingsOnBossDefeat, "ボス撃破時建造物復活", "ボス撃破時、破壊された建造物が最大HP50%で復活します。", StatIconCatalog.ForUpgrade(UpgradeType.ReviveBuildingsOnBossDefeat), new Vector2(110.7f, -210f), UpgradeType.UnlockShield),
                new SkillSpec(11, UpgradeType.UnlockFlag, "旗アンロック", "レベルアップ時の候補に旗が登場するようになります。", "Flag", new Vector2(110.7f, -302f), UpgradeType.ReviveBuildingsOnBossDefeat),
                new SkillSpec(9, UpgradeType.UnlockOpeningRelicChest, "開幕宝箱出現", "ゲーム開始時、中心塔の下にレリック宝箱が出現します。", StatIconCatalog.ForUpgrade(UpgradeType.UnlockOpeningRelicChest), new Vector2(0f, -394f), UpgradeType.UnlockTowerUpgrade, UpgradeType.UnlockFlag),
            };

            foreach (var spec in specs)
            {
                var node = GetOrCreateNode(globalByType, centerRoot, template, spec);
                if (node.transform.parent != centerRoot) node.transform.SetParent(centerRoot, false);
                ApplyNode(node, spec);
                globalByType[spec.type] = node;
                centerByType[spec.type] = node;
            }

            var linkRoot = FindSkillLinkRoot(centerRoot);
            ClearLinks(linkRoot);
            foreach (var spec in specs)
            {
                foreach (var prerequisite in spec.prerequisites)
                {
                    if (!centerByType.TryGetValue(prerequisite, out var from) || !centerByType.TryGetValue(spec.type, out var to)) continue;
                    CreateLink(linkRoot, $"{prerequisite} to {spec.type}", from, to, prerequisite);
                }
            }
        }

        static SkillNodeView GetOrCreateNode(Dictionary<UpgradeType, SkillNodeView> byType, Transform parent, SkillNodeView template, SkillSpec spec)
        {
            if (byType.TryGetValue(spec.type, out var existing) && existing != null) return existing;
            var go = Object.Instantiate(template.gameObject, parent);
            go.name = $"{spec.number:00} {spec.type}";
            return go.GetComponent<SkillNodeView>();
        }

        static void ApplyNode(SkillNodeView node, SkillSpec spec)
        {
            if (node == null) return;
            node.gameObject.name = $"{spec.number:00} {spec.title}";
            node.type = spec.type;
            node.useGridPosition = false;
            node.prerequisites = spec.prerequisites;
            node.linkRoutes = System.Array.Empty<SkillNodeView.SkillLinkRoute>();
            node.implemented = true;
            node.title = spec.title;
            node.description = spec.description;
            // Node placement is Scene-authored. Do not overwrite user-adjusted positions from setup scripts.
            node.ResolveReferences();

            var numberText = node.transform.Find("Node Button/Node No")?.GetComponent<Text>();
            if (numberText != null) numberText.text = spec.number.ToString();

            var sprite = GeneratedSpriteLoader.Load(spec.iconResource);
            if (node.icon != null && sprite != null)
            {
                node.icon.sprite = sprite;
                EditorUtility.SetDirty(node.icon);
            }

            EditorUtility.SetDirty(node);
        }

        static Transform FindSkillLinkRoot(Transform nodeRoot)
        {
            if (nodeRoot == null) return null;
            var linkRoot = nodeRoot.Find("Skill Links")
                ?? nodeRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == "Skill Links");
            if (linkRoot is RectTransform existingRect)
            {
                StretchToParent(existingRect);
                return existingRect;
            }

            if (linkRoot != null)
            {
                Object.DestroyImmediate(linkRoot.gameObject);
            }

            var rect = new GameObject("Skill Links", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(nodeRoot, false);
            rect.SetAsFirstSibling();
            StretchToParent(rect);
            return rect;
        }

        static void ClearLinks(Transform linkRoot)
        {
            if (linkRoot == null) return;
            for (int i = linkRoot.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(linkRoot.GetChild(i).gameObject);
            }
        }

        static void CreateLink(Transform parent, string name, SkillNodeView fromNode, SkillNodeView toNode, UpgradeType prerequisite)
        {
            if (parent == null || fromNode?.RectTransform == null || toNode?.RectTransform == null) return;
            var link = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkillLinkView)).GetComponent<SkillLinkView>();
            link.transform.SetParent(parent, false);
            var rect = (RectTransform)link.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            link.prerequisite = prerequisite;
            link.fromNode = fromNode;
            link.toNode = toNode;
            link.thickness = 5f;
            link.cornerRadius = 12f;
            link.cornerSegments = 6;
            link.activeColor = new Color(0.50f, 0.92f, 0.72f, 0.85f);
            link.inactiveColor = LinkInactiveColor;
            link.ApplyDirectionalAnchors();
            link.ApplyState(false);
            link.transform.SetAsFirstSibling();
        }

        static void StretchToParent(RectTransform rect)
        {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        readonly struct SkillSpec
        {
            public readonly int number;
            public readonly UpgradeType type;
            public readonly string title;
            public readonly string description;
            public readonly string iconResource;
            public readonly Vector2 position;
            public readonly UpgradeType[] prerequisites;

            public SkillSpec(int number, UpgradeType type, string title, string description, string iconResource, Vector2 position, params UpgradeType[] prerequisites)
            {
                this.number = number;
                this.type = type;
                this.title = title;
                this.description = description;
                this.iconResource = iconResource;
                this.position = position;
                this.prerequisites = prerequisites ?? System.Array.Empty<UpgradeType>();
            }
        }
    }
}
