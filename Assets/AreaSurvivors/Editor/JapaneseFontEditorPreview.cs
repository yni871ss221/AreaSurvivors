using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    [InitializeOnLoad]
    static class JapaneseFontEditorPreview
    {
        static JapaneseFontEditorPreview()
        {
            EditorApplication.delayCall += Apply;
            EditorApplication.hierarchyChanged += ScheduleApply;
            EditorSceneManager.sceneOpened += (_, __) => ScheduleApply();
            EditorApplication.playModeStateChanged += _ => ScheduleApply();
        }

        static void ScheduleApply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.delayCall -= Apply;
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            JapaneseFontProvider.ApplyAllLoadedText();
            SceneView.RepaintAll();
            GameViewRepaint();
        }

        static void GameViewRepaint()
        {
            var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;
            var window = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (window != null) window.Repaint();
        }
    }
}
