using UnityEngine;
using UnityEngine.EventSystems;

namespace AreaSurvivors
{
    public sealed class SelectOnPointerEnter : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}
