using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class LobbyTestButtonSceneBinder
    {
        const string LobbyScenePath = "Assets/AreaSurvivors/Scenes/03_Lobby.unity";

        [MenuItem("AreaSurvivors/Lobby/Ensure Stage Test Buttons")]
        public static void EnsureStageTestButtons()
        {
            var previousActiveScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(LobbyScenePath);
            bool openedAdditively = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Additive);
                openedAdditively = true;
            }

            EditorSceneManager.SetActiveScene(scene);
            var stage2Button = FindInScene(scene, "Start Stage 2 Test Button");
            if (stage2Button == null)
            {
                Debug.LogWarning("Start Stage 2 Test Button was not found in 03_Lobby.");
                RestorePreviousScene(previousActiveScene, scene, openedAdditively);
                return;
            }

            EnsureButton(scene, stage2Button, "Start Stage 3 Test Button", "ステージ3テスト", new Vector2(-509f, -105f));
            EnsureButton(scene, stage2Button, "Start Stage 4 Test Button", "ステージ4テスト", new Vector2(-509f, -165f));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RestorePreviousScene(previousActiveScene, scene, openedAdditively);
            Debug.Log("Stage test buttons ensured in 03_Lobby.");
        }

        static void EnsureButton(Scene scene, GameObject sourceButton, string objectName, string labelText, Vector2 anchoredPosition)
        {
            var target = FindInScene(scene, objectName);
            if (target == null)
            {
                target = Object.Instantiate(sourceButton, sourceButton.transform.parent);
                target.name = objectName;
            }

            var rect = target.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = new Vector2(180f, 52f);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
            }

            var label = FindChild(target.transform, "Label")?.GetComponent<Text>();
            if (label != null) label.text = labelText;

            var button = target.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = true;
            }
        }

        static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindChild(root.transform, name);
                if (found != null) return found.gameObject;
            }

            return null;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindChild(root.GetChild(i), name);
                if (found != null) return found;
            }

            return null;
        }

        static void RestorePreviousScene(Scene previousActiveScene, Scene editedScene, bool openedAdditively)
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                EditorSceneManager.SetActiveScene(previousActiveScene);
            }

            if (openedAdditively)
            {
                EditorSceneManager.CloseScene(editedScene, true);
            }
        }
    }
}
