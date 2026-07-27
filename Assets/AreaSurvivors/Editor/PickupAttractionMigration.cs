using System;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class PickupAttractionMigration
    {
        const string ExperienceOrbPrefabPath =
            "Assets/AreaSurvivors/Prefabs/Pickups/ExperienceOrb.prefab";

        [MenuItem("Area Survivors/Migrate/Player-Owned Pickup Attraction")]
        public static void MigrateFromMenu()
        {
            var root = PrefabUtility.LoadPrefabContents(ExperienceOrbPrefabPath);
            int removedColliders = 0;
            try
            {
                if (root.GetComponent<ExperienceOrb>() == null)
                {
                    throw new InvalidOperationException(
                        "ExperienceOrb prefab is missing ExperienceOrb.");
                }

                var colliders = root.GetComponentsInChildren<Collider2D>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(colliders[i], true);
                    removedColliders++;
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    ExperienceOrbPrefabPath);
                if (saved == null)
                {
                    throw new InvalidOperationException(
                        "Failed to save collider-free ExperienceOrb prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Player-owned pickup attraction migration completed. " +
                $"Removed XP orb colliders: {removedColliders}.");
            StageTransitionEnemyDefeatValidator.ValidateFromMenu();
        }
    }
}
