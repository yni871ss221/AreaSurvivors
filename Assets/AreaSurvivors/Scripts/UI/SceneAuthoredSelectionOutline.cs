using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class SceneAuthoredSelectionOutline : MonoBehaviour
    {
        public GameObject outlineRoot;

        Selectable selectable;
        bool lastVisible;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
            Refresh(true);
        }

        void OnEnable()
        {
            Refresh(true);
        }

        void OnDisable()
        {
            SetVisible(false);
        }

        void LateUpdate()
        {
            Refresh(false);
        }

        void Refresh(bool force)
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            bool visible = selectable != null &&
                selectable.IsInteractable() &&
                EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == gameObject;
            if (force || visible != lastVisible) SetVisible(visible);
        }

        void SetVisible(bool visible)
        {
            lastVisible = visible;
            if (outlineRoot != null && outlineRoot.activeSelf != visible)
            {
                outlineRoot.SetActive(visible);
            }
        }
    }
}
