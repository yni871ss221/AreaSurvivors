using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class GameTestLauncherScrollMigration
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/08_GameTestLauncher.unity";
        const string CompletionMarkerRelativePath = "Library/AreaSafeUnity/game-test-launcher-editor-expanded-scroll-v2.ok";
        const float BottomPadding = 32f;

        [MenuItem("Area Survivors/Migrations/Apply Game Test Launcher Scroll Layout")]
        public static void Apply()
        {
            string markerPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CompletionMarkerRelativePath);
            if (File.Exists(markerPath))
            {
                if (!GameTestLauncherScrollValidator.Validate(false))
                {
                    throw new InvalidOperationException("Game Test Launcher scroll migration was already applied, but validation now fails. Existing Scene layout was not overwritten.");
                }
                Debug.Log("Game Test Launcher scroll migration: already applied and still valid.");
                return;
            }

            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty)
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                throw new InvalidOperationException("08_GameTestLauncher.unity has unsaved changes. Save or discard them before running this one-time migration.");
            }

            try
            {
                var buttons = FindWeaponButtons(scene);
                var scrollRoots = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                    .Where(rect => rect.name == "Tool Scroll View")
                    .ToArray();
                if (scrollRoots.Length != 1)
                {
                    throw new InvalidOperationException("08_GameTestLauncher.unity must contain exactly one Tool Scroll View. found=" + scrollRoots.Length);
                }
                var scrollRoot = scrollRoots[0];
                var viewport = scrollRoot.childCount == 1 ? scrollRoot.GetChild(0) as RectTransform : null;
                var content = viewport != null && viewport.childCount == 1 ? viewport.GetChild(0) as RectTransform : null;
                if (viewport == null || viewport.name != "Viewport" || content == null || content.name != "Content")
                {
                    throw new InvalidOperationException("Tool Scroll View must directly contain Viewport/Content as its single outer scroll hierarchy.");
                }
                if (viewport.GetComponent<Mask>() == null && viewport.GetComponent<RectMask2D>() == null)
                {
                    throw new InvalidOperationException("Tool Scroll View outer Viewport requires a Mask or RectMask2D.");
                }
                if (buttons.Any(button => !button.transform.IsChildOf(content)))
                {
                    throw new InvalidOperationException("Every Scene-authored weapon test button must be a descendant of the outer Tool Scroll View Content.");
                }
                var outerViewport = scrollRoot.parent as RectTransform;
                if (outerViewport == null)
                {
                    throw new InvalidOperationException("Tool Scroll View requires a Scene-authored parent viewport.");
                }

                float requiredHeight = CalculateRequiredHeight(content, BottomPadding);
                var size = content.sizeDelta;
                size.y = Mathf.Max(size.y, Mathf.Ceil(requiredHeight));
                content.sizeDelta = size;

                float currentTop = scrollRoot.anchoredPosition.y + scrollRoot.rect.height * (1f - scrollRoot.pivot.y);
                var scrollRootSize = scrollRoot.sizeDelta;
                scrollRootSize.y = Mathf.Max(scrollRootSize.y, size.y);
                scrollRoot.sizeDelta = scrollRootSize;
                var scrollRootPosition = scrollRoot.anchoredPosition;
                scrollRootPosition.y = currentTop - scrollRootSize.y * (1f - scrollRoot.pivot.y);
                scrollRoot.anchoredPosition = scrollRootPosition;

                var legacyControllers = scrollRoot.GetComponentsInChildren<OptionsPanelScrollController>(true);
                foreach (var legacyController in legacyControllers)
                {
                    UnityEngine.Object.DestroyImmediate(legacyController);
                }

                var legacyScrollRect = scrollRoot.GetComponent<ScrollRect>();
                if (legacyScrollRect != null) UnityEngine.Object.DestroyImmediate(legacyScrollRect);

                var controller = scrollRoot.GetComponent<OptionsPanelScrollController>();
                if (controller == null) controller = scrollRoot.gameObject.AddComponent<OptionsPanelScrollController>();
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("content").objectReferenceValue = scrollRoot;
                serialized.FindProperty("viewport").objectReferenceValue = outerViewport;
                serialized.FindProperty("scrollSensitivity").floatValue = 64f;
                serialized.FindProperty("dragSensitivity").floatValue = 1f;
                serialized.FindProperty("bottomPadding").floatValue = BottomPadding;
                serialized.FindProperty("resetOnEnable").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(content);
                EditorUtility.SetDirty(scrollRoot);
                EditorUtility.SetDirty(controller);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    throw new InvalidOperationException("Failed to save 08_GameTestLauncher.unity.");
                }
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }

            if (!GameTestLauncherScrollValidator.Validate(false))
            {
                throw new InvalidOperationException("Game Test Launcher scroll migration completed, but post-validation failed.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Game Test Launcher scroll migration: completed.");
        }

        static Button[] FindWeaponButtons(Scene scene)
        {
            var allButtons = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .ToArray();
            var result = WeaponCatalog.TestableWeapons
                .Select(type => allButtons.SingleOrDefault(button => button.name == GameTestLaunchScreen.WeaponTestButtonName(type)))
                .ToArray();
            if (result.Any(button => button == null))
            {
                var missing = WeaponCatalog.TestableWeapons
                    .Where((type, index) => result[index] == null)
                    .Select(type => type.ToString());
                throw new InvalidOperationException("Scene-authored weapon test buttons are missing: " + string.Join(", ", missing));
            }
            return result;
        }

        internal static float CalculateRequiredHeight(RectTransform content, float bottomPadding)
        {
            float deepestBottom = 0f;
            for (int i = 0; i < content.childCount; i++)
            {
                if (!(content.GetChild(i) is RectTransform child)) continue;
                float bottom = child.anchoredPosition.y - child.rect.height * child.pivot.y;
                deepestBottom = Mathf.Min(deepestBottom, bottom);
            }
            return Mathf.Max(0f, -deepestBottom + Mathf.Max(0f, bottomPadding));
        }
    }
}
