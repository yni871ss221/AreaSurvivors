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
    public static class GameEndTestButtonsValidator
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/08_GameTestLauncher.unity";

        static readonly string[] ButtonNames =
        {
            GameTestLaunchScreen.GameEndDefeatTestButtonName,
            GameTestLaunchScreen.GameEndStageClearTestButtonName,
            GameTestLaunchScreen.GameEndStageFourClearTestButtonName,
            GameTestLaunchScreen.GameEndAllDifficultyFiveClearTestButtonName
        };

        static string SuccessMarkerPath => Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            "Library",
            "AreaSafeUnity",
            "game-end-test-buttons-validator.success");

        [MenuItem("Area Survivors/Validate/Game End Test Buttons")]
        public static void ValidateMenu()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
            if (!Validate(true)) throw new InvalidOperationException("Game End test buttons validation failed.");
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
                var roots = scene.GetRootGameObjects();
                var allButtons = roots.SelectMany(root => root.GetComponentsInChildren<Button>(true)).ToArray();
                var contents = roots
                    .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                    .Where(rect => rect.name == "Content" && rect.parent != null && rect.parent.name == "Viewport" && rect.parent.parent != null && rect.parent.parent.name == "Tool Scroll View")
                    .ToArray();
                var content = contents.Length == 1 ? contents[0] : null;

                if (content == null)
                {
                    Debug.LogError("Game End test buttons validator: Tool Scroll View/Viewport/Content was not found exactly once.");
                    errors++;
                }

                var rects = new RectTransform[ButtonNames.Length];
                for (int i = 0; i < ButtonNames.Length; i++)
                {
                    var matches = allButtons.Where(button => button.name == ButtonNames[i]).ToArray();
                    if (matches.Length != 1)
                    {
                        Debug.LogError("Game End test buttons validator: expected exactly one button named " + ButtonNames[i] + ", found=" + matches.Length + ".");
                        errors++;
                        continue;
                    }

                    var button = matches[0];
                    rects[i] = button.transform as RectTransform;
                    if (!button.gameObject.activeSelf || content == null || button.transform.parent != content)
                    {
                        Debug.LogError("Game End test buttons validator: button is inactive or outside the Scene-authored Content. name=" + ButtonNames[i] + ".");
                        errors++;
                    }
                    if (button.GetComponentInChildren<Text>(true) == null)
                    {
                        Debug.LogError("Game End test buttons validator: label is missing. name=" + ButtonNames[i] + ".");
                        errors++;
                    }
                }

                for (int i = 0; i < rects.Length; i++)
                {
                    if (rects[i] == null) continue;
                    if (content != null && rects[i].anchoredPosition.y - rects[i].rect.height * rects[i].pivot.y < -content.rect.height)
                    {
                        Debug.LogError("Game End test buttons validator: button extends below Content. name=" + ButtonNames[i] + ".");
                        errors++;
                    }
                    for (int j = i + 1; j < rects.Length; j++)
                    {
                        if (rects[j] == null) continue;
                        if (RectOverlaps(rects[i], rects[j]))
                        {
                            Debug.LogError("Game End test buttons validator: buttons overlap. left=" + ButtonNames[i] + ", right=" + ButtonNames[j] + ".");
                            errors++;
                        }
                    }
                }

                if (errors == 0 && logSuccess)
                {
                    Debug.Log("Game End test buttons validator: passed. buttons=" + ButtonNames.Length + ".");
                }
                return errors == 0;
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static bool RectOverlaps(RectTransform left, RectTransform right)
        {
            Rect leftRect = new Rect(
                left.anchoredPosition.x - left.rect.width * left.pivot.x,
                left.anchoredPosition.y - left.rect.height * left.pivot.y,
                left.rect.width,
                left.rect.height);
            Rect rightRect = new Rect(
                right.anchoredPosition.x - right.rect.width * right.pivot.x,
                right.anchoredPosition.y - right.rect.height * right.pivot.y,
                right.rect.width,
                right.rect.height);
            return leftRect.Overlaps(rightRect);
        }
    }
}
