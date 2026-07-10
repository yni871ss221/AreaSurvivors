using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class GameTestLaunchScreen : MonoBehaviour
    {
        static readonly Color RelicOwnedButtonColor = new Color(0.34f, 0.39f, 0.14f, 0.98f);
        static readonly Color RelicUnownedButtonColor = new Color(0.055f, 0.075f, 0.07f, 0.94f);
        static readonly Color StageClearedButtonColor = new Color(0.25f, 0.38f, 0.16f, 0.98f);
        static readonly Color StageUnclearedButtonColor = new Color(0.08f, 0.13f, 0.12f, 0.94f);

        SceneNavigator navigator;
        Text statusText;
        Button defaultButton;

        void Start()
        {
            AudioManager.PlayBgm(BgmTrack.LobbyUpgrades);

            navigator = GetComponent<SceneNavigator>();
            if (navigator == null) navigator = gameObject.AddComponent<SceneNavigator>();
            statusText = FindChild("Test Status Text")?.GetComponent<Text>();
            if (!RuntimeFeatureFlags.ShowTestFeatures)
            {
                HideTestControlsForReleaseBuild();
                BindButton("Lobby Button", navigator.LoadLobby);
                RefreshStatus("製品版ビルドではテスト操作は利用できません");
                SelectDefaultButton();
                return;
            }

            BindButton("Start Stage 1 Boss Test Button", StartStageOneBossTest);
            for (int stage = 1; stage <= 4; stage++)
            {
                int capturedStage = stage;
                BindButton(StageClearToggleButtonName(capturedStage), () => ToggleStageClearStateForTesting(capturedStage));
                foreach (BossTestSpawnSide side in System.Enum.GetValues(typeof(BossTestSpawnSide)))
                {
                    var capturedSide = side;
                    BindButton(BossTestButtonName(capturedStage, capturedSide), () => StartBossTest(capturedStage, capturedSide));
                }
            }
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
                BindButton(RelicToggleButtonName(capturedType), () => ToggleRelicForTesting(capturedType));
                BindButton(RelicUnlockButtonName(capturedType), () => ToggleRelicForTesting(capturedType));
                BindButton(RelicLockButtonName(capturedType), () => LockRelicForTesting(capturedType));
            }
            BindButton("Add Test Tokens Button", AddTestTokens);
            BindButton("Reset Upgrades Button", ResetUpgradesForTesting);
            BindButton("Reset Stage Clear State Button", ResetStageClearStateForTesting);
            BindButton("Reset All Relics Button", ResetRelicsForTesting);
            BindButton("Lobby Button", navigator.LoadLobby);
            RefreshStageClearButtonLabels();
            RefreshRelicButtonLabels();
            RefreshStatus("テスト操作を選択できます");
            SelectDefaultButton();
        }

        void Update()
        {
            var candidates = AvailableButtons();
            if (UiSelectionUtility.TickControllerSubmit(candidates)) return;
            if (UiSelectionUtility.CancelPressed())
            {
                AudioManager.PlayButtonConfirm();
                navigator.LoadLobby();
                return;
            }

            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            UiSelectionUtility.EnsureSelection(candidates);
        }

        void HideTestControlsForReleaseBuild()
        {
            var buttons = FindObjectsOfType<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                bool isLobbyButton = buttons[i].name == "Lobby Button";
                buttons[i].gameObject.SetActive(isLobbyButton);
            }
        }

        void StartGameFromStageForTesting(int stage)
        {
            RunState.SetNextStartStage(stage);
            navigator.LoadGame();
        }

        void StartStageOneBossTest()
        {
            RunState.SetNextStartStage(1);
            RunState.SetNextStartStageElapsed(119f);
            navigator.LoadGame();
        }

        void StartBossTest(int stage, BossTestSpawnSide side)
        {
            RunState.SetNextStartStage(stage);
            RunState.SetNextStartStageElapsed(119f);
            RunState.SetNextBossTestSpawnSide(side);
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
            RefreshStageClearButtonLabels();
            RefreshStatus("ステージクリア状態を初期化しました");
        }

        void ToggleStageClearStateForTesting(int stage)
        {
            bool cleared = ProgressionStore.ToggleStageClearedForTesting(stage);
            RefreshStageClearButtonLabels();
            RefreshStatus("STAGE " + stage + " を" + (cleared ? "クリア済みにしました" : "未クリアに戻しました"));
        }

        void UnlockRelicForTesting(RelicType relicType)
        {
            if (!RelicCatalog.TryGet(relicType, out var definition)) return;
            bool changed = ProgressionStore.UnlockRelic(relicType);
            RefreshRelicButtonLabels();
            RefreshStatus(changed ? definition.displayName + " を取得済みにしました" : definition.displayName + " は既に取得済みです");
        }

        void ToggleRelicForTesting(RelicType relicType)
        {
            if (!RelicCatalog.TryGet(relicType, out var definition)) return;
            bool changed = ProgressionStore.ToggleRelicForTesting(relicType, out bool isOwned);
            RefreshRelicButtonLabels();
            Debug.Log("[Relic Test] " + definition.displayName
                + " -> " + (ProgressionStore.HasRelic(relicType) ? "取得済" : "未取得")
                + " / 所持レリック " + CountOwnedRelics() + "/" + RelicCatalog.All.Length);
            if (!changed)
            {
                RefreshStatus(definition.displayName + " の取得状態を変更できませんでした");
                return;
            }

            RefreshStatus(isOwned
                ? definition.displayName + " を取得済みにしました"
                : definition.displayName + " を未取得に戻しました");
        }

        void LockRelicForTesting(RelicType relicType)
        {
            if (!RelicCatalog.TryGet(relicType, out var definition)) return;
            bool changed = ProgressionStore.LockRelicForTesting(relicType);
            RefreshRelicButtonLabels();
            RefreshStatus(changed ? definition.displayName + " を未取得に戻しました" : definition.displayName + " は既に未取得です");
        }

        void ResetRelicsForTesting()
        {
            ProgressionStore.ResetRelicsForTesting();
            RefreshRelicButtonLabels();
            RefreshStatus("全レリックを未取得に戻しました");
        }

        void RefreshRelicButtonLabels()
        {
            var buttons = FindObjectsOfType<Button>(true);
            foreach (var relic in RelicCatalog.GetDisplayOrdered())
            {
                if (relic == null) continue;
                bool owned = ProgressionStore.HasRelic(relic.type);
                string state = owned ? "【取得済】" : "【未取得】";
                Color buttonColor = owned ? RelicOwnedButtonColor : RelicUnownedButtonColor;
                string coloredName = ColorText(relic.displayName, RelicRarityVisuals.GetColor(relic.rarity));
                SetButtonLabel(buttons, RelicToggleButtonName(relic.type), state + "\n" + coloredName, buttonColor);
                SetButtonLabel(buttons, RelicUnlockButtonName(relic.type), "切替: " + state + " / " + coloredName, buttonColor);
                SetButtonLabel(buttons, RelicLockButtonName(relic.type), "未取得に戻す: " + coloredName, RelicUnownedButtonColor);
            }

            Canvas.ForceUpdateCanvases();
        }

        void RefreshStageClearButtonLabels()
        {
            var buttons = FindObjectsOfType<Button>(true);
            for (int stage = 1; stage <= 4; stage++)
            {
                bool cleared = ProgressionStore.IsStageCleared(stage);
                string state = cleared ? "クリア済" : "未クリア";
                Color buttonColor = cleared ? StageClearedButtonColor : StageUnclearedButtonColor;
                SetButtonLabel(buttons, StageClearToggleButtonName(stage), "STAGE " + stage + "\n" + state, buttonColor);
            }

            Canvas.ForceUpdateCanvases();
        }

        void RefreshStatus(string message)
        {
            if (statusText == null) return;
            int ownedRelics = CountOwnedRelics();

            statusText.text = message
                + "\n所持トークン: " + ProgressionStore.Data.tokens
                + " / クリア: " + ProgressionStore.Data.highestClearedStage + "/4"
                + " / 所持レリック: " + ownedRelics + "/" + RelicCatalog.All.Length;
        }

        static int CountOwnedRelics()
        {
            int ownedRelics = 0;
            foreach (var relic in RelicCatalog.All)
            {
                if (relic != null && ProgressionStore.HasRelic(relic.type)) ownedRelics++;
            }

            return ownedRelics;
        }

        void BindButton(string name, UnityEngine.Events.UnityAction action)
        {
            var button = FindChild(name)?.GetComponent<Button>();
            if (button == null) return;
            if (defaultButton == null && UiSelectionUtility.IsSelectable(button)) defaultButton = button;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                AudioManager.PlayButtonConfirm();
                action();
            });
        }

        void SelectDefaultButton()
        {
            if (!UiSelectionUtility.IsSelectable(defaultButton)) defaultButton = FirstAvailableButton();
            UiSelectionUtility.ConfigureVerticalNavigation(AvailableButtons());
            UiSelectionUtility.SelectFirst(defaultButton);
        }

        static Button FirstAvailableButton()
        {
            var buttons = FindObjectsOfType<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (UiSelectionUtility.IsSelectable(buttons[i])) return buttons[i];
            }

            return null;
        }

        static Selectable[] AvailableButtons()
        {
            var buttons = FindObjectsOfType<Button>(true);
            var selectables = new System.Collections.Generic.List<Selectable>();
            for (int i = 0; i < buttons.Length; i++)
            {
                if (UiSelectionUtility.IsSelectable(buttons[i])) selectables.Add(buttons[i]);
            }

            return selectables.ToArray();
        }

        void SetButtonLabel(Button[] buttons, string buttonName, string label, Color color)
        {
            if (buttons == null || string.IsNullOrEmpty(buttonName)) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                if (button == null || button.name != buttonName) continue;
                ApplyButtonBackground(button, color);
                var text = button.GetComponentInChildren<Text>(true);
                if (text != null)
                {
                    text.supportRichText = true;
                    text.fontSize = 14;
                    text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = 10;
                    text.resizeTextMaxSize = 14;
                    text.horizontalOverflow = HorizontalWrapMode.Wrap;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    text.text = label;
                }
            }
        }

        static void ApplyButtonBackground(Button button, Color color)
        {
            if (button == null) return;
            var image = button.GetComponent<Image>();
            if (image != null) image.color = color;

            var highlight = button.GetComponent<UiSelectionHighlight>();
            if (highlight != null) highlight.SetNormalBackgroundColor(color);
        }

        static string ColorText(string value, Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(color) + ">" + value + "</color>";
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

        public static string BossTestButtonName(int stage, BossTestSpawnSide side)
        {
            return "Start Stage " + Mathf.Clamp(stage, 1, 4) + " Boss " + side + " Test Button";
        }

        public static string StageClearToggleButtonName(int stage)
        {
            return "Toggle Stage " + Mathf.Clamp(stage, 1, 4) + " Clear State Button";
        }

        public static string RelicUnlockButtonName(RelicType relicType)
        {
            return "Unlock Relic " + relicType + " Button";
        }

        public static string RelicLockButtonName(RelicType relicType)
        {
            return "Lock Relic " + relicType + " Button";
        }

        public static string RelicToggleButtonName(RelicType relicType)
        {
            return "Toggle Relic " + relicType + " Button";
        }
    }
}
