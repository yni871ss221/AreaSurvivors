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
    public static class GameEndCompletionPopupSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/06_GameEnd.unity";
        const string SuccessMarkerRelativePath = "Library/AreaSafeUnity/game-end-completion-popup-setup.success";

        [MenuItem("Area Survivors/Migrations/Add Game End Completion Message")]
        public static void Apply()
        {
            string markerPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), SuccessMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty)
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                throw new InvalidOperationException("06_GameEnd.unity has unsaved changes. Existing Scene layout was not modified.");
            }

            try
            {
                var screens = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GameOverScreen>(true))
                    .ToArray();
                if (screens.Length != 1) throw new InvalidOperationException("Game End completion popup setup requires exactly one GameOverScreen.");

                var screen = screens[0];
                bool changed = false;
                if (screen.completionMessageText == null)
                {
                    if (screen.stageUnlockMessageText == null || screen.missionCompleteText == null)
                    {
                        throw new InvalidOperationException("Game End completion popup setup requires existing stage unlock and mission title Text references.");
                    }

                    var clone = UnityEngine.Object.Instantiate(
                        screen.stageUnlockMessageText.gameObject,
                        screen.missionCompleteText.transform.parent,
                        false);
                    clone.name = "Completion Message Text";
                    clone.SetActive(false);

                    var text = clone.GetComponent<Text>();
                    text.text = "すべてのボスを討伐しました！\n難易度5でのクリアを目指しましょう";
                    text.fontSize = 20;
                    text.fontStyle = FontStyle.Normal;
                    text.resizeTextForBestFit = false;
                    text.horizontalOverflow = HorizontalWrapMode.Wrap;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    text.lineSpacing = 1f;

                    var rect = text.rectTransform;
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(0f, -26f);
                    rect.sizeDelta = new Vector2(580f, 48f);
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;

                    var serialized = new SerializedObject(screen);
                    serialized.FindProperty("completionMessageText").objectReferenceValue = text;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    {
                        throw new InvalidOperationException("Failed to save 06_GameEnd.unity after adding the completion message.");
                    }
                }
                if (!GameEndCompletionPopupValidator.Validate(false))
                {
                    throw new InvalidOperationException("Game End completion popup setup completed but validation failed.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
                Debug.Log("Game End completion popup setup: added and validated the Scene-authored two-line message.");
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
