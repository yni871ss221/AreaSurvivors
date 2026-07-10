using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class DropdownPadNavigator : MonoBehaviour, ISubmitHandler, IPointerClickHandler
    {
        static readonly Color OriginalValueTextColor = new Color(1f, 0.92f, 0.28f, 1f);
        static DropdownPadNavigator activeDropdown;

        Dropdown dropdown;
        RectTransform dropdownList;
        RectTransform blocker;
        Toggle[] itemToggles;
        Text[] itemLabels;
        Color[] itemOriginalColors;
        int originalValue;
        bool waitingForPopup;
        bool open;

        public static bool HasOpenDropdown => activeDropdown != null && activeDropdown.open;

        void Awake()
        {
            dropdown = GetComponent<Dropdown>();
        }

        void Update()
        {
            if (dropdown == null) dropdown = GetComponent<Dropdown>();
            if (dropdown == null) return;

            if (!open)
            {
                var list = FindDropdownList();
                if (list != null)
                {
                    Open(list);
                }

                return;
            }

            if (dropdownList == null || !dropdownList.gameObject.activeInHierarchy)
            {
                Close(false);
                return;
            }

            if (!UiSelectionUtility.IsControllerInputMode && Input.GetMouseButtonDown(0) && !PointerInsideOpenDropdown())
            {
                RestoreAndClose();
                return;
            }

            SuppressBlockerVisual();
            EnsurePopupSelection();
        }

        void LateUpdate()
        {
            if (open)
            {
                SuppressBlockerVisual();
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            BeginOpenTracking();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            UiSelectionUtility.NotifyKeyboardMouseInput();
            BeginOpenTracking();
        }

        public static bool TryHandleControllerSubmit()
        {
            if (activeDropdown == null || !activeDropdown.open) return false;
            if (!ControllerInputSettingsStore.SubmitPressed()) return false;

            activeDropdown.CommitSelectedItem();
            return true;
        }

        public static bool TryHandleCancel()
        {
            if (activeDropdown == null || !activeDropdown.open) return false;
            bool cancel = ControllerInputSettingsStore.CancelPressed() || Input.GetKeyDown(KeyCode.Escape);
            if (!cancel) return false;

            activeDropdown.RestoreAndClose();
            return true;
        }

        public static void EnsureOpenDropdownSelection()
        {
            if (activeDropdown != null && activeDropdown.open)
            {
                activeDropdown.EnsurePopupSelection();
            }
        }

        void BeginOpenTracking()
        {
            originalValue = dropdown != null ? dropdown.value : 0;
            waitingForPopup = true;
        }

        void Open(RectTransform list)
        {
            dropdownList = list;
            blocker = FindDropdownBlocker();
            open = true;
            waitingForPopup = false;
            activeDropdown = this;
            SuppressBlockerVisual();
            RefreshItems();
            SelectOriginalItem();
        }

        void Close(bool restoreValue)
        {
            RestoreItemLabelColors();
            if (restoreValue && dropdown != null)
            {
                dropdown.SetValueWithoutNotify(originalValue);
                dropdown.RefreshShownValue();
            }

            open = false;
            waitingForPopup = false;
            dropdownList = null;
            blocker = null;
            itemToggles = null;
            itemLabels = null;
            itemOriginalColors = null;
            if (activeDropdown == this) activeDropdown = null;

            if (EventSystem.current != null && dropdown != null && dropdown.gameObject.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(dropdown.gameObject);
            }
        }

        void RestoreAndClose()
        {
            Close(true);
            if (dropdown != null) dropdown.Hide();
        }

        void CommitSelectedItem()
        {
            int index = SelectedItemIndex();
            if (index < 0) index = Mathf.Clamp(originalValue, 0, dropdown.options.Count - 1);
            if (dropdown != null)
            {
                dropdown.value = index;
                dropdown.RefreshShownValue();
                dropdown.Hide();
            }

            Close(false);
        }

        void EnsurePopupSelection()
        {
            RefreshItems();
            if (itemToggles == null || itemToggles.Length == 0) return;

            UiSelectionUtility.ConfigureVerticalNavigation(itemToggles);
            ApplyOriginalValueTextColor();

            if (EventSystem.current == null) return;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && dropdownList != null && selected.transform.IsChildOf(dropdownList)) return;

            SelectOriginalItem();
        }

        void RefreshItems()
        {
            if (dropdownList == null) return;
            itemToggles = dropdownList.GetComponentsInChildren<Toggle>(false);
            itemLabels = new Text[itemToggles.Length];
            itemOriginalColors = new Color[itemToggles.Length];
            for (int i = 0; i < itemToggles.Length; i++)
            {
                itemLabels[i] = itemToggles[i] != null ? itemToggles[i].GetComponentInChildren<Text>(true) : null;
                itemOriginalColors[i] = itemLabels[i] != null ? itemLabels[i].color : Color.white;
            }
        }

        void SelectOriginalItem()
        {
            if (itemToggles == null || itemToggles.Length == 0 || EventSystem.current == null) return;
            int index = Mathf.Clamp(originalValue, 0, itemToggles.Length - 1);
            var target = itemToggles[index];
            if (target == null) return;

            EventSystem.current.SetSelectedGameObject(target.gameObject);
            target.Select();
            ApplyOriginalValueTextColor();
        }

        int SelectedItemIndex()
        {
            if (itemToggles == null || EventSystem.current == null) return -1;
            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null) return -1;
            for (int i = 0; i < itemToggles.Length; i++)
            {
                if (itemToggles[i] != null && itemToggles[i].gameObject == selected) return i;
            }

            return -1;
        }

        void ApplyOriginalValueTextColor()
        {
            if (itemLabels == null || itemOriginalColors == null) return;
            int index = Mathf.Clamp(originalValue, 0, itemLabels.Length - 1);
            for (int i = 0; i < itemLabels.Length; i++)
            {
                if (itemLabels[i] == null) continue;
                itemLabels[i].color = i == index ? OriginalValueTextColor : itemOriginalColors[i];
            }
        }

        void RestoreItemLabelColors()
        {
            if (itemLabels == null || itemOriginalColors == null) return;
            for (int i = 0; i < itemLabels.Length; i++)
            {
                if (itemLabels[i] != null) itemLabels[i].color = itemOriginalColors[i];
            }
        }

        RectTransform FindDropdownList()
        {
            if (!waitingForPopup && !open) return null;
            if (dropdown == null || dropdown.template == null || dropdown.template.parent == null) return null;

            var parent = dropdown.template.parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                if (child == dropdown.template) continue;
                if (!child.name.StartsWith("Dropdown List")) continue;
                return child as RectTransform;
            }

            return null;
        }

        bool PointerInsideOpenDropdown()
        {
            return PointerInside(dropdown.transform as RectTransform) || PointerInside(dropdownList);
        }

        RectTransform FindDropdownBlocker()
        {
            if (dropdown == null) return null;
            var canvas = dropdown.GetComponentInParent<Canvas>();
            var root = canvas != null && canvas.rootCanvas != null ? canvas.rootCanvas.transform : dropdown.transform.root;
            if (root == null) return null;

            var rects = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                var rect = rects[i];
                if (rect == null || !rect.gameObject.activeInHierarchy) continue;
                if (rect.name != "Blocker") continue;
                return rect;
            }

            return null;
        }

        void SuppressBlockerVisual()
        {
            if (blocker == null) blocker = FindDropdownBlocker();
            if (blocker == null) return;

            var image = blocker.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.clear;
            }

            var selectable = blocker.GetComponent<Selectable>();
            if (selectable != null)
            {
                selectable.transition = Selectable.Transition.None;
                var colors = selectable.colors;
                colors.normalColor = Color.clear;
                colors.highlightedColor = Color.clear;
                colors.pressedColor = Color.clear;
                colors.selectedColor = Color.clear;
                colors.disabledColor = Color.clear;
                colors.colorMultiplier = 0f;
                selectable.colors = colors;
            }

            var highlight = blocker.GetComponent<UiSelectionHighlight>();
            if (highlight != null)
            {
                highlight.SetForceSelected(false);
                highlight.enabled = false;
            }

            var childImages = blocker.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < childImages.Length; i++)
            {
                var child = childImages[i];
                if (child == null || child.gameObject == blocker.gameObject) continue;
                if (child.name.StartsWith("State Fill") ||
                    child.name.StartsWith("Selected Edge") ||
                    child.name.StartsWith("Selected Shadow"))
                {
                    child.gameObject.SetActive(false);
                    child.color = Color.clear;
                }
            }
        }

        static bool PointerInside(RectTransform rect)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy) return false;
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, Input.mousePosition, camera);
        }
    }
}
