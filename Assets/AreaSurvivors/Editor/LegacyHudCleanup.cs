using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.Editor
{
    public static class LegacyHudCleanup
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";

        [MenuItem("Area Survivors/Rebuild/Cleanup Legacy HUD Objects")]
        public static void CleanupLegacyHudObjects()
        {
            var scene = OpenGameScene(out var openedAdditive);
            if (!scene.IsValid())
            {
                Debug.LogError("05_Game scene was not found.");
                return;
            }

            var hud = FindInScene(scene, "HUD");
            if (hud == null)
            {
                Debug.LogWarning("HUD was not found in 05_Game.");
                CloseIfOpened(scene, openedAdditive);
                return;
            }

            int removed = 0;
            removed += DestroyDirectChild(hud.transform, "Weapon Status");
            removed += DestroyDirectChild(hud.transform, "Construction Menu");
            removed += DestroyDirectChild(hud.transform, "Wood Resource");
            removed += DestroyDirectChild(hud.transform, "Stone Resource");
            removed += DestroyDirectChild(hud.transform, "Upgrade Building Button");
            removed += DestroyDirectChild(hud.transform, "Build Lobby Button");
            removed += DestroyDirectChild(hud.transform, "Upgrade Cursor Icon");
            removed += DestroyDeepChild(hud.transform, "Weapon Frame");

            if (removed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            CloseIfOpened(scene, openedAdditive);
            Debug.Log($"Legacy HUD cleanup completed. removed={removed}");
        }

        static Scene OpenGameScene(out bool openedAdditive)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == GameScenePath)
                {
                    openedAdditive = false;
                    return loaded;
                }
            }

            openedAdditive = true;
            return EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
        }

        static void CloseIfOpened(Scene scene, bool openedAdditive)
        {
            if (openedAdditive && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }

        static GameObject FindInScene(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var found = FindDeep(root.transform, name);
                if (found != null) return found.gameObject;
            }
            return null;
        }

        static Transform FindDeep(Transform parent, string name)
        {
            if (parent == null) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                var found = FindDeep(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static int DestroyDirectChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) return 0;
            Object.DestroyImmediate(child.gameObject);
            return 1;
        }

        static int DestroyDeepChild(Transform parent, string name)
        {
            var child = FindDeep(parent, name);
            if (child == null || child == parent) return 0;
            Object.DestroyImmediate(child.gameObject);
            return 1;
        }
    }
}
