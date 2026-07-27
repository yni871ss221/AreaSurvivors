using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AreaSurvivors.EditorTools
{
    public static class EndingCreditsSetup
    {
        internal const string ScenePath = "Assets/AreaSurvivors/Scenes/06_GameEnd.unity";
        internal const string PrefabPath = "Assets/AreaSurvivors/Prefabs/UI/EndingCreditsOverlay.prefab";
        internal const string ClipPath = "Assets/AreaSurvivors/Animations/UI/EndingCreditsScroll.anim";
        internal const string ControllerPath = "Assets/AreaSurvivors/Animations/UI/EndingCredits.controller";
        internal const string BackgroundPath = "Assets/AreaSurvivors/Sprites/Generated/UI/EndingCredits/EndingCreditsBackground.png";
        internal const string LogoPath = "Assets/AreaSurvivors/Sprites/Generated/UI/FuroPizzaStudioLogo.png";
        internal const string BgmPath = "Assets/AreaSurvivors/Resources/Audio/BGM/yuusou.mp3";
        internal const string SetupMarkerPath = "TokenReports/Validation/ending-credits-setup.success";
        internal const float Duration = 60f;
        internal const float BgmDuration = 56f;
        internal const float SceneFadeDuration = 2f;
        internal const float ScrollDuration = 50f;
        internal const float FinalHoldDuration = Duration - ScrollDuration;
        internal const float ScrollStartY = -1450f;
        internal const float ScrollEndY = 980f;

        internal static readonly string[] EntryNames =
        {
            "Story Line 1",
            "Story Line 2",
            "Story Line 3",
            "Story Line 4",
            "Studio Logo",
            "Developer Credit",
            "Thank You"
        };

        internal static readonly float[] EntryPositions = { 900f, 620f, 340f, 60f, -300f, -640f, -980f };
        internal static readonly int[] LocalizedEntryIndices = { 0, 1, 2, 3, 5, 6 };
        internal static readonly string[] JapaneseTexts =
        {
            "全ての敵を倒し、勇者は塔を守り切った",
            "しかし、この勝利は始まりに過ぎない",
            "いずれまた現れる敵に立ち向かうだろう",
            "平和が訪れるその日まで・・・",
            "ゲーム開発　しゅんたむ",
            "Thank you for playing!"
        };
        internal static readonly string[] EnglishTexts =
        {
            "The hero defeated every foe and defended the tower.",
            "But this victory was only the beginning.",
            "When enemies rise again, the hero will face them.",
            "Until the day peace finally comes...",
            "Game Development: Shuntamu",
            "Thank you for playing!"
        };

        [MenuItem("Area Survivors/Setup/Apply Ending Credits")]
        public static void ApplyFromMenu()
        {
            DeleteMarker(SetupMarkerPath);
            EnsureFolder("Assets/AreaSurvivors/Animations/UI");
            ConfigureBackgroundImporter();
            ConfigureBgmImporter();

            var clip = EnsureClip();
            var controller = EnsureController(clip);
            var prefab = EnsurePrefab(clip, controller);
            ApplyToGameEndScene(prefab);

            if (!EndingCreditsValidator.Validate(false))
                throw new InvalidOperationException("Ending credits setup completed but validation failed.");

            WriteMarker(SetupMarkerPath);
            Debug.Log("Ending credits setup: created the Scene-authored overlay, animation, and GameEnd reference.");
        }

        static void ConfigureBackgroundImporter()
        {
            AssetDatabase.ImportAsset(BackgroundPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Ending credits background importer is unavailable: " + BackgroundPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void ConfigureBgmImporter()
        {
            AssetDatabase.ImportAsset(BgmPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(BgmPath) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Ending credits BGM importer is unavailable: " + BgmPath);

            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
        }

        static AnimationClip EnsureClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "EndingCreditsScroll" };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            clip.frameRate = 60f;
            clip.wrapMode = WrapMode.ClampForever;

            SetLinearCurve(
                clip,
                string.Empty,
                typeof(CanvasGroup),
                "m_Alpha",
                new Keyframe(0f, 0f),
                new Keyframe(SceneFadeDuration, 1f),
                new Keyframe(Duration - SceneFadeDuration, 1f),
                new Keyframe(Duration, 0f));

            SetLinearCurve(
                clip,
                "Credits Viewport/Credits Content",
                typeof(RectTransform),
                "m_AnchoredPosition.y",
                new Keyframe(0f, ScrollStartY),
                new Keyframe(ScrollDuration, ScrollEndY),
                new Keyframe(Duration, ScrollEndY));

            for (int i = 0; i < EntryNames.Length; i++)
            {
                string path = "Credits Viewport/Credits Content/" + EntryNames[i];
                if (i == EntryNames.Length - 1)
                    SetFinalThankYouFadeCurve(clip, path, EntryPositions[i]);
                else
                    SetFadeCurve(clip, path, EntryPositions[i]);
            }

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }

        static void SetFinalThankYouFadeCurve(AnimationClip clip, string path, float localY)
        {
            float fadeInStart = TimeAtViewportY(-450f, localY);
            float fadeInEnd = TimeAtViewportY(-280f, localY);
            SetLinearCurve(
                clip,
                path,
                typeof(CanvasGroup),
                "m_Alpha",
                new Keyframe(fadeInStart, 0f),
                new Keyframe(fadeInEnd, 1f),
                new Keyframe(ScrollDuration, 1f),
                new Keyframe(Duration, 1f));
        }

        static void SetFadeCurve(AnimationClip clip, string path, float localY)
        {
            float fadeInStart = TimeAtViewportY(-450f, localY);
            float fadeInEnd = TimeAtViewportY(-280f, localY);
            float fadeOutStart = TimeAtViewportY(280f, localY);
            float fadeOutEnd = TimeAtViewportY(450f, localY);
            SetLinearCurve(
                clip,
                path,
                typeof(CanvasGroup),
                "m_Alpha",
                new Keyframe(fadeInStart, 0f),
                new Keyframe(fadeInEnd, 1f),
                new Keyframe(fadeOutStart, 1f),
                new Keyframe(fadeOutEnd, 0f));
        }

        static float TimeAtViewportY(float viewportY, float localY)
        {
            float progress = (viewportY - localY - ScrollStartY) / (ScrollEndY - ScrollStartY);
            return Mathf.Clamp01(progress) * ScrollDuration;
        }

        static void SetLinearCurve(AnimationClip clip, string path, Type type, string propertyName, params Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, propertyName), curve);
        }

        static AnimatorController EnsureController(AnimationClip clip)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (existing != null) return existing;

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState("EndingCreditsScroll");
            state.motion = clip;
            state.writeDefaultValues = true;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        static GameObject EnsurePrefab(AnimationClip clip, AnimatorController controller)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                var existingRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    ConfigureRootCanvas(existingRoot);
                    ConfigureRootFadeGroup(existingRoot);
                    RemoveObsoleteEdgeVignette(existingRoot);
                    var existingAnimator = existingRoot.GetComponent<Animator>();
                    var existingSequence = existingRoot.GetComponent<EndingCreditsSequence>();
                    if (existingAnimator == null || existingSequence == null)
                        throw new InvalidOperationException("Existing ending credits prefab is missing required runtime components.");
                    existingAnimator.runtimeAnimatorController = controller;
                    existingAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
                    existingAnimator.applyRootMotion = false;
                    existingSequence.root = existingRoot;
                    existingSequence.animator = existingAnimator;
                    existingSequence.creditsClip = clip;
                    existingSequence.stateName = "EndingCreditsScroll";
                    existingSequence.bgmDurationSeconds = BgmDuration;
                    ConfigureLocalizedLines(existingSequence, existingRoot.transform);
                    var upgraded = PrefabUtility.SaveAsPrefabAsset(existingRoot, PrefabPath);
                    if (upgraded == null) throw new InvalidOperationException("Ending credits prefab upgrade returned null.");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(existingRoot);
                }
                return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            var background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
            if (background == null) throw new InvalidOperationException("Ending credits background Sprite is missing.");
            if (logo == null) throw new InvalidOperationException("Furo Pizza Studio logo Sprite is missing.");

            var root = new GameObject(
                "EndingCreditsOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(CanvasGroup),
                typeof(Animator),
                typeof(EndingCreditsSequence));
            try
            {
                int uiLayer = LayerMask.NameToLayer("UI");
                root.layer = uiLayer >= 0 ? uiLayer : 0;
                SetStretch((RectTransform)root.transform, null);
                ConfigureRootCanvas(root);
                ConfigureRootFadeGroup(root);

                var backgroundImage = CreateStretchImage(root.transform, "Ending Background", background, Color.white);
                backgroundImage.preserveAspect = false;
                CreateStretchImage(root.transform, "Ending Background Dimmer", null, new Color(0f, 0f, 0f, 0.5f));
                CreateFrame(root.transform);

                var viewport = new GameObject("Credits Viewport", typeof(RectTransform), typeof(RectMask2D));
                viewport.layer = root.layer;
                var viewportRect = (RectTransform)viewport.transform;
                viewportRect.SetParent(root.transform, false);
                viewportRect.anchorMin = new Vector2(0.32f, 0.11f);
                viewportRect.anchorMax = new Vector2(0.68f, 0.89f);
                viewportRect.pivot = new Vector2(0.5f, 0.5f);
                viewportRect.anchoredPosition = Vector2.zero;
                viewportRect.sizeDelta = Vector2.zero;

                var content = new GameObject("Credits Content", typeof(RectTransform));
                content.layer = root.layer;
                var contentRect = (RectTransform)content.transform;
                contentRect.SetParent(viewportRect, false);
                contentRect.anchorMin = new Vector2(0.5f, 0.5f);
                contentRect.anchorMax = new Vector2(0.5f, 0.5f);
                contentRect.pivot = new Vector2(0.5f, 0.5f);
                contentRect.anchoredPosition = new Vector2(0f, ScrollStartY);
                contentRect.sizeDelta = new Vector2(640f, 2600f);

                CreateTextEntry(contentRect, EntryNames[0], "全ての敵を倒し、勇者は塔を守り切った", EntryPositions[0], 32);
                CreateTextEntry(contentRect, EntryNames[1], "しかし、この勝利は始まりに過ぎない", EntryPositions[1], 32);
                CreateTextEntry(contentRect, EntryNames[2], "いずれまた現れる敵に立ち向かうだろう", EntryPositions[2], 32);
                CreateTextEntry(contentRect, EntryNames[3], "平和が訪れるその日まで・・・", EntryPositions[3], 32);
                CreateLogoEntry(contentRect, logo, EntryPositions[4]);
                CreateTextEntry(contentRect, EntryNames[5], "ゲーム開発　しゅんたむ", EntryPositions[5], 30);
                CreateTextEntry(contentRect, EntryNames[6], "Thank you for playing!", EntryPositions[6], 40);

                var animator = root.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.applyRootMotion = false;

                var sequence = root.GetComponent<EndingCreditsSequence>();
                sequence.root = root;
                sequence.animator = animator;
                sequence.creditsClip = clip;
                sequence.stateName = "EndingCreditsScroll";
                sequence.bgmDurationSeconds = BgmDuration;
                ConfigureLocalizedLines(sequence, root.transform);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Ending credits prefab save returned null.");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static Image CreateStretchImage(Transform parent, string name, Sprite sprite, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.layer = parent.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            SetStretch(rect, parent);
            var image = gameObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void RemoveObsoleteEdgeVignette(GameObject root)
        {
            var existing = root.transform.Find("Ending Credits Edge Vignette");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        static void ConfigureLocalizedLines(EndingCreditsSequence sequence, Transform root)
        {
            var lines = new EndingCreditsSequence.LocalizedCreditLine[LocalizedEntryIndices.Length];
            for (int i = 0; i < LocalizedEntryIndices.Length; i++)
            {
                int entryIndex = LocalizedEntryIndices[i];
                string path = "Credits Viewport/Credits Content/" + EntryNames[entryIndex];
                var targetTransform = root.Find(path);
                var target = targetTransform != null ? targetTransform.GetComponent<Text>() : null;
                if (target == null) throw new InvalidOperationException("Ending credits localized Text is missing: " + path);
                lines[i] = new EndingCreditsSequence.LocalizedCreditLine
                {
                    target = target,
                    japanese = JapaneseTexts[i],
                    english = EnglishTexts[i]
                };
            }
            sequence.localizedLines = lines;
        }

        static void CreateFrame(Transform parent)
        {
            var frame = new GameObject("Ending Frame", typeof(RectTransform));
            frame.layer = parent.gameObject.layer;
            SetStretch((RectTransform)frame.transform, parent);
            Color outer = new Color(0.035f, 0.05f, 0.045f, 0.98f);
            Color accent = new Color(0.62f, 0.72f, 0.38f, 0.95f);

            CreateBar(frame.transform, "Top Frame", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 26f), outer);
            CreateBar(frame.transform, "Bottom Frame", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 26f), outer);
            CreateBar(frame.transform, "Left Frame", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(26f, 0f), outer);
            CreateBar(frame.transform, "Right Frame", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(26f, 0f), outer);
            CreateBar(frame.transform, "Top Accent", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -27f), new Vector2(-52f, 3f), accent);
            CreateBar(frame.transform, "Bottom Accent", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 27f), new Vector2(-52f, 3f), accent);
        }

        static void CreateBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.layer = parent.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static void CreateTextEntry(RectTransform parent, string name, string value, float y, int fontSize)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(CanvasGroup), typeof(Shadow));
            gameObject.layer = parent.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(620f, 120f);

            var text = gameObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Normal;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.1f;
            text.raycastTarget = false;
            text.text = value;

            var group = gameObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var shadow = gameObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;
        }

        static void CreateLogoEntry(RectTransform parent, Sprite logo, float y)
        {
            var gameObject = new GameObject(EntryNames[4], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            gameObject.layer = parent.gameObject.layer;
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(320f, 190f);

            var image = gameObject.GetComponent<Image>();
            image.sprite = logo;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var group = gameObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        static void SetStretch(RectTransform rect, Transform parent)
        {
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        static void ApplyToGameEndScene(GameObject prefab)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty)
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                throw new InvalidOperationException("06_GameEnd.unity has unsaved changes. Existing Scene layout was not modified.");
            }

            try
            {
                var screens = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameOverScreen>(true)).ToArray();
                if (screens.Length != 1) throw new InvalidOperationException("Ending credits setup requires exactly one GameOverScreen.");
                var screen = screens[0];

                var sequences = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<EndingCreditsSequence>(true)).ToArray();
                EndingCreditsSequence sequence;
                if (sequences.Length == 0)
                {
                    var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                    if (instance == null) throw new InvalidOperationException("Ending credits prefab instantiation failed.");
                    sequence = instance.GetComponent<EndingCreditsSequence>();
                    instance.SetActive(false);
                }
                else if (sequences.Length == 1)
                {
                    sequence = sequences[0];
                }
                else
                {
                    throw new InvalidOperationException("Multiple EndingCreditsSequence components exist in 06_GameEnd.unity.");
                }

                if (sequence.transform.parent != null) sequence.transform.SetParent(null, false);
                if (sequence.gameObject.scene != scene) SceneManager.MoveGameObjectToScene(sequence.gameObject, scene);
                sequence.transform.SetAsLastSibling();
                sequence.gameObject.SetActive(false);

                var serialized = new SerializedObject(screen);
                serialized.FindProperty("endingCredits").objectReferenceValue = sequence;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(screen);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    throw new InvalidOperationException("Failed to save 06_GameEnd.unity after adding ending credits.");
            }
            finally
            {
                if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static void ConfigureRootCanvas(GameObject root)
        {
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        static void ConfigureRootFadeGroup(GameObject root)
        {
            var group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        internal static void DeleteMarker(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        internal static void WriteMarker(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, DateTime.UtcNow.ToString("O"));
        }
    }
}
