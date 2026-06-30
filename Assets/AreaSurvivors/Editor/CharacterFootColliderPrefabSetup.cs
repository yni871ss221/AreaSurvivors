using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class CharacterFootColliderPrefabSetup
    {
        const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Player.prefab";
        const string EnemyPrefabPath = "Assets/AreaSurvivors/Prefabs/Enemy.prefab";
        const string SlideMaterialPath = "Assets/AreaSurvivors/Physics/CharacterSlide.physicsMaterial2D";
        const string PhysicsFolderPath = "Assets/AreaSurvivors/Physics";

        static readonly Vector2 PlayerFootSize = new Vector2(0.34f, 0.18f);
        static readonly Vector2 PlayerFootOffset = new Vector2(0f, -0.27f);
        static readonly Vector2 EnemyFootSize = new Vector2(0.46f, 0.22f);
        static readonly Vector2 EnemyFootOffset = new Vector2(0f, -0.22f);
        const float BuildingBlockingBottomInset = 0.1f;
        const float BuildingBlockingEdgeRadius = 0.04f;
        static readonly string[] BuildingPrefabPaths =
        {
            "Assets/AreaSurvivors/Prefabs/WoodenWall.prefab",
            "Assets/AreaSurvivors/Prefabs/BallistaTower.prefab",
            "Assets/AreaSurvivors/Prefabs/WatchTower.prefab",
            "Assets/AreaSurvivors/Prefabs/CenterTower.prefab"
        };

        [MenuItem("Area Survivors/Physics/Apply Character Foot Colliders")]
        public static void Apply()
        {
            var slideMaterial = EnsureSlideMaterial();
            ConfigurePrefab(PlayerPrefabPath, PlayerFootSize, PlayerFootOffset, 0.035f, slideMaterial);
            ConfigurePrefab(EnemyPrefabPath, EnemyFootSize, EnemyFootOffset, 0.04f, slideMaterial);
            ConfigureBuildingPrefabs(slideMaterial);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Character foot BoxCollider2D prefab setup was applied.");
        }

        [MenuItem("Area Survivors/Physics/Validate Character Foot Colliders")]
        public static void Validate()
        {
            var slideMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(SlideMaterialPath);
            bool valid = slideMaterial != null &&
                Mathf.Approximately(slideMaterial.friction, 0f) &&
                Mathf.Approximately(slideMaterial.bounciness, 0f);
            if (!valid) Debug.LogError("Character slide PhysicsMaterial2D is missing or misconfigured.");
            valid &= ValidatePrefab(PlayerPrefabPath, PlayerFootSize, PlayerFootOffset, 0.035f, slideMaterial);
            valid &= ValidatePrefab(EnemyPrefabPath, EnemyFootSize, EnemyFootOffset, 0.04f, slideMaterial);
            valid &= ValidateBuildingPrefabs(slideMaterial);
            if (valid) Debug.Log("Character foot BoxCollider2D prefab setup validation passed.");
        }

        static void ConfigurePrefab(string path, Vector2 size, Vector2 offset, float edgeRadius, PhysicsMaterial2D slideMaterial)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RemoveRootCircleColliders(root);
                var box = EnsureSingleRootBoxCollider(root);
                box.isTrigger = false;
                box.size = size;
                box.offset = offset;
                box.edgeRadius = edgeRadius;
                box.sharedMaterial = slideMaterial;
                EnsureFootprint(root, box);
                var gridVisual = root.GetComponent<GridObjectVisual>();
                if (gridVisual != null) gridVisual.ApplyCharacterYSortPivot();
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool ValidatePrefab(string path, Vector2 expectedSize, Vector2 expectedOffset, float expectedEdgeRadius, PhysicsMaterial2D expectedMaterial)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var circles = root.GetComponents<CircleCollider2D>();
                var boxes = root.GetComponents<BoxCollider2D>();
                bool valid = circles.Length == 0 &&
                    boxes.Length == 1 &&
                    !boxes[0].isTrigger &&
                    Approximately(boxes[0].size, expectedSize) &&
                    Approximately(boxes[0].offset, expectedOffset) &&
                    Mathf.Approximately(boxes[0].edgeRadius, expectedEdgeRadius) &&
                    boxes[0].sharedMaterial == expectedMaterial &&
                    HasExpectedFootprint(root, boxes[0]) &&
                    HasExpectedCharacterSortPivot(root, boxes[0]);
                if (!valid) Debug.LogError($"{path} foot collider setup is incomplete.");
                return valid;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static PhysicsMaterial2D EnsureSlideMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(SlideMaterialPath);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(PhysicsFolderPath))
                {
                    AssetDatabase.CreateFolder("Assets/AreaSurvivors", "Physics");
                }

                material = new PhysicsMaterial2D("CharacterSlide")
                {
                    friction = 0f,
                    bounciness = 0f
                };
                AssetDatabase.CreateAsset(material, SlideMaterialPath);
            }

            material.friction = 0f;
            material.bounciness = 0f;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void ConfigureBuildingPrefabs(PhysicsMaterial2D slideMaterial)
        {
            foreach (var path in BuildingPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var gridVisual = root.GetComponent<GridObjectVisual>();
                    if (gridVisual != null)
                    {
                        gridVisual.blockingColliderBottomInset = BuildingBlockingBottomInset;
                        gridVisual.blockingColliderEdgeRadius = BuildingBlockingEdgeRadius;
                        gridVisual.blockingColliderMaterial = slideMaterial;
                    }

                    foreach (var collider in root.GetComponents<BoxCollider2D>())
                    {
                        if (collider == null || collider.isTrigger) continue;
                        if (gridVisual != null)
                        {
                            gridVisual.ConfigureFootprintBox(collider, false);
                        }
                        else
                        {
                            collider.edgeRadius = BuildingBlockingEdgeRadius;
                            collider.sharedMaterial = slideMaterial;
                        }
                    }

                    EditorUtility.SetDirty(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        static bool ValidateBuildingPrefabs(PhysicsMaterial2D expectedMaterial)
        {
            bool valid = true;
            foreach (var path in BuildingPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var gridVisual = root.GetComponent<GridObjectVisual>();
                    if (gridVisual == null ||
                        !Mathf.Approximately(gridVisual.blockingColliderBottomInset, BuildingBlockingBottomInset) ||
                        !Mathf.Approximately(gridVisual.blockingColliderEdgeRadius, BuildingBlockingEdgeRadius) ||
                        gridVisual.blockingColliderMaterial != expectedMaterial)
                    {
                        Debug.LogError($"{path} GridObjectVisual blocking collider settings are incomplete.");
                        valid = false;
                    }

                    foreach (var collider in root.GetComponents<Collider2D>())
                    {
                        if (collider == null || collider.isTrigger) continue;
                        if (collider.sharedMaterial == expectedMaterial) continue;
                        Debug.LogError($"{path} has a blocking collider without the slide material.");
                        valid = false;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return valid;
        }

        static void RemoveRootCircleColliders(GameObject root)
        {
            foreach (var circle in root.GetComponents<CircleCollider2D>())
            {
                Object.DestroyImmediate(circle);
            }
        }

        static BoxCollider2D EnsureSingleRootBoxCollider(GameObject root)
        {
            var boxes = root.GetComponents<BoxCollider2D>();
            BoxCollider2D keep = boxes.Length > 0 ? boxes[0] : root.AddComponent<BoxCollider2D>();
            for (int i = 1; i < boxes.Length; i++)
            {
                Object.DestroyImmediate(boxes[i]);
            }

            return keep;
        }

        static CharacterFootprint EnsureFootprint(GameObject root, BoxCollider2D collider)
        {
            var footprint = root.GetComponent<CharacterFootprint>();
            if (footprint == null) footprint = root.AddComponent<CharacterFootprint>();
            footprint.SetFootCollider(collider);
            EditorUtility.SetDirty(footprint);
            return footprint;
        }

        static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
        }

        static bool HasExpectedCharacterSortPivot(GameObject root, BoxCollider2D footCollider)
        {
            var ySort = root.GetComponent<YSort>();
            if (ySort == null || footCollider == null) return false;
            float expected = (footCollider.offset.y - footCollider.size.y * 0.5f) * Mathf.Max(0.001f, Mathf.Abs(root.transform.lossyScale.y));
            return Mathf.Approximately(ySort.sortPivotOffsetY, expected);
        }

        static bool HasExpectedFootprint(GameObject root, BoxCollider2D footCollider)
        {
            var footprint = root.GetComponent<CharacterFootprint>();
            return footprint != null && footprint.FootCollider == footCollider;
        }
    }
}
