using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [ExecuteAlways]
    public sealed class LobbyScreen : MonoBehaviour
    {
        const int StagePanelCount = 4;
        static readonly Color LockedColor = new Color(0f, 0f, 0f, 0.72f);
        static readonly Color UnlockedColor = Color.white;

        public Button weaponBookButton;
        public Button relicButton;
        public ScreenFadeOverlay screenFade;

        [Header("Character Selection")]
        public Button characterPanelButton;
        public GameObject characterSelectionModal;
        public Button characterSelectionCancelButton;
        public Button knightCharacterButton;
        public Button archerCharacterButton;
        public Button mageCharacterButton;
        public Image knightCharacterChoiceIcon;
        public Image archerCharacterChoiceIcon;
        public Image mageCharacterChoiceIcon;
        public Image knightDisplayIcon;
        public Image archerDisplayIcon;
        public Image mageDisplayIcon;
        public Text characterNameText;
        public Text characterDescriptionText;

        Canvas lobbyUi;
        SceneNavigator navigator;
        Button startGameButton;
        Button upgradeButton;
        Button titleButton;
        Button testLaunchButton;
        bool isStartingGame;
        bool characterSelectionOpen;
        readonly Dictionary<Selectable, bool> lobbySelectableStates = new Dictionary<Selectable, bool>();

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
            if (!Application.isPlaying) return;

            AudioManager.PlayBgm(BgmTrack.LobbyUpgrades);

            navigator = GetComponent<SceneNavigator>();
            if (navigator == null) navigator = gameObject.AddComponent<SceneNavigator>();
            LoadSelectedCharacter();
            lobbyUi = FindLobbyCanvas();
            if (lobbyUi == null)
            {
                Debug.LogError("Lobby UI was not found in the scene. Place the Lobby UI in the scene instead of relying on runtime generation.");
                enabled = false;
                return;
            }

            BindStaticActions();
            InitializeCharacterSelection();
            Refresh();
            SelectDefaultButton();
        }

        void Update()
        {
            if (!Application.isPlaying) return;

            if (characterSelectionOpen && UiSelectionUtility.CancelPressed())
            {
                AudioManager.PlayButtonConfirm();
                CloseCharacterSelection();
                return;
            }

            var candidates = SelectionCandidates();
            if (UiSelectionUtility.TickControllerSubmit(candidates)) return;
            UiSelectionUtility.EnsureSelection(candidates);
        }

        void Refresh()
        {
            SetText("TokenInfo", string.Empty);
            SetText("TokenCountValue", ProgressionStore.Data.tokens.ToString());
            SetText("TotalKillsValue", ProgressionStore.Data.totalKills.ToString());
            SetText("PlayCountValue", ProgressionStore.Data.playCount.ToString());
            RefreshCharacterDisplay();
            DisableStatPanelFocus();
            RefreshStageCards();
            ConfigureLobbyNavigation();
        }

        void BindStaticActions()
        {
            BindButton("Start Game Button", StartSelectedStage);
            startGameButton = FindButton("Start Game Button");
            ConfigureTestLaunchButton();
            BindButton("Upgrade Button", navigator.LoadUpgrades);
            upgradeButton = FindButton("Upgrade Button");
            if (!BindButton(weaponBookButton, navigator.LoadWeaponBook))
            {
                BindButton("Weapon Book Button", navigator.LoadWeaponBook);
                weaponBookButton = FindButton("Weapon Book Button");
            }
            if (!BindButton(relicButton, navigator.LoadRelics))
            {
                BindButton("Relic Button", navigator.LoadRelics);
                relicButton = FindButton("Relic Button");
            }
            BindButton("Title Button", navigator.LoadTitle);
            titleButton = FindButton("Title Button");
            BindCharacterSelectionActions();
        }

        void ConfigureTestLaunchButton()
        {
            var testLaunch = FindChild("Test Launch Button");
            if (testLaunch == null) return;
            testLaunch.gameObject.SetActive(RuntimeFeatureFlags.ShowTestFeatures);
            testLaunchButton = testLaunch.GetComponent<Button>();
            if (RuntimeFeatureFlags.ShowTestFeatures) BindButton(testLaunchButton, navigator.LoadGameTestLauncher);
        }

        void BindCharacterSelectionActions()
        {
            if (characterPanelButton == null) characterPanelButton = FindButton("Character Knight");
            BindButton(characterPanelButton, OpenCharacterSelection);
            BindButton(characterSelectionCancelButton, CloseCharacterSelection);
            BindCharacterChoice(knightCharacterButton, CharacterType.Knight);
            BindCharacterChoice(archerCharacterButton, CharacterType.Archer);
            BindCharacterChoice(mageCharacterButton, CharacterType.Mage);
        }

        void BindCharacterChoice(Button button, CharacterType type)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                AudioManager.PlayButtonConfirm();
                SelectCharacter(type);
            });
        }

        void InitializeCharacterSelection()
        {
            characterSelectionOpen = false;
            lobbySelectableStates.Clear();
            if (characterSelectionModal != null) characterSelectionModal.SetActive(false);
            RefreshCharacterChoiceAvailability();
        }

        void OpenCharacterSelection()
        {
            if (characterSelectionOpen || characterSelectionModal == null) return;
            characterSelectionOpen = true;
            characterSelectionModal.SetActive(true);
            LockLobbyInteraction();
            RefreshCharacterChoiceAvailability();
            ConfigureCharacterChoiceNavigation();
            SelectCharacterChoice(RunState.SelectedCharacter);
        }

        void SelectCharacter(CharacterType type)
        {
            if (!CharacterUnlockCatalog.IsUnlocked(type)) return;
            RunState.SelectedCharacter = type;
            ProgressionStore.Data.selectedCharacter = type;
            ProgressionStore.Save();
            RefreshCharacterDisplay();
            CloseCharacterSelection();
        }

        void CloseCharacterSelection()
        {
            if (!characterSelectionOpen) return;
            characterSelectionOpen = false;
            RestoreLobbyInteraction();
            if (characterSelectionModal != null) characterSelectionModal.SetActive(false);
            ConfigureLobbyNavigation();
            if (EventSystem.current != null && UiSelectionUtility.IsSelectable(characterPanelButton))
            {
                EventSystem.current.SetSelectedGameObject(characterPanelButton.gameObject);
            }
        }

        void LockLobbyInteraction()
        {
            lobbySelectableStates.Clear();
            if (lobbyUi == null) return;
            var selectables = lobbyUi.GetComponentsInChildren<Selectable>(true);
            foreach (var selectable in selectables)
            {
                if (selectable == null || IsCharacterModalControl(selectable)) continue;
                lobbySelectableStates[selectable] = selectable.interactable;
                selectable.interactable = false;
            }
        }

        void RestoreLobbyInteraction()
        {
            foreach (var pair in lobbySelectableStates)
            {
                if (pair.Key != null) pair.Key.interactable = pair.Value;
            }
            lobbySelectableStates.Clear();
        }

        bool IsCharacterModalControl(Selectable selectable)
        {
            return selectable == characterSelectionCancelButton ||
                selectable == knightCharacterButton ||
                selectable == archerCharacterButton ||
                selectable == mageCharacterButton;
        }

        void RefreshCharacterChoiceAvailability()
        {
            ApplyCharacterChoiceAvailability(
                knightCharacterButton,
                knightCharacterChoiceIcon,
                CharacterUnlockCatalog.IsUnlocked(CharacterType.Knight));
            ApplyCharacterChoiceAvailability(
                archerCharacterButton,
                archerCharacterChoiceIcon,
                CharacterUnlockCatalog.IsUnlocked(CharacterType.Archer));
            ApplyCharacterChoiceAvailability(
                mageCharacterButton,
                mageCharacterChoiceIcon,
                CharacterUnlockCatalog.IsUnlocked(CharacterType.Mage));
        }

        static void ApplyCharacterChoiceAvailability(Button button, Image icon, bool unlocked)
        {
            if (button != null) button.interactable = unlocked;
            if (icon != null) icon.color = unlocked ? UnlockedColor : Color.black;
        }

        void SelectCharacterChoice(CharacterType type)
        {
            var selected = type == CharacterType.Archer
                ? archerCharacterButton
                : type == CharacterType.Mage
                    ? mageCharacterButton
                    : knightCharacterButton;
            if (EventSystem.current != null && UiSelectionUtility.IsSelectable(selected))
            {
                EventSystem.current.SetSelectedGameObject(selected.gameObject);
            }
        }

        void LoadSelectedCharacter()
        {
            var selected = ProgressionStore.Data.selectedCharacter;
            if ((selected != CharacterType.Knight && selected != CharacterType.Archer && selected != CharacterType.Mage) ||
                !CharacterUnlockCatalog.IsUnlocked(selected))
            {
                selected = CharacterType.Knight;
                ProgressionStore.Data.selectedCharacter = selected;
                ProgressionStore.Save();
            }
            RunState.SelectedCharacter = selected;
        }

        void RefreshCharacterDisplay()
        {
            var selected = RunState.SelectedCharacter;
            SetImageActive(knightDisplayIcon, selected == CharacterType.Knight);
            SetImageActive(archerDisplayIcon, selected == CharacterType.Archer);
            SetImageActive(mageDisplayIcon, selected == CharacterType.Mage);
            if (characterNameText != null) characterNameText.text = LocalizationService.LocalizeSource(CharacterName(selected));
            if (characterDescriptionText != null) characterDescriptionText.text = LocalizationService.LocalizeSource(CharacterDescription(selected));
        }

        static void SetImageActive(Image image, bool active)
        {
            if (image != null) image.gameObject.SetActive(active);
        }

        static string CharacterName(CharacterType type)
        {
            if (type == CharacterType.Archer) return "アーチャー";
            if (type == CharacterType.Mage) return "メイジ";
            return "ナイト";
        }

        static string CharacterDescription(CharacterType type)
        {
            if (type == CharacterType.Archer) return "遠くの敵を射抜く";
            if (type == CharacterType.Mage) return "魔法で敵を焼き払う";
            return "前方を切り払う";
        }

        void DisableStatPanelFocus()
        {
            DisableNonInteractivePanelFocus("Token Panel");
            DisableNonInteractivePanelFocus("Kill Panel");
            DisableNonInteractivePanelFocus("Play Panel");
        }

        void DisableNonInteractivePanelFocus(string name)
        {
            var panel = FindChild(name);
            if (panel == null) return;

            var pointerSelection = panel.GetComponent<SelectOnPointerEnter>();
            if (pointerSelection != null) pointerSelection.enabled = false;

            var selection = panel.GetComponent<UiSelectionHighlight>();
            if (selection != null)
            {
                selection.SetForceSelected(false);
                selection.enabled = false;
            }

            var button = panel.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.transition = Selectable.Transition.None;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.None;
                button.navigation = navigation;
                button.interactable = false;
            }

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == panel.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            HideGeneratedSelectionVisuals(panel);
        }

        static void HideGeneratedSelectionVisuals(Transform root)
        {
            SetActive(root, "State Fill", false);
            for (int i = 0; i < 4; i++)
            {
                SetActive(root, "Selected Edge " + i, false);
                SetActive(root, "Selected Shadow " + i, false);
            }
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
                        AudioManager.PlayButtonConfirm();
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
                RefreshStageDifficulty(panel, stage, cleared);
            }
        }

        void RefreshStageDifficulty(Transform panel, int stage, bool cleared)
        {
            var root = FindChild(panel, "Difficulty Root");
            if (root != null) root.gameObject.SetActive(cleared);
            if (!cleared) return;

            int difficulty = ProgressionStore.GetStageDifficulty(stage);
            int maxUnlockedDifficulty = ProgressionStore.GetStageMaxUnlockedDifficulty(stage);
            SetText(panel, "Difficulty Label", "\u96e3\u6613\u5ea6" + difficulty);
            ConfigureDifficultyButton(panel, "Difficulty Down Button", stage, difficulty - 1, difficulty > ProgressionStore.MinStageDifficulty);
            ConfigureDifficultyButton(panel, "Difficulty Up Button", stage, difficulty + 1, difficulty < maxUnlockedDifficulty);
        }

        void ConfigureDifficultyButton(Transform panel, string name, int stage, int nextDifficulty, bool visible)
        {
            var buttonTransform = FindChild(panel, name);
            if (buttonTransform == null) return;
            buttonTransform.gameObject.SetActive(visible);
            var button = buttonTransform.GetComponent<Button>();
            if (button == null) return;
            EnsureButtonFocus(button);
            button.onClick.RemoveAllListeners();
            if (!visible) return;
            button.interactable = true;
            button.onClick.AddListener(() =>
            {
                AudioManager.PlayButtonConfirm();
                ProgressionStore.SetStageDifficulty(stage, nextDifficulty);
                RefreshStageCards();
                ConfigureLobbyNavigation();
            });
        }

        void StartSelectedStage()
        {
            StartGameFromStage(ProgressionStore.SelectedStage);
        }

        void StartGameFromStage(int stage)
        {
            if (isStartingGame) return;
            if (!ProgressionStore.IsStageUnlocked(stage)) return;
            isStartingGame = true;
            StartCoroutine(LoadGameAfterFade(stage));
        }

        IEnumerator LoadGameAfterFade(int stage)
        {
            if (screenFade != null)
            {
                yield return screenFade.FadeToBlack();
            }
            else
            {
                Debug.LogError("LobbyScreen requires a Scene-authored ScreenFadeOverlay.");
            }

            ProgressionStore.IncrementPlayCount();
            RunState.SetNextStartStage(stage);
            navigator.LoadGame();
        }

        void BindButton(string name, UnityEngine.Events.UnityAction action)
        {
            var button = FindChild(name)?.GetComponent<Button>();
            BindButton(button, action);
        }

        Button FindButton(string name)
        {
            return FindChild(name)?.GetComponent<Button>();
        }

        bool BindButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return false;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                AudioManager.PlayButtonConfirm();
                action();
            });
            return true;
        }

        void SelectDefaultButton()
        {
            var candidates = SelectionCandidates();
            UiSelectionUtility.SelectFirst(candidates);
        }

        Selectable[] SelectionCandidates()
        {
            if (characterSelectionOpen)
            {
                var characterChoices = new List<Selectable>();
                AddCandidate(characterChoices, knightCharacterButton);
                AddCandidate(characterChoices, archerCharacterButton);
                AddCandidate(characterChoices, mageCharacterButton);
                return characterChoices.ToArray();
            }

            var candidates = new List<Selectable>
            {
                upgradeButton,
                startGameButton,
                weaponBookButton,
                relicButton,
                testLaunchButton,
                titleButton,
                characterPanelButton
            };

            for (int stage = 1; stage <= StagePanelCount; stage++)
            {
                var panel = FindChild("Stage " + stage + " Panel");
                var stageButton = panel != null ? panel.GetComponent<Button>() : null;
                if (UiSelectionUtility.IsSelectable(stageButton)) candidates.Add(stageButton);

                AddCandidate(candidates, FindChild(panel, "Difficulty Down Button")?.GetComponent<Button>());
                AddCandidate(candidates, FindChild(panel, "Difficulty Up Button")?.GetComponent<Button>());
            }

            return candidates.ToArray();
        }

        void ConfigureLobbyNavigation()
        {
            if (characterSelectionOpen)
            {
                ConfigureCharacterChoiceNavigation();
                return;
            }
            UiSelectionUtility.ConfigureDirectionalNavigation(SelectionCandidates());
            ConfigureDifficultyPairNavigation();
        }

        void ConfigureCharacterChoiceNavigation()
        {
            var choices = new List<Button>();
            if (UiSelectionUtility.IsSelectable(knightCharacterButton)) choices.Add(knightCharacterButton);
            if (UiSelectionUtility.IsSelectable(archerCharacterButton)) choices.Add(archerCharacterButton);
            if (UiSelectionUtility.IsSelectable(mageCharacterButton)) choices.Add(mageCharacterButton);

            for (int i = 0; i < choices.Count; i++)
            {
                var up = i > 0 ? choices[i - 1] : choices[i];
                var down = i + 1 < choices.Count ? choices[i + 1] : choices[i];
                ConfigureCharacterChoiceNavigation(choices[i], up, down);
            }
        }

        static void ConfigureCharacterChoiceNavigation(Button button, Selectable up, Selectable down)
        {
            if (button == null) return;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.wrapAround = false;
            navigation.selectOnUp = up;
            navigation.selectOnDown = down;
            navigation.selectOnLeft = button;
            navigation.selectOnRight = button;
            button.navigation = navigation;
        }

        void ConfigureDifficultyPairNavigation()
        {
            for (int stage = 1; stage <= StagePanelCount; stage++)
            {
                var panel = FindChild("Stage " + stage + " Panel");
                var downButton = FindChild(panel, "Difficulty Down Button")?.GetComponent<Button>();
                var upButton = FindChild(panel, "Difficulty Up Button")?.GetComponent<Button>();
                if (!UiSelectionUtility.IsSelectable(downButton) || !UiSelectionUtility.IsSelectable(upButton)) continue;

                var downNavigation = downButton.navigation;
                downNavigation.mode = Navigation.Mode.Explicit;
                downNavigation.selectOnRight = upButton;
                downButton.navigation = downNavigation;

                var upNavigation = upButton.navigation;
                upNavigation.mode = Navigation.Mode.Explicit;
                upNavigation.selectOnLeft = downButton;
                upButton.navigation = upNavigation;
            }
        }

        static void AddCandidate(List<Selectable> candidates, Selectable selectable)
        {
            if (UiSelectionUtility.IsSelectable(selectable)) candidates.Add(selectable);
        }

        static void EnsureButtonFocus(Button button)
        {
            if (button == null) return;
            button.transition = Selectable.Transition.None;
            var highlight = button.GetComponent<UiSelectionHighlight>();
            if (highlight == null) highlight = button.gameObject.AddComponent<UiSelectionHighlight>();
            highlight.padding = 6f;
            highlight.thickness = 4f;
            highlight.enabled = true;
            if (button.GetComponent<SelectOnPointerEnter>() == null) button.gameObject.AddComponent<SelectOnPointerEnter>();
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
            if (text != null) text.text = LocalizationService.LocalizeSource(value);
        }

        static void SetText(Transform root, string name, string value)
        {
            var text = FindChild(root, name)?.GetComponent<Text>();
            if (text != null) text.text = LocalizationService.LocalizeSource(value);
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
