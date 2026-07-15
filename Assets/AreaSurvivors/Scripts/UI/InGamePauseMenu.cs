using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class InGamePauseMenu : MonoBehaviour
    {
        public GameObject menuPanel;
        public GameObject optionsPanel;
        public GameObject abandonDialog;
        public GameObject pauseBackdrop;
        public GeneralOptionsPanel generalOptionsPanel;
        public AudioOptionsPanel audioOptionsPanel;
        public DisplayOptionsPanel displayOptionsPanel;
        public Text controlMoveText;
        public Text controlAttackText;
        public Button controlMoveRebindButton;
        public Text controlMoveUpText;
        public Text controlMoveDownText;
        public Text controlMoveLeftText;
        public Text controlMoveRightText;
        public Button controlMoveUpButton;
        public Button controlMoveDownButton;
        public Button controlMoveLeftButton;
        public Button controlMoveRightButton;
        public InputField controlMoveUpInput;
        public InputField controlMoveDownInput;
        public InputField controlMoveLeftInput;
        public InputField controlMoveRightInput;
        public InputField controlMoveUpAlternateInput;
        public InputField controlMoveDownAlternateInput;
        public InputField controlMoveLeftAlternateInput;
        public InputField controlMoveRightAlternateInput;
        public InputField controllerMoveUpInput;
        public InputField controllerMoveDownInput;
        public InputField controllerMoveLeftInput;
        public InputField controllerMoveRightInput;
        public InputField controllerMoveUpAlternateInput;
        public InputField controllerMoveDownAlternateInput;
        public InputField controllerMoveLeftAlternateInput;
        public InputField controllerMoveRightAlternateInput;
        public InputField controllerSubmitInput;
        public InputField controllerCancelInput;
        public Button resetOptionsButton;
        public Button optionsButton;
        public Button abandonButton;
        public Button resumeButton;
        public Button confirmBackButton;
        public Button confirmAbandonButton;

        bool pausedByMenu;
        GameHudController gameHud;
        readonly KeyboardMouseControlOptionsBinding controlBinding = new KeyboardMouseControlOptionsBinding();
        readonly ControllerControlOptionsBinding controllerBinding = new ControllerControlOptionsBinding();

        void Start()
        {
            BindButtons();
            HideAll();
            SetPauseDetailsVisible(false);
        }

        void Update()
        {
            if (pausedByMenu && optionsPanel != null && optionsPanel.activeSelf && (controlBinding.Tick() || controllerBinding.Tick())) return;
            if (pausedByMenu)
            {
                var candidates = ActivePanelSelectionCandidates();
                if (UiSelectionUtility.TickControllerSubmit(candidates)) return;
                EnsureActivePanelSelection();
                if (!UiSelectionUtility.CancelPressed() && !UiSelectionUtility.PausePressed()) return;

                if (abandonDialog != null && abandonDialog.activeSelf)
                {
                    ShowMainMenu();
                    return;
                }

                if (optionsPanel != null && optionsPanel.activeSelf)
                {
                    ShowMainMenu();
                    return;
                }

                ResumeGame();
                return;
            }

            if (!UiSelectionUtility.PausePressed()) return;
            if (Time.timeScale <= 0f) return;
            OpenMenu();
        }

        void BindButtons()
        {
            Bind(optionsButton, ShowOptions);
            Bind(abandonButton, ShowAbandonDialog);
            Bind(resumeButton, ResumeGame);
            Bind(confirmBackButton, ShowMainMenu);
            Bind(confirmAbandonButton, AbandonRun);
            Bind(resetOptionsButton, ResetAllOptions);
            if (generalOptionsPanel != null) generalOptionsPanel.Bind();
            if (audioOptionsPanel != null) audioOptionsPanel.Bind(ShowMainMenu);
            if (displayOptionsPanel != null) displayOptionsPanel.Bind();
            controlBinding.Bind(
                controlMoveText,
                controlAttackText,
                controlMoveRebindButton,
                new[] { controlMoveUpText, controlMoveDownText, controlMoveLeftText, controlMoveRightText },
                new[] { controlMoveUpButton, controlMoveDownButton, controlMoveLeftButton, controlMoveRightButton },
                new[] { controlMoveUpInput, controlMoveDownInput, controlMoveLeftInput, controlMoveRightInput },
                new[] { controlMoveUpAlternateInput, controlMoveDownAlternateInput, controlMoveLeftAlternateInput, controlMoveRightAlternateInput });
            controllerBinding.Bind(
                new[] { controllerMoveUpInput, controllerMoveDownInput, controllerMoveLeftInput, controllerMoveRightInput },
                new[] { controllerMoveUpAlternateInput, controllerMoveDownAlternateInput, controllerMoveLeftAlternateInput, controllerMoveRightAlternateInput },
                controllerSubmitInput,
                controllerCancelInput);
        }

        static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                AudioManager.PlayButtonConfirm();
                action();
            });
        }

        void OpenMenu()
        {
            pausedByMenu = true;
            Time.timeScale = 0f;
            SetActive(pauseBackdrop, true);
            SetPauseDetailsVisible(true);
            ShowMainMenu();
        }

        void ShowMainMenu()
        {
            SetActive(menuPanel, true);
            SetActive(optionsPanel, false);
            SetActive(abandonDialog, false);
            var candidates = MainMenuSelectionCandidates();
            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            UiSelectionUtility.SelectFirst(candidates);
        }

        void ShowOptions()
        {
            SetActive(menuPanel, false);
            SetActive(optionsPanel, true);
            SetActive(abandonDialog, false);
            var candidates = OptionsSelectionCandidates();
            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            UiSelectionUtility.SelectFirst(candidates);
            ResetOptionScrollToTop();
        }

        void ShowAbandonDialog()
        {
            SetActive(menuPanel, false);
            SetActive(optionsPanel, false);
            SetActive(abandonDialog, true);
            var candidates = AbandonDialogSelectionCandidates();
            UiSelectionUtility.ConfigureHorizontalNavigation(candidates);
            UiSelectionUtility.SelectFirst(candidates);
        }

        void ResumeGame()
        {
            HideAll();
            SetPauseDetailsVisible(false);
            if (pausedByMenu) Time.timeScale = 1f;
            pausedByMenu = false;
        }

        void AbandonRun()
        {
            SetActive(pauseBackdrop, false);
            SetPauseDetailsVisible(false);
            Time.timeScale = 1f;
            pausedByMenu = false;
            SceneManager.LoadScene(SceneNames.Lobby);
        }

        void SetPauseDetailsVisible(bool visible)
        {
            if (gameHud == null) gameHud = FindObjectOfType<GameHudController>();
            if (gameHud != null) gameHud.SetPauseDetailsVisible(visible);
        }

        void ResetAllOptions()
        {
            GeneralOptionsResetter.ResetAll(generalOptionsPanel, audioOptionsPanel, displayOptionsPanel, controlBinding, controllerBinding);
        }

        void HideAll()
        {
            SetActive(pauseBackdrop, false);
            SetActive(menuPanel, false);
            SetActive(optionsPanel, false);
            SetActive(abandonDialog, false);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }

        void EnsureActivePanelSelection()
        {
            var candidates = ActivePanelSelectionCandidates();
            if (abandonDialog != null && abandonDialog.activeSelf)
            {
                UiSelectionUtility.ConfigureHorizontalNavigation(candidates);
                UiSelectionUtility.EnsureSelection(candidates);
                return;
            }

            if (optionsPanel != null && optionsPanel.activeSelf)
            {
                UiSelectionUtility.ConfigureVerticalNavigation(candidates);
                UiSelectionUtility.EnsureSelection(candidates);
                return;
            }

            if (menuPanel != null && menuPanel.activeSelf)
            {
                UiSelectionUtility.ConfigureVerticalNavigation(candidates);
                UiSelectionUtility.EnsureSelection(candidates);
            }
        }

        Selectable[] ActivePanelSelectionCandidates()
        {
            if (abandonDialog != null && abandonDialog.activeSelf) return AbandonDialogSelectionCandidates();
            if (optionsPanel != null && optionsPanel.activeSelf) return OptionsSelectionCandidates();
            if (menuPanel != null && menuPanel.activeSelf) return MainMenuSelectionCandidates();
            return new Selectable[0];
        }

        Selectable[] MainMenuSelectionCandidates()
        {
            return new Selectable[] { resumeButton, optionsButton, abandonButton };
        }

        Selectable[] AbandonDialogSelectionCandidates()
        {
            return new Selectable[] { confirmBackButton, confirmAbandonButton };
        }

        Selectable[] OptionsSelectionCandidates()
        {
            return new Selectable[]
            {
                generalOptionsPanel != null ? generalOptionsPanel.languageDropdown : null,
                audioOptionsPanel != null ? audioOptionsPanel.bgmSlider : null,
                audioOptionsPanel != null ? audioOptionsPanel.sfxSlider : null,
                displayOptionsPanel != null ? displayOptionsPanel.modeDropdown : null,
                displayOptionsPanel != null ? displayOptionsPanel.windowSizeDropdown : null,
                controlMoveUpInput,
                controlMoveUpAlternateInput,
                controlMoveLeftInput,
                controlMoveLeftAlternateInput,
                controlMoveDownInput,
                controlMoveDownAlternateInput,
                controlMoveRightInput,
                controlMoveRightAlternateInput,
                controllerMoveUpInput,
                controllerMoveUpAlternateInput,
                controllerMoveLeftInput,
                controllerMoveLeftAlternateInput,
                controllerMoveDownInput,
                controllerMoveDownAlternateInput,
                controllerMoveRightInput,
                controllerMoveRightAlternateInput,
                controllerSubmitInput,
                controllerCancelInput,
                resetOptionsButton,
                audioOptionsPanel != null ? audioOptionsPanel.backButton : null
            };
        }

        void ResetOptionScrollToTop()
        {
            var scrollController = generalOptionsPanel != null
                ? generalOptionsPanel.GetComponentInParent<OptionsPanelScrollController>()
                : GetComponentInChildren<OptionsPanelScrollController>(true);
            if (scrollController != null) scrollController.ResetToTop();
        }
    }
}
