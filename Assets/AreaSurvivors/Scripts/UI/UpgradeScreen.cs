using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class UpgradeScreen : MonoBehaviour
    {
        readonly List<Node> nodes = new List<Node>();
        Transform graphRoot;
        Text tooltipTitle;
        Text tooltipDescription;

        void Start()
        {
            BuildNodes();

            var ui = SimpleUi.Root("Upgrade UI", new Color(0.10f, 0.09f, 0.12f), "Generated/UI/UpgradeBackground");
            SimpleUi.Panel(ui.transform, "Upgrade Board", new Vector2(0, 12), new Vector2(940, 560), new Color(0.035f, 0.032f, 0.043f, 0.80f));
            SimpleUi.Label(ui.transform, "永続強化", 40, new Vector2(0, 268), new Vector2(460, 52));
            SimpleUi.Label(ui.transform, $"所持トークン {ProgressionStore.Data.tokens}", 24, new Vector2(0, 226), new Vector2(360, 36), "TokenLabel", new Color(0.96f, 0.90f, 0.62f));

            graphRoot = new GameObject("Skill Tree").transform;
            graphRoot.SetParent(ui.transform, false);

            var tooltip = SimpleUi.Panel(ui.transform, "Tooltip", new Vector2(0, -230), new Vector2(720, 86), new Color(0.05f, 0.07f, 0.08f, 0.88f));
            tooltipTitle = SimpleUi.Label(tooltip.transform, "スキルを選択", 22, new Vector2(0, 18), new Vector2(660, 28), "Tooltip Title", new Color(0.96f, 0.90f, 0.62f));
            tooltipDescription = SimpleUi.Label(tooltip.transform, "アイコンにカーソルを合わせると強化内容を確認できます。", 18, new Vector2(0, -18), new Vector2(660, 34), "Tooltip Description", new Color(0.86f, 0.93f, 0.88f));

            DrawTree();

            var nav = gameObject.AddComponent<SceneNavigator>();
            SimpleUi.Button(ui.transform, "ロビーへ", new Vector2(0, -318), nav.LoadLobby, new Vector2(250, 54), "Generated/Slash_0");
        }

        void BuildNodes()
        {
            nodes.Clear();
            nodes.Add(new Node(UpgradeType.AttackPower, null, Vector2.zero, "攻撃力", "全キャラクターの基礎攻撃力を上げます。最初に取得する中心スキルです。", "Generated/Slash_1"));
            nodes.Add(new Node(UpgradeType.AttackCooldown, UpgradeType.AttackPower, new Vector2(0, 152), "攻撃間隔短縮", "武器の使用間隔を短くし、攻撃頻度を上げます。", "Generated/Arrow"));
            nodes.Add(new Node(UpgradeType.TowerMaxHp, UpgradeType.AttackPower, new Vector2(-210, 74), "塔HP増加", "防衛対象である塔の最大HPを増やします。", "Generated/Tower"));
            nodes.Add(new Node(UpgradeType.PaintRadius, UpgradeType.AttackPower, new Vector2(210, 74), "塗り範囲拡大", "移動時に自分の領地へ変える床の範囲を広げます。", "Generated/PaintTile"));
            nodes.Add(new Node(UpgradeType.MaxHp, UpgradeType.TowerMaxHp, new Vector2(-210, -98), "プレイヤーHP増加", "プレイヤーの最大HPを増やして倒されにくくします。", "Generated/Knight"));
            nodes.Add(new Node(UpgradeType.MoveSpeed, UpgradeType.PaintRadius, new Vector2(210, -98), "移動速度増加", "プレイヤーの移動速度を上げ、領地を広げやすくします。", "Generated/Mage"));
            nodes.Add(new Node(UpgradeType.ReviveSpeed, UpgradeType.MaxHp, new Vector2(0, -168), "復活時間短縮", "倒された後に全回復して復活するまでの時間を短縮します。", "Generated/Orb"));
        }

        void DrawTree()
        {
            foreach (var node in nodes)
            {
                if (!IsVisible(node)) continue;
                if (node.parent.HasValue)
                {
                    var parent = Find(node.parent.Value);
                    if (parent != null && IsVisible(parent.Value)) DrawLine(parent.Value.position, node.position, IsPurchased(parent.Value));
                }
            }

            foreach (var node in nodes)
            {
                if (IsVisible(node)) DrawNode(node);
            }
        }

        void DrawNode(Node node)
        {
            int level = ProgressionStore.GetLevel(node.type);
            int cost = ProgressionStore.GetCost(node.type, level);
            bool purchased = level > 0;
            bool affordable = ProgressionStore.Data.tokens >= cost;

            var root = new GameObject(node.title).AddComponent<RectTransform>();
            root.SetParent(graphRoot, false);
            root.anchoredPosition = node.position;
            root.sizeDelta = new Vector2(92, 104);

            var buttonObject = new GameObject("Node Button");
            buttonObject.transform.SetParent(root, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = purchased ? new Color(0.28f, 0.55f, 0.38f, 0.96f) : affordable ? new Color(0.17f, 0.28f, 0.24f, 0.96f) : new Color(0.12f, 0.13f, 0.14f, 0.92f);
            image.rectTransform.sizeDelta = new Vector2(78, 78);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = affordable;
            button.onClick.AddListener(() =>
            {
                ProgressionStore.TryBuy(node.type);
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Upgrades);
            });

            var iconSprite = Resources.Load<Sprite>(node.iconResource);
            if (iconSprite != null)
            {
                var icon = new GameObject("Icon").AddComponent<Image>();
                icon.transform.SetParent(buttonObject.transform, false);
                icon.sprite = iconSprite;
                icon.preserveAspect = true;
                icon.color = purchased || affordable ? Color.white : new Color(0.45f, 0.48f, 0.48f, 1f);
                icon.rectTransform.sizeDelta = new Vector2(48, 48);
            }

            var hover = buttonObject.AddComponent<UpgradeNodeHover>();
            hover.title = tooltipTitle;
            hover.description = tooltipDescription;
            hover.titleText = $"{node.title}  Lv {level}  Cost {cost}";
            hover.descriptionText = node.description;

            SimpleUi.Label(root, purchased ? $"Lv {level}" : $"Cost {cost}", 16, new Vector2(0, -46), new Vector2(96, 24), "Node Cost", purchased ? new Color(0.72f, 1f, 0.74f) : new Color(0.96f, 0.90f, 0.62f));
        }

        void DrawLine(Vector2 from, Vector2 to, bool active)
        {
            var line = new GameObject("Link").AddComponent<Image>();
            line.transform.SetParent(graphRoot, false);
            line.color = active ? new Color(0.50f, 0.92f, 0.72f, 0.85f) : new Color(0.30f, 0.36f, 0.34f, 0.75f);
            var rect = line.rectTransform;
            var delta = to - from;
            rect.anchoredPosition = from + delta * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude - 72f, 5f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        bool IsVisible(Node node)
        {
            return !node.parent.HasValue || ProgressionStore.GetLevel(node.parent.Value) > 0;
        }

        bool IsPurchased(Node node)
        {
            return ProgressionStore.GetLevel(node.type) > 0;
        }

        Node? Find(UpgradeType type)
        {
            foreach (var node in nodes)
            {
                if (node.type == type) return node;
            }

            return null;
        }

        readonly struct Node
        {
            public readonly UpgradeType type;
            public readonly UpgradeType? parent;
            public readonly Vector2 position;
            public readonly string title;
            public readonly string description;
            public readonly string iconResource;

            public Node(UpgradeType type, UpgradeType? parent, Vector2 position, string title, string description, string iconResource)
            {
                this.type = type;
                this.parent = parent;
                this.position = position;
                this.title = title;
                this.description = description;
                this.iconResource = iconResource;
            }
        }
    }
}
