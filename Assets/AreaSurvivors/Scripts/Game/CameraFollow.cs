using System.Collections;
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
        public float targetWeight = 1f;
        public TileGrid grid;
        public bool useGridBounds = true;
        public Vector2 minimumPosition = new Vector2(-25f, -25f);
        public Vector2 maximumPosition = new Vector2(25f, 25f);
        [Min(0f)]
        public float boundsPadding = 0.25f;

        Camera cachedCamera;
        float zoom = 0.5f;
        bool cutsceneCameraMoveActive;

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
            useGridBounds = config.cameraUseGridBounds;
            minimumPosition = config.cameraMinimumPosition;
            maximumPosition = config.cameraMaximumPosition;
            boundsPadding = config.cameraBoundsPadding;
            if (grid == null) grid = FindObjectOfType<TileGrid>();
            zoom = Mathf.Clamp01(defaultZoom);
            if (cachedCamera == null) cachedCamera = GetComponent<Camera>();
            ApplyZoomImmediate();
        }

        public IEnumerator MoveToCutsceneTarget(Transform focusTarget, float duration)
        {
            if (focusTarget == null) yield break;
            if (cachedCamera == null) cachedCamera = GetComponent<Camera>();

            cutsceneCameraMoveActive = true;
            target = focusTarget;
            anchor = null;
            targetWeight = 1f;

            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            float startSize = cachedCamera != null ? cachedCamera.orthographicSize : Mathf.Lerp(zoomedInOrthographicSize, orthographicSize, zoom);
            float currentPitch = Mathf.Lerp(zoomedInPitch, pitch, zoom);
            Vector3 currentOffset = Vector3.Lerp(zoomedInOffset, offset, zoom);
            Quaternion targetRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            float elapsed = 0f;
            duration = Mathf.Max(0.1f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                Vector3 targetPosition = ClampCameraPosition(focusTarget.position + currentOffset, currentPitch, startSize);
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);
                if (cachedCamera != null) cachedCamera.orthographicSize = startSize;
                yield return null;
            }

            transform.position = ClampCameraPosition(focusTarget.position + currentOffset, currentPitch, startSize);
            transform.rotation = targetRotation;
            if (cachedCamera != null) cachedCamera.orthographicSize = startSize;
            cutsceneCameraMoveActive = false;
        }

        void LateUpdate()
        {
            if (cutsceneCameraMoveActive) return;
            if (target == null) return;
            if (Time.timeScale > 0f)
            {
                var wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.001f)
                {
                    zoom = Mathf.Clamp01(zoom - wheel * Mathf.Max(0.01f, scrollSpeed));
                }
            }

            var focus = target.position;
            if (anchor != null) focus = Vector3.Lerp(anchor.position, target.position, targetWeight);
            var currentOffset = Vector3.Lerp(zoomedInOffset, offset, zoom);
            var currentPitch = Mathf.Lerp(zoomedInPitch, pitch, zoom);
            var currentSize = Mathf.Lerp(zoomedInOrthographicSize, orthographicSize, zoom);
            var desired = focus + currentOffset;
            desired = ClampCameraPosition(desired, currentPitch, currentSize);
            float deltaTime = Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
            float activeSmooth = Time.timeScale > 0f ? smooth : smooth * 0.18f;
            float damp = 1f - Mathf.Exp(-activeSmooth * deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, damp);
            transform.rotation = Quaternion.Euler(currentPitch, 0f, 0f);
            if (cachedCamera == null) cachedCamera = GetComponent<Camera>();
            if (cachedCamera != null) cachedCamera.orthographicSize = Mathf.Lerp(cachedCamera.orthographicSize, currentSize, damp);
        }

        void ApplyZoomImmediate()
        {
            transform.rotation = Quaternion.Euler(Mathf.Lerp(zoomedInPitch, pitch, zoom), 0f, 0f);
            if (cachedCamera != null) cachedCamera.orthographicSize = Mathf.Lerp(zoomedInOrthographicSize, orthographicSize, zoom);
        }

        Vector3 ClampCameraPosition(Vector3 desired, float currentPitch, float currentSize)
        {
            Bounds bounds;
            if (useGridBounds && grid != null)
            {
                bounds = grid.GetWorldBounds();
            }
            else
            {
                var min = Vector2.Min(minimumPosition, maximumPosition);
                var max = Vector2.Max(minimumPosition, maximumPosition);
                bounds = new Bounds((min + max) * 0.5f, new Vector3(max.x - min.x, max.y - min.y, 0.1f));
            }

            var rotation = Quaternion.Euler(currentPitch, 0f, 0f);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;
            float halfHeight = currentSize;
            float halfWidth = currentSize * (cachedCamera != null ? cachedCamera.aspect : 16f / 9f);
            float minOffsetX = float.PositiveInfinity;
            float maxOffsetX = float.NegativeInfinity;
            float minOffsetY = float.PositiveInfinity;
            float maxOffsetY = float.NegativeInfinity;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    Vector3 origin = desired + right * (halfWidth * x) + up * (halfHeight * y);
                    float distance = Mathf.Abs(forward.z) > 0.0001f ? -origin.z / forward.z : 0f;
                    Vector3 ground = origin + forward * distance;
                    Vector3 offsetFromCamera = ground - desired;
                    minOffsetX = Mathf.Min(minOffsetX, offsetFromCamera.x);
                    maxOffsetX = Mathf.Max(maxOffsetX, offsetFromCamera.x);
                    minOffsetY = Mathf.Min(minOffsetY, offsetFromCamera.y);
                    maxOffsetY = Mathf.Max(maxOffsetY, offsetFromCamera.y);
                }
            }

            float minX = bounds.min.x + boundsPadding - minOffsetX;
            float maxX = bounds.max.x - boundsPadding - maxOffsetX;
            float minY = bounds.min.y + boundsPadding - minOffsetY;
            float maxY = bounds.max.y - boundsPadding - maxOffsetY;
            desired.x = ClampOrCenter(desired.x, minX, maxX, bounds.center.x);
            desired.y = ClampOrCenter(desired.y, minY, maxY, bounds.center.y);
            return desired;
        }

        static float ClampOrCenter(float value, float min, float max, float center)
        {
            return min <= max ? Mathf.Clamp(value, min, max) : center;
        }
    }
}
