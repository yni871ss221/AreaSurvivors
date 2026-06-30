using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class AdvancedWeaponRuntime : MonoBehaviour
    {
        public GameObject areaPrefab;
        public GameObject arrowRainAreaPrefab;
        public GameObject frostAreaPrefab;
        public GameObject boomerangPrefab;
        public GameObject auraSlashPrefab;
        public GameObject bulletPrefab;
        public GameObject thunderBallPrefab;
        [SerializeField, Range(0.2f, 1f)] float flagAreaPerspectiveYScale = 0.65f;
        [SerializeField, Range(0.2f, 1f)] float arrowRainAreaPerspectiveYScale = 0.65f;
        [SerializeField, Range(0.2f, 1f)] float frostAreaPerspectiveYScale = 0.65f;

        readonly Dictionary<WeaponType, float> cooldownTimers = new Dictionary<WeaponType, float>();
        readonly Dictionary<WeaponType, Coroutine> burstRoutines = new Dictionary<WeaponType, Coroutine>();
        AdvancedWeaponArea flagArea;
        WeaponController weapon;
        PlayerController player;
        GameConfig config;

        public void Configure(WeaponController owner, PlayerController ownerPlayer, GameConfig gameConfig)
        {
            weapon = owner;
            player = ownerPlayer;
            config = gameConfig;
            cooldownTimers.Clear();
            StopAllCoroutines();
            burstRoutines.Clear();
            if (flagArea != null) flagArea.gameObject.SetActive(false);
        }

        public void Sync()
        {
            UpdateFlagArea();
        }

        void Update()
        {
            if (weapon == null || player == null || config == null) return;
            UpdateFlagArea();
            if (player.IsReviving) return;

            TryTickWeapon(WeaponType.BoomerangSword);
            TryTickWeapon(WeaponType.AuraSword);
            TryTickWeapon(WeaponType.ArrowRain);
            TryTickWeapon(WeaponType.Gun);
            TryTickWeapon(WeaponType.Frost);
            TryTickWeapon(WeaponType.ThunderBall);
        }

        void TryTickWeapon(WeaponType type)
        {
            if (!weapon.IsWeaponUnlocked(type)) return;
            float timer = cooldownTimers.TryGetValue(type, out var current) ? current : 0f;
            timer -= Time.deltaTime;
            if (timer > 0f)
            {
                cooldownTimers[type] = timer;
                return;
            }

            var stats = weapon.GetEffectiveWeaponStatsFor(type);
            cooldownTimers[type] = Mathf.Max(0.05f, stats.cooldownSeconds);
            Launch(type, stats);
        }

        void UpdateFlagArea()
        {
            bool active = weapon != null && player != null && weapon.IsWeaponUnlocked(WeaponType.Flag) && !player.IsReviving;
            if (!active)
            {
                if (flagArea != null) flagArea.gameObject.SetActive(false);
                return;
            }

            if (flagArea == null)
            {
                var instance = areaPrefab != null ? Instantiate(areaPrefab, transform) : null;
                if (instance == null) return;
                instance.name = "Flag Aura";
                flagArea = instance.GetComponent<AdvancedWeaponArea>();
            }

            if (flagArea == null) return;
            var stats = weapon.GetEffectiveWeaponStatsFor(WeaponType.Flag);
            flagArea.gameObject.SetActive(true);
            flagArea.Configure(
                player.transform,
                player.transform.position,
                stats.range,
                stats.attackPower,
                stats.damageIntervalSeconds,
                0f,
                stats.slowAmount,
                SfxTrack.ShieldHit,
                0f,
                0.28f,
                FlagAreaPerspectiveYScale());
        }

        void Launch(WeaponType type, WeaponStatBlock stats)
        {
            switch (type)
            {
                case WeaponType.ArrowRain:
                    SpawnArea(type, stats, Direction() * Mathf.Max(0f, stats.distance), SfxTrack.ArrowRainTick, 0.3f);
                    break;
                case WeaponType.Frost:
                    SpawnArea(type, stats, Vector2.zero, SfxTrack.FrostCast, 0f);
                    break;
                case WeaponType.BoomerangSword:
                case WeaponType.AuraSword:
                case WeaponType.Gun:
                case WeaponType.ThunderBall:
                    StartBurst(type, stats);
                    break;
            }
        }

        void SpawnArea(WeaponType type, WeaponStatBlock stats, Vector2 offset, SfxTrack sfx, float repeatSeconds)
        {
            GameObject prefab = AreaPrefabFor(type);
            var instance = prefab != null ? Instantiate(prefab) : null;
            if (instance == null) return;
            instance.name = WeaponCatalog.DisplayName(type) + " Area";
            var area = instance.GetComponent<AdvancedWeaponArea>();
            if (area == null) return;
            if (repeatSeconds <= 0f) AudioManager.PlaySfx(sfx);
            area.Configure(
                null,
                player.transform.position + (Vector3)offset,
                stats.range,
                stats.attackPower,
                stats.damageIntervalSeconds,
                Mathf.Max(0.1f, stats.durationSeconds),
                stats.slowAmount,
                sfx,
                repeatSeconds,
                0.42f,
                AreaPerspectiveYScale(type),
                type == WeaponType.ArrowRain);
        }

        float AreaPerspectiveYScale(WeaponType type)
        {
            if (type == WeaponType.ArrowRain) return arrowRainAreaPerspectiveYScale > 0f ? arrowRainAreaPerspectiveYScale : 0.65f;
            if (type == WeaponType.Frost) return frostAreaPerspectiveYScale > 0f ? frostAreaPerspectiveYScale : 0.65f;
            return 1f;
        }

        float FlagAreaPerspectiveYScale()
        {
            return flagAreaPerspectiveYScale > 0f ? flagAreaPerspectiveYScale : 0.65f;
        }

        GameObject AreaPrefabFor(WeaponType type)
        {
            if (type == WeaponType.ArrowRain && arrowRainAreaPrefab != null) return arrowRainAreaPrefab;
            if (type == WeaponType.Frost && frostAreaPrefab != null) return frostAreaPrefab;
            return areaPrefab;
        }

        void StartBurst(WeaponType type, WeaponStatBlock stats)
        {
            if (burstRoutines.TryGetValue(type, out var routine) && routine != null) return;
            burstRoutines[type] = StartCoroutine(BurstRoutine(type, stats));
        }

        IEnumerator BurstRoutine(WeaponType type, WeaponStatBlock stats)
        {
            int count = Mathf.Max(1, stats.projectileCount);
            for (int i = 0; i < count; i++)
            {
                SpawnProjectile(type, stats);
                if (i < count - 1) yield return new WaitForSeconds(0.5f);
            }

            burstRoutines[type] = null;
        }

        void SpawnProjectile(WeaponType type, WeaponStatBlock stats)
        {
            GameObject prefab = PrefabFor(type);
            if (prefab == null) return;
            if (type == WeaponType.BoomerangSword) AudioManager.PlaySfx(SfxTrack.BoomerangSwordThrow);
            else if (type == WeaponType.AuraSword) AudioManager.PlaySfx(SfxTrack.AuraSwordCast);
            else if (type == WeaponType.Gun) AudioManager.PlaySfx(SfxTrack.GunShot);
            else if (type == WeaponType.ThunderBall) AudioManager.PlaySfx(SfxTrack.ThunderBallCast);

            var instance = Instantiate(prefab, transform.position, Quaternion.identity);
            instance.name = WeaponCatalog.DisplayName(type) + " Projectile";
            var projectile = instance.GetComponent<AdvancedWeaponProjectile>();
            if (projectile == null) return;
            projectile.Configure(type, DirectionForProjectile(type), stats, config);
        }

        GameObject PrefabFor(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.BoomerangSword: return boomerangPrefab;
                case WeaponType.AuraSword: return auraSlashPrefab;
                case WeaponType.Gun: return bulletPrefab;
                case WeaponType.ThunderBall: return thunderBallPrefab;
                default: return null;
            }
        }

        Vector2 DirectionForProjectile(WeaponType type)
        {
            var direction = Direction();
            if (type != WeaponType.AuraSword) return direction;
            float angle = Random.Range(-22.5f, 22.5f);
            return Quaternion.Euler(0f, 0f, angle) * direction;
        }

        Vector2 Direction()
        {
            var direction = player != null && player.Facing.sqrMagnitude > 0.01f ? player.Facing.normalized : Vector2.down;
            return direction.sqrMagnitude > 0.01f ? direction : Vector2.down;
        }
    }
}
