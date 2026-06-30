using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class GameTestLaunchScreen : MonoBehaviour
    {
        SceneNavigator navigator;

        void Start()
        {
            AudioManager.PlayBgm(BgmTrack.LobbyUpgrades);

            navigator = GetComponent<SceneNavigator>();
            if (navigator == null) navigator = gameObject.AddComponent<SceneNavigator>();

            BindButton("Start Stage 2 Test Button", () => StartGameFromStageForTesting(2));
            BindButton("Start Stage 3 Test Button", () => StartGameFromStageForTesting(3));
            BindButton("Start Stage 4 Test Button", () => StartGameFromStageForTesting(4));
            foreach (var weaponType in WeaponCatalog.TestableWeapons)
            {
                var capturedType = weaponType;
                BindButton(WeaponTestButtonName(capturedType), () => StartWeaponTest(capturedType));
            }
            BindButton("Lobby Button", navigator.LoadLobby);
        }

        void StartGameFromStageForTesting(int stage)
        {
            RunState.SetNextStartStage(stage);
            navigator.LoadGame();
        }

        void StartWeaponTest(WeaponType weaponType)
        {
            RunState.SetNextWeaponTest(weaponType);
            navigator.LoadGame();
        }

        void BindButton(string name, UnityEngine.Events.UnityAction action)
        {
            var button = FindChild(name)?.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                AudioManager.PlayButtonConfirm();
                action();
            });
        }

        static Transform FindChild(string name)
        {
            var canvases = FindObjectsOfType<Canvas>(true);
            foreach (var canvas in canvases)
            {
                var found = FindChild(canvas.transform, name);
                if (found != null) return found;
            }

            return null;
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

        public static string WeaponTestButtonName(WeaponType weaponType)
        {
            return "Start Weapon Test " + weaponType + " Button";
        }
    }
}
