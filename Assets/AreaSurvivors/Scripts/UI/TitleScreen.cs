using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class TitleScreen : MonoBehaviour
    {
        public SceneNavigator navigator;
        public Button playButton;
        public Button optionsButton;
        public Button creditsButton;
        public Button quitButton;
        public GameObject creditsPanel;
        public Button creditsCloseButton;

        void Start()
        {
            if (!StudioLogoIntro.IsPlaying) AudioManager.PlayBgm(BgmTrack.TitleOptions);
            HideCredits(false);

            if (navigator == null) navigator = GetComponent<SceneNavigator>();
            if (navigator == null)
            {
                Debug.LogError("TitleScreen requires a SceneNavigator reference.");
                return;
            }

            BindButton(playButton, navigator.LoadLobby, "Play Button");
            BindButton(optionsButton, navigator.LoadOptions, "Options Button");
            BindButton(creditsButton, ShowCredits, "Credits Button");
            BindButton(creditsCloseButton, HideCredits, "Credits Close Button");
            BindButton(quitButton, navigator.Quit, "Quit Button");
            var candidates = SelectionCandidates();
            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            if (!StudioLogoIntro.IsPlaying) UiSelectionUtility.SelectFirst(candidates);
        }

        void Update()
        {
            if (StudioLogoIntro.IsPlaying) return;

            var candidates = SelectionCandidates();
            if (UiSelectionUtility.TickControllerSubmit(candidates)) return;
            if (creditsPanel != null && creditsPanel.activeSelf && UiSelectionUtility.CancelPressed())
            {
                HideCredits();
                return;
            }

            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            UiSelectionUtility.EnsureSelection(candidates);
        }

        Selectable[] SelectionCandidates()
        {
            if (creditsPanel != null && creditsPanel.activeSelf)
            {
                return new Selectable[] { creditsCloseButton };
            }

            return new Selectable[] { playButton, optionsButton, creditsButton, quitButton };
        }

        void ShowCredits()
        {
            if (creditsPanel == null)
            {
                Debug.LogError("TitleScreen is missing Credits Panel.");
                return;
            }

            creditsPanel.SetActive(true);
            UiSelectionUtility.SelectFirst(creditsCloseButton);
        }

        void HideCredits()
        {
            HideCredits(true);
        }

        void HideCredits(bool restoreSelection)
        {
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (restoreSelection) UiSelectionUtility.SelectFirst(creditsButton, optionsButton, playButton);
        }

        void BindButton(Button button, UnityAction action, string name)
        {
            if (button == null)
            {
                Debug.LogError($"TitleScreen is missing {name}.");
                return;
            }

            button.onClick.AddListener(() =>
            {
                AudioManager.PlayButtonConfirm();
                action.Invoke();
            });
        }
    }
}
