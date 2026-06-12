using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.Editor
{
    public static class LobbySceneBuilder
    {
        const string LobbyScenePath = "Assets/AreaSurvivors/Scenes/03_Lobby.unity";
        const string AutoRunMarkerPath = "Assets/AreaSurvivors/Editor/LobbySceneBuilder.autorun";

        [MenuItem("AreaSurvivors/Lobby/Rebuild Lobby Scene UI")]
        public static void RebuildLobbyScene()
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
            RebuildInLoadedScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                EditorSceneManager.SetActiveScene(previousActiveScene);
            }

            if (openedAdditively)
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log("Lobby scene UI was rebuilt in 03_Lobby.");
        }

        [InitializeOnLoadMethod]
        static void AutoRunWhenRequested()
        {
            if (!File.Exists(AutoRunMarkerPath)) return;
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(AutoRunMarkerPath)) return;
                File.Delete(AutoRunMarkerPath);
                var meta = AutoRunMarkerPath + ".meta";
                if (File.Exists(meta)) File.Delete(meta);
                RebuildLobbyScene();
                AssetDatabase.Refresh();
            };
        }

        static void RebuildInLoadedScene(Scene scene)
        {
            DestroyRootIfExists(scene, "Lobby UI");
            DestroyRootIfExists(scene, "Main Camera");
            DestroyRootIfExists(scene, "EventSystem");

            EnsureController(scene);
            LobbyUiFactory.Create();
        }

        static void EnsureController(Scene scene)
        {
            var controller = FindRoot(scene, "03_Lobby Controller");
            if (controller == null)
            {
                controller = new GameObject("03_Lobby Controller");
                SceneManager.MoveGameObjectToScene(controller, scene);
            }

            if (controller.GetComponent<LobbyScreen>() == null) controller.AddComponent<LobbyScreen>();
        }

        static void DestroyRootIfExists(Scene scene, string name)
        {
            var target = FindRoot(scene, name);
            if (target != null) Object.DestroyImmediate(target);
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
            }

            return null;
        }
    }
}
