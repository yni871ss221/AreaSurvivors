using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public static class UiSelectionUtility
    {
        static readonly Color UnifiedFocusColor = Color.white;
        static readonly Color UnifiedFocusShadowColor = new Color(0f, 0f, 0f, 0f);
        static readonly Color UnifiedFocusBackgroundColor = new Color(0.114f, 0.529f, 0.298f, 0.98f);
        static readonly Color UnifiedHoverBackgroundColor = new Color(0.106f, 0.353f, 0.216f, 0.98f);
        const float UnifiedFocusPadding = 0f;
        const float UnifiedFocusThickness = 4f;
        const float ScrollPadding = 28f;
        const float PointerMoveThresholdSqr = 0.25f;
        const float DirectionDotThreshold = 0.15f;
        static Selectable lastValidSelection;
        static GameObject lastPresentedSelection;
        static Vector3 lastPointerPosition;
        static bool pointerPositionInitialized;
        static bool controllerInputMode;
        static int lastInputModeUpdateFrame = -1;
        static int dropdownCancelConsumedFrame = -1;

        public static bool IsControllerInputMode
        {
            get
            {
                UpdateInputMode();
                return controllerInputMode;
            }
        }

        public static void SelectFirst(params Selectable[] candidates)
        {
            ApplyFocusStyle(candidates);
            var target = FirstSelectable(candidates);
            if (target == null) return;

            Select(target);
        }

        public static void EnsureSelection(params Selectable[] candidates)
        {
            ApplyFocusStyle(candidates);
            UpdateInputMode();
            if (DropdownPadNavigator.HasOpenDropdown)
            {
                DropdownPadNavigator.EnsureOpenDropdownSelection();
                return;
            }

            if (!controllerInputMode)
            {
                if (HasValidSelection(candidates))
                {
                    lastValidSelection = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();
                    UpdateCurrentSelectionPresentation(false);
                }

                return;
            }

            if (HasValidSelection(candidates))
            {
                lastValidSelection = EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>();
                UpdateCurrentSelectionPresentation();
                return;
            }

            if (IsCandidate(lastValidSelection, candidates))
            {
                Select(lastValidSelection);
                return;
            }

            SelectFirst(candidates);
        }

        public static bool CancelPressed()
        {
            if (DropdownPadNavigator.TryHandleCancel())
            {
                dropdownCancelConsumedFrame = Time.frameCount;
                return false;
            }

            bool controllerCancel = ControllerInputSettingsStore.CancelPressed();
            if (controllerCancel) SetControllerInputMode();
            bool keyboardCancel = SafeGetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape);
            if (keyboardCancel) SetKeyboardMouseInputMode();
            return keyboardCancel || controllerCancel;
        }

        public static bool PausePressed()
        {
            if (dropdownCancelConsumedFrame == Time.frameCount) return false;

            bool controllerPause = Input.GetKeyDown(KeyCode.JoystickButton7) || ControllerInputSettingsStore.CancelPressed();
            if (controllerPause) SetControllerInputMode();
            bool keyboardPause = Input.GetKeyDown(KeyCode.Escape);
            if (keyboardPause) SetKeyboardMouseInputMode();
            return keyboardPause || controllerPause;
        }

        public static bool TickControllerSubmit()
        {
            if (DropdownPadNavigator.TryHandleControllerSubmit()) return true;
            if (!ControllerInputSettingsStore.SubmitPressed()) return false;
            SetControllerInputMode();
            if (EventSystem.current == null) return false;

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return false;

            ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            UpdateCurrentSelectionPresentation();
            return true;
        }

        public static bool TickControllerSubmit(params Selectable[] candidates)
        {
            if (DropdownPadNavigator.TryHandleControllerSubmit()) return true;
            if (!ControllerInputSettingsStore.SubmitPressed()) return false;
            SetControllerInputMode();
            EnsureSelection(candidates);
            if (EventSystem.current == null) return false;

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return false;
            var selectable = selected.GetComponent<Selectable>();
            if (!IsCandidate(selectable, candidates)) return false;

            ExecuteEvents.Execute(selected, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            UpdateCurrentSelectionPresentation();
            return true;
        }

        public static bool TryHandleControllerCancel()
        {
            if (!ControllerInputSettingsStore.CancelPressed()) return false;
            SetControllerInputMode();
            if (EventSystem.current == null) return false;

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return false;

            var handler = ExecuteEvents.GetEventHandler<ICancelHandler>(selected);
            if (handler == null) return false;
            ExecuteEvents.Execute(handler, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            UpdateCurrentSelectionPresentation();
            return true;
        }

        public static bool PointerCanDriveFocus()
        {
            UpdateInputMode();
            return !controllerInputMode;
        }

        public static void NotifyKeyboardMouseInput()
        {
            SetKeyboardMouseInputMode();
        }

        public static Button FirstButtonInChildren(Transform root)
        {
            if (root == null) return null;
            var buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (IsSelectable(buttons[i])) return buttons[i];
            }

            return null;
        }

        public static Selectable FirstSelectable(params Selectable[] candidates)
        {
            if (candidates == null) return null;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (IsSelectable(candidates[i])) return candidates[i];
            }

            return null;
        }

        public static bool IsSelectable(Selectable selectable)
        {
            return selectable != null
                && selectable.gameObject.activeInHierarchy
                && selectable.IsInteractable();
        }

        public static void ConfigureVerticalNavigation(params Selectable[] candidates)
        {
            ConfigureDirectionalNavigation(candidates);
        }

        public static void ConfigureHorizontalNavigation(params Selectable[] candidates)
        {
            ConfigureDirectionalNavigation(candidates);
        }

        public static void ConfigureDirectionalNavigation(params Selectable[] candidates)
        {
            if (candidates == null) return;

            var active = new System.Collections.Generic.List<Selectable>();
            for (int i = 0; i < candidates.Length; i++)
            {
                if (IsSelectable(candidates[i])) active.Add(candidates[i]);
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                var selectable = candidates[i];
                if (selectable == null) continue;

                var navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.wrapAround = false;
                navigation.selectOnUp = BestDirectionalSelectable(selectable, active, Vector2.up);
                navigation.selectOnDown = BestDirectionalSelectable(selectable, active, Vector2.down);
                navigation.selectOnLeft = BestDirectionalSelectable(selectable, active, Vector2.left);
                navigation.selectOnRight = BestDirectionalSelectable(selectable, active, Vector2.right);
                ReserveSliderValueAxis(selectable, ref navigation);
                selectable.navigation = navigation;
            }
        }

        static void ReserveSliderValueAxis(Selectable selectable, ref Navigation navigation)
        {
            var slider = selectable as Slider;
            if (slider == null) return;

            bool horizontal = slider.direction == Slider.Direction.LeftToRight ||
                slider.direction == Slider.Direction.RightToLeft;
            if (horizontal)
            {
                navigation.selectOnLeft = null;
                navigation.selectOnRight = null;
                return;
            }

            navigation.selectOnUp = null;
            navigation.selectOnDown = null;
        }

        static void Select(Selectable selectable)
        {
            if (!IsSelectable(selectable)) return;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }

            selectable.Select();
            lastValidSelection = selectable;
            UpdateCurrentSelectionPresentation(true);
        }

        static bool IsCandidate(Selectable selectable, Selectable[] candidates)
        {
            if (!IsSelectable(selectable) || candidates == null) return false;
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == selectable) return true;
            }

            return false;
        }

        static Selectable BestDirectionalSelectable(Selectable source, System.Collections.Generic.List<Selectable> candidates, Vector2 direction)
        {
            if (!IsSelectable(source) || candidates == null || candidates.Count == 0) return null;

            var sourceRectTransform = source.transform as RectTransform;
            if (sourceRectTransform == null) return null;

            Rect sourceRect = ScreenRect(sourceRectTransform);
            Vector2 sourceCenter = sourceRect.center;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            bool horizontalMove = Mathf.Abs(direction.x) > Mathf.Abs(direction.y);
            Selectable best = null;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (candidate == source || !IsSelectable(candidate)) continue;

                var candidateRectTransform = candidate.transform as RectTransform;
                if (candidateRectTransform == null) continue;

                Rect candidateRect = ScreenRect(candidateRectTransform);
                Vector2 delta = candidateRect.center - sourceCenter;
                float forward = Vector2.Dot(delta, direction);
                if (forward <= 0.5f) continue;

                float distance = Mathf.Max(0.001f, delta.magnitude);
                float directionDot = forward / distance;
                if (directionDot < DirectionDotThreshold) continue;

                float laneGap = horizontalMove
                    ? AxisGap(sourceRect.yMin, sourceRect.yMax, candidateRect.yMin, candidateRect.yMax)
                    : AxisGap(sourceRect.xMin, sourceRect.xMax, candidateRect.xMin, candidateRect.xMax);
                float perpendicularDistance = Mathf.Abs(Vector2.Dot(delta, perpendicular));
                float score = laneGap * 10000f + perpendicularDistance * 10f + forward;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        static Rect ScreenRect(RectTransform rectTransform)
        {
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, screen);
                max = Vector2.Max(max, screen);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static float AxisGap(float minA, float maxA, float minB, float maxB)
        {
            if (maxA < minB) return minB - maxA;
            if (maxB < minA) return minA - maxB;
            return 0f;
        }

        static void ApplyFocusStyle(params Selectable[] candidates)
        {
            if (candidates != null)
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    ApplyFocusStyle(candidates[i]);
                }
            }

            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null) ApplyFocusStyle(selected.GetComponent<Selectable>());
        }

        static void ApplyFocusStyle(Selectable selectable)
        {
            if (selectable == null) return;
            if (selectable.GetComponent<UiSceneManagedSelectable>() != null) return;
            if (IsRuntimeDropdownBlocker(selectable.gameObject))
            {
                selectable.transition = Selectable.Transition.None;
                return;
            }

            // Focus visuals are handled by UiSelectionHighlight so mouse and controller
            // modes do not stack Unity's built-in ColorTint selection on top.
            selectable.transition = Selectable.Transition.None;
            var highlight = selectable.GetComponent<UiSelectionHighlight>();
            if (highlight == null) highlight = selectable.gameObject.AddComponent<UiSelectionHighlight>();
            highlight.padding = UnifiedFocusPadding;
            highlight.thickness = UnifiedFocusThickness;
            highlight.focusColor = UnifiedFocusColor;
            highlight.shadowColor = UnifiedFocusShadowColor;
            highlight.selectedBackgroundColor = UnifiedFocusBackgroundColor;
            highlight.hoverBackgroundColor = UnifiedHoverBackgroundColor;
            highlight.showBackgroundOnFocus = true;
            highlight.bringToFrontOnHighlight = true;
            if (selectable.GetComponent<SelectOnPointerEnter>() == null)
            {
                selectable.gameObject.AddComponent<SelectOnPointerEnter>();
            }

            if (selectable is Dropdown && selectable.GetComponent<DropdownPadNavigator>() == null)
            {
                selectable.gameObject.AddComponent<DropdownPadNavigator>();
            }
        }

        static bool IsRuntimeDropdownBlocker(GameObject target)
        {
            return target != null && target.name == "Blocker";
        }

        static void UpdateCurrentSelectionPresentation(bool forceKeepSelectedInView = false)
        {
            UpdateInputMode();
            if (EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return;

            ApplyFocusStyle(selected.GetComponent<Selectable>());
            bool selectionChanged = selected != lastPresentedSelection;
            if (controllerInputMode && (forceKeepSelectedInView || selectionChanged))
            {
                KeepSelectedInView(selected.transform as RectTransform);
            }

            lastPresentedSelection = selected;
        }

        static void KeepSelectedInView(RectTransform selected)
        {
            if (selected == null) return;

            var skillTreeViewport = selected.GetComponentInParent<SkillTreeViewportController>();
            if (skillTreeViewport != null)
            {
                skillTreeViewport.EnsureVisible(selected, ScrollPadding);
                return;
            }

            var customScroll = selected.GetComponentInParent<OptionsPanelScrollController>();
            if (customScroll != null)
            {
                customScroll.EnsureVisible(selected, ScrollPadding);
                return;
            }

            var scrollRect = selected.GetComponentInParent<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null) return;

            RectTransform viewport = scrollRect.viewport != null
                ? scrollRect.viewport
                : scrollRect.transform as RectTransform;
            if (viewport == null) return;

            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, selected);
            var viewportRect = viewport.rect;
            float deltaY = 0f;

            if (bounds.max.y > viewportRect.yMax - ScrollPadding)
            {
                deltaY = viewportRect.yMax - ScrollPadding - bounds.max.y;
            }
            else if (bounds.min.y < viewportRect.yMin + ScrollPadding)
            {
                deltaY = viewportRect.yMin + ScrollPadding - bounds.min.y;
            }

            if (Mathf.Approximately(deltaY, 0f)) return;
            var position = scrollRect.content.anchoredPosition;
            position.y += deltaY;
            scrollRect.content.anchoredPosition = position;
        }

        static bool HasValidSelection(params Selectable[] candidates)
        {
            if (EventSystem.current == null) return false;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return false;
            var selectable = selected.GetComponent<Selectable>();
            if (!IsSelectable(selectable)) return false;
            return candidates == null || candidates.Length == 0 || IsCandidate(selectable, candidates);
        }

        static bool SafeGetButtonDown(string buttonName)
        {
            try
            {
                return Input.GetButtonDown(buttonName);
            }
            catch (System.ArgumentException)
            {
                return false;
            }
        }

        static void UpdateInputMode()
        {
            if (lastInputModeUpdateFrame == Time.frameCount) return;
            lastInputModeUpdateFrame = Time.frameCount;

            var pointer = Input.mousePosition;
            bool pointerMoved = false;
            if (!pointerPositionInitialized)
            {
                lastPointerPosition = pointer;
                pointerPositionInitialized = true;
            }
            else
            {
                pointerMoved = (pointer - lastPointerPosition).sqrMagnitude > PointerMoveThresholdSqr;
            }

            bool pointerAction = pointerMoved
                || Input.GetMouseButtonDown(0)
                || Input.GetMouseButtonDown(1)
                || Input.GetMouseButtonDown(2)
                || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;
            bool keyboardMouseAction = pointerAction || KeyboardMouseKeyPressed();
            if (keyboardMouseAction)
            {
                SetKeyboardMouseInputMode();
                lastPointerPosition = pointer;
                return;
            }

            bool controllerInput = ControllerInputSettingsStore.MoveVector().sqrMagnitude > 0.25f
                || ControllerInputSettingsStore.PressedBinding().kind != ControllerInputKind.None;
            if (controllerInput) SetControllerInputMode();
            lastPointerPosition = pointer;
        }

        static void SetControllerInputMode()
        {
            controllerInputMode = true;
        }

        static void SetKeyboardMouseInputMode()
        {
            controllerInputMode = false;
        }

        static bool KeyboardMouseKeyPressed()
        {
            if (!Input.anyKeyDown) return false;
            if (!string.IsNullOrEmpty(Input.inputString)) return true;
            return Input.GetKeyDown(KeyCode.Escape)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Tab)
                || Input.GetKeyDown(KeyCode.Backspace)
                || Input.GetKeyDown(KeyCode.Delete)
                || Input.GetKeyDown(KeyCode.UpArrow)
                || Input.GetKeyDown(KeyCode.DownArrow)
                || Input.GetKeyDown(KeyCode.LeftArrow)
                || Input.GetKeyDown(KeyCode.RightArrow)
                || Input.GetKeyDown(KeyCode.W)
                || Input.GetKeyDown(KeyCode.A)
                || Input.GetKeyDown(KeyCode.S)
                || Input.GetKeyDown(KeyCode.D);
        }
    }
}
