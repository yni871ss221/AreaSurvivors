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
        public Button quitButton;

        void Start()
        {
            AudioManager.PlayBgm(BgmTrack.TitleOptions);

            if (navigator == null) navigator = GetComponent<SceneNavigator>();
            if (navigator == null)
            {
                Debug.LogError("TitleScreen requires a SceneNavigator reference.");
                return;
            }

            BindButton(playButton, navigator.LoadLobby, "Play Button");
            BindButton(optionsButton, navigator.LoadOptions, "Options Button");
            BindButton(quitButton, navigator.Quit, "Quit Button");
            var candidates = SelectionCandidates();
            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            UiSelectionUtility.SelectFirst(candidates);
        }

        void Update()
        {
            if (UiSelectionUtility.TickControllerSubmit()) return;
            var candidates = SelectionCandidates();
            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            UiSelectionUtility.EnsureSelection(candidates);
        }

        Selectable[] SelectionCandidates()
        {
            return new Selectable[] { playButton, optionsButton, quitButton };
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
