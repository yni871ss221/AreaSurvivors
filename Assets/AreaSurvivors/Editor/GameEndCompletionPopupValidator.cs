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
    public static class GameEndCompletionPopupValidator
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/06_GameEnd.unity";

        static string SuccessMarkerPath => Path.Combine(
            Path.GetDirectoryName(Application.dataPath),
            "Library",
            "AreaSafeUnity",
            "game-end-completion-popup-validator.success");

        [MenuItem("Area Survivors/Validate/Game End Completion Popup")]
        public static void ValidateMenu()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
            if (!Validate(true)) throw new InvalidOperationException("Game End completion popup validation failed.");
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
                var screens = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameOverScreen>(true))
                    .ToArray();
                if (screens.Length != 1)
                {
                    Debug.LogError("Game End completion popup validator: expected exactly one GameOverScreen. found=" + screens.Length + ".");
                    return false;
                }

                var screen = screens[0];
                var message = screen.completionMessageText;
                if (message == null || message.name != "Completion Message Text")
                {
                    Debug.LogError("Game End completion popup validator: completionMessageText reference is missing or invalid.");
                    errors++;
                }
                else
                {
                    if (message.gameObject.activeSelf)
                    {
                        Debug.LogError("Game End completion popup validator: Completion Message Text must be inactive in the saved Scene.");
                        errors++;
                    }
                    if (screen.missionCompleteText == null || message.transform.parent != screen.missionCompleteText.transform.parent)
                    {
                        Debug.LogError("Game End completion popup validator: title and completion message must share the popup content parent.");
                        errors++;
                    }
                    else if (Mathf.Abs(screen.missionCompleteText.rectTransform.anchoredPosition.y - 40f) > 0.5f)
                    {
                        Debug.LogError("Game End completion popup validator: completion title Scene-authored vertical position is invalid.");
                        errors++;
                    }
                    if (message.fontSize != 20 || message.horizontalOverflow != HorizontalWrapMode.Wrap || message.verticalOverflow != VerticalWrapMode.Overflow)
                    {
                        Debug.LogError("Game End completion popup validator: completion message typography does not match the two-line specification.");
                        errors++;
                    }

                    var rect = message.rectTransform;
                    if (rect == null || Mathf.Abs(rect.anchoredPosition.y + 26f) > 0.5f || Mathf.Abs(rect.sizeDelta.x - 580f) > 0.5f || Mathf.Abs(rect.sizeDelta.y - 48f) > 0.5f)
                    {
                        Debug.LogError("Game End completion popup validator: completion message Scene-authored RectTransform is invalid.");
                        errors++;
                    }
                }

                if (errors == 0 && logSuccess)
                {
                    Debug.Log("Game End completion popup validator: passed.");
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
