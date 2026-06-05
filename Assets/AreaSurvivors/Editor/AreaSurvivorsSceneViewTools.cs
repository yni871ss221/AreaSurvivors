using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class AreaSurvivorsSceneViewTools
    {
        [MenuItem("Area Survivors/Align Scene View To Game Camera")]
        public static void AlignSceneViewToGameCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("Main Camera was not found.");
                return;
            }

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                sceneView = EditorWindow.GetWindow<SceneView>();
            }

            if (sceneView == null) return;

            sceneView.in2DMode = false;
            sceneView.orthographic = camera.orthographic;
            sceneView.rotation = camera.transform.rotation;
            sceneView.pivot = camera.transform.position + camera.transform.forward * Mathf.Max(0.1f, sceneView.cameraDistance);
            sceneView.size = camera.orthographic ? camera.orthographicSize : sceneView.size;
            sceneView.Repaint();
        }
    }
}
