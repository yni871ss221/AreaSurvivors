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
    public static class EndingCreditsValidator
    {
        const string MarkerPath = "TokenReports/Validation/ending-credits-validator.success";

        [MenuItem("Area Survivors/Validate/Ending Credits")]
        public static void ValidateFromMenu()
        {
            EndingCreditsSetup.DeleteMarker(MarkerPath);
            if (!Validate(true)) return;
            EndingCreditsSetup.WriteMarker(MarkerPath);
        }

        public static bool Validate(bool logSuccess = true)
        {
            try
            {
                ValidateAssets();
                ValidateScene();
                if (logSuccess) Debug.Log("Ending credits validator passed for assets, prefab, animation, and 06_GameEnd.unity.");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Ending credits validator failed: " + exception.Message);
                return false;
            }
        }

        static void ValidateAssets()
        {
            ValidateOneShotSaveContract();

            var background = AssetDatabase.LoadAssetAtPath<Sprite>(EndingCreditsSetup.BackgroundPath);
            if (background == null) throw new InvalidOperationException("Ending credits background Sprite is missing.");
            var importer = AssetImporter.GetAtPath(EndingCreditsSetup.BackgroundPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Ending credits background TextureImporter is missing.");
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
                throw new InvalidOperationException("Ending credits background must be a single Sprite.");
            if (importer.filterMode != FilterMode.Point || importer.mipmapEnabled)
                throw new InvalidOperationException("Ending credits background must use Point filtering without mipmaps.");
            if (importer.textureCompression != TextureImporterCompression.Uncompressed || importer.maxTextureSize < 2048)
                throw new InvalidOperationException("Ending credits background must be uncompressed with a 2048 max texture size.");

            var bgm = AssetDatabase.LoadAssetAtPath<AudioClip>(EndingCreditsSetup.BgmPath);
            if (bgm == null) throw new InvalidOperationException("Ending credits BGM is missing.");
            if (Mathf.Abs(bgm.length - EndingCreditsSetup.BgmDuration) > 1f)
                throw new InvalidOperationException("Ending credits BGM length must be approximately 56 seconds.");
            var bgmImporter = AssetImporter.GetAtPath(EndingCreditsSetup.BgmPath) as AudioImporter;
            if (bgmImporter == null || bgmImporter.defaultSampleSettings.loadType != AudioClipLoadType.Streaming
                || bgmImporter.defaultSampleSettings.preloadAudioData || !bgmImporter.loadInBackground)
                throw new InvalidOperationException("Ending credits BGM must use background streaming import settings.");

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(EndingCreditsSetup.ClipPath);
            if (clip == null) throw new InvalidOperationException("Ending credits AnimationClip is missing.");
            if (clip.isLooping) throw new InvalidOperationException("Ending credits AnimationClip must not loop.");
            if (Mathf.Abs(clip.length - EndingCreditsSetup.Duration) > 0.1f)
                throw new InvalidOperationException("Ending credits AnimationClip must match the configured 60 second duration.");

            var rootFadeCurve = AnimationUtility.GetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(CanvasGroup), "m_Alpha"));
            if (rootFadeCurve == null || rootFadeCurve.length < 4)
                throw new InvalidOperationException("Ending credits root fade curve is missing.");
            if (Mathf.Abs(rootFadeCurve[0].time) > 0.1f || rootFadeCurve[0].value > 0.01f
                || Mathf.Abs(rootFadeCurve[1].time - EndingCreditsSetup.SceneFadeDuration) > 0.1f || rootFadeCurve[1].value < 0.99f
                || Mathf.Abs(rootFadeCurve[rootFadeCurve.length - 2].time - (EndingCreditsSetup.Duration - EndingCreditsSetup.SceneFadeDuration)) > 0.1f
                || rootFadeCurve[rootFadeCurve.length - 2].value < 0.99f
                || Mathf.Abs(rootFadeCurve[rootFadeCurve.length - 1].time - EndingCreditsSetup.Duration) > 0.1f
                || rootFadeCurve[rootFadeCurve.length - 1].value > 0.01f)
                throw new InvalidOperationException("Ending credits root must fade in and out over two seconds.");

            var scrollBinding = EditorCurveBinding.FloatCurve(
                "Credits Viewport/Credits Content",
                typeof(RectTransform),
                "m_AnchoredPosition.y");
            var scrollCurve = AnimationUtility.GetEditorCurve(clip, scrollBinding);
            if (scrollCurve == null || scrollCurve.length < 3)
                throw new InvalidOperationException("Ending credits scroll curve is missing.");
            if (!Mathf.Approximately(scrollCurve[0].value, EndingCreditsSetup.ScrollStartY)
                || !Mathf.Approximately(scrollCurve[scrollCurve.length - 1].value, EndingCreditsSetup.ScrollEndY))
                throw new InvalidOperationException("Ending credits scroll curve does not span the configured vertical range.");
            var holdStartKey = scrollCurve[scrollCurve.length - 2];
            var holdEndKey = scrollCurve[scrollCurve.length - 1];
            if (Mathf.Abs(holdStartKey.time - EndingCreditsSetup.ScrollDuration) > 0.1f
                || Mathf.Abs(holdEndKey.time - EndingCreditsSetup.Duration) > 0.1f
                || !Mathf.Approximately(holdStartKey.value, EndingCreditsSetup.ScrollEndY)
                || !Mathf.Approximately(holdEndKey.value, EndingCreditsSetup.ScrollEndY))
                throw new InvalidOperationException("Ending credits must hold its final centered position through the final ten seconds.");
            if (!Mathf.Approximately(EndingCreditsSetup.ScrollEndY + EndingCreditsSetup.EntryPositions[6], 0f))
                throw new InvalidOperationException("Thank you message is not centered at the final scroll position.");

            foreach (string entryName in EndingCreditsSetup.EntryNames)
            {
                string path = "Credits Viewport/Credits Content/" + entryName;
                var alphaCurve = AnimationUtility.GetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(path, typeof(CanvasGroup), "m_Alpha"));
                if (alphaCurve == null || alphaCurve.length < 4)
                    throw new InvalidOperationException("Ending credits fade curve is missing: " + entryName);
                bool isFinalThankYou = entryName == EndingCreditsSetup.EntryNames[EndingCreditsSetup.EntryNames.Length - 1];
                if (alphaCurve[0].value > 0.01f)
                    throw new InvalidOperationException("Ending credits entry must fade in from transparent: " + entryName);
                if (isFinalThankYou)
                {
                    if (alphaCurve[alphaCurve.length - 1].value < 0.99f
                        || alphaCurve[alphaCurve.length - 1].time < EndingCreditsSetup.Duration - 0.1f)
                        throw new InvalidOperationException("Final Thank you message must remain visible through the final hold and root fade-out.");
                }
                else if (alphaCurve[alphaCurve.length - 1].value > 0.01f)
                {
                    throw new InvalidOperationException("Ending credits entry must fade out at the upper viewport edge: " + entryName);
                }
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EndingCreditsSetup.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Ending credits prefab is missing.");
            ValidateOverlay(prefab.GetComponent<EndingCreditsSequence>(), false);
        }

        static void ValidateOneShotSaveContract()
        {
            var qualifyingResult = new RunResult
            {
                gameClear = true,
                clearedStage = ProgressionStore.ImplementedStageCount,
                allStagesDifficultyFiveCleared = true
            };
            if (!GameOverScreen.ShouldPlayEndingCredits(qualifyingResult, false, true))
                throw new InvalidOperationException("The first all-stage difficulty 5 clear must play ending credits.");
            if (GameOverScreen.ShouldPlayEndingCredits(qualifyingResult, true, true))
                throw new InvalidOperationException("Viewed ending credits must not play again for the same save.");
            if (GameOverScreen.ShouldPlayEndingCredits(qualifyingResult, false, false))
                throw new InvalidOperationException("Ending credits must not start without the configured Scene sequence.");

            qualifyingResult.clearedStage = ProgressionStore.ImplementedStageCount - 1;
            if (GameOverScreen.ShouldPlayEndingCredits(qualifyingResult, false, true))
                throw new InvalidOperationException("Clearing an earlier stage must not play ending credits.");
            qualifyingResult.clearedStage = ProgressionStore.ImplementedStageCount;
            qualifyingResult.allStagesDifficultyFiveCleared = false;
            if (GameOverScreen.ShouldPlayEndingCredits(qualifyingResult, false, true))
                throw new InvalidOperationException("Ending credits require every stage to be cleared at difficulty 5.");

            string serializedSave = JsonUtility.ToJson(new SaveData());
            if (serializedSave.IndexOf("\"endingCreditsViewed\"", StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("SaveData must serialize the ending credits viewed flag.");

            const string gameOverScreenPath = "Assets/AreaSurvivors/Scripts/UI/GameOverScreen.cs";
            string gameOverScreenSource = File.ReadAllText(gameOverScreenPath);
            if (gameOverScreenSource.IndexOf(
                    "ProgressionStore.TryMarkEndingCreditsViewed()",
                    StringComparison.Ordinal) < 0)
                throw new InvalidOperationException("GameOverScreen must persist the one-shot flag before playback.");
        }

        static void ValidateScene()
        {
            var scene = SceneManager.GetSceneByPath(EndingCreditsSetup.ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(EndingCreditsSetup.ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty)
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                throw new InvalidOperationException("06_GameEnd.unity has unsaved changes; validation stopped.");
            }

            try
            {
                var screens = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameOverScreen>(true)).ToArray();
                if (screens.Length != 1) throw new InvalidOperationException("06_GameEnd.unity must contain exactly one GameOverScreen.");
                var screen = screens[0];
                if (screen.endingCredits == null) throw new InvalidOperationException("GameOverScreen.endingCredits is not assigned.");
                if (screen.endingCredits.gameObject.scene != scene)
                    throw new InvalidOperationException("GameOverScreen.endingCredits does not reference the scene instance.");

                var sequences = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndingCreditsSequence>(true)).ToArray();
                if (sequences.Length != 1 || sequences[0] != screen.endingCredits)
                    throw new InvalidOperationException("06_GameEnd.unity must contain one referenced EndingCreditsSequence.");
                if (screen.endingCredits.gameObject.activeSelf)
                    throw new InvalidOperationException("Ending credits scene instance must be inactive until the qualifying clear.");
                if (screen.root != null && screen.endingCredits.transform.IsChildOf(screen.root.transform))
                    throw new InvalidOperationException("Ending credits must not be a child of the result UI that is hidden during playback.");
                if (screen.endingCredits.transform.parent != null)
                    throw new InvalidOperationException("Ending credits must be an independent Scene root.");

                var canvas = screen.endingCredits.GetComponent<Canvas>();
                if (canvas == null || !canvas.enabled || canvas.sortingOrder < 100)
                    throw new InvalidOperationException("Ending credits scene instance requires its own high-priority Canvas.");
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(screen.endingCredits.gameObject);
                if (prefabPath != EndingCreditsSetup.PrefabPath)
                    throw new InvalidOperationException("Ending credits scene instance is not linked to the expected prefab.");

                ValidateOverlay(screen.endingCredits, true);
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ValidateOverlay(EndingCreditsSequence sequence, bool sceneInstance)
        {
            if (sequence == null) throw new InvalidOperationException("EndingCreditsSequence component is missing.");
            if (sequence.root != sequence.gameObject) throw new InvalidOperationException("EndingCreditsSequence.root must reference its own root.");
            if (sequence.animator == null || sequence.creditsClip == null)
                throw new InvalidOperationException("Ending credits Animator or AnimationClip reference is missing.");
            if (Mathf.Abs(sequence.bgmDurationSeconds - EndingCreditsSetup.BgmDuration) > 0.1f)
                throw new InvalidOperationException("Ending credits BGM stop time must remain at 56 seconds.");
            if (AssetDatabase.GetAssetPath(sequence.creditsClip) != EndingCreditsSetup.ClipPath)
                throw new InvalidOperationException("Ending credits clip reference does not use the expected asset.");
            if (sequence.animator.updateMode != AnimatorUpdateMode.UnscaledTime)
                throw new InvalidOperationException("Ending credits Animator must use unscaled time.");
            if (AssetDatabase.GetAssetPath(sequence.animator.runtimeAnimatorController) != EndingCreditsSetup.ControllerPath)
                throw new InvalidOperationException("Ending credits AnimatorController reference is incorrect.");
            var rootCanvas = sequence.GetComponent<Canvas>();
            var scaler = sequence.GetComponent<CanvasScaler>();
            var rootFadeGroup = sequence.GetComponent<CanvasGroup>();
            if (rootCanvas == null || rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay || rootCanvas.sortingOrder < 100)
                throw new InvalidOperationException("Ending credits prefab requires an independent Screen Space Overlay Canvas.");
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                throw new InvalidOperationException("Ending credits prefab requires a scale-with-screen CanvasScaler.");
            if (rootFadeGroup == null || rootFadeGroup.alpha > 0.01f || rootFadeGroup.interactable || rootFadeGroup.blocksRaycasts)
                throw new InvalidOperationException("Ending credits prefab requires a non-interactive root CanvasGroup initialized transparent.");

            var background = RequireImage(sequence.transform, "Ending Background");
            if (AssetDatabase.GetAssetPath(background.sprite) != EndingCreditsSetup.BackgroundPath)
                throw new InvalidOperationException("Ending credits background Image does not reference the generated Sprite.");
            var dimmer = RequireImage(sequence.transform, "Ending Background Dimmer");
            if (dimmer.color.r > 0.01f || dimmer.color.g > 0.01f || dimmer.color.b > 0.01f || dimmer.color.a < 0.35f || dimmer.color.a > 0.7f)
                throw new InvalidOperationException("Ending credits dimmer must be translucent black.");
            if (background.transform.GetSiblingIndex() >= dimmer.transform.GetSiblingIndex())
                throw new InvalidOperationException("Ending credits dimmer must render above the background.");

            if (sequence.transform.Find("Ending Credits Edge Vignette") != null)
                throw new InvalidOperationException("Ending credits must not contain the removed opening story edge vignette.");

            var frame = sequence.transform.Find("Ending Frame");
            if (frame == null || frame.Find("Top Frame") == null || frame.Find("Bottom Frame") == null
                || frame.Find("Left Frame") == null || frame.Find("Right Frame") == null)
                throw new InvalidOperationException("Ending credits frame is incomplete.");

            var viewport = sequence.transform.Find("Credits Viewport") as RectTransform;
            if (viewport == null || viewport.GetComponent<RectMask2D>() == null)
                throw new InvalidOperationException("Ending credits masked viewport is missing.");
            if (!Approximately(viewport.anchorMin.x, 0.32f) || !Approximately(viewport.anchorMax.x, 0.68f))
                throw new InvalidOperationException("Ending credits viewport must stay within the center third of the screen.");

            var content = viewport.Find("Credits Content");
            if (content == null) throw new InvalidOperationException("Ending credits content root is missing.");
            for (int i = 0; i < EndingCreditsSetup.EntryNames.Length; i++)
            {
                var entry = content.Find(EndingCreditsSetup.EntryNames[i]);
                if (entry == null || entry.GetComponent<CanvasGroup>() == null)
                    throw new InvalidOperationException("Ending credits entry or CanvasGroup is missing: " + EndingCreditsSetup.EntryNames[i]);
            }

            for (int i = 0; i < EndingCreditsSetup.LocalizedEntryIndices.Length; i++)
            {
                int entryIndex = EndingCreditsSetup.LocalizedEntryIndices[i];
                ValidateText(content, EndingCreditsSetup.EntryNames[entryIndex], EndingCreditsSetup.JapaneseTexts[i]);
            }
            ValidateLocalizedLines(sequence, content);

            var logo = RequireImage(content, EndingCreditsSetup.EntryNames[4]);
            if (AssetDatabase.GetAssetPath(logo.sprite) != EndingCreditsSetup.LogoPath || !logo.preserveAspect)
                throw new InvalidOperationException("Ending credits studio logo reference or aspect setting is incorrect.");
            if (sceneInstance && sequence.transform.parent != null)
                throw new InvalidOperationException("Ending credits scene instance must remain an independent Scene root.");
        }

        static void ValidateText(Transform parent, string name, string expected)
        {
            var child = parent.Find(name);
            var text = child != null ? child.GetComponent<Text>() : null;
            if (text == null || text.text != expected || text.font == null)
                throw new InvalidOperationException("Ending credits text is missing or changed: " + name);
        }

        static void ValidateLocalizedLines(EndingCreditsSequence sequence, Transform content)
        {
            var lines = sequence.localizedLines;
            if (lines == null || lines.Length != EndingCreditsSetup.LocalizedEntryIndices.Length)
                throw new InvalidOperationException("Ending credits localized line references are incomplete.");

            for (int i = 0; i < lines.Length; i++)
            {
                int entryIndex = EndingCreditsSetup.LocalizedEntryIndices[i];
                var expectedTarget = content.Find(EndingCreditsSetup.EntryNames[entryIndex]);
                var line = lines[i];
                if (line == null || line.target == null || line.target.transform != expectedTarget)
                    throw new InvalidOperationException("Ending credits localized Text reference is incorrect: " + EndingCreditsSetup.EntryNames[entryIndex]);
                if (line.japanese != EndingCreditsSetup.JapaneseTexts[i] || line.english != EndingCreditsSetup.EnglishTexts[i])
                    throw new InvalidOperationException("Ending credits Japanese/English text pair is incorrect: " + EndingCreditsSetup.EntryNames[entryIndex]);
            }
        }

        static Image RequireImage(Transform parent, string name)
        {
            var child = parent.Find(name);
            var image = child != null ? child.GetComponent<Image>() : null;
            if (image == null) throw new InvalidOperationException("Ending credits Image is missing: " + name);
            return image;
        }

        static bool Approximately(float value, float expected)
        {
            return Mathf.Abs(value - expected) <= 0.001f;
        }
    }
}
