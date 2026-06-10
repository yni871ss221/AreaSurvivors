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
            SimpleUi.Panel(ui.transform, "Upgrade Board", new Vector2(0, 6), new Vector2(1210, 620), new Color(0.035f, 0.032f, 0.043f, 0.80f));
            SimpleUi.Label(ui.transform, "永続強化", 40, new Vector2(0, 290), new Vector2(460, 52));
            SimpleUi.Label(ui.transform, $"所持トークン {ProgressionStore.Data.tokens}", 24, new Vector2(0, 245), new Vector2(360, 36), "TokenLabel", new Color(0.96f, 0.90f, 0.62f));

            graphRoot = new GameObject("Skill Tree").transform;
            graphRoot.SetParent(ui.transform, false);
            graphRoot.localPosition = new Vector3(0f, 18f, 0f);
            graphRoot.localScale = Vector3.one * 0.82f;

            var tooltip = SimpleUi.Panel(ui.transform, "Tooltip", new Vector2(-80, -274), new Vector2(760, 72), new Color(0.05f, 0.07f, 0.08f, 0.88f));
            tooltipTitle = SimpleUi.Label(tooltip.transform, "スキルを選択", 22, new Vector2(0, 18), new Vector2(660, 28), "Tooltip Title", new Color(0.96f, 0.90f, 0.62f));
            tooltipDescription = SimpleUi.Label(tooltip.transform, "アイコンにカーソルを合わせると強化内容を確認できます。", 18, new Vector2(0, -18), new Vector2(660, 34), "Tooltip Description", new Color(0.86f, 0.93f, 0.88f));

            DrawTree();

            var nav = gameObject.AddComponent<SceneNavigator>();
            SimpleUi.Button(ui.transform, "ロビーへ", new Vector2(500, -318), nav.LoadLobby, new Vector2(210, 54), "Generated/Slash_0");
        }

        void BuildNodes()
        {
            nodes.Clear();
            // Combat / character branch
            Add(UpgradeType.AttackPower, null, -500, 210, "攻撃力", "全キャラクターの基礎攻撃力を上げます。", "Generated/Slash_1");
            Add(UpgradeType.AttackCooldown, UpgradeType.AttackPower, -500, 105, "攻撃間隔短縮", "武器の使用間隔を短くします。", "Generated/Arrow");
            Add(UpgradeType.Knockback, UpgradeType.AttackPower, -390, 105, "ノックバック", "攻撃命中時に敵を押し戻す強度を上げます。", "Generated/Slash_0");
            Add(UpgradeType.MaxHp, UpgradeType.AttackPower, -500, 0, "プレイヤーHP", "プレイヤーの最大HPを増やします。", "Generated/Knight");
            Add(UpgradeType.Defense, UpgradeType.MaxHp, -390, 0, "防御力", "受けるダメージを軽減します。", "Generated/Knight");
            Add(UpgradeType.AutoRegen, UpgradeType.Defense, -390, -105, "体力自動回復", "2秒ごとのHP回復量を増やします。", "Generated/Orb");
            Add(UpgradeType.MoveSpeed, UpgradeType.MaxHp, -500, -105, "移動速度", "プレイヤーの移動速度を上げます。", "Generated/Mage");
            Add(UpgradeType.ReviveSpeed, UpgradeType.MaxHp, -500, -210, "復活時間短縮", "復活までの時間を短縮します。", "Generated/Orb");
            Add(UpgradeType.XpGain, UpgradeType.MoveSpeed, -390, -210, "経験値獲得量", "取得経験値の倍率を上げます。", "Generated/ExperienceOrb");
            Add(UpgradeType.UnlockDefenseCharacter, UpgradeType.AttackPower, -280, 105, "防衛キャラ追加", "選択していないキャラクターを防衛役として追加します。", "Generated/Knight", false);
            Add(UpgradeType.UnlockClassChange, UpgradeType.UnlockDefenseCharacter, -280, 0, "クラスチェンジ", "クラスチェンジを可能にします。", "Generated/Mage", false);

            // Resource branch
            Add(UpgradeType.StartingWood, null, -145, 210, "初期木材", "ゲーム開始時の木材を増やします。", "Generated/WoodIcon");
            Add(UpgradeType.StartingStone, null, -35, 210, "初期石材", "ゲーム開始時の石材を増やします。", "Generated/StoneIcon");
            Add(UpgradeType.WorkSpeed, UpgradeType.StartingWood, -145, 105, "作業速度", "建造、伐採、採掘の速度を上げます。", "Generated/Hammer");
            Add(UpgradeType.ResourceGain, UpgradeType.StartingStone, -35, 105, "資源獲得量", "伐採、採掘で得る資源量を増やします。", "Generated/WoodIcon");
            Add(UpgradeType.WoodcuttingSpeed, UpgradeType.WorkSpeed, -145, 0, "伐採速度", "プレイヤーの伐採速度を上げます。", "Generated/Axe");
            Add(UpgradeType.WoodcuttingGain, UpgradeType.ResourceGain, -35, 0, "伐採数", "プレイヤーが一度に得る木材を増やします。", "Generated/WoodIcon");
            Add(UpgradeType.MiningSpeed, UpgradeType.WorkSpeed, -145, -105, "採掘速度", "プレイヤーの採掘速度を上げます。", "Generated/Pickaxe");
            Add(UpgradeType.MiningGain, UpgradeType.ResourceGain, -35, -105, "採掘数", "プレイヤーが一度に得る石材を増やします。", "Generated/StoneIcon");
            Add(UpgradeType.UnlockLargeWorkshop, UpgradeType.MiningGain, -90, -210, "大型作業場", "8セル森・山に隣接して配置できる大型作業場をアンロックします。", "Generated/Hammer", false);

            // Construction branch
            Add(UpgradeType.UnlockBallista, null, 95, 210, "バリスタ解禁", "バリスタを建造可能にします。", "Generated/Ballista");
            Add(UpgradeType.BallistaRange, UpgradeType.UnlockBallista, 95, 105, "バリスタ射程", "バリスタの攻撃射程を広げます。", "Generated/Arrow");
            Add(UpgradeType.UnlockWatchTower, UpgradeType.UnlockBallista, 205, 105, "監視塔解禁", "建造物「監視塔」をアンロックします。", "Generated/Tower", false);
            Add(UpgradeType.UnlockCarpenterHut, UpgradeType.UnlockBallista, 95, 0, "大工小屋解禁", "自動建造を行う大工を追加できる建造物をアンロックします。", "Generated/Hammer", false);
            Add(UpgradeType.UnlockAutoBuild, UpgradeType.UnlockCarpenterHut, 95, -105, "大工の自動建造", "大工がエリア内に適切な建造物を建築します。", "Generated/Hammer", false);
            Add(UpgradeType.AutoBuildSpeed, UpgradeType.UnlockAutoBuild, 95, -210, "自動建造速度", "大工による自動建造の速度を上げます。", "Generated/Hammer", false);
            Add(UpgradeType.UnlockWorkerHut, UpgradeType.UnlockCarpenterHut, 205, 0, "作業小屋解禁", "自動採取を行う作業者を追加できる建造物をアンロックします。", "Generated/Pickaxe", false);
            Add(UpgradeType.AutoWoodcuttingSpeed, UpgradeType.UnlockWorkerHut, 205, -105, "自動伐採速度", "作業者の自動伐採速度を上げます。", "Generated/Axe", false);
            Add(UpgradeType.AutoMiningSpeed, UpgradeType.UnlockWorkerHut, 205, -210, "自動採掘速度", "作業者の自動採掘速度を上げます。", "Generated/Pickaxe", false);

            // Tower / territory branch
            Add(UpgradeType.TowerMaxHp, null, 340, 210, "中心塔HP", "中心塔の最大HPを増やします。", "Generated/Tower");
            Add(UpgradeType.InitialTerritory, UpgradeType.TowerMaxHp, 340, 105, "初期青エリア", "ゲーム開始時の中心塔周囲の青エリアを広げます。", "Generated/PaintTile");
            Add(UpgradeType.PaintRadius, UpgradeType.InitialTerritory, 340, 0, "塗り範囲", "プレイヤーの塗り範囲を広げます。", "Generated/PaintTile");
            Add(UpgradeType.TowerAutoRegen, UpgradeType.TowerMaxHp, 450, 105, "中心塔自動回復", "中心塔が一定間隔でHPを回復します。", "Generated/Orb");
            Add(UpgradeType.UnlockTowerCannon, UpgradeType.TowerMaxHp, 450, 0, "中心塔大砲", "中心塔から大砲を発射可能にします。", "Generated/Fireball", false);
            Add(UpgradeType.UnlockTowerUpgrade, UpgradeType.UnlockTowerCannon, 450, -105, "中心塔アップグレード", "ゲーム中に中心塔をアップグレード可能にします。", "Generated/Tower", false);

            // Challenge / reward branch
            Add(UpgradeType.EndTokenGain, null, 560, 210, "終了時トークン", "ラン終了時に獲得するトークン数を増やします。", "Generated/Token");
            Add(UpgradeType.EliteSpawnRate, UpgradeType.EndTokenGain, 560, 105, "エリート出現率", "通常スポーンがエリートに置き換わる確率を上げます。", "Generated/EnemyBoar");
        }

        void Add(UpgradeType type, UpgradeType? parent, float x, float y, string title, string description, string icon, bool implemented = true)
        {
            nodes.Add(new Node(type, parent, new Vector2(x, y), title, description, icon, implemented));
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
            bool prerequisiteMet = !node.parent.HasValue || ProgressionStore.GetLevel(node.parent.Value) > 0;
            bool maxed = level >= ProgressionStore.GetMaxLevel(node.type);

            var root = new GameObject(node.title).AddComponent<RectTransform>();
            root.SetParent(graphRoot, false);
            root.anchoredPosition = node.position;
            root.sizeDelta = new Vector2(82, 94);

            var buttonObject = new GameObject("Node Button");
            buttonObject.transform.SetParent(root, false);
            var image = buttonObject.AddComponent<Image>();
            image.color = purchased ? new Color(0.28f, 0.55f, 0.38f, 0.96f) : affordable ? new Color(0.17f, 0.28f, 0.24f, 0.96f) : new Color(0.12f, 0.13f, 0.14f, 0.92f);
            image.rectTransform.sizeDelta = new Vector2(68, 68);
            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = node.implemented && prerequisiteMet && affordable && !maxed;
            button.onClick.AddListener(() =>
            {
                if (ProgressionStore.TryBuy(node.type)) UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Upgrades);
            });

            var iconSprite = Resources.Load<Sprite>(node.iconResource);
            if (iconSprite != null)
            {
                var icon = new GameObject("Icon").AddComponent<Image>();
                icon.transform.SetParent(buttonObject.transform, false);
                icon.sprite = iconSprite;
                icon.preserveAspect = true;
                icon.color = node.implemented && (purchased || affordable) ? Color.white : new Color(0.45f, 0.48f, 0.48f, 1f);
                icon.rectTransform.sizeDelta = new Vector2(42, 42);
            }

            var hover = buttonObject.AddComponent<UpgradeNodeHover>();
            hover.title = tooltipTitle;
            hover.description = tooltipDescription;
            hover.titleText = node.implemented ? $"{node.title}  Lv {level}  Cost {cost}" : $"{node.title}  実装予定";
            hover.descriptionText = node.implemented ? node.description : node.description + "（現在はツリー上の予約ノードです）";

            string status = !node.implemented ? "予定" : maxed ? $"Lv {level} MAX" : purchased ? $"Lv {level}" : prerequisiteMet ? $"Cost {cost}" : "LOCK";
            SimpleUi.Label(root, status, 13, new Vector2(0, -41), new Vector2(88, 22), "Node Cost", purchased ? new Color(0.72f, 1f, 0.74f) : new Color(0.96f, 0.90f, 0.62f));
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
            return true;
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
            public readonly bool implemented;

            public Node(UpgradeType type, UpgradeType? parent, Vector2 position, string title, string description, string iconResource, bool implemented)
            {
                this.type = type;
                this.parent = parent;
                this.position = position;
                this.title = title;
                this.description = description;
                this.iconResource = iconResource;
                this.implemented = implemented;
            }
        }
    }
}
