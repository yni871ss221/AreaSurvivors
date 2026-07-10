using UnityEngine;

namespace AreaSurvivors
{
    public sealed class ExperienceOrb : MonoBehaviour
    {
        public int value = 1;
        public float attractRange = 3f;
        public float speed = 6f;

        void Update()
        {
            var player = GameManager.Instance == null ? null : GameManager.Instance.Player;
            if (player == null) return;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < attractRange)
            {
                transform.position = Vector3.MoveTowards(transform.position, player.transform.position, speed * Time.deltaTime);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null) return;
            AudioManager.PlaySfx(SfxTrack.ExperiencePickup);
            GameManager.Instance?.AddExperience(value);
            Destroy(gameObject);
        }
    }
}
