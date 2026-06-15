using UnityEngine;

namespace AreaSurvivors
{
    public sealed class AnchoredPaperBillboard : MonoBehaviour
    {
        public Vector3 localAnchorPoint = new Vector3(0f, -0.5f, 0f);
        public bool projectAnchorToParentGroundPlane = true;
        public float rollDegrees;

        Vector3 parentAnchorPosition;
        bool captured;

        void Awake()
        {
            CaptureAnchor();
        }

        public void CaptureAnchor()
        {
            parentAnchorPosition = transform.localPosition + transform.localRotation * Vector3.Scale(transform.localScale, localAnchorPoint);
            if (projectAnchorToParentGroundPlane) parentAnchorPosition.z = 0f;
            captured = true;
        }

        void LateUpdate()
        {
            if (Camera.main == null) return;
            if (!captured) CaptureAnchor();

            var worldRotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 0f, rollDegrees);
            transform.localRotation = transform.parent != null
                ? Quaternion.Inverse(transform.parent.rotation) * worldRotation
                : worldRotation;
            transform.localPosition = parentAnchorPosition - transform.localRotation * Vector3.Scale(transform.localScale, localAnchorPoint);
        }
    }
}
