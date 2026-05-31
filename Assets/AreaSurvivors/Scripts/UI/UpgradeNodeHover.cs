using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AreaSurvivors
{
    public sealed class UpgradeNodeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Text title;
        public Text description;
        public string titleText;
        public string descriptionText;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (title != null) title.text = titleText;
            if (description != null) description.text = descriptionText;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (title != null) title.text = "\u30b9\u30ad\u30eb\u3092\u9078\u629e";
            if (description != null) description.text = "\u30a2\u30a4\u30b3\u30f3\u306b\u30ab\u30fc\u30bd\u30eb\u3092\u5408\u308f\u305b\u308b\u3068\u5f37\u5316\u5185\u5bb9\u3092\u78ba\u8a8d\u3067\u304d\u307e\u3059\u3002";
        }
    }
}
