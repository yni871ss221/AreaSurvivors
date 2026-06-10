using AreaSurvivors;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace AreaSurvivors.Editor
{
    [CustomEditor(typeof(MapPerimeterController))]
    public sealed class MapPerimeterControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();

            var perimeter = (MapPerimeterController)target;
            if (GUILayout.Button("Rebuild Perimeter"))
            {
                perimeter.Rebuild();
                EditorUtility.SetDirty(perimeter);
                EditorSceneManager.MarkSceneDirty(perimeter.gameObject.scene);
            }

            if (GUILayout.Button("Clear Generated Perimeter"))
            {
                perimeter.ClearGenerated();
                EditorUtility.SetDirty(perimeter);
                EditorSceneManager.MarkSceneDirty(perimeter.gameObject.scene);
            }
        }

    }
}
