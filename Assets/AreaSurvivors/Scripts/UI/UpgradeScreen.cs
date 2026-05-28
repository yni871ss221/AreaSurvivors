using UnityEngine;

namespace AreaSurvivors
{
    public sealed class UpgradeScreen : MonoBehaviour
    {
        Transform content;

        void Start()
        {
            var ui = SimpleUi.Root("Upgrade UI", new Color(0.10f, 0.09f, 0.12f));
            SimpleUi.Label(ui.transform, "永続強化", 38, new Vector2(0, 175), new Vector2(480, 65));
            content = ui.transform;
            Rebuild();
            var nav = gameObject.AddComponent<SceneNavigator>();
            SimpleUi.Button(ui.transform, "ロビーへ", new Vector2(0, -245), nav.LoadLobby);
        }

        void Rebuild()
        {
            SimpleUi.Label(content, $"所持トークン {ProgressionStore.Data.tokens}", 22, new Vector2(0, 125), new Vector2(360, 42), "TokenLabel");
            var types = new[] { UpgradeType.AttackPower, UpgradeType.AttackCooldown, UpgradeType.TowerMaxHp, UpgradeType.ReviveSpeed, UpgradeType.MaxHp, UpgradeType.MoveSpeed };
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                int level = ProgressionStore.GetLevel(type);
                int cost = ProgressionStore.GetCost(type, level);
                string label = $"{Name(type)} Lv{level}  Cost {cost}";
                SimpleUi.Button(content, label, new Vector2(0, 65 - i * 48), () =>
                {
                    ProgressionStore.TryBuy(type);
                    UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.Upgrades);
                }, new Vector2(520, 38));
            }
        }

        static string Name(UpgradeType type)
        {
            if (type == UpgradeType.AttackPower) return "攻撃力 +1";
            if (type == UpgradeType.AttackCooldown) return "攻撃間隔短縮";
            if (type == UpgradeType.TowerMaxHp) return "塔HP増加";
            if (type == UpgradeType.ReviveSpeed) return "復活時間短縮";
            if (type == UpgradeType.MaxHp) return "プレイヤーHP増加";
            return "移動速度増加";
        }
    }
}
