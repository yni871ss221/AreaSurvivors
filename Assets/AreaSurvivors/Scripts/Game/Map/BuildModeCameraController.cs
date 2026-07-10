using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Camera))]
    public sealed class BuildModeCameraController : MonoBehaviour
    {
        public TileGrid grid;
        public Transform focusTarget;
        public float minOrthographicSize = 5f;
        public float maxOrthographicSize = 18f;
        public float zoomSpeed = 1.25f;

        Camera targetCamera;
        Vector3 lastPointerWorld;
        bool dragging;
        int focusFramesRemaining;
        bool userMovedCamera;

        void Awake()
        {
            targetCamera = GetComponent<Camera>();
        }

        public void Configure(TileGrid tileGrid, Transform target = null)
        {
            grid = tileGrid;
            focusTarget = target;
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera != null) targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize, minOrthographicSize, maxOrthographicSize);
            focusFramesRemaining = 3;
            userMovedCamera = false;
            CenterOnFocus();
        }

        void Update()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) return;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                userMovedCamera = true;
                targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize - wheel * zoomSpeed, minOrthographicSize, maxOrthographicSize);
                ClampToMap();
            }

            bool dragButton = Input.GetMouseButton(1) || Input.GetMouseButton(2);
            if (!dragButton)
            {
                dragging = false;
                return;
            }

            var pointerWorld = PointerWorld();
            if (!dragging)
            {
                dragging = true;
                lastPointerWorld = pointerWorld;
                return;
            }

            transform.position += lastPointerWorld - pointerWorld;
            userMovedCamera = true;
            ClampToMap();
        }

        void LateUpdate()
        {
            if (focusFramesRemaining <= 0 || userMovedCamera) return;
            focusFramesRemaining--;
            CenterOnFocus();
        }

        void CenterOnFocus()
        {
            if (targetCamera == null) return;
            Vector3 center;
            if (focusTarget != null)
            {
                center = FocusWorldCenter(focusTarget);
            }
            else
            {
                if (grid == null) return;
                center = grid.GridToWorld(grid.width / 2, grid.height / 2);
            }
            MoveViewCenterTo(center);
        }

        Vector3 PointerWorld()
        {
            var ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            var plane = new Plane(Vector3.forward, Vector3.zero);
            return plane.Raycast(ray, out var distance) ? ray.GetPoint(distance) : transform.position;
        }

        void ClampToMap()
        {
            if (grid == null || targetCamera == null) return;
            var min = grid.GridToWorld(0, 0);
            var max = grid.GridToWorld(grid.width - 1, grid.height - 1);
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * targetCamera.aspect;
            float minX = Mathf.Min(min.x, max.x) + halfWidth;
            float maxX = Mathf.Max(min.x, max.x) - halfWidth;
            float minY = Mathf.Min(min.y, max.y) + halfHeight;
            float maxY = Mathf.Max(min.y, max.y) - halfHeight;
            var center = ViewCenterOnWorldPlane();
            center.x = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : (min.x + max.x) * 0.5f;
            center.y = minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : (min.y + max.y) * 0.5f;
            MoveViewCenterTo(center);
        }

        void MoveViewCenterTo(Vector3 worldPoint)
        {
            var currentCenter = ViewCenterOnWorldPlane();
            transform.position += worldPoint - currentCenter;
        }

        Vector3 ViewCenterOnWorldPlane()
        {
            var ray = targetCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var plane = new Plane(Vector3.forward, Vector3.zero);
            return plane.Raycast(ray, out var distance) ? ray.GetPoint(distance) : transform.position;
        }

        static Vector3 FocusWorldCenter(Transform target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return target.position;

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds.center;
        }
    }
}
