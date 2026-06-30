using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class ShieldOrbitController : MonoBehaviour
    {
        public GameObject shieldPrefab;

        readonly List<ShieldOrbitShield> shields = new List<ShieldOrbitShield>();
        WeaponController weapon;
        Transform orbitTarget;
        GameConfig config;
        WeaponStatBlock stats;
        bool active;
        float angleDegrees;

        public void Configure(WeaponController owner, Transform target, GameConfig gameConfig)
        {
            weapon = owner;
            orbitTarget = target;
            config = gameConfig;
            SetActive(false);
        }

        public void SetActive(bool value)
        {
            active = value;
            for (int i = 0; i < shields.Count; i++)
            {
                if (shields[i] != null) shields[i].gameObject.SetActive(value);
            }
        }

        public void SetStats(WeaponStatBlock value)
        {
            stats = value;
            EnsureShieldCount(Mathf.Max(1, stats.projectileCount));
            ApplyShieldStats();
        }

        void Update()
        {
            if (!active || orbitTarget == null) return;
            EnsureShieldCount(Mathf.Max(1, stats.projectileCount));
            angleDegrees += Mathf.Max(0f, stats.rotationSpeed) * Time.deltaTime;
            PositionShields();
        }

        void EnsureShieldCount(int count)
        {
            for (int i = shields.Count; i < count; i++)
            {
                var instance = shieldPrefab != null ? Instantiate(shieldPrefab, transform) : null;
                if (instance == null) break;
                instance.name = "Orbit Shield " + (i + 1);
                var shield = instance.GetComponent<ShieldOrbitShield>();
                if (shield == null)
                {
                    Debug.LogWarning("Shield prefab is missing ShieldOrbitShield.");
                    Destroy(instance);
                    break;
                }

                shields.Add(shield);
            }

            for (int i = 0; i < shields.Count; i++)
            {
                if (shields[i] == null) continue;
                shields[i].gameObject.SetActive(active && i < count);
            }
        }

        void ApplyShieldStats()
        {
            float knockbackForce = stats.knockback * (config != null ? config.knockbackForceUnit : 2.2f);
            float knockbackSeconds = config != null ? config.knockbackDuration : 0.16f;
            float hitCooldown = config != null ? config.shieldHitCooldownSeconds : 0.35f;
            for (int i = 0; i < shields.Count; i++)
            {
                if (shields[i] == null) continue;
                shields[i].Configure(weapon, stats.attackPower, knockbackForce, knockbackSeconds, hitCooldown);
            }
        }

        void PositionShields()
        {
            int count = Mathf.Max(1, stats.projectileCount);
            float radius = Mathf.Max(0.05f, stats.range);
            for (int i = 0; i < shields.Count; i++)
            {
                var shield = shields[i];
                if (shield == null || !shield.gameObject.activeSelf) continue;
                float degree = angleDegrees + 360f * i / count;
                float radian = degree * Mathf.Deg2Rad;
                var offset = new Vector3(Mathf.Cos(radian), Mathf.Sin(radian), 0f) * radius;
                shield.transform.position = orbitTarget.position + offset;
                shield.transform.rotation = Quaternion.Euler(0f, 0f, degree);
            }
        }
    }

}
