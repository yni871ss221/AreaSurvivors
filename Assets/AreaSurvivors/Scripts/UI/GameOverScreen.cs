using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class GameOverScreen : MonoBehaviour
    {
        public GameObject root;
        public Text titleText;
        public Text survivedValueText;
        public Text killsValueText;
        public Text damageValueText;
        public Text levelValueText;
        public Text tokensValueText;
        public Text reachedStageValueText;
        public Text upgradeText;
        public Text clearMessageText;
        public Button lobbyButton;
        public SceneNavigator navigator;
        public GameOverIntroAnimator introAnimator;

        void Start()
        {
            AudioManager.StopBgm();
            var result = RunResult.Last ?? new RunResult();

            var survived = TimeSpan.FromSeconds(result.survivedSeconds);
            SetText(titleText, result.gameClear ? "GAME CLEAR" : "GAME OVER");
            if (titleText != null) titleText.color = result.gameClear ? new Color(0.66f, 1f, 0.64f) : new Color(1f, 0.76f, 0.62f);
            SetText(survivedValueText, $"{survived.Minutes:00}:{survived.Seconds:00}");
            SetText(killsValueText, result.kills.ToString());
            SetText(damageValueText, result.damageDealt.ToString());
            SetText(levelValueText, $"Lv {result.level}");
            SetText(tokensValueText, result.tokensEarned.ToString());
            SetText(reachedStageValueText, $"STAGE {Mathf.Max(1, result.reachedStage)}");
            SetText(upgradeText, UpgradeText(result));

            bool hasClearMessage = !string.IsNullOrWhiteSpace(result.clearMessage);
            if (clearMessageText != null)
            {
                clearMessageText.gameObject.SetActive(hasClearMessage);
                clearMessageText.text = hasClearMessage ? result.clearMessage : string.Empty;
            }

            if (lobbyButton != null && navigator != null)
            {
                lobbyButton.onClick.RemoveListener(navigator.LoadLobby);
                lobbyButton.onClick.AddListener(navigator.LoadLobby);
            }

            if (root != null) root.SetActive(true);
            if (introAnimator != null) introAnimator.Play(result.gameClear);
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

        static void SetText(Text text, string value)
        {
            if (text != null) text.text = value;
        }
    }
}
