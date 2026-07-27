using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AreaSurvivors.Editor
{
    public static class CombatFeedbackPerformanceMigration
    {
        public const string EnemyPrefabPath = "Assets/AreaSurvivors/Prefabs/Characters/Enemy.prefab";
        public const string DamagePopupPrefabPath = "Assets/AreaSurvivors/Prefabs/UI/DamagePopup.prefab";
        public const string EnemyHitFlashMaterialPath = "Assets/AreaSurvivors/Materials/EnemyHitFlash.mat";
        public const string DamagePopupOutlineMaterialPath = "Assets/AreaSurvivors/Materials/DamagePopupOutline.mat";
        public const string SuccessMarkerPath = "Library/AreaSafeUnity/combat-feedback-performance-migration.success";
        const string EnemyHitFlashChildName = "Enemy Hit Flash";

        [MenuItem("Area Survivors/Migrate/Combat Feedback Performance")]
        public static void Migrate()
        {
            DeleteSuccessMarker();
            var hitFlashMaterial = EnsureEnemyHitFlashMaterial();
            var damagePopupMaterial = EnsureDamagePopupOutlineMaterial();
            MigrateEnemyPrefab(hitFlashMaterial);
            MigrateDamagePopupPrefab(damagePopupMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMigratedAssets();
            WriteSuccessMarker();
            Debug.Log("Combat feedback performance migration completed.");
        }

        public static Material EnsureEnemyHitFlashMaterial()
        {
            return EnsureMaterial(
                EnemyHitFlashMaterialPath,
                "AreaSurvivors/SpriteAlphaOutline");
        }

        public static Material EnsureDamagePopupOutlineMaterial()
        {
            return EnsureMaterial(
                DamagePopupOutlineMaterialPath,
                "AreaSurvivors/TextMeshAlphaOutline");
        }

        static Material EnsureMaterial(string assetPath, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Shader not found: {shaderName}");

            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(assetPath)
                };
                AssetDatabase.CreateAsset(material, assetPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        static void MigrateEnemyPrefab(Material hitFlashMaterial)
        {
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                var visual = root.GetComponentInChildren<PaperMeshVisual>(true);
                if (visual == null)
                    throw new InvalidOperationException("Enemy prefab is missing PaperMeshVisual.");

                var flash = root.GetComponent<EnemyHitFlash>();
                if (flash == null) flash = root.AddComponent<EnemyHitFlash>();

                var flashTransform = visual.transform.Find(EnemyHitFlashChildName);
                if (flashTransform == null)
                {
                    var flashObject = new GameObject(EnemyHitFlashChildName);
                    flashTransform = flashObject.transform;
                    flashTransform.SetParent(visual.transform, false);
                }
                flashTransform.localPosition = Vector3.zero;
                flashTransform.localRotation = Quaternion.identity;
                flashTransform.localScale = Vector3.one;

                var filter = flashTransform.GetComponent<MeshFilter>();
                if (filter == null) filter = flashTransform.gameObject.AddComponent<MeshFilter>();
                var renderer = flashTransform.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = flashTransform.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = hitFlashMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                flash.ConfigurePrefabReferences(visual, filter, renderer, hitFlashMaterial);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
                if (saved == null) throw new InvalidOperationException("Failed to save Enemy prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void MigrateDamagePopupPrefab(Material outlineMaterial)
        {
            var root = PrefabUtility.LoadPrefabContents(DamagePopupPrefabPath);
            try
            {
                var popup = root.GetComponent<DamagePopup>();
                if (popup == null)
                    throw new InvalidOperationException("DamagePopup prefab is missing DamagePopup.");

                var text = popup.text != null ? popup.text : root.GetComponentInChildren<TextMesh>(true);
                if (text == null)
                    throw new InvalidOperationException("DamagePopup prefab is missing TextMesh.");

                var outline = text.GetComponent<RuntimeTextMeshOutline>();
                if (outline == null) outline = text.gameObject.AddComponent<RuntimeTextMeshOutline>();
                outline.sharedOutlineMaterial = outlineMaterial;
                popup.text = text;
                popup.textOutline = outline;

                var renderer = text.GetComponent<MeshRenderer>();
                if (renderer == null)
                    throw new InvalidOperationException("DamagePopup TextMesh is missing MeshRenderer.");
                renderer.sharedMaterial = outlineMaterial;
                if (text.GetComponent<PreserveSortingOrder>() == null)
                    text.gameObject.AddComponent<PreserveSortingOrder>();

                var saved = PrefabUtility.SaveAsPrefabAsset(root, DamagePopupPrefabPath);
                if (saved == null) throw new InvalidOperationException("Failed to save DamagePopup prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ValidateMigratedAssets()
        {
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            var flash = enemy != null ? enemy.GetComponent<EnemyHitFlash>() : null;
            if (flash == null || !flash.HasPrefabReferences || flash.SharedOverlayMaterial == null)
                throw new InvalidOperationException("Enemy hit flash prefab references were not saved.");

            var popupObject = AssetDatabase.LoadAssetAtPath<GameObject>(DamagePopupPrefabPath);
            var popup = popupObject != null ? popupObject.GetComponent<DamagePopup>() : null;
            if (popup == null ||
                popup.text == null ||
                popup.textOutline == null ||
                popup.textOutline.SharedOutlineMaterial == null ||
                popup.text.GetComponent<PreserveSortingOrder>() == null)
            {
                throw new InvalidOperationException("Damage popup pooled visual references were not saved.");
            }
        }

        static void DeleteSuccessMarker()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
        }

        static void WriteSuccessMarker()
        {
            var directory = Path.GetDirectoryName(SuccessMarkerPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("O"));
        }
    }
}
