using AreaSurvivors;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class BuildingHealthBarPrefabSetup
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";
        const float BuildingHealthBarWidth = 0.7f;
        const float BuildingHealthBarPitch = -35f;

        static readonly BuildingHealthBarSpec[] Specs =
        {
            new BuildingHealthBarSpec("Assets/AreaSurvivors/Prefabs/Buildings/WoodenWall.prefab", typeof(WoodenBarrier), new Vector3(0f, 0.72f, 0f)),
            new BuildingHealthBarSpec("Assets/AreaSurvivors/Prefabs/Buildings/BallistaTower.prefab", typeof(BallistaTower), new Vector3(0f, 1.02f, 0f)),
            new BuildingHealthBarSpec("Assets/AreaSurvivors/Prefabs/Buildings/WatchTower.prefab", typeof(WatchTower), new Vector3(0f, 1.55f, 0f)),
            new BuildingHealthBarSpec("Assets/AreaSurvivors/Prefabs/Buildings/CenterTower.prefab", typeof(TowerController), new Vector3(0f, 1.72f, 0f))
        };

        public static void Apply()
        {
            foreach (var spec in Specs)
            {
                ConfigurePrefab(spec);
            }

            ConfigureGameScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Building health bars were applied.");
        }

        [MenuItem("Area Survivors/UI/Validate Building Health Bars")]
        public static void Validate()
        {
            bool valid = true;
            foreach (var spec in Specs)
            {
                valid &= ValidatePrefab(spec);
            }

            if (valid) Debug.Log("Building health bar validation passed.");
        }

        static void ConfigurePrefab(BuildingHealthBarSpec spec)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefabPath);
            if (prefab == null) return;

            var root = PrefabUtility.LoadPrefabContents(spec.prefabPath);
            try
            {
                ConfigureRoot(root, spec);
                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, spec.prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool ValidatePrefab(BuildingHealthBarSpec spec)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefabPath);
            if (prefab == null) return true;

            var root = PrefabUtility.LoadPrefabContents(spec.prefabPath);
            try
            {
                bool valid = HasValidHealthBar(root, spec);
                if (!valid) Debug.LogError($"{spec.prefabPath} is missing a configured BuildingHealthBar.");
                return valid;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ConfigureGameScene()
        {
            var activePath = SceneManager.GetActiveScene().path;
            var scene = activePath == GameScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            bool changed = false;
            var objects = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
            {
                objects.Add(root);
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child != null) objects.Add(child.gameObject);
                }
            }

            foreach (var obj in objects)
            {
                if (obj == null) continue;
                changed |= ConfigureSceneObject(obj);
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        static bool ConfigureSceneObject(GameObject root)
        {
            foreach (var spec in Specs)
            {
                if (root.GetComponent(spec.buildingType) == null) continue;
                ConfigureRoot(root, spec);
                EditorUtility.SetDirty(root);
                return true;
            }

            return false;
        }

        static void ConfigureRoot(GameObject root, BuildingHealthBarSpec spec)
        {
            var health = root.GetComponent<Health>();
            if (health == null) health = root.AddComponent<Health>();

            var slider = EnsureWorldSlider(root.transform, spec.localPosition);
            slider.gameObject.SetActive(false);

            var healthBar = root.GetComponent<BuildingHealthBar>();
            if (healthBar == null) healthBar = root.AddComponent<BuildingHealthBar>();
            healthBar.hpBar = slider;
            healthBar.fullHideThreshold = 0.999f;

            var tower = root.GetComponent<TowerController>();
            if (tower != null) tower.hpBar = slider;

            EditorUtility.SetDirty(health);
            EditorUtility.SetDirty(healthBar);
        }

        static bool HasValidHealthBar(GameObject root, BuildingHealthBarSpec spec)
        {
            var healthBar = root.GetComponent<BuildingHealthBar>();
            if (healthBar == null || healthBar.hpBar == null) return false;
            var anchor = healthBar.hpBar.transform.parent;
            if (anchor == null || anchor.name != "Building HP Bar") return false;

            var canvas = healthBar.hpBar.GetComponent<Canvas>();
            if (canvas == null || !canvas.overrideSorting || canvas.sortingOrder < 32000) return false;
            if (healthBar.hpBar.GetComponent<PaperBillboard>() != null) return false;
            if (!Approximately(healthBar.hpBar.transform.localEulerAngles, new Vector3(BuildingHealthBarPitch, 0f, 0f))) return false;

            var rect = healthBar.hpBar.GetComponent<RectTransform>();
            return rect != null &&
                Approximately(anchor.localPosition, spec.localPosition) &&
                Mathf.Approximately(rect.sizeDelta.x, BuildingHealthBarWidth);
        }

        static Slider EnsureWorldSlider(Transform parent, Vector3 localPosition)
        {
            RemoveExistingHealthBar(parent);

            var anchor = new GameObject("Building HP Bar");
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            anchor.transform.localRotation = Quaternion.identity;
            anchor.transform.localScale = Vector3.one;

            var root = new GameObject("World Canvas");
            root.transform.SetParent(anchor.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.Euler(BuildingHealthBarPitch, 0f, 0f);
            root.transform.localScale = Vector3.one;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;

            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localPosition = Vector3.zero;
            rect.sizeDelta = new Vector2(BuildingHealthBarWidth, 0.07f);

            var slider = root.AddComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.direction = Slider.Direction.LeftToRight;
            slider.wholeNumbers = false;

            var background = EnsureStretchImage(root.transform, "Background", new Color(0.12f, 0.04f, 0.04f, 0.74f));
            var fillArea = EnsureRect(root.transform, "Fill Area");
            Stretch(fillArea);
            var fill = EnsureStretchImage(fillArea, "Fill", new Color(0.25f, 0.88f, 0.35f, 0.9f));
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);

            slider.targetGraphic = background;
            slider.fillRect = fill.rectTransform;
            return slider;
        }

        static void RemoveExistingHealthBar(Transform parent)
        {
            var named = parent.Find("Building HP Bar");
            if (named != null) Object.DestroyImmediate(named.gameObject);

            var legacy = parent.Find("HP Bar");
            if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
        }

        static RectTransform EnsureRect(Transform parent, string name)
        {
            var child = parent.Find(name);
            var rect = child != null ? child.GetComponent<RectTransform>() : null;
            if (rect != null) return rect;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        static Image EnsureStretchImage(Transform parent, string name, Color color)
        {
            var rect = EnsureRect(parent, name);
            var image = rect.GetComponent<Image>();
            if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            Stretch(rect);
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static bool Approximately(Vector3 a, Vector3 b)
        {
            return Mathf.Approximately(a.x, b.x) &&
                Mathf.Approximately(a.y, b.y) &&
                Mathf.Approximately(a.z, b.z);
        }

        readonly struct BuildingHealthBarSpec
        {
            public readonly string prefabPath;
            public readonly System.Type buildingType;
            public readonly Vector3 localPosition;

            public BuildingHealthBarSpec(string prefabPath, System.Type buildingType, Vector3 localPosition)
            {
                this.prefabPath = prefabPath;
                this.buildingType = buildingType;
                this.localPosition = localPosition;
            }
        }
    }
}
