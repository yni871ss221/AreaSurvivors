using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors
{
    public sealed class SceneNavigator : MonoBehaviour
    {
        public void LoadTitle() => SceneManager.LoadScene(SceneNames.Title);
        public void LoadOptions() => SceneManager.LoadScene(SceneNames.Options);
        public void LoadLobby() => SceneManager.LoadScene(SceneNames.Lobby);
        public void LoadUpgrades() => SceneManager.LoadScene(SceneNames.Upgrades);
        public void LoadGame() => SceneManager.LoadScene(SceneNames.Game);
        public void LoadWeaponBook() => SceneManager.LoadScene(SceneNames.WeaponBook);
        public void LoadGameTestLauncher() => SceneManager.LoadScene(SceneNames.GameTestLauncher);
        public void LoadRelics() => SceneManager.LoadScene(SceneNames.Relics);
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
