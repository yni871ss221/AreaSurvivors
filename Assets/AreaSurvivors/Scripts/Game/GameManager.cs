using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameConfig config;
        public TileGrid grid;
        public PlayerController playerPrefab;
        public TowerController towerPrefab;
        public EnemySpawner spawner;
        public Text timerText;
        public Text killText;
        public Text levelText;
        public Slider xpBar;
        public GameObject levelUpPanel;
        public Button[] upgradeButtons;

        public PlayerController Player { get; private set; }
        int kills;
        int level = 1;
        int xp;
        int xpToNext = 5;
        int damageDealt;
        float elapsed;
        readonly List<string> runUpgrades = new List<string>();

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            Time.timeScale = 1f;
            config = Instantiate(config);
            if (grid != null) grid.Build();
            var tower = Instantiate(towerPrefab, Vector3.zero, Quaternion.identity);
            tower.Configure(config.towerMaxHp + ProgressionStore.GetLevel(UpgradeType.TowerMaxHp) * 12);
            Player = Instantiate(playerPrefab, new Vector3(0f, -2.8f, 0f), Quaternion.identity);
            Player.Configure(config, grid, RunState.SelectedCharacter);
            Camera.main.GetComponent<CameraFollow>().target = Player.transform;
            spawner.Begin(config, grid, tower.transform);
            UpdateHud();
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            UpdateHud();
        }

        public void RegisterKill()
        {
            kills++;
        }

        public void RegisterDamageDealt(int amount)
        {
            damageDealt += Mathf.Max(0, amount);
        }

        public void AddExperience(int amount)
        {
            xp += Mathf.Max(1, amount);
            while (xp >= xpToNext)
            {
                xp -= xpToNext;
                level++;
                xpToNext = Mathf.RoundToInt(xpToNext * 1.35f + 3);
                ShowLevelUp();
            }
            UpdateHud();
        }

        void ShowLevelUp()
        {
            Time.timeScale = 0f;
            levelUpPanel.SetActive(true);
            var choices = RollUpgrades();
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                int index = i;
                var label = upgradeButtons[i].GetComponentInChildren<Text>();
                label.text = choices[index].label;
                upgradeButtons[i].onClick.RemoveAllListeners();
                upgradeButtons[i].onClick.AddListener(() => ApplyRunUpgrade(choices[index]));
            }

            SelectFirstUpgrade();
        }

        void SelectFirstUpgrade()
        {
            if (upgradeButtons.Length == 0 || upgradeButtons[0] == null) return;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(upgradeButtons[0].gameObject);
            }
            upgradeButtons[0].Select();
        }

        List<RunUpgradeChoice> RollUpgrades()
        {
            var pool = new List<RunUpgradeChoice>
            {
                new RunUpgradeChoice("\u653b\u6483\u529b +2", () => config.baseAttackPower += 2),
                new RunUpgradeChoice("\u653b\u6483\u9593\u9694 -8%", () => { config.knightCooldown *= .92f; config.archerCooldown *= .92f; config.mageCooldown *= .92f; }),
                new RunUpgradeChoice("\u79fb\u52d5\u901f\u5ea6 +8%", () => config.playerMoveSpeed *= 1.08f),
                new RunUpgradeChoice("\u5857\u308a\u7bc4\u56f2 +1", () => config.paintRadius += 1),
                new RunUpgradeChoice("\u6700\u5927HP +8", () => Player.Health.SetMax(Player.Health.maxHp + 8))
            };
            var result = new List<RunUpgradeChoice>();
            while (result.Count < 3 && pool.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }
            return result;
        }

        void ApplyRunUpgrade(RunUpgradeChoice choice)
        {
            choice.apply();
            runUpgrades.Add(choice.label);
            Player.Configure(config, grid, Player.characterType);
            levelUpPanel.SetActive(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Time.timeScale = 1f;
        }

        public void GameOver()
        {
            Time.timeScale = 1f;
            int tokensEarned = Mathf.Max(0, kills / Mathf.Max(1, config.tokenKillsDivisor));
            RunResult.Last = new RunResult
            {
                kills = kills,
                damageDealt = damageDealt,
                level = level,
                tokensEarned = tokensEarned,
                survivedSeconds = elapsed,
                upgrades = new List<string>(runUpgrades)
            };
            ProgressionStore.AddRunRewards(kills, config.tokenKillsDivisor);
            SceneManager.LoadScene(SceneNames.GameOver);
        }

        void UpdateHud()
        {
            if (timerText != null)
            {
                var span = TimeSpan.FromSeconds(elapsed);
                timerText.text = $"{span.Minutes:00}:{span.Seconds:00}";
            }
            if (killText != null) killText.text = $"\u6483\u7834 {kills}";
            if (levelText != null) levelText.text = $"Lv {level}";
            if (xpBar != null) xpBar.value = xpToNext <= 0 ? 0f : (float)xp / xpToNext;
        }

        sealed class RunUpgradeChoice
        {
            public readonly string label;
            public readonly Action apply;
            public RunUpgradeChoice(string label, Action apply)
            {
                this.label = label;
                this.apply = apply;
            }
        }
    }
}
