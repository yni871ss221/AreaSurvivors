using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class AdvancedWeaponArea : MonoBehaviour
    {
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
            bool paintTerritory = false)
        {
            followTarget = target;
            transform.position = followTarget != null ? followTarget.position : position;
            radius = Mathf.Max(0.05f, areaRadius);
            verticalRadiusMultiplier = Mathf.Max(0.05f, areaVerticalScale);
            damage = Mathf.Max(0, attackPower);
            damageInterval = Mathf.Max(0.05f, intervalSeconds);
            expireAt = durationSeconds > 0f ? Time.time + durationSeconds : float.PositiveInfinity;
            slowAmount = Mathf.Clamp01(slow);
            tickSfx = sfx;
            sfxInterval = Mathf.Max(0f, repeatSfxSeconds);
            paintsTerritory = paintTerritory;
            transform.localScale = new Vector3(radius, radius * verticalRadiusMultiplier, radius);
            var arrowRainVisual = GetComponentInChildren<ArrowRainAreaVisual>();
            if (arrowRainVisual != null) arrowRainVisual.SetAreaAlpha(visualAlpha);
            else
            {
                var frostVisual = GetComponentInChildren<FrostAreaVisual>();
                if (frostVisual != null) frostVisual.SetAreaAlpha(visualAlpha);
                else ApplyVisualAlpha(visualAlpha);
            }
            PaintTerritoryIfNeeded();
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
            if (sfxInterval > 0f && Time.time >= nextSfxAt)
            {
                AudioManager.PlaySfx(tickSfx);
                nextSfxAt = Time.time + sfxInterval;
            }

            DamageEnemiesInRadius();
        }

        void DamageEnemiesInRadius()
        {
            var colliders = Physics2D.OverlapCircleAll(transform.position, radius);
            for (int i = 0; i < colliders.Length; i++)
            {
                var enemy = colliders[i] != null ? colliders[i].GetComponentInParent<EnemyController>() : null;
                if (enemy == null) continue;
                if (!ContainsPoint(colliders[i].ClosestPoint(transform.position))) continue;
                if (slowAmount > 0f) EnemySlowEffect.Apply(enemy.gameObject, slowAmount, 0.25f);
                var health = enemy.GetComponent<Health>();
                if (health == null || health.IsDead) continue;
                if (!CanHit(health)) continue;
                if (damage > 0) health.Damage(damage, enemy.transform.position);
            }
        }

        void PaintTerritoryIfNeeded()
        {
            if (!paintsTerritory) return;
            var grid = FindObjectOfType<TileGrid>();
            if (grid == null) return;
            float cellSize = Mathf.Max(0.01f, grid.cellSize);
            int radiusX = Mathf.Max(0, Mathf.CeilToInt(radius / cellSize));
            int radiusY = Mathf.Max(0, Mathf.CeilToInt(radius * verticalRadiusMultiplier / cellSize));
            grid.PaintEllipse(transform.position, TileOwner.Player, radiusX, radiusY);
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
