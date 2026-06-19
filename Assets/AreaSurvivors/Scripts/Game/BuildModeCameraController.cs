using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Camera))]
    public sealed class BuildModeCameraController : MonoBehaviour
    {
        public TileGrid grid;
        public float minOrthographicSize = 5f;
        public float maxOrthographicSize = 18f;
        public float zoomSpeed = 1.25f;

        Camera targetCamera;
        Vector3 lastPointerWorld;
        bool dragging;

        void Awake()
        {
            targetCamera = GetComponent<Camera>();
        }

        public void Configure(TileGrid tileGrid)
        {
            grid = tileGrid;
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera != null) targetCamera.orthographicSize = Mathf.Clamp(targetCamera.orthographicSize, minOrthographicSize, maxOrthographicSize);
            CenterOnMap();
        }

        void Update()
        {
            if (targetCamera == null) targetCamera = GetComponent<Camera>();
            if (targetCamera == null) return;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
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
            ClampToMap();
        }

        void CenterOnMap()
        {
            if (grid == null) return;
            var center = grid.GridToWorld(grid.width / 2, grid.height / 2);
            transform.position = new Vector3(center.x, center.y, transform.position.z);
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
            var pos = transform.position;
            pos.x = minX <= maxX ? Mathf.Clamp(pos.x, minX, maxX) : (min.x + max.x) * 0.5f;
            pos.y = minY <= maxY ? Mathf.Clamp(pos.y, minY, maxY) : (min.y + max.y) * 0.5f;
            transform.position = pos;
        }
    }
}
