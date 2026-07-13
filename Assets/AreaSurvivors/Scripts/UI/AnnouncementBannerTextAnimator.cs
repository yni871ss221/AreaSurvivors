using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AreaSurvivors
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class AnnouncementBannerTextAnimator : BaseMeshEffect
    {
        [Min(0f)] public float enterDuration = 0.7f;
        [Min(0f)] public float holdDuration = 1.1f;
        [Min(0f)] public float exitDuration = 0.65f;
        [Min(0f)] public float edgePadding = 48f;

        float horizontalOffset;

        public IEnumerator Play(string message)
        {
            var label = graphic as Text;
            var viewport = transform.parent as RectTransform;
            if (label == null || viewport == null) yield break;

            label.text = LocalizationService.LocalizeSource(message);
            label.SetVerticesDirty();

            float travelDistance = viewport.rect.width * 0.5f
                + label.preferredWidth * 0.5f
                + edgePadding;

            yield return AnimateOffset(-travelDistance, 0f, enterDuration, EaseOutCubic);

            if (holdDuration > 0f)
            {
                float holdUntil = Time.unscaledTime + holdDuration;
                while (Time.unscaledTime < holdUntil) yield return null;
            }

            yield return AnimateOffset(0f, travelDistance, exitDuration, EaseInCubic);
        }

        public void ResetVisual()
        {
            SetHorizontalOffset(0f);
        }

        public override void ModifyMesh(VertexHelper vertexHelper)
        {
            if (!IsActive() || vertexHelper == null || Mathf.Approximately(horizontalOffset, 0f)) return;

            UIVertex vertex = default;
            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                vertex.position.x += horizontalOffset;
                vertexHelper.SetUIVertex(vertex, i);
            }
        }

        IEnumerator AnimateOffset(float from, float to, float duration, System.Func<float, float> ease)
        {
            if (duration <= 0f)
            {
                SetHorizontalOffset(to);
                yield break;
            }

            float elapsed = 0f;
            SetHorizontalOffset(from);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetHorizontalOffset(Mathf.LerpUnclamped(from, to, ease(t)));
                yield return null;
            }

            SetHorizontalOffset(to);
        }

        void SetHorizontalOffset(float offset)
        {
            horizontalOffset = offset;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        static float EaseOutCubic(float t)
        {
            float inverse = 1f - t;
            return 1f - inverse * inverse * inverse;
        }

        static float EaseInCubic(float t)
        {
            return t * t * t;
        }
    }
}
