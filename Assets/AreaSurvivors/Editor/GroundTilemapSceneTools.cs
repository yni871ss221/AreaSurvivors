using AreaSurvivors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.Editor
{
    public static class GroundTilemapSceneTools
    {
        const string GameScenePath = "Assets/AreaSurvivors/Scenes/05_Game.unity";

        [MenuItem("Area Survivors/Map/Clear Saved Ground Tiles In Active Scene")]
        public static void ClearSavedGroundTilesInActiveScene()
        {
            var grid = Object.FindObjectOfType<TileGrid>();
            if (grid == null || grid.groundTilemap == null)
            {
                Debug.LogWarning("TileGrid or Ground Tilemap was not found in the active scene.");
                return;
            }

            Undo.RegisterCompleteObjectUndo(grid.groundTilemap, "Clear saved ground tiles");
            grid.groundTilemap.ClearAllTiles();
            EditorUtility.SetDirty(grid.groundTilemap);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Saved Ground Tilemap cells were cleared. Runtime ground is rebuilt by TileGrid.Build().");
        }

        [MenuItem("Area Survivors/Map/Clear Saved Ground Tiles In 05_Game")]
        public static void ClearSavedGroundTilesInGameScene()
        {
            var previousScene = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            ClearSavedGroundTilesInActiveScene();
            EditorSceneManager.SaveScene(scene);
            if (!string.IsNullOrEmpty(previousScene) && previousScene != GameScenePath)
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }

        [MenuItem("Area Survivors/Map/Rebuild Ground Preview In Active Scene")]
        public static void RebuildGroundPreviewInActiveScene()
        {
            var grid = Object.FindObjectOfType<TileGrid>();
            if (grid == null)
            {
                Debug.LogWarning("TileGrid was not found in the active scene.");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(grid.gameObject, "Rebuild ground preview");
            grid.Build();
            EditorUtility.SetDirty(grid);
            if (grid.groundTilemap != null) EditorUtility.SetDirty(grid.groundTilemap);
            if (grid.paintTilemap != null) EditorUtility.SetDirty(grid.paintTilemap);
            if (grid.objectTilemap != null) EditorUtility.SetDirty(grid.objectTilemap);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Ground preview was rebuilt. Clear saved ground tiles before committing if this scene should stay lightweight.");
        }
    }
}
