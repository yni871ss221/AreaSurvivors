using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class AdvancedWeaponArea : MonoBehaviour
    {
        static readonly Color HealPopupColor = new Color(0.35f, 1f, 0.34f, 1f);

        [SerializeField] Transform visualScaleRoot;
        [SerializeField] GameObject healPopupPrefab;
        [SerializeField] Vector3 healPopupOffset = new Vector3(0f, 0.58f, 0f);
        readonly Dictionary<Health, float> hitTimers = new Dictionary<Health, float>();
        Transform followTarget;
        float radius;
        float verticalRadiusMultiplier = 1f;
        int damage;
        float damageInterval;
        float expireAt;
        float slowAmount;
        SfxTrack tickSfx;
        float sfxInterval;
        float nextSfxAt;
        PaperMeshVisual visual;
        bool paintsTerritory;
        WeaponType sourceWeaponType = WeaponType.Flag;
        int allyHealAmount;
        float nextHealAt;
        float activateAt;
        bool activated;
        bool configured;

        public void Configure(
            Transform target,
            Vector3 position,
            float areaRadius,
            int attackPower,
            float intervalSeconds,
            float durationSeconds,
            float slow,
            SfxTrack sfx,
            float repeatSfxSeconds,
            float visualAlpha = 0.45f,
            float areaVerticalScale = 1f,
            bool paintTerritory = false,
            WeaponType weaponType = WeaponType.Flag,
            int healAmount = 0,
            float activationDelaySeconds = 0f)
        {
            if (visual == null) visual = GetComponentInChildren<PaperMeshVisual>();
            followTarget = target;
            transform.position = followTarget != null ? followTarget.position : position;
            radius = Mathf.Max(0.05f, areaRadius);
            bool usesSpriteShape = visual != null && visual.UsesEllipseShape;
            float spriteShapeAspectY = usesSpriteShape
                ? Mathf.Max(0.05f, visual.EllipseShapeAspectY)
                : 1f;
            verticalRadiusMultiplier = Mathf.Max(0.05f, areaVerticalScale);
            if (paintTerritory)
            {
                var grid = FindObjectOfType<TileGrid>();
                if (grid != null)
                {
                    Vector2 cellSize = grid.WorldCellSize();
                    verticalRadiusMultiplier = Mathf.Max(0.05f, cellSize.y / Mathf.Max(0.01f, cellSize.x));
                }
            }
            damage = Mathf.Max(0, attackPower);
            damageInterval = Mathf.Max(0.05f, intervalSeconds);
            expireAt = durationSeconds > 0f ? Time.time + durationSeconds : float.PositiveInfinity;
            slowAmount = Mathf.Clamp01(slow);
            tickSfx = sfx;
            sfxInterval = Mathf.Max(0f, repeatSfxSeconds);
            paintsTerritory = paintTerritory;
            sourceWeaponType = weaponType;
            activateAt = Time.time + Mathf.Max(0f, activationDelaySeconds);
            activated = activationDelaySeconds <= 0f;
            int previousHealAmount = allyHealAmount;
            allyHealAmount = Mathf.Max(0, healAmount);
            if (!configured || (previousHealAmount <= 0 && allyHealAmount > 0)) nextHealAt = Time.time;
            configured = true;
            var arrowRainVisual = GetComponentInChildren<ArrowRainAreaVisual>();
            bool usesAreaMeshAspect = arrowRainVisual != null && paintTerritory;
            Vector3 visualScale = usesAreaMeshAspect
                ? Vector3.one * radius
                : usesSpriteShape
                ? new Vector3(radius, radius * verticalRadiusMultiplier / spriteShapeAspectY, radius)
                : new Vector3(radius, radius * verticalRadiusMultiplier, radius);
            (visualScaleRoot != null ? visualScaleRoot : transform).localScale = visualScale;
            if (arrowRainVisual != null)
            {
                if (usesAreaMeshAspect) arrowRainVisual.SetAreaShape(verticalRadiusMultiplier);
                arrowRainVisual.SetAreaAlpha(visualAlpha);
            }
            else ApplyVisualAlpha(visualAlpha);
            if (activated) PaintTerritoryIfNeeded();
        }

        void ApplyVisualAlpha(float alpha)
        {
            if (visual == null) visual = GetComponentInChildren<PaperMeshVisual>();
            if (visual == null) return;
            var color = visual.color;
            color.a = Mathf.Clamp01(alpha);
            visual.color = color;
        }

        void Update()
        {
            if (Time.time >= expireAt)
            {
                Destroy(gameObject);
                return;
            }

            if (followTarget != null) transform.position = followTarget.position;
            if (!activated)
            {
                if (Time.time < activateAt) return;
                activated = true;
                PaintTerritoryIfNeeded();
            }
            if (sfxInterval > 0f && Time.time >= nextSfxAt)
            {
                AudioManager.PlaySfx(tickSfx);
                nextSfxAt = Time.time + sfxInterval;
            }

            DamageEnemiesInRadius();
            HealAlliesInRadius();
        }

        void HealAlliesInRadius()
        {
            if (allyHealAmount <= 0 || Time.time < nextHealAt) return;
            nextHealAt = Time.time + damageInterval;
            var manager = GameManager.Instance;
            var playerHealth = manager != null && manager.Player != null ? manager.Player.GetComponent<Health>() : null;
            HealIfInside(playerHealth);
            var towerHealth = manager != null && manager.Tower != null ? manager.Tower.GetComponent<Health>() : null;
            HealIfInside(towerHealth);
            var buildings = FindObjectsOfType<BuildingPersistentState>();
            for (int i = 0; i < buildings.Length; i++)
            {
                if (buildings[i] == null) continue;
                var health = buildings[i].GetComponent<Health>();
                if (health == towerHealth) continue;
                HealIfInside(health);
            }
        }

        void HealIfInside(Health health)
        {
            if (health == null || health.IsDead || !ContainsPoint(health.transform.position)) return;
            int healed = health.Heal(allyHealAmount);
            if (healed <= 0) return;
            DamagePopup.Show(
                healPopupPrefab,
                health.transform.position + healPopupOffset,
                healed,
                HealPopupColor);
        }

        void DamageEnemiesInRadius()
        {
            float searchRadius = Mathf.Max(radius, radius * verticalRadiusMultiplier);
            var colliders = Physics2D.OverlapCircleAll(transform.position, searchRadius);
            for (int i = 0; i < colliders.Length; i++)
            {
                var enemy = colliders[i] != null ? colliders[i].GetComponentInParent<EnemyController>() : null;
                if (enemy == null) continue;
                if (!ContainsPoint(colliders[i].ClosestPoint(transform.position))) continue;
                if (slowAmount > 0f) EnemySlowEffect.Apply(enemy.gameObject, slowAmount, 0.25f);
                var health = enemy.GetComponent<Health>();
                if (health == null || health.IsDead) continue;
                if (!CanHit(health)) continue;
                if (damage <= 0) continue;
                int creditedDamage = health.DamageAmount(damage);
                health.Damage(damage, enemy.transform.position);
                GameManager.Instance?.RegisterWeaponDamage(sourceWeaponType, creditedDamage);
            }
        }

        void PaintTerritoryIfNeeded()
        {
            if (!paintsTerritory) return;
            var grid = FindObjectOfType<TileGrid>();
            if (grid == null) return;
            Vector2 cellSize = grid.WorldCellSize();
            float radiusX = radius / Mathf.Max(0.01f, cellSize.x);
            float radiusY = radius * verticalRadiusMultiplier / Mathf.Max(0.01f, cellSize.y);
            grid.PaintEllipseOverlappingCells(transform.position, TileOwner.Player, radiusX, radiusY);
        }

        bool ContainsPoint(Vector2 point)
        {
            float radiusX = Mathf.Max(0.05f, radius);
            float radiusY = Mathf.Max(0.05f, radius * verticalRadiusMultiplier);
            Vector2 local = point - (Vector2)transform.position;
            float normalized = (local.x * local.x) / (radiusX * radiusX) + (local.y * local.y) / (radiusY * radiusY);
            return normalized <= 1f;
        }

        bool CanHit(Health health)
        {
            if (health == null) return false;
            if (hitTimers.TryGetValue(health, out var next) && Time.time < next) return false;
            hitTimers[health] = Time.time + damageInterval;
            return true;
        }
    }
}
