using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class MissingScriptCleaner
    {
        const string OneShotMarkerPath = "Temp/AreaSurvivorsCleanMissingScripts.once";

        [InitializeOnLoadMethod]
        static void RunOneShotCleanupIfRequested()
        {
            if (!System.IO.File.Exists(OneShotMarkerPath)) return;
            System.IO.File.Delete(OneShotMarkerPath);
            EditorApplication.delayCall += CleanOpenScenes;
        }

        [MenuItem("AreaSurvivors/Tools/Clean Missing Scripts In Open Scenes")]
        public static void CleanOpenScenes()
        {
            var removed = 0;
            for (var sceneIndex = 0; sceneIndex < UnityEngine.SceneManagement.SceneManager.sceneCount; sceneIndex++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded) continue;

                foreach (var root in scene.GetRootGameObjects())
                    removed += CleanRecursive(root);

                if (removed > 0)
                    EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log($"AreaSurvivors missing script cleanup finished. removed={removed}");
        }

        [MenuItem("AreaSurvivors/Tools/Clean Missing Scripts In Lobby Scene")]
        public static void CleanLobbyScene()
        {
            const string lobbyScenePath = "Assets/AreaSurvivors/Scenes/03_Lobby.unity";

            var scene = EditorSceneManager.OpenScene(lobbyScenePath);
            var removed = 0;
            foreach (var root in scene.GetRootGameObjects())
                removed += CleanRecursive(root);

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"AreaSurvivors lobby missing script cleanup finished. removed={removed}");
        }

        static int CleanRecursive(GameObject gameObject)
        {
            var removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
            foreach (Transform child in gameObject.transform)
                removed += CleanRecursive(child.gameObject);
            return removed;
        }
    }
}
