using UnityEngine;

namespace AreaSurvivors
{
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Transform anchor;
        public float smooth = 8f;
        public Vector3 offset = new Vector3(0f, -15.5f, -19f);
        public Vector3 zoomedInOffset = new Vector3(0f, -8.5f, -9f);
        public float orthographicSize = 12.5f;
        public float zoomedInOrthographicSize = 3.9f;
        public float pitch = -45f;
        public float zoomedInPitch = -35f;
        [Range(0f, 1f)]
        public float defaultZoom = 0.5f;
        public float scrollSpeed = 0.16f;
        [Range(0f, 1f)]
        public float targetWeight = 0.55f;

        Camera cachedCamera;
        float zoom = 0.5f;

        void Awake()
        {
            cachedCamera = GetComponent<Camera>();
        }

        public void Configure(Transform followTarget, Transform mapAnchor, GameConfig config)
        {
            target = followTarget;
            anchor = mapAnchor;
            if (config == null) return;
            offset = config.cameraOffset;
            zoomedInOffset = config.cameraZoomedInOffset;
            orthographicSize = config.cameraOrthographicSize;
            zoomedInOrthographicSize = config.cameraZoomedInOrthographicSize;
            pitch = config.cameraPitch;
            zoomedInPitch = config.cameraZoomedInPitch;
            defaultZoom = config.cameraDefaultZoom;
            scrollSpeed = config.cameraZoomScrollSpeed;
            targetWeight = config.cameraPlayerWeight;
            zoom = Mathf.Clamp01(defaultZoom);
            if (cachedCamera == null) cachedCamera = GetComponent<Camera>();
            ApplyZoomImmediate();
        }

        void LateUpdate()
        {
            if (target == null) return;
            var wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.001f)
            {
                zoom = Mathf.Clamp01(zoom - wheel * Mathf.Max(0.01f, scrollSpeed));
            }

            var focus = target.position;
            if (anchor != null) focus = Vector3.Lerp(anchor.position, target.position, targetWeight);
            var currentOffset = Vector3.Lerp(zoomedInOffset, offset, zoom);
            var currentPitch = Mathf.Lerp(zoomedInPitch, pitch, zoom);
            var currentSize = Mathf.Lerp(zoomedInOrthographicSize, orthographicSize, zoom);
            var desired = focus + currentOffset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-smooth * Time.deltaTime));
            transform.rotation = Quaternion.Euler(currentPitch, 0f, 0f);
            if (cachedCamera == null) cachedCamera = GetComponent<Camera>();
            if (cachedCamera != null) cachedCamera.orthographicSize = Mathf.Lerp(cachedCamera.orthographicSize, currentSize, 1f - Mathf.Exp(-smooth * Time.deltaTime));
        }

        void ApplyZoomImmediate()
        {
            transform.rotation = Quaternion.Euler(Mathf.Lerp(zoomedInPitch, pitch, zoom), 0f, 0f);
            if (cachedCamera != null) cachedCamera.orthographicSize = Mathf.Lerp(zoomedInOrthographicSize, orthographicSize, zoom);
        }
    }
}
