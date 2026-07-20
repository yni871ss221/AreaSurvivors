using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class AdvancedWeaponRuntime : MonoBehaviour
    {
        public GameObject areaPrefab;
        public GameObject goddessBlessingAreaPrefab;
        public GameObject arrowRainAreaPrefab;
        public GameObject arrowShowerStrikePrefab;
        public GameObject frostAreaPrefab;
        public GameObject frostStormSpikePrefab;
        public GameObject boomerangPrefab;
        public GameObject bananaPrefab;
        public GameObject auraSlashPrefab;
        public GameObject excaliburSlashPrefab;
        public GameObject bulletPrefab;
        public GameObject machineGunBulletPrefab;
        public GameObject thunderBallPrefab;
        public GameObject thunderStormOrbPrefab;
        readonly Dictionary<WeaponType, float> cooldownTimers = new Dictionary<WeaponType, float>();
        readonly HashSet<WeaponType> activeBursts = new HashSet<WeaponType>();
        readonly List<AdvancedWeaponProjectile> thunderStormOrbits = new List<AdvancedWeaponProjectile>();
        AdvancedWeaponArea flagArea;
        WeaponType flagAreaDisplayType;
        WeaponController weapon;
        PlayerController player;
        GameConfig config;
        bool runtimeStopped;

        public void Configure(WeaponController owner, PlayerController ownerPlayer, GameConfig gameConfig)
        {
            weapon = owner;
            player = ownerPlayer;
            config = gameConfig;
            runtimeStopped = false;
            cooldownTimers.Clear();
            StopAllCoroutines();
            activeBursts.Clear();
            DestroyThunderStormOrbits();
            thunderStormOrbits.Clear();
            if (flagArea != null) flagArea.gameObject.SetActive(false);
        }

        public void Sync()
        {
            if (runtimeStopped) return;
            UpdateFlagArea();
        }

        public void StopRuntimeWeapons()
        {
            runtimeStopped = true;
            StopAllCoroutines();
            activeBursts.Clear();
            DestroyThunderStormOrbits();
            thunderStormOrbits.Clear();
            if (flagArea != null) flagArea.gameObject.SetActive(false);
            foreach (var area in FindObjectsOfType<AdvancedWeaponArea>())
            {
                if (area != null) Destroy(area.gameObject);
            }
        }

        void Update()
        {
            if (runtimeStopped) return;
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
            var displayType = weapon.GetDisplayWeaponType(type);
            float cooldown = displayType == WeaponType.Excalibur && config != null
                ? config.excaliburCooldownSeconds
                : stats.cooldownSeconds;
            cooldownTimers[type] = Mathf.Max(0.05f, cooldown);
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

            var displayType = weapon.GetDisplayWeaponType(WeaponType.Flag);
            if (flagArea != null && flagAreaDisplayType != displayType)
            {
                Destroy(flagArea.gameObject);
                flagArea = null;
            }
            if (flagArea == null)
            {
                var selectedPrefab = displayType == WeaponType.GoddessBlessing && goddessBlessingAreaPrefab != null
                    ? goddessBlessingAreaPrefab
                    : areaPrefab;
                var instance = selectedPrefab != null ? Instantiate(selectedPrefab, transform) : null;
                if (instance == null) return;
                instance.name = WeaponCatalog.DisplayName(displayType) + " Area";
                flagArea = instance.GetComponent<AdvancedWeaponArea>();
                flagAreaDisplayType = displayType;
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
                GridCellAspectY(),
                false,
                WeaponType.Flag,
                displayType == WeaponType.GoddessBlessing && config != null ? config.goddessBlessingHealAmount : 0);
        }

        void Launch(WeaponType type, WeaponStatBlock stats)
        {
            var displayType = weapon.GetDisplayWeaponType(type);
            switch (type)
            {
                case WeaponType.ArrowRain:
                    if (displayType == WeaponType.ArrowShower) StartGroundStrikeBurst(type, displayType, stats);
                    else SpawnArea(type, stats, Direction() * Mathf.Max(0f, stats.distance), SfxTrack.ArrowRainTick, 0.3f);
                    break;
                case WeaponType.Frost:
                    if (displayType == WeaponType.FrostStorm) StartGroundStrikeBurst(type, displayType, stats);
                    else SpawnArea(type, stats, Vector2.zero, SfxTrack.FrostCast, 0f);
                    break;
                case WeaponType.BoomerangSword:
                case WeaponType.AuraSword:
                case WeaponType.Gun:
                case WeaponType.ThunderBall:
                    StartBurst(type, displayType, stats);
                    break;
            }
        }

        void StartGroundStrikeBurst(WeaponType sourceType, WeaponType displayType, WeaponStatBlock stats)
        {
            if (!activeBursts.Add(sourceType)) return;
            StartCoroutine(TrackBurst(sourceType, GroundStrikeRoutine(displayType, stats)));
        }

        IEnumerator GroundStrikeRoutine(WeaponType displayType, WeaponStatBlock stats)
        {
            if (displayType == WeaponType.FrostStorm)
            {
                int targetCount = config != null ? Mathf.Max(1, config.frostStormTargetCount) : 5;
                var targets = CollectGroundStrikeTargets();
                for (int i = 0; i < targetCount && targets.Count > 0; i++)
                {
                    int index = Random.Range(0, targets.Count);
                    var target = targets[index];
                    targets.RemoveAt(index);
                    if (target != null) SpawnGroundStrike(displayType, stats, target);
                }
                AudioManager.PlaySfx(SfxTrack.FrostCast);
            }
            else
            {
                float interval = config != null ? Mathf.Max(0.05f, config.arrowShowerStrikeIntervalSeconds) : 0.25f;
                float duration = Mathf.Max(interval, stats.durationSeconds);
                float elapsed = 0f;
                while (elapsed < duration && !runtimeStopped && player != null && !player.IsReviving)
                {
                    var targets = CollectGroundStrikeTargets();
                    if (targets.Count > 0)
                    {
                        var target = targets[Random.Range(0, targets.Count)];
                        if (target != null) SpawnGroundStrike(displayType, stats, target);
                    }
                    AudioManager.PlaySfx(SfxTrack.ArrowRainTick);
                    yield return new WaitForSeconds(interval);
                    elapsed += interval;
                }
            }
        }

        IEnumerator TrackBurst(WeaponType sourceType, IEnumerator routine)
        {
            try
            {
                while (routine.MoveNext()) yield return routine.Current;
            }
            finally
            {
                activeBursts.Remove(sourceType);
            }
        }

        List<EnemyController> CollectGroundStrikeTargets()
        {
            var result = new List<EnemyController>();
            if (player == null) return result;

            Vector2 origin = player.transform.position;
            float cellWidth = TileGrid.DefaultCellSize;
            var grid = FindObjectOfType<TileGrid>();
            if (grid != null) cellWidth = Mathf.Max(0.01f, grid.WorldCellSize().x);
            float radiusCells = config != null ? Mathf.Max(0.1f, config.evolvedGroundStrikeTargetRadiusCells) : 15f;
            var enemies = FindObjectsOfType<EnemyController>();
            for (int i = 0; i < enemies.Length; i++)
            {
                var enemy = enemies[i];
                if (enemy == null) continue;
                var health = enemy.GetComponent<Health>();
                if (health == null || health.IsDead) continue;
                if (!IsWithinGroundStrikeTargetRadius(origin, enemy.transform.position, cellWidth, radiusCells)) continue;
                result.Add(enemy);
            }
            return result;
        }

        public static bool IsWithinGroundStrikeTargetRadius(Vector2 origin, Vector2 target, float cellWidth, float radiusCells)
        {
            float radius = Mathf.Max(0.01f, cellWidth) * Mathf.Max(0f, radiusCells);
            return (target - origin).sqrMagnitude <= radius * radius;
        }

        void SpawnGroundStrike(WeaponType displayType, WeaponStatBlock stats, EnemyController target)
        {
            if (target == null) return;
            Vector3 position = target.transform.position;
            var prefab = displayType == WeaponType.ArrowShower ? arrowShowerStrikePrefab : frostStormSpikePrefab;
            var instance = prefab != null ? Instantiate(prefab) : null;
            if (instance == null) return;
            instance.name = WeaponCatalog.DisplayName(displayType) + " Strike";
            var area = instance.GetComponent<AdvancedWeaponArea>();
            if (area == null)
            {
                Destroy(instance);
                return;
            }
            var animatorPlayback = instance.GetComponentInChildren<GroundStrikeAnimatorPlayback>(true);
            bool useAnimatorPlayback = animatorPlayback != null && animatorPlayback.enabled && animatorPlayback.gameObject.activeInHierarchy;
            if (useAnimatorPlayback) animatorPlayback.Restart();
            float impactDelay = useAnimatorPlayback ? animatorPlayback.ImpactDelaySeconds : 0f;
            float visualDuration = useAnimatorPlayback ? animatorPlayback.AnimationDurationSeconds : 0.2f;

            if (displayType == WeaponType.FrostStorm)
            {
                var spikeImpact = instance.GetComponent<FrostStormSpikeImpact>();
                if (spikeImpact == null)
                {
                    Debug.LogError("Frost Storm prefab requires FrostStormSpikeImpact.", instance);
                    Destroy(instance);
                    return;
                }

                area.Configure(null, position, stats.range, stats.attackPower, stats.damageIntervalSeconds,
                    Mathf.Max(0.1f, stats.durationSeconds), stats.slowAmount, SfxTrack.FrostCast, 0f, 0.42f,
                    GridCellAspectY(), false, WeaponType.Frost, 0, impactDelay);
                spikeImpact.Configure(stats.attackPower, impactDelay, WeaponType.Frost);
                return;
            }

            area.Configure(null, position, stats.range, stats.attackPower, 2f, Mathf.Max(0.2f, visualDuration),
                0f, SfxTrack.ArrowRainTick, 0f, 0.72f, GridCellAspectY(), true,
                WeaponCatalog.BaseWeaponOf(displayType), 0, impactDelay);
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
                GridCellAspectY(),
                type == WeaponType.ArrowRain,
                type);
        }

        float GridCellAspectY()
        {
            var grid = player != null ? player.grid : null;
            if (grid == null) grid = FindObjectOfType<TileGrid>();
            if (grid == null) return 1f;

            Vector2 cellSize = grid.WorldCellSize();
            return Mathf.Max(0.05f, cellSize.y / Mathf.Max(0.01f, cellSize.x));
        }

        GameObject AreaPrefabFor(WeaponType type)
        {
            if (type == WeaponType.ArrowRain && arrowRainAreaPrefab != null) return arrowRainAreaPrefab;
            if (type == WeaponType.Frost && frostAreaPrefab != null) return frostAreaPrefab;
            return areaPrefab;
        }

        void StartBurst(WeaponType sourceType, WeaponType displayType, WeaponStatBlock stats)
        {
            if (!activeBursts.Add(sourceType)) return;
            StartCoroutine(TrackBurst(sourceType, BurstRoutine(displayType, stats)));
        }

        IEnumerator BurstRoutine(WeaponType displayType, WeaponStatBlock stats)
        {
            int count = Mathf.Max(1, stats.projectileCount);
            if (displayType == WeaponType.Banana)
            {
                AudioManager.PlaySfx(SfxTrack.BoomerangSwordThrow);
                float angleOffset = Random.Range(0f, 360f);
                for (int i = 0; i < count; i++)
                {
                    float angle = angleOffset + 360f * i / count;
                    var direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                    SpawnProjectile(displayType, stats, direction, false);
                }

                yield break;
            }

            if (displayType == WeaponType.ThunderStorm)
            {
                int orbitCount = config != null ? Mathf.Max(1, config.thunderStormOrbitCount) : 3;
                int launchedCount = Mathf.Max(1, count - orbitCount);
                for (int i = 0; i < launchedCount; i++) SpawnProjectile(displayType, stats, DirectionForProjectile(displayType), i == 0);
                DestroyThunderStormOrbits();
                thunderStormOrbits.Clear();
                for (int i = 0; i < orbitCount; i++) SpawnOrbitProjectile(displayType, stats, i, orbitCount);
                yield break;
            }

            if (displayType == WeaponType.Excalibur)
            {
                SpawnProjectile(displayType, stats, DirectionForProjectile(displayType), true);
                yield break;
            }

            float interval = displayType == WeaponType.MachineGun && config != null
                ? Mathf.Max(0.05f, config.machineGunShotIntervalSeconds)
                : 0.5f;
            for (int i = 0; i < count; i++)
            {
                SpawnProjectile(displayType, stats, DirectionForProjectile(displayType), true);
                if (i < count - 1) yield return new WaitForSeconds(interval);
            }

        }

        void DestroyThunderStormOrbits()
        {
            for (int i = thunderStormOrbits.Count - 1; i >= 0; i--)
            {
                if (thunderStormOrbits[i] != null) Destroy(thunderStormOrbits[i].gameObject);
            }
        }

        void SpawnOrbitProjectile(WeaponType type, WeaponStatBlock stats, int index, int count)
        {
            GameObject prefab = PrefabFor(type);
            if (prefab == null) return;
            var instance = Instantiate(prefab, transform.position, Quaternion.identity);
            instance.name = WeaponCatalog.DisplayName(type) + " Orbit " + (index + 1);
            var projectile = instance.GetComponent<AdvancedWeaponProjectile>();
            if (projectile == null) return;
            projectile.Configure(type, Vector2.right, stats, config);
            projectile.ConfigureOrbit(player.transform, index, count, Mathf.Max(0.05f, stats.range));
            thunderStormOrbits.Add(projectile);
        }

        void SpawnProjectile(WeaponType type, WeaponStatBlock stats, Vector2 direction, bool playSfx)
        {
            GameObject prefab = PrefabFor(type);
            if (prefab == null) return;
            if (playSfx)
            {
                if (type == WeaponType.BoomerangSword) AudioManager.PlaySfx(SfxTrack.BoomerangSwordThrow);
                else if (type == WeaponType.AuraSword || type == WeaponType.Excalibur) AudioManager.PlaySfx(SfxTrack.AuraSwordCast);
                else if (type == WeaponType.Gun || type == WeaponType.MachineGun) AudioManager.PlaySfx(SfxTrack.GunShot);
                else if (type == WeaponType.ThunderBall || type == WeaponType.ThunderStorm) AudioManager.PlaySfx(SfxTrack.ThunderBallCast);
            }

            var instance = Instantiate(prefab, transform.position, Quaternion.identity);
            instance.name = WeaponCatalog.DisplayName(type) + " Projectile";
            var projectile = instance.GetComponent<AdvancedWeaponProjectile>();
            if (projectile == null) return;
            projectile.Configure(type, direction, stats, config);
        }

        GameObject PrefabFor(WeaponType type)
        {
            switch (type)
            {
                case WeaponType.BoomerangSword: return boomerangPrefab;
                case WeaponType.Banana: return bananaPrefab;
                case WeaponType.AuraSword: return auraSlashPrefab;
                case WeaponType.Excalibur: return excaliburSlashPrefab != null ? excaliburSlashPrefab : auraSlashPrefab;
                case WeaponType.Gun: return bulletPrefab;
                case WeaponType.MachineGun: return machineGunBulletPrefab != null ? machineGunBulletPrefab : bulletPrefab;
                case WeaponType.ThunderBall: return thunderBallPrefab;
                case WeaponType.ThunderStorm: return thunderStormOrbPrefab != null ? thunderStormOrbPrefab : thunderBallPrefab;
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
