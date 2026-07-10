using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class EnemyOutlinePrefabSetup
    {
        const string EnemyPrefabPath = "Assets/AreaSurvivors/Prefabs/Characters/Enemy.prefab";
        const string VisualName = "Paper Visual";
        const string OutlineChildName = "Runtime Outline";
        const float EnemyOutlineThickness = 0.018f;
        static readonly Color EnemySilhouetteColor = new Color(1f, 0.52f, 0.28f, 0.56f);

        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, EnemyPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("Enemy prefab outline and silhouette setup was applied.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Area Survivors/Visuals/Validate Enemy Outline Prefab Setup")]
        public static void Validate()
        {
            var root = PrefabUtility.LoadPrefabContents(EnemyPrefabPath);
            try
            {
                bool valid = ValidateRoot(root);
                if (valid) Debug.Log("Enemy prefab outline and silhouette setup validation passed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void Configure(GameObject root)
        {
            var visual = FindChild(root.transform, VisualName);
            if (visual == null)
            {
                Debug.LogError("Paper Visual was not found in Enemy.prefab.");
                return;
            }

            var outline = visual.GetComponent<RuntimeSpriteOutline>();
            if (outline == null) outline = visual.gameObject.AddComponent<RuntimeSpriteOutline>();
            outline.outlineColor = Color.black;
            outline.thickness = EnemyOutlineThickness;
            outline.compensateTransformScale = true;
            outline.requireExistingOutlineObject = true;
            outline.blink = false;
            outline.blinkSpeed = 5f;

            var outlineChild = FindChild(visual, OutlineChildName);
            if (outlineChild == null)
            {
                var child = new GameObject(OutlineChildName);
                child.transform.SetParent(visual, false);
                outlineChild = child.transform;
            }

            if (outlineChild.GetComponent<MeshFilter>() == null) outlineChild.gameObject.AddComponent<MeshFilter>();
            var renderer = outlineChild.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = outlineChild.gameObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var reveal = root.GetComponent<CharacterOcclusionReveal>();
            if (reveal == null) reveal = root.AddComponent<CharacterOcclusionReveal>();
            reveal.silhouetteColor = EnemySilhouetteColor;
            reveal.outlineColor = Color.white;
            reveal.checkInterval = 0.08f;
        }

        static bool ValidateRoot(GameObject root)
        {
            var visual = FindChild(root.transform, VisualName);
            var outline = visual != null ? visual.GetComponent<RuntimeSpriteOutline>() : null;
            var outlineChild = visual != null ? FindChild(visual, OutlineChildName) : null;
            var reveal = root.GetComponent<CharacterOcclusionReveal>();
            bool valid = visual != null &&
                outline != null &&
                outline.compensateTransformScale &&
                outline.requireExistingOutlineObject &&
                Mathf.Approximately(outline.thickness, EnemyOutlineThickness) &&
                outlineChild != null &&
                outlineChild.GetComponent<MeshFilter>() != null &&
                outlineChild.GetComponent<MeshRenderer>() != null &&
                reveal != null;
            if (!valid) Debug.LogError("Enemy prefab outline and silhouette setup is incomplete.");
            return valid;
        }

        static Transform FindChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName) return child;
            }

            return null;
        }
    }
}
