using System;
using System.Text;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class GameOverScreen : MonoBehaviour
    {
        void Start()
        {
            var result = RunResult.Last ?? new RunResult();
            var ui = SimpleUi.Root("Game Over UI", new Color(0.08f, 0.07f, 0.08f), "Generated/UI/UpgradeBackground");
            SimpleUi.Panel(ui.transform, "Result Board", new Vector2(0, 18), new Vector2(820, 540), new Color(0.035f, 0.03f, 0.04f, 0.84f));
            SimpleUi.Label(ui.transform, result.gameClear ? "GAME CLEAR" : "GAME OVER", 48, new Vector2(0, 250), new Vector2(520, 58), "Title", result.gameClear ? new Color(0.66f, 1f, 0.64f) : new Color(1f, 0.76f, 0.62f));
            SimpleUi.Label(ui.transform, "\u30e9\u30f3\u30ea\u30b6\u30eb\u30c8", 25, new Vector2(0, 204), new Vector2(360, 34), "Subtitle", new Color(0.96f, 0.90f, 0.68f));

            var survived = TimeSpan.FromSeconds(result.survivedSeconds);
            AddStat(ui.transform, "\u751f\u5b58\u6642\u9593", $"{survived.Minutes:00}:{survived.Seconds:00}", new Vector2(-210, 126));
            AddStat(ui.transform, "\u6575\u6483\u7834\u6570", result.kills.ToString(), new Vector2(210, 126));
            AddStat(ui.transform, "\u7dcf\u30c0\u30e1\u30fc\u30b8", result.damageDealt.ToString(), new Vector2(-210, 38));
            AddStat(ui.transform, "\u5230\u9054\u30ec\u30d9\u30eb", $"Lv {result.level}", new Vector2(210, 38));
            AddStat(ui.transform, "\u7372\u5f97\u30c8\u30fc\u30af\u30f3", result.tokensEarned.ToString(), new Vector2(-210, -50));
            AddStat(ui.transform, "\u7372\u5f97\u6728\u6750/\u77f3\u6750", $"{result.woodEarned} / {result.stoneEarned}", new Vector2(210, -50));

            SimpleUi.Panel(ui.transform, "Upgrade List", new Vector2(0, -154), new Vector2(660, 70), new Color(0.05f, 0.07f, 0.08f, 0.86f));
            SimpleUi.Label(ui.transform, "\u53d6\u5f97\u3057\u305f\u5f37\u5316", 19, new Vector2(-245, -134), new Vector2(150, 28), "Upgrade Header", new Color(0.96f, 0.90f, 0.68f));
            SimpleUi.Label(ui.transform, UpgradeText(result), 18, new Vector2(70, -164), new Vector2(500, 44), "Upgrade Text", new Color(0.86f, 0.93f, 0.88f), TextAnchor.MiddleLeft);
            if (!string.IsNullOrWhiteSpace(result.clearMessage))
            {
                SimpleUi.Label(ui.transform, result.clearMessage, 22, new Vector2(0, -204), new Vector2(620, 34), "Clear Message", new Color(0.72f, 1f, 0.74f));
            }

            var nav = gameObject.AddComponent<SceneNavigator>();
            SimpleUi.Button(ui.transform, "\u30ed\u30d3\u30fc\u3078", new Vector2(0, -264), nav.LoadLobby, new Vector2(250, 56), "Generated/Tower");
        }

        static void AddStat(Transform parent, string label, string value, Vector2 pos)
        {
            SimpleUi.Panel(parent, label + " Panel", pos, new Vector2(300, 70), new Color(0.08f, 0.12f, 0.11f, 0.82f));
            SimpleUi.Label(parent, label, 18, pos + new Vector2(0, 16), new Vector2(260, 24), label, new Color(0.78f, 0.91f, 0.80f));
            SimpleUi.Label(parent, value, 25, pos + new Vector2(0, -14), new Vector2(260, 32), value);
        }

        static string UpgradeText(RunResult result)
        {
            if (result.upgrades == null || result.upgrades.Count == 0) return "\u306a\u3057";
            var builder = new StringBuilder();
            for (int i = 0; i < result.upgrades.Count; i++)
            {
                if (i > 0) builder.Append(" / ");
                builder.Append(result.upgrades[i]);
            }
            return builder.ToString();
        }
    }
}
