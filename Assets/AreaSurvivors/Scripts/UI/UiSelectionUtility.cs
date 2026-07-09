using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public static class UiSelectionUtility
    {
        static readonly Color FocusColor = new Color(0.98f, 0.9f, 0.38f, 1f);
        static readonly Color FocusPressedColor = new Color(0.78f, 0.95f, 0.58f, 1f);
        const float ScrollPadding = 28f;
        const float PointerMoveThresholdSqr = 0.25f;
        const float DirectionDotThreshold = 0.15f;
        static Selectable lastValidSelection;
        static GameObject lastPresentedSelection;
        static Vector3 lastPointerPosition;
        static bool pointerPositionInitialized;
        static bool controllerInputMode;
        static int lastInputModeUpdateFrame = -1;

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
            if (HasValidSelection())
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
            bool controllerCancel = ControllerInputSettingsStore.CancelPressed();
            if (controllerCancel) SetControllerInputMode();
            return SafeGetButtonDown("Cancel") || Input.GetKeyDown(KeyCode.Escape) || controllerCancel;
        }

        public static bool PausePressed()
        {
            bool controllerPause = Input.GetKeyDown(KeyCode.JoystickButton7) || ControllerInputSettingsStore.CancelPressed();
            if (controllerPause) SetControllerInputMode();
            return Input.GetKeyDown(KeyCode.Escape) || controllerPause;
        }

        public static bool TickControllerSubmit()
        {
            if (!ControllerInputSettingsStore.SubmitPressed()) return false;
            SetControllerInputMode();
            if (EventSystem.current == null) return false;

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return false;

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
                selectable.navigation = navigation;
            }
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
            if (selectable.transition == Selectable.Transition.ColorTint)
            {
                var colors = selectable.colors;
                colors.highlightedColor = FocusColor;
                colors.selectedColor = FocusColor;
                colors.pressedColor = FocusPressedColor;
                colors.colorMultiplier = Mathf.Max(colors.colorMultiplier, 1f);
                colors.fadeDuration = Mathf.Min(colors.fadeDuration, 0.08f);
                selectable.colors = colors;
            }
        }

        static void UpdateCurrentSelectionPresentation(bool forceKeepSelectedInView = false)
        {
            UpdateInputMode();
            if (EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return;

            ApplyFocusStyle(selected.GetComponent<Selectable>());
            bool selectionChanged = selected != lastPresentedSelection;
            if (forceKeepSelectedInView || selectionChanged)
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

        static bool HasValidSelection()
        {
            if (EventSystem.current == null) return false;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy) return false;
            var selectable = selected.GetComponent<Selectable>();
            return IsSelectable(selectable);
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
            if (!pointerPositionInitialized)
            {
                lastPointerPosition = pointer;
                pointerPositionInitialized = true;
            }

            bool controllerInput = ControllerInputSettingsStore.MoveVector().sqrMagnitude > 0.25f
                || ControllerInputSettingsStore.PressedBinding().kind != ControllerInputKind.None;
            if (controllerInput)
            {
                SetControllerInputMode();
                lastPointerPosition = pointer;
                return;
            }

            bool pointerMoved = (pointer - lastPointerPosition).sqrMagnitude > PointerMoveThresholdSqr;
            bool pointerAction = pointerMoved
                || Input.GetMouseButtonDown(0)
                || Input.GetMouseButtonDown(1)
                || Input.GetMouseButtonDown(2)
                || Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;
            if (pointerAction) controllerInputMode = false;
            lastPointerPosition = pointer;
        }

        static void SetControllerInputMode()
        {
            controllerInputMode = true;
        }
    }
}
