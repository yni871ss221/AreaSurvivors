using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class SkillNodeView : MonoBehaviour
    {
        [System.Serializable]
        public sealed class SkillLinkRoute
        {
            public UpgradeType prerequisite;
            public Vector2Int[] waypoints;
        }

        public UpgradeType type;
        [Header("Layout")]
        public bool useGridPosition = true;
        public Vector2Int gridPosition;
        public Vector2 gridCellSize = new Vector2(110f, 90f);
        [Header("Links")]
        public UpgradeType[] prerequisites;
        public SkillLinkRoute[] linkRoutes;
        [Header("Content")]
        public bool implemented = true;
        public string title;
        [TextArea]
        public string description;
        public Button button;
        public Image background;
        public Image icon;
        public Text statusText;

        public RectTransform RectTransform => transform as RectTransform;

        void OnValidate()
        {
            ApplyGridPosition();
        }

        public void ApplyGridPosition()
        {
            if (!useGridPosition) return;
            var rect = RectTransform;
            if (rect == null) return;
            rect.anchoredPosition = GridToAnchored(gridPosition);
        }

        public Vector2 GridToAnchored(Vector2Int grid)
        {
            return new Vector2(grid.x * gridCellSize.x, grid.y * gridCellSize.y);
        }

        public UpgradeType[] EffectivePrerequisites()
        {
            if (linkRoutes != null && linkRoutes.Length > 0)
            {
                var result = new UpgradeType[linkRoutes.Length];
                for (int i = 0; i < linkRoutes.Length; i++)
                {
                    result[i] = linkRoutes[i].prerequisite;
                }

                return result;
            }

            return prerequisites ?? System.Array.Empty<UpgradeType>();
        }

        public void ResolveReferences()
        {
            if (button == null) button = transform.Find("Node Button")?.GetComponent<Button>();
            if (background == null && button != null) background = button.targetGraphic as Image;
            if (background == null) background = transform.Find("Node Button")?.GetComponent<Image>();
            if (icon == null) icon = transform.Find("Node Button/Icon")?.GetComponent<Image>();
            if (statusText == null) statusText = transform.Find("Node Cost")?.GetComponent<Text>();
        }
    }
}
