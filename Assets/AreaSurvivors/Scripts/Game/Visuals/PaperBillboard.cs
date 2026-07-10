using UnityEngine;

namespace AreaSurvivors
{
    public sealed class PaperBillboard : MonoBehaviour
    {
        public bool faceCamera = true;
        public float rollDegrees;

        void LateUpdate()
        {
            if (!faceCamera || Camera.main == null) return;
            transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 0f, rollDegrees);
        }
    }
}
