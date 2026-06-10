using UnityEngine;

namespace AreaSurvivors
{
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class KnockbackReceiver : MonoBehaviour
    {
        Rigidbody2D body;
        float timer;
        Vector2 velocity;

        public bool Active => timer > 0f;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        public void Apply(Vector2 direction, float strength, float duration)
        {
            if (body == null || strength <= 0f || duration <= 0f) return;
            if (direction.sqrMagnitude < 0.001f) direction = Vector2.down;
            velocity = direction.normalized * strength;
            timer = duration;
            body.velocity = velocity;
        }

        void FixedUpdate()
        {
            if (timer <= 0f) return;
            timer -= Time.fixedDeltaTime;
            body.velocity = velocity;
            if (timer <= 0f) velocity = Vector2.zero;
        }
    }
}
