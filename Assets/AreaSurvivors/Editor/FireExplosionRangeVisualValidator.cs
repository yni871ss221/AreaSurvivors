using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class FireExplosionRangeVisualValidator
    {
        const string MarkerRelativePath = "Library/AreaSafeUnity/fire-explosion-range-visual-validator.ok";
        const float Epsilon = 0.0001f;

        static readonly string[] RangeEnabledProjectilePaths =
        {
            FireExplosionRangeVisualMigration.FireballPrefabPath,
            FireExplosionRangeVisualMigration.FireMissilePrefabPath
        };

        static readonly string[] RangeDisabledProjectilePaths =
        {
            "Assets/AreaSurvivors/Prefabs/Weapons/Arrow.prefab",
            "Assets/AreaSurvivors/Prefabs/Weapons/BallistaArrow.prefab",
            "Assets/AreaSurvivors/Prefabs/Weapons/GoldenArrow.prefab",
            "Assets/AreaSurvivors/Prefabs/Weapons/PlayerArrow.prefab",
            "Assets/AreaSurvivors/Prefabs/Weapons/TowerCannonball.prefab"
        };

        [MenuItem("Area Survivors/Validate/Fire Explosion Range Visual")]
        public static void ValidateMenu()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string markerPath = Path.Combine(projectRoot, MarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            int errors = ValidateAssets();
            if (errors != 0)
            {
                throw new InvalidOperationException(
                    "Fire explosion range visual validation failed. errors=" + errors);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log(
                "Fire explosion range visual validator: passed. Fireball and FireMissile use the shared light-red ellipse at the exact explosion radius.");
        }

        public static int ValidateAssets()
        {
            int errors = 0;
            ValidateExplosionPrefab(ref errors);
            ValidateProjectileFlags(RangeEnabledProjectilePaths, true, ref errors);
            ValidateProjectileFlags(RangeDisabledProjectilePaths, false, ref errors);
            return errors;
        }

        static void ValidateExplosionPrefab(ref int errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FireExplosionRangeVisualMigration.ExplosionPrefabPath);
            var explosion = prefab != null ? prefab.GetComponent<ProjectileExplosionHitbox>() : null;
            if (explosion == null)
            {
                Error("ProjectileExplosionHitbox prefab/component is missing.", ref errors);
                return;
            }

            var explosionSerialized = new SerializedObject(explosion);
            var fill = explosionSerialized.FindProperty("rangeFillRenderer").objectReferenceValue as PaperMeshVisual;
            var outline = explosionSerialized.FindProperty("rangeOutlineRenderer").objectReferenceValue as EllipseOutlineMeshVisual;
            float lifetime = explosionSerialized.FindProperty("rangeVisualLifetime").floatValue;

            var sourceTowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                FireExplosionRangeVisualMigration.WatchTowerPrefabPath);
            var sourceTower = sourceTowerPrefab != null ? sourceTowerPrefab.GetComponent<WatchTower>() : null;
            var sourceFill = sourceTower != null ? sourceTower.rangeFillRenderer : null;

            if (fill == null ||
                fill.name != FireExplosionRangeVisualMigration.FillObjectName ||
                fill.transform.parent != prefab.transform ||
                !Approximately(fill.transform.localPosition, Vector3.zero) ||
                !HasZeroXYRotation(fill.transform) ||
                fill.GetComponent<PaperBillboard>() != null ||
                !fill.UsesEllipseShape ||
                fill.useTexture ||
                fill.visible ||
                fill.order != WeaponSortingOrders.AreaEffect ||
                !Approximately(fill.color, FireExplosionRangeVisualMigration.FillColor))
            {
                Error("Explosion range fill must be the hidden, non-billboard light-red ellipse child.", ref errors);
            }

            if (outline == null ||
                outline.name != FireExplosionRangeVisualMigration.OutlineObjectName ||
                outline.transform.parent != prefab.transform ||
                !Approximately(outline.transform.localPosition, Vector3.zero) ||
                !HasZeroXYRotation(outline.transform) ||
                outline.GetComponent<PaperBillboard>() != null ||
                outline.order != WeaponSortingOrders.AreaEffect + 1)
            {
                Error("Explosion range outline must be the hidden, non-billboard ellipse child.", ref errors);
            }
            else
            {
                var outlineSerialized = new SerializedObject(outline);
                if (outlineSerialized.FindProperty("visible").boolValue ||
                    Mathf.Abs(
                        outlineSerialized.FindProperty("outlineWidth").floatValue -
                        FireExplosionRangeVisualMigration.OutlineWidth) > Epsilon ||
                    !Approximately(
                        outlineSerialized.FindProperty("color").colorValue,
                        FireExplosionRangeVisualMigration.OutlineColor))
                {
                    Error("Explosion range outline style does not match the configured light-red style.", ref errors);
                }
            }

            if (sourceFill == null || fill == null || fill.sprite != sourceFill.sprite ||
                ShapeSprite(fill) == null || ShapeSprite(fill) != ShapeSprite(sourceFill))
            {
                Error("Explosion range fill must reuse the WatchTower ellipse sprite and shape sprite.", ref errors);
            }

            if (Mathf.Abs(lifetime - FireExplosionRangeVisualMigration.VisualLifetimeSeconds) > Epsilon)
            {
                Error("Explosion range visual lifetime must match the impact animation duration.", ref errors);
            }

            if (prefab.GetComponentsInChildren<PaperMeshVisual>(true).Length != 1 ||
                prefab.GetComponentsInChildren<EllipseOutlineMeshVisual>(true).Length != 1)
            {
                Error("Explosion hitbox prefab must contain exactly one fill and one outline range visual.", ref errors);
            }
        }

        static void ValidateProjectileFlags(string[] prefabPaths, bool expected, ref int errors)
        {
            foreach (string prefabPath in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var projectile = prefab != null ? prefab.GetComponent<Projectile>() : null;
                if (projectile == null || projectile.showExplosionRangeVisual != expected)
                {
                    Error(
                        "Projectile explosion range flag mismatch. expected=" + expected +
                        " path=" + prefabPath,
                        ref errors);
                }
            }
        }

        static Sprite ShapeSprite(PaperMeshVisual visual)
        {
            if (visual == null) return null;
            return new SerializedObject(visual)
                .FindProperty("shapeSpriteOverride")
                .objectReferenceValue as Sprite;
        }

        static bool HasZeroXYRotation(Transform target)
        {
            return Mathf.Abs(Mathf.DeltaAngle(target.localEulerAngles.x, 0f)) <= Epsilon &&
                Mathf.Abs(Mathf.DeltaAngle(target.localEulerAngles.y, 0f)) <= Epsilon;
        }

        static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= Epsilon * Epsilon;
        }

        static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) <= Epsilon &&
                Mathf.Abs(a.g - b.g) <= Epsilon &&
                Mathf.Abs(a.b - b.b) <= Epsilon &&
                Mathf.Abs(a.a - b.a) <= Epsilon;
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError(message);
        }
    }
}
