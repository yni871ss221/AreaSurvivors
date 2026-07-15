using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class GroundStrikeEvolutionMigration
    {
        const string ConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const string ArrowShowerPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/ArrowShowerStrike.prefab";
        const string ArrowRainAreaPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/ArrowRainArea.prefab";
        const string LegacyWaterFramePath = "Assets/AreaSurvivors/Sprites/Generated/Weapons/ArrowShowerImpactFrame04.png";
        const string LegacyRangeVisualObjectName = "Arrow Shower Attack Range Visual";
        const string RangeVisualObjectName = "Arrow Shower Attack Range Visual Mesh Only";
        const string RangeFillObjectName = "Arrow Shower Range Fill";
        const string RangeOutlineObjectName = "Arrow Shower Range Outline";
        const string AnimatorVisualObjectName = "Arrow Shower Animator Visual";
        const string AnimationFolderPath = "Assets/AreaSurvivors/Animations/Weapons/ArrowShower";
        const string AnimationClipPath = AnimationFolderPath + "/ArrowShowerFall.anim";
        const string AnimatorControllerPath = AnimationFolderPath + "/ArrowShower.controller";
        const string AnimationStateName = "ArrowShowerFall";
        const float TargetRadiusCells = 15f;
        const float ArrowFramePixelsPerUnit = 384f;
        const float AnimatorVisualScale = 2f;
        const float FrameDurationSeconds = 0.08f;
        const int ImpactFrameIndex = 4;
        static readonly Vector2 ArrowFrameImpactPivot = new Vector2(90f / 192f, 22f / 192f);

        static readonly string[] FrameNames =
        {
            "Weapons/ArrowShowerImpactFrame01",
            "Weapons/ArrowShowerImpactFrame02",
            "Weapons/ArrowShowerImpactFrame03"
        };

        [MenuItem("Area Survivors/Migrations/Apply Ground Strike Range And Animation")]
        public static void Apply()
        {
            var frames = new Sprite[FrameNames.Length];
            for (int i = 0; i < FrameNames.Length; i++)
            {
                ImportArrowFrame(FrameNames[i]);
                frames[i] = GeneratedSpriteAssetUtility.LoadSprite(FrameNames[i]);
                if (frames[i] == null) throw new InvalidOperationException("Ground strike animation frame is missing: " + FrameNames[i]);
            }

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) throw new InvalidOperationException("GameConfig is missing: " + ConfigPath);
            config.evolvedGroundStrikeTargetRadiusCells = TargetRadiusCells;
            EditorUtility.SetDirty(config);

            var animationClip = EnsureAnimationClip(frames);
            var animatorController = EnsureAnimatorController(animationClip);

            var root = PrefabUtility.LoadPrefabContents(ArrowShowerPrefabPath);
            try
            {
                RemoveLegacyRuntimeAnimation(root);
                var rangeVisualRoot = ConfigureAttackRangeVisual(root);
                ConfigureRangeVisualScaleRoot(root, rangeVisualRoot);
                ConfigureAnimatorVisual(root, frames[0], animationClip, animatorController);
                PrefabUtility.SaveAsPrefabAsset(root, ArrowShowerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(LegacyWaterFramePath) != null && !AssetDatabase.DeleteAsset(LegacyWaterFramePath))
            {
                throw new InvalidOperationException("Failed to remove obsolete Arrow Shower water-splash frame: " + LegacyWaterFramePath);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Ground strike range and Arrow Shower Animator migration: completed. Legacy runtime animation removed.");
        }

        static AnimationClip EnsureAnimationClip(Sprite[] frames)
        {
            EnsureAssetFolder(AnimationFolderPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationClipPath);
            if (clip == null)
            {
                clip = new AnimationClip
                {
                    name = AnimationStateName,
                    frameRate = 100f
                };
                AssetDatabase.CreateAsset(clip, AnimationClipPath);
            }

            clip.frameRate = 100f;
            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite");
            var keyframes = new[]
            {
                new ObjectReferenceKeyframe { time = 0f, value = frames[0] },
                new ObjectReferenceKeyframe { time = FrameDurationSeconds, value = frames[0] },
                new ObjectReferenceKeyframe { time = FrameDurationSeconds * 2f, value = frames[1] },
                new ObjectReferenceKeyframe { time = FrameDurationSeconds * 3f, value = frames[1] },
                new ObjectReferenceKeyframe { time = FrameDurationSeconds * ImpactFrameIndex, value = frames[2] },
                new ObjectReferenceKeyframe { time = FrameDurationSeconds * 5f, value = frames[2] }
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            var existingFloatBindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < existingFloatBindings.Length; i++)
            {
                AnimationUtility.SetEditorCurve(clip, existingFloatBindings[i], null);
            }
            var positionBinding = EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalPosition.y");
            var positionCurve = new AnimationCurve(
                new Keyframe(0f, 1.6f),
                new Keyframe(FrameDurationSeconds, 1.25f),
                new Keyframe(FrameDurationSeconds * 2f, 0.9f),
                new Keyframe(FrameDurationSeconds * 3f, 0.45f),
                new Keyframe(FrameDurationSeconds * ImpactFrameIndex, 0f),
                new Keyframe(FrameDurationSeconds * 5f, 0f));
            for (int i = 0; i < positionCurve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(positionCurve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(positionCurve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, positionBinding, positionCurve);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            if (AnimationUtility.GetAnimationEvents(clip).Length != 0)
            {
                AnimationUtility.SetAnimationEvents(clip, new AnimationEvent[0]);
            }
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.ImportAsset(AnimationClipPath, ImportAssetOptions.ForceUpdate);

            var reloadedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationClipPath);
            if (reloadedClip == null) throw new InvalidOperationException("Arrow Shower AnimationClip reload failed: " + AnimationClipPath);
            if (AnimationUtility.GetAnimationEvents(reloadedClip).Length != 0)
            {
                throw new InvalidOperationException("Arrow Shower AnimationClip must not depend on cached Animation Events.");
            }
            return reloadedClip;
        }

        static AnimatorController EnsureAnimatorController(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
            if (controller != null) return controller;

            controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState(AnimationStateName);
            state.motion = clip;
            state.writeDefaultValues = true;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void EnsureAssetFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        static void ConfigureAnimatorVisual(
            GameObject root,
            Sprite firstFrame,
            AnimationClip animationClip,
            RuntimeAnimatorController animatorController)
        {
            var visualTransform = root.transform.Find(AnimatorVisualObjectName);
            bool created = visualTransform == null;
            if (created)
            {
                var visualObject = new GameObject(AnimatorVisualObjectName);
                visualTransform = visualObject.transform;
                visualTransform.SetParent(root.transform, false);
            }
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = Vector3.one * AnimatorVisualScale;

            var spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = firstFrame;
            spriteRenderer.sortingOrder = WeaponSortingOrders.Projectile;

            var billboard = visualTransform.GetComponent<PaperBillboard>();
            if (billboard != null) UnityEngine.Object.DestroyImmediate(billboard, true);

            var unityAnimator = visualTransform.GetComponent<Animator>();
            if (unityAnimator == null) unityAnimator = visualTransform.gameObject.AddComponent<Animator>();
            unityAnimator.runtimeAnimatorController = animatorController;
            unityAnimator.applyRootMotion = false;
            unityAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            unityAnimator.updateMode = AnimatorUpdateMode.Normal;
            unityAnimator.enabled = true;

            var playback = visualTransform.GetComponent<GroundStrikeAnimatorPlayback>();
            if (playback == null) playback = visualTransform.gameObject.AddComponent<GroundStrikeAnimatorPlayback>();
            playback.Configure(unityAnimator, animationClip, FrameDurationSeconds * ImpactFrameIndex);
            playback.enabled = true;
            visualTransform.gameObject.SetActive(true);

            if (created) EditorUtility.SetDirty(visualTransform);
            EditorUtility.SetDirty(spriteRenderer);
            EditorUtility.SetDirty(unityAnimator);
            EditorUtility.SetDirty(playback);
        }

        static void ImportArrowFrame(string frameName)
        {
            GeneratedSpriteAssetUtility.ImportSprite(frameName, ArrowFramePixelsPerUnit);
            string path = GeneratedSpriteAssetUtility.FindSpritePath(frameName);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Ground strike animation frame importer is missing: " + frameName);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = ArrowFrameImpactPivot;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        static Transform ConfigureAttackRangeVisual(GameObject targetRoot)
        {
            var sourceRoot = PrefabUtility.LoadPrefabContents(ArrowRainAreaPrefabPath);
            Transform rangeVisualRoot = null;
            try
            {
                var sourceArea = sourceRoot.GetComponent<ArrowRainAreaVisual>();
                if (sourceArea == null) throw new InvalidOperationException("ArrowRainArea source visual is missing.");
                var sourceSerialized = new SerializedObject(sourceArea);
                var sourceFillFilter = sourceSerialized.FindProperty("fillMeshFilter")?.objectReferenceValue as MeshFilter;
                var sourceFillRenderer = sourceSerialized.FindProperty("fillRenderer")?.objectReferenceValue as MeshRenderer;
                var sourceOutline = sourceSerialized.FindProperty("outlineRenderer")?.objectReferenceValue as LineRenderer;
                if (sourceFillFilter == null || sourceFillRenderer == null || sourceOutline == null)
                {
                    throw new InvalidOperationException("ArrowRainArea source range visual references are incomplete.");
                }

                var existingArea = targetRoot.GetComponent<ArrowRainAreaVisual>();
                if (existingArea != null) UnityEngine.Object.DestroyImmediate(existingArea);
                var legacyContainer = targetRoot.transform.Find(LegacyRangeVisualObjectName);
                if (legacyContainer != null) UnityEngine.Object.DestroyImmediate(legacyContainer.gameObject);
                var existingContainer = targetRoot.transform.Find(RangeVisualObjectName);
                if (existingContainer != null) UnityEngine.Object.DestroyImmediate(existingContainer.gameObject);

                var container = new GameObject(RangeVisualObjectName);
                container.transform.SetParent(targetRoot.transform, false);
                rangeVisualRoot = container.transform;
                var fillObject = CreateVisualObject(sourceFillFilter.transform, container.transform, RangeFillObjectName);
                var fillFilter = fillObject.AddComponent<MeshFilter>();
                var fillRenderer = fillObject.AddComponent<MeshRenderer>();
                EditorUtility.CopySerialized(sourceFillFilter, fillFilter);
                EditorUtility.CopySerialized(sourceFillRenderer, fillRenderer);

                var outlineObject = CreateVisualObject(sourceOutline.transform, container.transform, RangeOutlineObjectName);
                var outline = outlineObject.AddComponent<LineRenderer>();
                EditorUtility.CopySerialized(sourceOutline, outline);

                var targetArea = targetRoot.AddComponent<ArrowRainAreaVisual>();
                CopyRangeVisualSettings(sourceSerialized, targetArea);
                foreach (var spriteVisual in container.GetComponentsInChildren<PaperMeshVisual>(true))
                {
                    UnityEngine.Object.DestroyImmediate(spriteVisual);
                }
                targetArea.Initialize(fillFilter, fillRenderer, outline);
                EditorUtility.SetDirty(targetArea);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
            return rangeVisualRoot;
        }

        static void ConfigureRangeVisualScaleRoot(GameObject root, Transform rangeVisualRoot)
        {
            if (rangeVisualRoot == null) throw new InvalidOperationException("Arrow Shower range visual root is missing.");
            var area = root.GetComponent<AdvancedWeaponArea>();
            if (area == null) throw new InvalidOperationException("ArrowShowerStrike requires AdvancedWeaponArea.");

            var serializedArea = new SerializedObject(area);
            var scaleRoot = serializedArea.FindProperty("visualScaleRoot");
            if (scaleRoot == null) throw new InvalidOperationException("AdvancedWeaponArea.visualScaleRoot is missing.");
            scaleRoot.objectReferenceValue = rangeVisualRoot;
            serializedArea.ApplyModifiedPropertiesWithoutUndo();

            root.transform.localScale = Vector3.one;
            rangeVisualRoot.localScale = Vector3.one;
            EditorUtility.SetDirty(area);
            EditorUtility.SetDirty(rangeVisualRoot);
        }

        static void RemoveLegacyRuntimeAnimation(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transforms[i].gameObject);
            }

            var visuals = root.GetComponentsInChildren<PaperMeshVisual>(true);
            for (int i = 0; i < visuals.Length; i++)
            {
                var visual = visuals[i];
                if (visual != null && !IsInsideRangeVisual(visual.transform))
                {
                    UnityEngine.Object.DestroyImmediate(visual.gameObject);
                }
            }
        }

        static GameObject CreateVisualObject(Transform source, Transform parent, string objectName)
        {
            var result = new GameObject(objectName);
            result.transform.SetParent(parent, false);
            result.transform.localPosition = source.localPosition;
            result.transform.localRotation = source.localRotation;
            result.transform.localScale = source.localScale;
            return result;
        }

        static void CopyRangeVisualSettings(SerializedObject source, ArrowRainAreaVisual targetArea)
        {
            var target = new SerializedObject(targetArea);
            CopyColor(source, target, "fillColor");
            CopyColor(source, target, "outlineColor");
            CopyInteger(source, target, "fillSortingOrder");
            CopyInteger(source, target, "outlineSortingOrder");
            CopyFloat(source, target, "outlineWidth");
            CopyFloat(source, target, "areaVerticalAspect");
            target.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CopyColor(SerializedObject source, SerializedObject target, string propertyName)
        {
            var sourceProperty = source.FindProperty(propertyName);
            var targetProperty = target.FindProperty(propertyName);
            if (sourceProperty != null && targetProperty != null) targetProperty.colorValue = sourceProperty.colorValue;
        }

        static void CopyInteger(SerializedObject source, SerializedObject target, string propertyName)
        {
            var sourceProperty = source.FindProperty(propertyName);
            var targetProperty = target.FindProperty(propertyName);
            if (sourceProperty != null && targetProperty != null) targetProperty.intValue = sourceProperty.intValue;
        }

        static void CopyFloat(SerializedObject source, SerializedObject target, string propertyName)
        {
            var sourceProperty = source.FindProperty(propertyName);
            var targetProperty = target.FindProperty(propertyName);
            if (sourceProperty != null && targetProperty != null) targetProperty.floatValue = sourceProperty.floatValue;
        }

        static bool IsInsideRangeVisual(Transform candidate)
        {
            while (candidate != null)
            {
                if (candidate.name == RangeVisualObjectName) return true;
                candidate = candidate.parent;
            }
            return false;
        }

    }
}
