using UnityEngine;

namespace AreaSurvivors
{
    public sealed class CharacterSelectionHighlight : MonoBehaviour
    {
        public CharacterType type;
        UiSelectionHighlight highlight;

        void Awake()
        {
            highlight = GetComponent<UiSelectionHighlight>();
        }

        void Update()
        {
            if (highlight == null) highlight = GetComponent<UiSelectionHighlight>();
            if (highlight != null) highlight.forceSelected = RunState.SelectedCharacter == type;
        }
    }
}
