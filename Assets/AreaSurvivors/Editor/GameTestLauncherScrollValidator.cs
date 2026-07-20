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
    public static class GameTestLauncherScrollValidator
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/08_GameTestLauncher.unity";
        const float BottomPadding = 32f;

        static string SuccessMarkerPath => Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            "Library",
            "AreaSafeUnity",
            "game-test-launcher-scroll-validator.success");

        [MenuItem("Area Survivors/Validate/Game Test Launcher Scroll Layout")]
        public static void ValidateMenu()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
            if (!Validate(true)) throw new InvalidOperationException("Game Test Launcher scroll layout validation failed.");
            Directory.CreateDirectory(Path.GetDirectoryName(SuccessMarkerPath));
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("O"));
        }

        public static bool Validate(bool logSuccess)
        {
            int errors = 0;
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var allButtons = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                    .ToArray();
                var weaponButtons = WeaponCatalog.TestableWeapons
                    .Select(type => allButtons.SingleOrDefault(button => button.name == GameTestLaunchScreen.WeaponTestButtonName(type)))
                    .ToArray();
                if (weaponButtons.Any(button => button == null))
                {
                    Debug.LogError("Game Test Launcher scroll validator: one or more Scene-authored weapon buttons are missing.");
                    return false;
                }
                if (weaponButtons.Any(button => !button.gameObject.activeSelf))
                {
                    Debug.LogError("Game Test Launcher scroll validator: one or more Scene-authored weapon buttons are inactive.");
                    errors++;
                }

                for (int i = 0; i < weaponButtons.Length; i++)
                {
                    var left = weaponButtons[i].transform as RectTransform;
                    for (int j = i + 1; j < weaponButtons.Length; j++)
                    {
                        var right = weaponButtons[j].transform as RectTransform;
                        if (left.parent != right.parent) continue;
                        if (Mathf.Abs(left.anchoredPosition.y - right.anchoredPosition.y) > 0.5f) continue;

                        Debug.LogError("Game Test Launcher scroll validator: weapon button rows overlap. left="
                            + weaponButtons[i].name + ", right=" + weaponButtons[j].name
                            + ", anchoredY=" + left.anchoredPosition.y.ToString("0.##") + ".");
                        errors++;
                    }
                }

                var scrollRoots = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                    .Where(rect => rect.name == "Tool Scroll View")
                    .ToArray();
                var scrollRoot = scrollRoots.Length == 1 ? scrollRoots[0] : null;
                var viewport = scrollRoot != null && scrollRoot.childCount == 1 ? scrollRoot.GetChild(0) as RectTransform : null;
                var content = viewport != null && viewport.childCount == 1 ? viewport.GetChild(0) as RectTransform : null;
                var outerViewport = scrollRoot != null ? scrollRoot.parent as RectTransform : null;
                if (scrollRoots.Length != 1)
                {
                    Debug.LogError("Game Test Launcher scroll validator: exactly one Tool Scroll View is required. found=" + scrollRoots.Length);
                    errors++;
                }
                if (viewport == null || viewport.name != "Viewport" || content == null || content.name != "Content")
                {
                    Debug.LogError("Game Test Launcher scroll validator: Tool Scroll View does not directly contain its outer Viewport/Content hierarchy.");
                    errors++;
                }
                if (viewport == null || viewport.GetComponent<Mask>() == null && viewport.GetComponent<RectMask2D>() == null)
                {
                    Debug.LogError("Game Test Launcher scroll validator: outer Viewport or mask is missing.");
                    errors++;
                }
                if (content == null || weaponButtons.Any(button => !button.transform.IsChildOf(content)))
                {
                    Debug.LogError("Game Test Launcher scroll validator: one or more weapon buttons are outside the outer Tool Scroll View Content.");
                    errors++;
                }
                if (outerViewport == null)
                {
                    Debug.LogError("Game Test Launcher scroll validator: Tool Scroll View outer viewport is missing.");
                    errors++;
                }

                var controller = scrollRoot != null ? scrollRoot.GetComponent<OptionsPanelScrollController>() : null;
                if (controller == null)
                {
                    Debug.LogError("Game Test Launcher scroll validator: Tool Scroll View has no OptionsPanelScrollController.");
                    errors++;
                }
                else
                {
                    var serialized = new SerializedObject(controller);
                    if (serialized.FindProperty("content").objectReferenceValue != scrollRoot || serialized.FindProperty("viewport").objectReferenceValue != outerViewport)
                    {
                        Debug.LogError("Game Test Launcher scroll validator: controller references are invalid.");
                        errors++;
                    }
                }
                if (scrollRoot != null && scrollRoot.GetComponentsInChildren<OptionsPanelScrollController>(true).Any(candidate => candidate != controller))
                {
                    Debug.LogError("Game Test Launcher scroll validator: a nested Content still owns the legacy scroll controller.");
                    errors++;
                }
                if (scrollRoot != null && scrollRoot.GetComponent<ScrollRect>() != null)
                {
                    Debug.LogError("Game Test Launcher scroll validator: the legacy ScrollRect still clips the Editor-authored full layout.");
                    errors++;
                }

                float requiredHeight = content != null ? GameTestLauncherScrollMigration.CalculateRequiredHeight(content, BottomPadding) : 0f;
                if (content == null || content.rect.height + 0.5f < requiredHeight)
                {
                    Debug.LogError("Game Test Launcher scroll validator: Content is shorter than its Scene-authored buttons.");
                    errors++;
                }
                if (content == null || viewport == null || scrollRoot == null ||
                    scrollRoot.rect.height + 0.5f < content.rect.height ||
                    viewport.rect.height + 0.5f < content.rect.height)
                {
                    Debug.LogError("Game Test Launcher scroll validator: Tool Scroll View does not expose the full Content height in the Editor.");
                    errors++;
                }

                if (errors == 0 && logSuccess)
                {
                    int evolutionCount = WeaponCatalog.TestableWeapons.Count(WeaponCatalog.IsEvolution);
                    Debug.Log("Game Test Launcher scroll validator: passed. weaponButtons=" + weaponButtons.Length
                        + ", evolutionButtons=" + evolutionCount + ", contentHeight=" + content.rect.height.ToString("0.#")
                        + ", editorRootHeight=" + scrollRoot.rect.height.ToString("0.#") + ".");
                }
                return errors == 0;
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
