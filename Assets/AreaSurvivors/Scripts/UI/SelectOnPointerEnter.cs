using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class SelectOnPointerEnter : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!UiSelectionUtility.PointerCanDriveFocus()) return;
            UiSelectionUtility.NotifyKeyboardMouseInput();
            var selectable = GetComponent<Selectable>();
            if (EventSystem.current != null && selectable != null && selectable.IsInteractable())
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }
        }
    }
}
