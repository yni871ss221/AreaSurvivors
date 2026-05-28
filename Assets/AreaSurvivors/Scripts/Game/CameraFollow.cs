using UnityEngine;

namespace AreaSurvivors
{
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float smooth = 8f;

        void LateUpdate()
        {
            if (target == null) return;
            var desired = new Vector3(target.position.x, target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        }
    }
}
