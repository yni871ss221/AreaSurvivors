using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [ExecuteAlways]
    public sealed class LobbyScreen : MonoBehaviour
    {
        const int StagePanelCount = 4;
        static readonly Color LockedColor = new Color(0f, 0f, 0f, 0.72f);
        static readonly Color UnlockedColor = Color.white;

        Canvas lobbyUi;
        SceneNavigator navigator;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall -= EnsureSceneUiForEditing;
            UnityEditor.EditorApplication.delayCall += EnsureSceneUiForEditing;
        }

        void EnsureSceneUiForEditing()
        {
            if (this == null || Application.isPlaying) return;
            if (FindLobbyCanvas() != null) return;
            LobbyUiFactory.Create();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        void Start()
        {
            navigator = GetComponent<SceneNavigator>();
            if (navigator == null) navigator = gameObject.AddComponent<SceneNavigator>();
            NormalizeCharacterSelection();
            lobbyUi = FindLobbyCanvas();
            if (lobbyUi == null)
            {
                Debug.LogError("Lobby UI was not found in the scene. Place the Lobby UI in the scene instead of relying on runtime generation.");
                enabled = false;
                return;
            }

            BindStaticActions();
            Refresh();
        }

        void Refresh()
        {
            SetText("TokenInfo", string.Format("\u30c8\u30fc\u30af\u30f3 {0}   \u6728\u6750 {1}   \u77f3\u6750 {2}   \u7d2f\u8a08\u6483\u7834 {3}", ProgressionStore.Data.tokens, ProgressionStore.Data.wood, ProgressionStore.Data.stone, ProgressionStore.Data.totalKills));
            RefreshKnightLoadout();
            RefreshStageCards();
        }

        void BindStaticActions()
        {
            BindButton("Start Game Button", StartSelectedStage);
            BindButton("Start Stage 2 Test Button", StartStage2ForTesting);
            BindButton("Start Stage 3 Test Button", StartStage3ForTesting);
            BindButton("Start Stage 4 Test Button", StartStage4ForTesting);
            BindButton("Upgrade Button", navigator.LoadUpgrades);
            BindButton("Title Button", navigator.LoadTitle);
        }

        void RefreshKnightLoadout()
        {
            RefreshKnightLoadout("Character Knight");
        }

        void RefreshKnightLoadout(string name)
        {
            var card = FindChild(name);
            if (card == null) return;
            var highlight = card.GetComponent<CharacterSelectionHighlight>();
            if (highlight != null) highlight.type = CharacterType.Knight;
            var selection = card.GetComponent<UiSelectionHighlight>();
            if (selection != null) selection.forceSelected = true;
        }

        void NormalizeCharacterSelection()
        {
            RunState.SelectedCharacter = CharacterType.Knight;
            ProgressionStore.Data.selectedCharacter = CharacterType.Knight;
            ProgressionStore.Save();
        }

        void RefreshStageCards()
        {
            for (int stage = 1; stage <= StagePanelCount; stage++)
            {
                var panel = FindChild("Stage " + stage + " Panel");
                if (panel == null) continue;

                bool unlocked = ProgressionStore.IsStageUnlocked(stage);
                bool cleared = ProgressionStore.IsStageCleared(stage);
                var button = panel.GetComponent<Button>();
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.interactable = unlocked;
                    int selectedStage = stage;
                    button.onClick.AddListener(() =>
                    {
                        ProgressionStore.SelectedStage = selectedStage;
                        RefreshStageCards();
                    });
                }

                var selection = panel.GetComponent<UiSelectionHighlight>();
                if (selection == null && button != null) selection = panel.gameObject.AddComponent<UiSelectionHighlight>();
                if (selection != null) selection.forceSelected = unlocked && ProgressionStore.SelectedStage == stage;

                var boss = FindChild(panel, "Boss Image")?.GetComponent<Image>();
                if (boss != null)
                {
                    boss.sprite = BossSprite(stage);
                    boss.color = unlocked ? UnlockedColor : LockedColor;
                    boss.enabled = boss.sprite != null;
                }

                SetActive(panel, "Unknown Boss", boss == null || boss.sprite == null);
                SetText(panel, "Boss Name", unlocked ? BossName(stage) : "???");
                SetActive(panel, "Clear", cleared);

                var toggle = FindChild(panel, "Fast Mode Toggle")?.GetComponent<Toggle>();
                if (toggle == null) continue;
                toggle.gameObject.SetActive(cleared);
                toggle.onValueChanged.RemoveAllListeners();
                toggle.SetIsOnWithoutNotify(ProgressionStore.IsFastStage(stage));
                int capturedStage = stage;
                toggle.onValueChanged.AddListener(value => ProgressionStore.SetFastStage(capturedStage, value));
            }
        }

        void StartSelectedStage()
        {
            StartGameFromStage(ProgressionStore.SelectedStage);
        }

        void StartStage2ForTesting()
        {
            StartGameFromStageForTesting(2);
        }

        void StartStage3ForTesting()
        {
            StartGameFromStageForTesting(3);
        }

        void StartStage4ForTesting()
        {
            StartGameFromStageForTesting(4);
        }

        void StartGameFromStage(int stage)
        {
            if (!ProgressionStore.IsStageUnlocked(stage)) return;
            RunState.SetNextStartStage(stage);
            navigator.LoadGame();
        }

        void StartGameFromStageForTesting(int stage)
        {
            RunState.SetNextStartStage(stage);
            navigator.LoadGame();
        }

        void BindButton(string name, UnityEngine.Events.UnityAction action)
        {
            var button = FindChild(name)?.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        Canvas FindLobbyCanvas()
        {
            var root = GameObject.Find("Lobby UI");
            if (root != null)
            {
                var canvas = root.GetComponent<Canvas>();
                if (canvas != null) return canvas;
            }

            var canvases = FindObjectsOfType<Canvas>(true);
            foreach (var canvas in canvases)
            {
                if (canvas != null && canvas.name == "Lobby UI") return canvas;
            }

            return null;
        }

        Transform FindChild(string name)
        {
            if (lobbyUi == null) return null;
            return FindChild(lobbyUi.transform, name);
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        void SetText(string name, string value)
        {
            var text = FindChild(name)?.GetComponent<Text>();
            if (text != null) text.text = value;
        }

        static void SetText(Transform root, string name, string value)
        {
            var text = FindChild(root, name)?.GetComponent<Text>();
            if (text != null) text.text = value;
        }

        static void SetActive(Transform root, string name, bool active)
        {
            var child = FindChild(root, name);
            if (child != null) child.gameObject.SetActive(active);
        }

        static Sprite BossSprite(int stage)
        {
            if (stage == 1) return GeneratedSpriteLoader.Load("Walk/EnemyOrcKing/Down_1");
            if (stage == 2) return GeneratedSpriteLoader.Load("Walk/EnemyGoblinLord/Down_1");
            if (stage == 3) return GeneratedSpriteLoader.Load("Walk/EnemyLich/Down_1");
            if (stage == 4) return GeneratedSpriteLoader.Load("Walk/EnemyDragon/Down_1");
            return null;
        }

        static string BossName(int stage)
        {
            if (stage == 1) return "\u30aa\u30fc\u30af\u30ad\u30f3\u30b0";
            if (stage == 2) return "\u30b4\u30d6\u30ea\u30f3\u30ed\u30fc\u30c9";
            if (stage == 3) return "\u30ea\u30c3\u30c1";
            if (stage == 4) return "\u30c9\u30e9\u30b4\u30f3";
            return "\u672a\u78ba\u8a8d";
        }
    }
}
