using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    public sealed class UiBoxOutline : MonoBehaviour
    {
        public Color color = new Color(0.58f, 0.68f, 0.40f, 0.9f);
        public float thickness = 2f;

        Outline outline;

        public static UiBoxOutline Apply(Transform target, Color color, float thickness)
        {
            if (target == null || target.GetComponent<Graphic>() == null) return null;
            var box = target.GetComponent<UiBoxOutline>();
            if (box == null) box = target.gameObject.AddComponent<UiBoxOutline>();
            box.color = color;
            box.thickness = thickness;
            box.Sync();
            RemoveLegacyEdges(target);
            return box;
        }

        void OnEnable()
        {
            Sync();
        }

        void OnValidate()
        {
            Sync();
        }

        void Sync()
        {
            outline = GetComponent<Outline>();
            if (outline == null) outline = gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(thickness, -thickness);
            outline.useGraphicAlpha = true;
        }

        static void RemoveLegacyEdges(Transform target)
        {
            RemoveChild(target, "Top Edge");
            RemoveChild(target, "Bottom Edge");
            RemoveChild(target, "Left Edge");
            RemoveChild(target, "Right Edge");
        }

        static void RemoveChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) return;
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
