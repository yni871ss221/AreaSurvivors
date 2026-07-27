using System;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class EnemyCollisionLayerMigration
    {
        public const string EnemyPrefabPath =
            "Assets/AreaSurvivors/Prefabs/Characters/Enemy.prefab";
        const string TagManagerPath = "ProjectSettings/TagManager.asset";
        const int FirstUserLayer = 8;
        const int LastLayer = 31;

        [MenuItem("Area Survivors/Migrate/Enemy Collision Layer")]
        public static void MigrateFromMenu()
        {
            int enemyLayer = EnsureEnemyLayer();
            if (Physics2D.GetIgnoreLayerCollision(enemyLayer, enemyLayer))
            {
                throw new InvalidOperationException(
                    "Enemy-to-Enemy collision must remain enabled.");
            }

            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                if (root.GetComponent<EnemyController>() == null)
                {
                    throw new InvalidOperationException(
                        "Enemy prefab is missing EnemyController.");
                }

                var transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i].gameObject.layer = enemyLayer;
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "Failed to save Enemy prefab with the Enemy layer.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Enemy collision layer migration completed. " +
                $"Layer={enemyLayer}, Enemy-to-Enemy collision enabled.");
            CombatPerformanceProbeValidator.Validate();
        }

        static int EnsureEnemyLayer()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (assets == null || assets.Length == 0)
            {
                throw new InvalidOperationException(
                    "Unable to load ProjectSettings/TagManager.asset.");
            }

            var tagManager = new SerializedObject(assets[0]);
            var layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray || layers.arraySize <= LastLayer)
            {
                throw new InvalidOperationException(
                    "TagManager layers property is unavailable or incomplete.");
            }

            int emptyLayer = -1;
            for (int i = FirstUserLayer; i <= LastLayer; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == EnemyController.EnemyLayerName) return i;
                if (emptyLayer < 0 && string.IsNullOrEmpty(layer.stringValue))
                {
                    emptyLayer = i;
                }
            }

            if (emptyLayer < 0)
            {
                throw new InvalidOperationException(
                    "No free user layer is available for Enemy.");
            }

            layers.GetArrayElementAtIndex(emptyLayer).stringValue =
                EnemyController.EnemyLayerName;
            if (!tagManager.ApplyModifiedProperties())
            {
                throw new InvalidOperationException(
                    "Failed to write the Enemy layer to TagManager.");
            }
            EditorUtility.SetDirty(tagManager.targetObject);
            AssetDatabase.SaveAssets();
            return emptyLayer;
        }
    }
}
