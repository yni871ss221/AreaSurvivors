using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.Editor
{
    public static class OpeningStoryOverlaySetup
    {
        const string PrefabPath = "Assets/AreaSurvivors/Prefabs/UI/OpeningStoryOverlay.prefab";
        const string ScenePath = "Assets/AreaSurvivors/Scenes/01_Title.unity";
        const string DimmerName = "Story Background Dimmer";
        const string CaptionName = "Story Caption";
        const string ApplyMarkerPath = "TokenReports/Validation/opening-story-overlay-setup.success";
        const string ValidateMarkerPath = "TokenReports/Validation/opening-story-overlay-validator.success";

        static readonly string[] DefaultCaptions =
        {
            "静かな塔の中で、ナイトは眠りについていた。",
            "門番は夜の平原に、ただならぬ気配を感じ取る。",
            "イノシシの大群が、中心塔へ向かって押し寄せる。",
            "門番の知らせを受け、ナイトは跳ね起きた。",
            "ナイトは塔を守るため、戦場へ駆け出した。",
            "ストーリーを見届けたナイトは、塔を守るため戦場へ向かう。"
        };

        [MenuItem("Area Survivors/Setup/Apply Opening Story Overlay")]
        public static void ApplyFromMenu()
        {
            DeleteMarker(ApplyMarkerPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root == null) throw new InvalidOperationException("Opening story prefab could not be loaded.");
                ApplyToRoot(root);

                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (savedPrefab == null) throw new InvalidOperationException("Opening story prefab save returned null.");
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }

            ApplyToTitleScene();
            WriteMarker(ApplyMarkerPath);
            Debug.Log("Opening story overlay setup applied to the prefab and title scene.");
        }

        [MenuItem("Area Survivors/Validate/Opening Story Overlay")]
        public static void ValidateFromMenu()
        {
            DeleteMarker(ValidateMarkerPath);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root == null) throw new InvalidOperationException("Opening story prefab could not be loaded.");
                Validate(root);
            }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
            }

            ValidateTitleScene();
            WriteMarker(ValidateMarkerPath);
            Debug.Log("Opening story overlay validator passed for the prefab and title scene.");
        }

        static void ApplyToRoot(GameObject root)
        {
            var sequence = root.GetComponent<OpeningStorySequence>();
            if (sequence == null) throw new InvalidOperationException("OpeningStorySequence is missing from " + root.name + ".");

            var dimmer = EnsureDimmer(root.transform);
            var caption = EnsureCaption(root.transform);
            sequence.backgroundDimmer = dimmer;
            sequence.captionText = caption;
            sequence.captionGroup = caption.GetComponent<CanvasGroup>();
            if (sequence.captions == null || sequence.captions.Length == 0)
            {
                sequence.captions = (string[])DefaultCaptions.Clone();
            }
            else if (sequence.captions.Length != sequence.sceneGroups.Length)
            {
                throw new InvalidOperationException("Existing opening story captions do not match the scene count; preserving them without overwrite.");
            }
        }

        static void ApplyToTitleScene()
        {
            bool wasLoaded;
            var scene = OpenTitleScene(out wasLoaded);
            try
            {
                var root = FindSceneOverlay(scene);
                ApplyToRoot(root);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Title scene save failed: " + ScenePath);
            }
            finally
            {
                if (!wasLoaded && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void ValidateTitleScene()
        {
            bool wasLoaded;
            var scene = OpenTitleScene(out wasLoaded);
            try
            {
                var root = FindSceneOverlay(scene);
                Validate(root);

                var sequence = root.GetComponent<OpeningStorySequence>();
                StudioLogoIntro intro = null;
                foreach (var sceneRoot in scene.GetRootGameObjects())
                {
                    var candidate = sceneRoot.GetComponentInChildren<StudioLogoIntro>(true);
                    if (candidate == null) continue;
                    if (intro != null) throw new InvalidOperationException("Multiple StudioLogoIntro components exist in the title scene.");
                    intro = candidate;
                }

                if (intro == null) throw new InvalidOperationException("StudioLogoIntro is missing from the title scene.");
                if (intro.openingStorySequence != sequence)
                    throw new InvalidOperationException("StudioLogoIntro does not reference the validated scene OpeningStorySequence.");

                var canvas = root.GetComponentInParent<Canvas>(true);
                if (canvas == null || !canvas.enabled || !canvas.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("The scene opening story overlay does not have an active parent Canvas.");
            }
            finally
            {
                if (!wasLoaded && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static Scene OpenTitleScene(out bool wasLoaded)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            wasLoaded = scene.IsValid() && scene.isLoaded;
            if (wasLoaded)
            {
                if (scene.isDirty) throw new InvalidOperationException("Title scene has unsaved changes; refusing to overwrite them.");
                return scene;
            }

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        }

        static GameObject FindSceneOverlay(Scene scene)
        {
            foreach (var sceneRoot in scene.GetRootGameObjects())
            {
                foreach (var sequence in sceneRoot.GetComponentsInChildren<OpeningStorySequence>(true))
                {
                    if (sequence.gameObject.name == "OpeningStoryOverlay") return sequence.gameObject;
                }
            }

            throw new InvalidOperationException("OpeningStoryOverlay is missing from the title scene.");
        }

        static Image EnsureDimmer(Transform root)
        {
            var existing = root.Find(DimmerName);
            if (existing != null)
            {
                var existingImage = existing.GetComponent<Image>();
                if (existingImage == null) throw new InvalidOperationException(DimmerName + " exists without an Image component.");
                return existingImage;
            }

            var gameObject = new GameObject(DimmerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.layer = root.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var image = gameObject.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.5f);
            image.raycastTarget = false;

            var vignette = root.Find("Opening Story Edge Vignette");
            if (vignette != null) rect.SetSiblingIndex(vignette.GetSiblingIndex());
            return image;
        }

        static Text EnsureCaption(Transform root)
        {
            var existing = root.Find(CaptionName);
            if (existing != null)
            {
                var existingText = existing.GetComponent<Text>();
                if (existingText == null) throw new InvalidOperationException(CaptionName + " exists without a Text component.");
                if (existing.GetComponent<CanvasGroup>() == null) throw new InvalidOperationException(CaptionName + " exists without a CanvasGroup component.");
                return existingText;
            }

            var gameObject = new GameObject(
                CaptionName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text),
                typeof(CanvasGroup),
                typeof(Shadow));
            gameObject.layer = root.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0.08f, 0.05f);
            rect.anchorMax = new Vector2(0.92f, 0.25f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.SetAsLastSibling();

            var text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1.1f;
            text.raycastTarget = false;
            text.text = string.Empty;

            var group = gameObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var shadow = gameObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;
            return text;
        }

        static void Validate(GameObject root)
        {
            var sequence = root.GetComponent<OpeningStorySequence>();
            if (sequence == null) throw new InvalidOperationException("OpeningStorySequence is missing.");
            if (sequence.backgroundDimmer == null) throw new InvalidOperationException("Background dimmer reference is missing.");
            if (sequence.captionText == null) throw new InvalidOperationException("Caption Text reference is missing.");
            if (sequence.captionGroup == null) throw new InvalidOperationException("Caption CanvasGroup reference is missing.");
            if (sequence.sceneGroups == null || sequence.sceneGroups.Length == 0) throw new InvalidOperationException("Story scenes are missing.");
            if (sequence.captions == null || sequence.captions.Length != sequence.sceneGroups.Length)
                throw new InvalidOperationException("Caption count does not match the story scene count.");

            for (int i = 0; i < sequence.captions.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(sequence.captions[i]))
                    throw new InvalidOperationException("Story caption is empty at index " + i + ".");
            }

            var dimmer = sequence.backgroundDimmer;
            if (dimmer.transform.parent != root.transform) throw new InvalidOperationException("Background dimmer is not a direct overlay child.");
            if (dimmer.raycastTarget) throw new InvalidOperationException("Background dimmer must not block raycasts.");
            if (dimmer.color.r > 0.01f || dimmer.color.g > 0.01f || dimmer.color.b > 0.01f || dimmer.color.a <= 0f || dimmer.color.a >= 1f)
                throw new InvalidOperationException("Background dimmer must be translucent black.");

            int dimmerIndex = dimmer.transform.GetSiblingIndex();
            foreach (var sceneGroup in sequence.sceneGroups)
            {
                if (sceneGroup == null) throw new InvalidOperationException("A story scene reference is missing.");
                if (sceneGroup.transform.GetSiblingIndex() >= dimmerIndex)
                    throw new InvalidOperationException("Background dimmer must be rendered above every story scene.");
            }

            if (sequence.captionText.transform.parent != root.transform) throw new InvalidOperationException("Caption is not a direct overlay child.");
            if (sequence.captionText.transform.GetSiblingIndex() <= dimmerIndex)
                throw new InvalidOperationException("Caption must be rendered above the background dimmer.");
            if (sequence.captionText.font == null) throw new InvalidOperationException("Caption Font reference is missing.");
            if (sequence.captionGroup.gameObject != sequence.captionText.gameObject)
                throw new InvalidOperationException("Caption Text and CanvasGroup must share one object.");
            if (!sequence.captionText.gameObject.activeSelf) throw new InvalidOperationException("Caption object must be active in its saved source.");
            if (sequence.captionText.raycastTarget) throw new InvalidOperationException("Caption must not block raycasts.");
        }

        static void DeleteMarker(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        static void WriteMarker(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
        }
    }
}
