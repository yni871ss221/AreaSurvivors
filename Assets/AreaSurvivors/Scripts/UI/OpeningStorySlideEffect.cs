using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Graphic))]
    public sealed class OpeningStorySlideEffect : BaseMeshEffect
    {
        [SerializeField] Vector2 offset;

        public Vector2 Offset
        {
            get => offset;
            set
            {
                if ((offset - value).sqrMagnitude < 0.0001f) return;
                offset = value;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper == null || vertexHelper.currentVertCount == 0) return;

            UIVertex vertex = default;
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                vertex.position += (Vector3)offset;
                vertexHelper.SetUIVertex(vertex, i);
            }
        }
    }
}
