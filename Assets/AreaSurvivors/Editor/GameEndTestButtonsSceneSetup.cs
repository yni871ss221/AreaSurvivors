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
    public static class GameEndTestButtonsSceneSetup
    {
        const string ScenePath = "Assets/AreaSurvivors/Scenes/08_GameTestLauncher.unity";
        const string SuccessMarkerRelativePath = "Library/AreaSafeUnity/game-end-test-buttons-setup.success";

        static readonly ButtonSpec[] Specs =
        {
            new ButtonSpec(GameTestLaunchScreen.GameEndDefeatTestButtonName, "終了演出：敗北", new Vector2(-235f, -2085f)),
            new ButtonSpec(GameTestLaunchScreen.GameEndStageClearTestButtonName, "終了演出：通常クリア", new Vector2(235f, -2085f)),
            new ButtonSpec(GameTestLaunchScreen.GameEndStageFourClearTestButtonName, "終了演出：ステージ4クリア", new Vector2(-235f, -2150f)),
            new ButtonSpec(GameTestLaunchScreen.GameEndAllDifficultyFiveClearTestButtonName, "終了演出：全ステージ難易度5クリア", new Vector2(235f, -2150f))
        };

        [MenuItem("Area Survivors/Migrations/Add Game End Test Buttons")]
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
                throw new InvalidOperationException("08_GameTestLauncher.unity has unsaved changes. Existing Scene layout was not modified.");
            }

            try
            {
                var roots = scene.GetRootGameObjects();
                var allButtons = roots.SelectMany(root => root.GetComponentsInChildren<Button>(true)).ToArray();
                var template = allButtons.SingleOrDefault(button => button.name == "Opening Story Test Button");
                var contents = roots
                    .SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                    .Where(rect => rect.name == "Content" && rect.parent != null && rect.parent.name == "Viewport" && rect.parent.parent != null && rect.parent.parent.name == "Tool Scroll View")
                    .ToArray();
                if (template == null || contents.Length != 1)
                {
                    throw new InvalidOperationException("Game End test button setup requires one Opening Story Test Button and one Tool Scroll View/Viewport/Content.");
                }

                var content = contents[0];
                bool changed = false;
                foreach (var spec in Specs)
                {
                    var existing = allButtons.Where(button => button.name == spec.Name).ToArray();
                    if (existing.Length > 1)
                    {
                        throw new InvalidOperationException("Duplicate Game End test buttons found before setup. name=" + spec.Name + ".");
                    }

                    if (existing.Length == 1)
                    {
                        if (existing[0].transform.parent != content)
                        {
                            throw new InvalidOperationException("Existing Game End test button is outside the expected Content. name=" + spec.Name + ".");
                        }
                        continue;
                    }

                    var clone = UnityEngine.Object.Instantiate(template.gameObject, content, false);
                    clone.name = spec.Name;
                    clone.SetActive(true);
                    var button = clone.GetComponent<Button>();

                    button.onClick = new Button.ButtonClickedEvent();
                    var rect = button.transform as RectTransform;
                    rect.anchorMin = new Vector2(0.5f, 1f);
                    rect.anchorMax = new Vector2(0.5f, 1f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = spec.Position;
                    rect.sizeDelta = new Vector2(450f, 46f);
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;

                    var label = button.GetComponentInChildren<Text>(true);
                    if (label == null) throw new InvalidOperationException("Cloned Game End test button has no Text label. name=" + spec.Name + ".");
                    label.text = spec.Label;
                    changed = true;
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    {
                        throw new InvalidOperationException("Failed to save 08_GameTestLauncher.unity after adding Game End test buttons.");
                    }
                }
                if (!GameEndTestButtonsValidator.Validate(false))
                {
                    throw new InvalidOperationException("Game End test buttons were saved but validation failed.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
                Debug.Log("Game End test button setup: added and validated four Scene-authored buttons without changing existing RectTransforms.");
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        readonly struct ButtonSpec
        {
            public readonly string Name;
            public readonly string Label;
            public readonly Vector2 Position;

            public ButtonSpec(string name, string label, Vector2 position)
            {
                Name = name;
                Label = label;
                Position = position;
            }
        }
    }
}
