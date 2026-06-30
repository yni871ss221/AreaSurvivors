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
        public AudioOptionsPanel audioOptionsPanel;
        public Button optionsButton;
        public Button abandonButton;
        public Button resumeButton;
        public Button confirmBackButton;
        public Button confirmAbandonButton;

        bool pausedByMenu;

        void Start()
        {
            BindButtons();
            HideAll();
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (pausedByMenu)
            {
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
            if (audioOptionsPanel != null) audioOptionsPanel.Bind(ShowMainMenu);
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
            ShowMainMenu();
        }

        void ShowMainMenu()
        {
            SetActive(menuPanel, true);
            SetActive(optionsPanel, false);
            SetActive(abandonDialog, false);
        }

        void ShowOptions()
        {
            SetActive(menuPanel, false);
            SetActive(optionsPanel, true);
            SetActive(abandonDialog, false);
        }

        void ShowAbandonDialog()
        {
            SetActive(menuPanel, false);
            SetActive(optionsPanel, false);
            SetActive(abandonDialog, true);
        }

        void ResumeGame()
        {
            HideAll();
            if (pausedByMenu) Time.timeScale = 1f;
            pausedByMenu = false;
        }

        void AbandonRun()
        {
            Time.timeScale = 1f;
            pausedByMenu = false;
            SceneManager.LoadScene(SceneNames.Lobby);
        }

        void HideAll()
        {
            SetActive(menuPanel, false);
            SetActive(optionsPanel, false);
            SetActive(abandonDialog, false);
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null) target.SetActive(active);
        }
    }
}
