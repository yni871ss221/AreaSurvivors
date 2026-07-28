using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class FireExplosionRangeVisualMigration
    {
        public const string ExplosionPrefabPath = "Assets/AreaSurvivors/Prefabs/Effects/ProjectileExplosionHitbox.prefab";
        public const string FireballPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/Fireball.prefab";
        public const string FireMissilePrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/FireMissile.prefab";
        public const string WatchTowerPrefabPath = "Assets/AreaSurvivors/Prefabs/Buildings/WatchTower.prefab";
        public const string FillObjectName = "Fire Explosion Range Fill";
        public const string OutlineObjectName = "Fire Explosion Range Outline";
        public const string SuccessMarkerPath = "Library/AreaSafeUnity/fire-explosion-range-visual-migration.success";
        public const float VisualLifetimeSeconds = 0.26f;
        public const float OutlineWidth = 0.035f;

        public static readonly Color FillColor = new Color(1f, 0.18f, 0.14f, 0.22f);
        public static readonly Color OutlineColor = new Color(1f, 0.30f, 0.24f, 0.68f);

        [MenuItem("Area Survivors/Migrations/Apply Fire Explosion Range Visual")]
        public static void ApplyMenu()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);

            Apply();
            int errors = FireExplosionRangeVisualValidator.ValidateAssets();
            if (errors != 0)
            {
                throw new InvalidOperationException(
                    "Fire explosion range visual migration validation failed. errors=" + errors);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(SuccessMarkerPath));
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log("Fire explosion range visual migration: passed.");
        }

        static void Apply()
        {
            var watchTowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WatchTowerPrefabPath);
            var watchTower = watchTowerPrefab != null ? watchTowerPrefab.GetComponent<WatchTower>() : null;
            var sourceFill = watchTower != null ? watchTower.rangeFillRenderer : null;
            if (sourceFill == null || sourceFill.sprite == null)
            {
                throw new InvalidOperationException(
                    "WatchTower range fill is missing and cannot provide the shared ellipse sprite.");
            }

            var sourceSerialized = new SerializedObject(sourceFill);
            var shapeProperty = sourceSerialized.FindProperty("shapeSpriteOverride");
            var cropProperty = sourceSerialized.FindProperty("ellipseTextureCrop");
            var segmentsProperty = sourceSerialized.FindProperty("ellipseSegments");
            var shapeSprite = shapeProperty != null ? shapeProperty.objectReferenceValue as Sprite : null;
            if (shapeSprite == null)
            {
                throw new InvalidOperationException("WatchTower range fill has no ellipse shape sprite.");
            }

            float textureCrop = cropProperty != null ? cropProperty.floatValue : 0f;
            int ellipseSegments = segmentsProperty != null ? segmentsProperty.intValue : 64;
            ApplyExplosionPrefab(sourceFill.sprite, shapeSprite, textureCrop, ellipseSegments);
            ApplyProjectileFlag(FireballPrefabPath);
            ApplyProjectileFlag(FireMissilePrefabPath);

            AssetDatabase.SaveAssets();
            NormalizePrefabYaml(ExplosionPrefabPath);
            ValidateNoTrailingWhitespace(ExplosionPrefabPath);
            AssetDatabase.ImportAsset(ExplosionPrefabPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(FireballPrefabPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(FireMissilePrefabPath, ImportAssetOptions.ForceUpdate);
        }

        static void ApplyExplosionPrefab(
            Sprite fillSprite,
            Sprite shapeSprite,
            float textureCrop,
            int ellipseSegments)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(ExplosionPrefabPath);
                if (root == null)
                {
                    throw new InvalidOperationException(
                        "Explosion hitbox prefab could not be loaded: " + ExplosionPrefabPath);
                }

                var explosion = root.GetComponent<ProjectileExplosionHitbox>();
                if (explosion == null)
                {
                    throw new InvalidOperationException(
                        "Explosion hitbox prefab is missing ProjectileExplosionHitbox.");
                }

                var fillTransform = FindOrCreateChild(root.transform, FillObjectName);
                var fill = fillTransform.GetComponent<PaperMeshVisual>();
                if (fill == null) fill = fillTransform.gameObject.AddComponent<PaperMeshVisual>();
                RemoveBillboard(fillTransform.gameObject);
                fill.Configure(fillSprite, FillColor, WeaponSortingOrders.AreaEffect);
                fill.ConfigureEllipseShape(shapeSprite, textureCrop, ellipseSegments);
                fill.useTexture = false;
                fill.visible = false;

                var outlineTransform = FindOrCreateChild(root.transform, OutlineObjectName);
                var outline = outlineTransform.GetComponent<EllipseOutlineMeshVisual>();
                if (outline == null)
                {
                    outline = outlineTransform.gameObject.AddComponent<EllipseOutlineMeshVisual>();
                }
                RemoveBillboard(outlineTransform.gameObject);
                ConfigureOutline(outline);

                explosion.InitializeRangeVisual(fill, outline, VisualLifetimeSeconds);
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, ExplosionPrefabPath);
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "Explosion range visual prefab save returned null: " + ExplosionPrefabPath);
                }
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ConfigureOutline(EllipseOutlineMeshVisual outline)
        {
            var serialized = new SerializedObject(outline);
            serialized.FindProperty("segments").intValue = 96;
            serialized.FindProperty("outlineWidth").floatValue = OutlineWidth;
            serialized.FindProperty("color").colorValue = OutlineColor;
            serialized.FindProperty("sortingOrder").intValue = WeaponSortingOrders.AreaEffect + 1;
            serialized.FindProperty("visible").boolValue = false;
            serialized.FindProperty("radius").vector2Value = Vector2.one;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            outline.Configure(Vector2.one, false);
        }

        static void ApplyProjectileFlag(string prefabPath)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    throw new InvalidOperationException("Projectile prefab could not be loaded: " + prefabPath);
                }

                var projectile = root.GetComponent<Projectile>();
                if (projectile == null)
                {
                    throw new InvalidOperationException(
                        "Projectile prefab is missing Projectile: " + prefabPath);
                }

                projectile.showExplosionRangeVisual = true;
                EditorUtility.SetDirty(projectile);
                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                if (savedPrefab == null)
                {
                    throw new InvalidOperationException(
                        "Projectile prefab save returned null: " + prefabPath);
                }
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform FindOrCreateChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var childObject = new GameObject(name);
                child = childObject.transform;
                child.SetParent(parent, false);
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            child.gameObject.SetActive(true);
            return child;
        }

        static void RemoveBillboard(GameObject target)
        {
            var billboard = target.GetComponent<PaperBillboard>();
            if (billboard != null) UnityEngine.Object.DestroyImmediate(billboard);
        }

        static void NormalizePrefabYaml(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string[] lines = File.ReadAllLines(fullPath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd();
            File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
        }

        static void ValidateNoTrailingWhitespace(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
            string[] lines = File.ReadAllLines(fullPath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length != lines[i].TrimEnd().Length)
                {
                    throw new InvalidOperationException(
                        "Fire explosion range prefab still contains trailing whitespace at line " +
                        (i + 1) + ": " + assetPath);
                }
            }
        }
    }
}
