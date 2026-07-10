using UnityEngine;

namespace AreaSurvivors
{
    [DisallowMultipleComponent]
    public sealed class CharacterFootprint : MonoBehaviour
    {
        [SerializeField] BoxCollider2D footCollider;
        public Color gizmoColor = new Color(0.25f, 0.95f, 1f, 0.9f);

        public BoxCollider2D FootCollider
        {
            get
            {
                Refresh();
                return footCollider;
            }
        }

        public bool HasUsableCollider
        {
            get
            {
                var collider = FootCollider;
                return collider != null && collider.enabled;
            }
        }

        public Bounds Bounds
        {
            get
            {
                var collider = FootCollider;
                return collider != null ? collider.bounds : new Bounds(transform.position, Vector3.zero);
            }
        }

        public Vector3 SamplePosition
        {
            get
            {
                var collider = FootCollider;
                if (collider == null || !collider.enabled) return transform.position;
                var center = collider.bounds.center;
                return new Vector3(center.x, center.y, transform.position.z);
            }
        }

        public Vector3 BottomCenter
        {
            get
            {
                var bounds = Bounds;
                return new Vector3(bounds.center.x, bounds.min.y, transform.position.z);
            }
        }

        public float FrontY
        {
            get
            {
                var collider = FootCollider;
                return collider != null && collider.enabled ? collider.bounds.min.y : transform.position.y;
            }
        }

        public float ProbeRadiusWorld
        {
            get
            {
                var bounds = Bounds;
                return Mathf.Max(bounds.size.x, bounds.size.y) * 0.5f;
            }
        }

        public void SetFootCollider(BoxCollider2D collider)
        {
            footCollider = collider;
        }

        public void Refresh()
        {
            if (footCollider == null) footCollider = GetComponent<BoxCollider2D>();
        }

        void Awake()
        {
            Refresh();
        }

        void OnValidate()
        {
            Refresh();
        }

        void OnDrawGizmosSelected()
        {
            var collider = FootCollider;
            if (collider == null) return;

            var bounds = collider.bounds;
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.DrawLine(BottomCenter + Vector3.left * 0.08f, BottomCenter + Vector3.right * 0.08f);
            Gizmos.DrawLine(BottomCenter + Vector3.down * 0.08f, BottomCenter + Vector3.up * 0.08f);
        }
    }
}
