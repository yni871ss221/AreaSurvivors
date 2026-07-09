using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class OptionsScreen : MonoBehaviour
    {
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
        public Button resetDataButton;
        public GameObject resetDataDialog;
        public Button resetDataOkButton;
        public Button resetDataCancelButton;
        public Button resetOptionsButton;
        public SceneNavigator navigator;

        readonly KeyboardMouseControlOptionsBinding controlBinding = new KeyboardMouseControlOptionsBinding();
        readonly ControllerControlOptionsBinding controllerBinding = new ControllerControlOptionsBinding();

        void Start()
        {
            AudioManager.PlayBgm(BgmTrack.TitleOptions);

            if (navigator == null) navigator = GetComponent<SceneNavigator>();
            if (generalOptionsPanel == null || audioOptionsPanel == null || displayOptionsPanel == null || navigator == null)
            {
                Debug.LogError("OptionsScreen requires Scene-authored option panel and SceneNavigator references.");
                enabled = false;
                return;
            }

            generalOptionsPanel.Bind();
            audioOptionsPanel.Bind(navigator.LoadTitle);
            displayOptionsPanel.Bind();
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
            BindResetDataButton();
            BindResetButton();
            HideResetDataDialog();
            SelectDefaultControl();
        }

        void Update()
        {
            if (IsResetDataDialogVisible())
            {
                if (UiSelectionUtility.TickControllerSubmit()) return;
                if (UiSelectionUtility.CancelPressed())
                {
                    CancelResetData();
                    return;
                }

                var dialogCandidates = ResetDataDialogSelectionCandidates();
                UiSelectionUtility.ConfigureHorizontalNavigation(dialogCandidates);
                UiSelectionUtility.EnsureSelection(dialogCandidates);
                return;
            }

            if (controlBinding.Tick()) return;
            if (controllerBinding.Tick()) return;
            if (UiSelectionUtility.TickControllerSubmit()) return;
            if (UiSelectionUtility.CancelPressed())
            {
                AudioManager.PlayButtonConfirm();
                navigator.LoadTitle();
                return;
            }

            EnsureSelection();
        }

        void OnDestroy()
        {
            if (resetDataButton != null) resetDataButton.onClick.RemoveListener(ShowResetDataDialog);
            if (resetDataOkButton != null) resetDataOkButton.onClick.RemoveListener(ConfirmResetData);
            if (resetDataCancelButton != null) resetDataCancelButton.onClick.RemoveListener(CancelResetData);
            if (resetOptionsButton != null) resetOptionsButton.onClick.RemoveListener(ResetAllOptions);
        }

        void BindResetDataButton()
        {
            if (resetDataButton != null)
            {
                resetDataButton.onClick.RemoveListener(ShowResetDataDialog);
                resetDataButton.onClick.AddListener(ShowResetDataDialog);
            }

            if (resetDataOkButton != null)
            {
                resetDataOkButton.onClick.RemoveListener(ConfirmResetData);
                resetDataOkButton.onClick.AddListener(ConfirmResetData);
            }

            if (resetDataCancelButton != null)
            {
                resetDataCancelButton.onClick.RemoveListener(CancelResetData);
                resetDataCancelButton.onClick.AddListener(CancelResetData);
            }
        }

        void BindResetButton()
        {
            if (resetOptionsButton == null) return;
            resetOptionsButton.onClick.RemoveListener(ResetAllOptions);
            resetOptionsButton.onClick.AddListener(ResetAllOptions);
        }

        void ResetAllOptions()
        {
            AudioManager.PlayButtonConfirm();
            GeneralOptionsResetter.ResetAll(generalOptionsPanel, audioOptionsPanel, displayOptionsPanel, controlBinding, controllerBinding);
        }

        void ShowResetDataDialog()
        {
            AudioManager.PlayButtonConfirm();
            if (resetDataDialog == null) return;
            resetDataDialog.SetActive(true);
            var candidates = ResetDataDialogSelectionCandidates();
            UiSelectionUtility.ConfigureHorizontalNavigation(candidates);
            UiSelectionUtility.SelectFirst(candidates);
        }

        void HideResetDataDialog()
        {
            if (resetDataDialog != null) resetDataDialog.SetActive(false);
            if (resetDataButton != null) UiSelectionUtility.SelectFirst(resetDataButton);
        }

        void ConfirmResetData()
        {
            AudioManager.PlayButtonConfirm();
            ProgressionStore.ResetPlayData();
            HideResetDataDialog();
        }

        void CancelResetData()
        {
            AudioManager.PlayButtonConfirm();
            HideResetDataDialog();
        }

        bool IsResetDataDialogVisible()
        {
            return resetDataDialog != null && resetDataDialog.activeSelf;
        }

        void SelectDefaultControl()
        {
            var candidates = SelectionCandidates();
            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            UiSelectionUtility.SelectFirst(candidates);
        }

        void EnsureSelection()
        {
            var candidates = SelectionCandidates();
            UiSelectionUtility.ConfigureVerticalNavigation(candidates);
            UiSelectionUtility.EnsureSelection(candidates);
        }

        Selectable[] SelectionCandidates()
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
                resetDataButton,
                resetOptionsButton,
                audioOptionsPanel != null ? audioOptionsPanel.backButton : null
            };
        }

        Selectable[] ResetDataDialogSelectionCandidates()
        {
            return new Selectable[] { resetDataCancelButton, resetDataOkButton };
        }
    }

    public static class GeneralOptionsResetter
    {
        public static void ResetAll(GeneralOptionsPanel general, AudioOptionsPanel audio, DisplayOptionsPanel display, KeyboardMouseControlOptionsBinding controls, ControllerControlOptionsBinding controllerControls = null)
        {
            AudioManager.ResetDefaults();
            DisplaySettingsStore.ResetDefaults();
            InputSettingsStore.ResetDefaults();
            ControllerInputSettingsStore.ResetDefaults();

            if (general != null) general.Refresh();
            if (audio != null) audio.Refresh();
            if (display != null) display.Refresh();
            if (controls != null) controls.Refresh();
            if (controllerControls != null) controllerControls.Refresh();
        }
    }

    public sealed class KeyboardMouseControlOptionsBinding
    {
        readonly KeyboardMouseAction[] moveActions =
        {
            KeyboardMouseAction.MoveUp,
            KeyboardMouseAction.MoveDown,
            KeyboardMouseAction.MoveLeft,
            KeyboardMouseAction.MoveRight
        };

        Text moveText;
        Text attackText;
        Button moveRebindButton;
        Text[] actionTexts;
        Button[] actionButtons;
        InputField[] actionInputs;
        InputField[] alternateActionInputs;
        int rebindIndex = -1;
        bool rebindAlternate;
        bool sequentialRebind;

        public void Bind(Text moveText, Text attackText, Button moveRebindButton, Text[] actionTexts, Button[] actionButtons, InputField[] actionInputs, InputField[] alternateActionInputs = null)
        {
            if (this.moveRebindButton != null) this.moveRebindButton.onClick.RemoveListener(BeginMoveRebind);
            this.moveText = moveText;
            this.attackText = attackText;
            this.moveRebindButton = moveRebindButton;
            this.actionTexts = actionTexts;
            this.actionButtons = actionButtons;
            this.actionInputs = actionInputs;
            this.alternateActionInputs = alternateActionInputs;
            if (this.moveRebindButton != null)
            {
                this.moveRebindButton.onClick.RemoveListener(BeginMoveRebind);
                this.moveRebindButton.onClick.AddListener(BeginMoveRebind);
            }

            BindActionButton(0);
            BindActionButton(1);
            BindActionButton(2);
            BindActionButton(3);
            BindActionInput(0, false);
            BindActionInput(1, false);
            BindActionInput(2, false);
            BindActionInput(3, false);
            BindActionInput(0, true);
            BindActionInput(1, true);
            BindActionInput(2, true);
            BindActionInput(3, true);
            Refresh();
        }

        public bool Tick()
        {
            if (rebindIndex < 0) return false;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelRebind();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                InputSettingsStore.ResetDefaults();
                CancelRebind();
                return true;
            }

            var key = PressedKey();
            if (key == KeyCode.None) return false;
            if (IsDuplicateMoveKey(rebindIndex, key, rebindAlternate))
            {
                ShowDuplicateKey(key);
                return true;
            }

            if (rebindAlternate)
            {
                InputSettingsStore.SetAlternateKey(moveActions[rebindIndex], key);
            }
            else
            {
                InputSettingsStore.SetKey(moveActions[rebindIndex], key);
            }

            if (sequentialRebind)
            {
                rebindIndex++;
                if (rebindIndex < moveActions.Length)
                {
                    ShowRebindPrompt();
                    return true;
                }
            }

            rebindIndex = -1;
            rebindAlternate = false;
            sequentialRebind = false;
            Refresh();
            return true;
        }

        void BeginMoveRebind()
        {
            AudioManager.PlayButtonConfirm();
            rebindIndex = 0;
            rebindAlternate = false;
            sequentialRebind = true;
            ShowRebindPrompt();
        }

        void BeginSingleRebind(int index, bool alternate = false)
        {
            if (index < 0 || index >= moveActions.Length) return;
            AudioManager.PlayButtonConfirm();
            rebindIndex = index;
            rebindAlternate = alternate;
            sequentialRebind = false;
            ShowRebindPrompt();
        }

        void CancelRebind()
        {
            rebindIndex = -1;
            rebindAlternate = false;
            sequentialRebind = false;
            Refresh();
        }

        public void Refresh()
        {
            if (moveText != null)
            {
                moveText.text = HasActionControls() ? string.Empty : InputSettingsStore.MovementLabel() + "  ［変更］";
            }

            if (attackText != null) attackText.text = "自動攻撃（変更不可）";
            RefreshActionTexts();
        }

        void ShowRebindPrompt()
        {
            if (rebindIndex < 0 || rebindIndex >= moveActions.Length) return;
            if (moveText != null) moveText.text = $"{ActionLabel(moveActions[rebindIndex])}キー入力中";
            RefreshActionTexts();
            SetWaitingText("入力待ち");
        }

        void ShowDuplicateKey(KeyCode key)
        {
            if (rebindIndex < 0 || rebindIndex >= moveActions.Length) return;
            if (moveText != null) moveText.text = $"{InputSettingsStore.KeyLabel(key)} は使用中です";
            RefreshActionTexts();
            SetWaitingText("再入力");
        }

        void BindActionButton(int index)
        {
            if (actionButtons == null || index < 0 || index >= actionButtons.Length) return;
            var button = actionButtons[index];
            if (button == null) return;
            button.onClick.AddListener(() => BeginSingleRebind(index));
        }

        void BindActionInput(int index, bool alternate)
        {
            var inputs = alternate ? alternateActionInputs : actionInputs;
            if (inputs == null || index < 0 || index >= inputs.Length) return;
            var input = inputs[index];
            if (input == null) return;
            input.onEndEdit.RemoveAllListeners();
            input.onValueChanged.RemoveAllListeners();
            input.readOnly = true;
            input.shouldActivateOnSelect = false;
            input.characterLimit = 0;
            input.caretWidth = 0;
            BindInputTrigger(input, index, alternate);
        }

        void RefreshActionTexts()
        {
            for (int i = 0; i < moveActions.Length; i++)
            {
                string key = InputSettingsStore.KeyLabel(InputSettingsStore.GetKey(moveActions[i]));
                string alternateKey = InputSettingsStore.KeyLabel(InputSettingsStore.GetAlternateKey(moveActions[i]));
                if (HasActionInput(i))
                {
                    SetActionInput(i, key, false);
                }
                else
                {
                    SetActionText(i, $"{ShortActionLabel(moveActions[i])}  [ {key} ]");
                }

                SetActionInput(i, alternateKey, true);
            }
        }

        void SetActionText(int index, string value)
        {
            if (actionTexts == null || index < 0 || index >= actionTexts.Length) return;
            if (actionTexts[index] != null) actionTexts[index].text = value;
        }

        void SetWaitingText(string value)
        {
            if (rebindIndex < 0 || rebindIndex >= moveActions.Length) return;
            if (HasActionInput(rebindIndex, rebindAlternate))
            {
                SetActionInput(rebindIndex, value, rebindAlternate);
            }
            else
            {
                SetActionText(rebindIndex, $"{ShortActionLabel(moveActions[rebindIndex])}  [ {value} ]");
            }
        }

        void SetActionInput(int index, string value, bool alternate)
        {
            var inputs = alternate ? alternateActionInputs : actionInputs;
            if (inputs == null || index < 0 || index >= inputs.Length) return;
            if (inputs[index] != null) inputs[index].SetTextWithoutNotify(value);
        }

        bool HasActionInput(int index)
        {
            return HasActionInput(index, false);
        }

        bool HasActionInput(int index, bool alternate)
        {
            var inputs = alternate ? alternateActionInputs : actionInputs;
            return inputs != null
                && index >= 0
                && index < inputs.Length
                && inputs[index] != null;
        }

        bool HasActionControls()
        {
            if (actionInputs != null && actionInputs.Length >= moveActions.Length)
            {
                bool hasInputs = true;
                for (int i = 0; i < moveActions.Length; i++)
                {
                    if (actionInputs[i] == null)
                    {
                        hasInputs = false;
                        break;
                    }
                }

                if (hasInputs) return true;
            }

            if (actionButtons == null || actionButtons.Length < moveActions.Length) return false;
            for (int i = 0; i < moveActions.Length; i++)
            {
                if (actionButtons[i] == null) return false;
            }

            return true;
        }

        bool IsDuplicateMoveKey(int actionIndex, KeyCode key, bool alternate)
        {
            for (int i = 0; i < moveActions.Length; i++)
            {
                if (!(i == actionIndex && !alternate) && InputSettingsStore.GetKey(moveActions[i]) == key) return true;
                if (!(i == actionIndex && alternate) && InputSettingsStore.GetAlternateKey(moveActions[i]) == key) return true;
            }

            return false;
        }

        static KeyCode PressedKey()
        {
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (!IsBindableKeyboardKey(key)) continue;
                if (Input.GetKeyDown(key)) return key;
            }

            return KeyCode.None;
        }

        static bool IsBindableKeyboardKey(KeyCode key)
        {
            if (key == KeyCode.None || key == KeyCode.Escape || key == KeyCode.Backspace) return false;
            if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) return false;
            if (key >= KeyCode.JoystickButton0 && key <= KeyCode.Joystick8Button19) return false;
            return true;
        }

        static string ActionLabel(KeyboardMouseAction action)
        {
            switch (action)
            {
                case KeyboardMouseAction.MoveUp: return "上方向";
                case KeyboardMouseAction.MoveDown: return "下方向";
                case KeyboardMouseAction.MoveLeft: return "左方向";
                case KeyboardMouseAction.MoveRight: return "右方向";
                default: return action.ToString();
            }
        }

        static string ShortActionLabel(KeyboardMouseAction action)
        {
            switch (action)
            {
                case KeyboardMouseAction.MoveUp: return "上";
                case KeyboardMouseAction.MoveDown: return "下";
                case KeyboardMouseAction.MoveLeft: return "左";
                case KeyboardMouseAction.MoveRight: return "右";
                default: return action.ToString();
            }
        }

        void BindInputTrigger(InputField input, int index, bool alternate)
        {
            var trigger = input.GetComponent<EventTrigger>();
            if (trigger == null) trigger = input.gameObject.AddComponent<EventTrigger>();

            trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerClick || entry.eventID == EventTriggerType.Submit);
            AddTrigger(trigger, EventTriggerType.PointerClick, _ => BeginSingleRebind(index, alternate));
            AddTrigger(trigger, EventTriggerType.Submit, _ => BeginSingleRebind(index, alternate));
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

    }

    public sealed class ControllerControlOptionsBinding
    {
        readonly KeyboardMouseAction[] moveActions =
        {
            KeyboardMouseAction.MoveUp,
            KeyboardMouseAction.MoveDown,
            KeyboardMouseAction.MoveLeft,
            KeyboardMouseAction.MoveRight
        };

        InputField[] actionInputs;
        InputField[] alternateActionInputs;
        InputField submitInput;
        InputField cancelInput;
        int rebindIndex = -1;
        bool rebindAlternate;
        bool rebindSubmit;
        bool rebindCancel;

        public void Bind(InputField[] actionInputs, InputField[] alternateActionInputs = null, InputField submitInput = null, InputField cancelInput = null)
        {
            this.actionInputs = actionInputs;
            this.alternateActionInputs = alternateActionInputs;
            this.submitInput = submitInput;
            this.cancelInput = cancelInput;
            BindActionInput(0, false);
            BindActionInput(1, false);
            BindActionInput(2, false);
            BindActionInput(3, false);
            BindActionInput(0, true);
            BindActionInput(1, true);
            BindActionInput(2, true);
            BindActionInput(3, true);
            BindSubmitCancelInput(submitInput, true);
            BindSubmitCancelInput(cancelInput, false);
            Refresh();
        }

        public bool Tick()
        {
            if (rebindIndex < 0 && !rebindSubmit && !rebindCancel) return false;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CancelRebind();
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ControllerInputSettingsStore.ResetDefaults();
                CancelRebind();
                return true;
            }

            var binding = ControllerInputSettingsStore.PressedBinding();
            if (binding.kind == ControllerInputKind.None) return false;
            if ((rebindSubmit || rebindCancel) && binding.kind != ControllerInputKind.Button)
            {
                SetWaitingInput("ボタンのみ");
                return true;
            }

            if (IsDuplicateBinding(rebindIndex, binding, rebindAlternate, rebindSubmit, rebindCancel))
            {
                SetWaitingInput("再入力");
                return true;
            }

            if (rebindSubmit)
            {
                ControllerInputSettingsStore.SetSubmitBinding(binding);
            }
            else if (rebindCancel)
            {
                ControllerInputSettingsStore.SetCancelBinding(binding);
            }
            else if (rebindAlternate)
            {
                ControllerInputSettingsStore.SetAlternateBinding(moveActions[rebindIndex], binding);
            }
            else
            {
                ControllerInputSettingsStore.SetBinding(moveActions[rebindIndex], binding);
            }

            rebindIndex = -1;
            rebindAlternate = false;
            rebindSubmit = false;
            rebindCancel = false;
            Refresh();
            return true;
        }

        public void Refresh()
        {
            for (int i = 0; i < moveActions.Length; i++)
            {
                SetActionInput(i, ControllerInputSettingsStore.BindingLabel(ControllerInputSettingsStore.GetBinding(moveActions[i])), false);
                SetActionInput(i, ControllerInputSettingsStore.BindingLabel(ControllerInputSettingsStore.GetAlternateBinding(moveActions[i])), true);
            }

            if (submitInput != null) submitInput.SetTextWithoutNotify(ControllerInputSettingsStore.BindingLabel(ControllerInputSettingsStore.GetSubmitBinding()));
            if (cancelInput != null) cancelInput.SetTextWithoutNotify(ControllerInputSettingsStore.BindingLabel(ControllerInputSettingsStore.GetCancelBinding()));
        }

        void BeginSingleRebind(int index, bool alternate)
        {
            if (index < 0 || index >= moveActions.Length) return;
            AudioManager.PlayButtonConfirm();
            rebindIndex = index;
            rebindAlternate = alternate;
            rebindSubmit = false;
            rebindCancel = false;
            SetActionInput(index, "入力待ち", alternate);
        }

        void BeginSubmitCancelRebind(bool submit)
        {
            AudioManager.PlayButtonConfirm();
            rebindIndex = -1;
            rebindAlternate = false;
            rebindSubmit = submit;
            rebindCancel = !submit;
            SetWaitingInput("入力待ち");
        }

        void CancelRebind()
        {
            rebindIndex = -1;
            rebindAlternate = false;
            rebindSubmit = false;
            rebindCancel = false;
            Refresh();
        }

        void BindActionInput(int index, bool alternate)
        {
            var inputs = alternate ? alternateActionInputs : actionInputs;
            if (inputs == null || index < 0 || index >= inputs.Length) return;
            var input = inputs[index];
            if (input == null) return;
            input.onEndEdit.RemoveAllListeners();
            input.onValueChanged.RemoveAllListeners();
            input.readOnly = true;
            input.shouldActivateOnSelect = false;
            input.characterLimit = 0;
            input.caretWidth = 0;

            var trigger = input.GetComponent<EventTrigger>();
            if (trigger == null) trigger = input.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerClick || entry.eventID == EventTriggerType.Submit);
            AddTrigger(trigger, EventTriggerType.PointerClick, _ => BeginSingleRebind(index, alternate));
            AddTrigger(trigger, EventTriggerType.Submit, _ => BeginSingleRebind(index, alternate));
        }

        void BindSubmitCancelInput(InputField input, bool submit)
        {
            if (input == null) return;
            input.onEndEdit.RemoveAllListeners();
            input.onValueChanged.RemoveAllListeners();
            input.readOnly = true;
            input.shouldActivateOnSelect = false;
            input.characterLimit = 0;
            input.caretWidth = 0;

            var trigger = input.GetComponent<EventTrigger>();
            if (trigger == null) trigger = input.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.RemoveAll(entry => entry.eventID == EventTriggerType.PointerClick || entry.eventID == EventTriggerType.Submit);
            AddTrigger(trigger, EventTriggerType.PointerClick, _ => BeginSubmitCancelRebind(submit));
            AddTrigger(trigger, EventTriggerType.Submit, _ => BeginSubmitCancelRebind(submit));
        }

        void SetActionInput(int index, string value, bool alternate)
        {
            var inputs = alternate ? alternateActionInputs : actionInputs;
            if (inputs == null || index < 0 || index >= inputs.Length) return;
            if (inputs[index] != null) inputs[index].SetTextWithoutNotify(value);
        }

        void SetWaitingInput(string value)
        {
            if (rebindSubmit)
            {
                if (submitInput != null) submitInput.SetTextWithoutNotify(value);
                return;
            }

            if (rebindCancel)
            {
                if (cancelInput != null) cancelInput.SetTextWithoutNotify(value);
                return;
            }

            SetActionInput(rebindIndex, value, rebindAlternate);
        }

        static string ShortActionLabel(KeyboardMouseAction action)
        {
            switch (action)
            {
                case KeyboardMouseAction.MoveUp: return "上";
                case KeyboardMouseAction.MoveDown: return "下";
                case KeyboardMouseAction.MoveLeft: return "左";
                case KeyboardMouseAction.MoveRight: return "右";
                default: return action.ToString();
            }
        }

        bool IsDuplicateBinding(int actionIndex, ControllerInputBinding binding, bool alternate, bool submit, bool cancel)
        {
            for (int i = 0; i < moveActions.Length; i++)
            {
                if (!(i == actionIndex && !alternate && !submit && !cancel) && IsSameBinding(ControllerInputSettingsStore.GetBinding(moveActions[i]), binding)) return true;
                if (!(i == actionIndex && alternate && !submit && !cancel) && IsSameBinding(ControllerInputSettingsStore.GetAlternateBinding(moveActions[i]), binding)) return true;
            }

            if (!submit && IsSameBinding(ControllerInputSettingsStore.GetSubmitBinding(), binding)) return true;
            if (!cancel && IsSameBinding(ControllerInputSettingsStore.GetCancelBinding(), binding)) return true;
            return false;
        }

        static bool IsSameBinding(ControllerInputBinding lhs, ControllerInputBinding rhs)
        {
            return lhs.kind == rhs.kind && lhs.button == rhs.button;
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }
    }
}
