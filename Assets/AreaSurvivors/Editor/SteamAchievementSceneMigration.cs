using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.Editor
{
    public static class SteamAchievementSceneMigration
    {
        public const string TitleScenePath = "Assets/AreaSurvivors/Scenes/01_Title.unity";
        public const string SuccessMarkerPath = "Library/AreaSurvivors/steam-achievement-scene-migration.success";

        [MenuItem("Area Survivors/Migrate/Install Steam Achievement Runtime")]
        public static void Install()
        {
            DeleteMarker();
            Scene scene = OpenSceneIfNeeded(TitleScenePath, out bool openedHere);
            if (!scene.IsValid()) throw new System.InvalidOperationException("Title scene could not be opened.");

            var runtimes = FindInScene<SteamAchievementRuntime>(scene);
            if (runtimes.Length == 0)
            {
                var gameObject = new GameObject("Steam Achievement Runtime");
                SceneManager.MoveGameObjectToScene(gameObject, scene);
                gameObject.AddComponent<SteamAchievementRuntime>();
                EditorSceneManager.MarkSceneDirty(scene);
            }
            else if (runtimes.Length > 1)
            {
                throw new System.InvalidOperationException("Title scene contains multiple SteamAchievementRuntime components.");
            }

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new System.InvalidOperationException("Title scene could not be saved.");
            }

            WriteMarker();
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            Debug.Log("Steam achievement runtime installed in title scene.");
        }

        static Scene OpenSceneIfNeeded(string path, out bool openedHere)
        {
            openedHere = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == path) return loaded;
            }

            openedHere = true;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        static T[] FindInScene<T>(Scene scene) where T : Component
        {
            var results = new System.Collections.Generic.List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                results.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return results.ToArray();
        }

        static void DeleteMarker()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
        }

        static void WriteMarker()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SuccessMarkerPath));
            File.WriteAllText(SuccessMarkerPath, System.DateTime.UtcNow.ToString("O"));
        }
    }
}
