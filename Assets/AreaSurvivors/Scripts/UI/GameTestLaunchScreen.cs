using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class GameTestLaunchScreen : MonoBehaviour
    {
        SceneNavigator navigator;
        Text statusText;

        void Start()
        {
            AudioManager.PlayBgm(BgmTrack.LobbyUpgrades);

            navigator = GetComponent<SceneNavigator>();
            if (navigator == null) navigator = gameObject.AddComponent<SceneNavigator>();
            statusText = FindChild("Test Status Text")?.GetComponent<Text>();

            BindButton("Start Stage 2 Test Button", () => StartGameFromStageForTesting(2));
            BindButton("Start Stage 3 Test Button", () => StartGameFromStageForTesting(3));
            BindButton("Start Stage 4 Test Button", () => StartGameFromStageForTesting(4));
            foreach (var weaponType in WeaponCatalog.TestableWeapons)
            {
                var capturedType = weaponType;
                BindButton(WeaponTestButtonName(capturedType), () => StartWeaponTest(capturedType));
            }
            foreach (var relic in RelicCatalog.All)
            {
                var capturedType = relic.type;
                BindButton(RelicUnlockButtonName(capturedType), () => UnlockRelicForTesting(capturedType));
                BindButton(RelicLockButtonName(capturedType), () => LockRelicForTesting(capturedType));
            }
            BindButton("Add Test Tokens Button", AddTestTokens);
            BindButton("Reset Upgrades Button", ResetUpgradesForTesting);
            BindButton("Reset Stage Clear State Button", ResetStageClearStateForTesting);
            BindButton("Reset All Relics Button", ResetRelicsForTesting);
            BindButton("Lobby Button", navigator.LoadLobby);
            RefreshStatus("テスト操作を選択できます");
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

        void AddTestTokens()
        {
            ProgressionStore.AddTokensForTesting(99999);
            RefreshStatus("トークンを +99999 しました");
        }

        void ResetUpgradesForTesting()
        {
            ProgressionStore.ResetUpgradesForTesting();
            RefreshStatus("強化状態を初期化しました");
        }

        void ResetStageClearStateForTesting()
        {
            ProgressionStore.ResetStageClearStateForTesting();
            RefreshStatus("ステージクリア状態を初期化しました");
        }

        void UnlockRelicForTesting(RelicType relicType)
        {
            if (!RelicCatalog.TryGet(relicType, out var definition)) return;
            bool changed = ProgressionStore.UnlockRelic(relicType);
            RefreshStatus(changed ? definition.displayName + " を取得済みにしました" : definition.displayName + " は既に取得済みです");
        }

        void LockRelicForTesting(RelicType relicType)
        {
            if (!RelicCatalog.TryGet(relicType, out var definition)) return;
            bool changed = ProgressionStore.LockRelicForTesting(relicType);
            RefreshStatus(changed ? definition.displayName + " を未取得に戻しました" : definition.displayName + " は既に未取得です");
        }

        void ResetRelicsForTesting()
        {
            ProgressionStore.ResetRelicsForTesting();
            RefreshStatus("全レリックを未取得に戻しました");
        }

        void RefreshStatus(string message)
        {
            if (statusText == null) return;
            int ownedRelics = 0;
            foreach (var relic in RelicCatalog.All)
            {
                if (ProgressionStore.HasRelic(relic.type)) ownedRelics++;
            }

            statusText.text = message
                + "\n所持トークン: " + ProgressionStore.Data.tokens
                + " / クリア: " + ProgressionStore.Data.highestClearedStage + "/4"
                + " / 所持レリック: " + ownedRelics + "/" + RelicCatalog.All.Length;
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

        public static string RelicUnlockButtonName(RelicType relicType)
        {
            return "Unlock Relic " + relicType + " Button";
        }

        public static string RelicLockButtonName(RelicType relicType)
        {
            return "Lock Relic " + relicType + " Button";
        }
    }
}
