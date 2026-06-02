using UnityEngine;

namespace AreaSurvivors
{
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smooth = 8f;
        public Vector3 offset = new Vector3(0f, -12f, -14f);

        void LateUpdate()
        {
            if (target == null) return;
            var desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        }
    }
}
