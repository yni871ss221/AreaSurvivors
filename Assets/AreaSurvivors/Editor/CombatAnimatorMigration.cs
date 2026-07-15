using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    /// <summary>
    /// One-shot migration from the legacy runtime sprite swapping used by Slash, Frost and Arrow Rain
    /// to prefab-owned SpriteRenderer/Animator visuals. Existing animation assets and existing prefab
    /// transform placement/scale are intentionally preserved on subsequent runs.
    /// </summary>
    public static class CombatAnimatorMigration
    {
        const string AnimationRoot = "Assets/AreaSurvivors/Animations/Weapons";
        const string SpriteRoot = "Assets/AreaSurvivors/Sprites/Generated/Weapons";

        const string SlashPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/Slash.prefab";
        const string SwordRushPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/SwordRushSlash.prefab";
        const string FrostPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/FrostArea.prefab";
        const string FrostStormPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/FrostStormSpike.prefab";
        const string ArrowRainPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/ArrowRainArea.prefab";

        internal const string SlashAnimatorObjectName = "Slash Animator Visual";
        internal const string SwordRushFrame0StateName = "SwordRushFrame0";
        internal const string SwordRushFrame1StateName = "SwordRushFrame1";
        internal const string FrostVisualObjectName = "Frost Area Visual";
        internal const string ArrowRainAnimatorObjectName = "ArrowRainArea";
        internal const string ArrowRainVisualPrefix = "Arrow Rain Animation ";
        internal const int ArrowRainVisualCount = 7;

        static readonly Vector2[] ArrowRainLandingPositions =
        {
            new Vector2(-0.68f, 0.46f),
            new Vector2(0.19f, -0.76f),
            new Vector2(0.66f, 0.42f),
            new Vector2(-0.04f, 0.03f),
            new Vector2(-0.78f, -0.16f),
            new Vector2(0.59f, -0.54f),
            new Vector2(-0.16f, 0.78f)
        };

        [MenuItem("Area Survivors/Migrations/Apply Combat Animator Migration")]
        public static void ApplyMenu()
        {
            EnsureAssetFolder(AnimationRoot);
            MigrateSlash(SlashPrefabPath, "Slash", new[] { "Slash_0.png", "Slash_1.png", "Slash_2.png" });
            MigrateSlash(SwordRushPrefabPath, "SwordRush", new[] { "SwordRushSlashEffect.png", "SwordRushSlashEffectAlt.png" });
            MigrateFrost(FrostPrefabPath, "Frost");
            MigrateFrost(FrostStormPrefabPath, "FrostStorm");
            MigrateArrowRain();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Combat Animator migration completed. Run Area Survivors/Validate/Combat Animator Migration after Phase2 runtime cleanup.");
        }

        [MenuItem("Area Survivors/Migrations/Spread Arrow Rain Animator Visuals")]
        public static void SpreadArrowRainAnimatorVisualsMenu()
        {
            var root = LoadPrefab(ArrowRainPrefabPath);
            try
            {
                string clipPath = AnimationRoot + "/ArrowRain/ArrowRainFall.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    throw new InvalidOperationException("Arrow Rain AnimationClip is missing: " + clipPath);
                }

                var visuals = new List<Transform>(ArrowRainVisualCount);
                for (int i = 0; i < ArrowRainVisualCount; i++)
                {
                    string objectName = ArrowRainVisualPrefix + (i + 1).ToString("00");
                    var child = root.transform.Find(objectName);
                    if (child == null)
                    {
                        throw new InvalidOperationException("Arrow Rain visual child is missing: " + objectName);
                    }
                    visuals.Add(child);
                }

                ApplyArrowRainLandingLayout(root.transform, clip, visuals);
                SaveClip(clip, clipPath);
                SavePrefabOrThrow(root, ArrowRainPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Arrow Rain Animator uses one Clip with seven evenly distributed irregular landing points.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        internal static Vector2 ArrowRainLandingPosition(int index)
        {
            if (index < 0 || index >= ArrowRainLandingPositions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    "Arrow Rain landing position index is outside the seven visual slots.");
            }
            return ArrowRainLandingPositions[index];
        }

        static void MigrateSlash(string prefabPath, string animationName, string[] fallbackFrameNames)
        {
            var root = LoadPrefab(prefabPath);
            try
            {
                RemoveMissingScriptsRecursively(root, prefabPath);
                var slashView = root.GetComponent<SlashView>();
                if (slashView == null) throw new InvalidOperationException("SlashView is missing: " + prefabPath);

                var serialized = new SerializedObject(slashView);
                var visualRoot = ObjectReference<Transform>(serialized, "visualRoot") ?? root.transform.Find("Visual");
                if (visualRoot == null) throw new InvalidOperationException("Slash visual root is missing: " + prefabPath);

                var frames = ReadSpriteArray(serialized.FindProperty("animationFrames"));
                if (frames.Length == 0) frames = LoadSprites(fallbackFrameNames);
                RequireFrames(frames, animationName);

                float frameSeconds = ReadFloat(serialized, "frameSeconds", 0.055f);
                Color color = ReadColor(serialized, "slashColor", Color.white);
                int sortingOrder = ReadInt(serialized, "slashSortingOrder", WeaponSortingOrders.Slash);

                string folder = AnimationRoot + "/" + animationName;
                string clipPath = folder + "/" + animationName + ".anim";
                string controllerPath = folder + "/" + animationName + ".controller";
                EnsureAssetFolder(folder);
                AnimationClip runtimeClip;
                AnimatorController controller;
                if (animationName == "SwordRush")
                {
                    if (frames.Length != 2)
                    {
                        throw new InvalidOperationException("SwordRush requires exactly two alternating frame Sprites.");
                    }
                    var frame0Clip = EnsureSwordRushFrameClip(folder, SwordRushFrame0StateName, frames[0],
                        frameSeconds, color.a);
                    var frame1Clip = EnsureSwordRushFrameClip(folder, SwordRushFrame1StateName, frames[1],
                        frameSeconds, color.a);
                    controller = EnsureSwordRushAnimatorController(controllerPath, frame0Clip, frame1Clip);
                    runtimeClip = frame0Clip;
                    if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
                    {
                        AssetDatabase.DeleteAsset(clipPath);
                    }
                }
                else
                {
                    runtimeClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (runtimeClip == null)
                    {
                        runtimeClip = CreateSlashClip(clipPath, animationName, frames, frameSeconds, color.a);
                    }
                    controller = EnsureAnimatorController(controllerPath, animationName, runtimeClip);
                }

                var animatorTransform = FindDirectChild(visualRoot, SlashAnimatorObjectName);
                if (animatorTransform == null)
                {
                    var animatorObject = new GameObject(SlashAnimatorObjectName);
                    animatorTransform = animatorObject.transform;
                    animatorTransform.SetParent(visualRoot, false);
                }
                ZeroPitch(animatorTransform);

                var spriteRenderer = animatorTransform.GetComponent<SpriteRenderer>();
                bool createdRenderer = spriteRenderer == null;
                if (createdRenderer) spriteRenderer = animatorTransform.gameObject.AddComponent<SpriteRenderer>();
                if (createdRenderer)
                {
                    spriteRenderer.sprite = frames[0];
                    spriteRenderer.color = color;
                    spriteRenderer.sortingOrder = sortingOrder;
                }
                else if (spriteRenderer.sprite == null)
                {
                    spriteRenderer.sprite = frames[0];
                }
                var animator = ConfigureAnimator(animatorTransform.gameObject, controller);

                ClearArray(serialized, "animationFrames");
                ClearObjectReference(serialized, "visual");
                ClearObjectReference(serialized, "billboard");
                SetObjectReferenceIfPresent(serialized, "animator", animator);
                SetObjectReferenceIfPresent(serialized, "animationClip", runtimeClip);
                SetObjectReferenceIfPresent(serialized, "spriteRenderer", spriteRenderer);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                RemoveLegacyVisualComponents(visualRoot.gameObject, true);

                EditorUtility.SetDirty(spriteRenderer);
                SavePrefabOrThrow(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void MigrateFrost(string prefabPath, string animationName)
        {
            var root = LoadPrefab(prefabPath);
            try
            {
                RemoveMissingScriptsRecursively(root, prefabPath);
                var legacyAnimator = FindBehaviour(root, "PaperMeshSpriteAnimator");
                Transform visualTransform = legacyAnimator != null ? legacyAnimator.transform : FindDescendant(root.transform, FrostVisualObjectName);
                if (visualTransform == null) throw new InvalidOperationException("Frost visual is missing: " + prefabPath);

                Sprite[] frames = Array.Empty<Sprite>();
                float framesPerSecond = 3f;
                if (legacyAnimator != null)
                {
                    var serialized = new SerializedObject(legacyAnimator);
                    frames = ReadSpriteArray(serialized.FindProperty("frames"));
                    framesPerSecond = ReadFloat(serialized, "framesPerSecond", framesPerSecond);
                }
                if (frames.Length == 0) frames = LoadSprites(new[] { "FrostAreaTexture.png", "FrostAreaTextureAlt.png" });
                RequireFrames(frames, "Frost");

                var appearance = ReadLegacyAppearance(visualTransform.gameObject, frames[0], Color.white, WeaponSortingOrders.AreaEffect);
                RemoveLegacyVisualComponents(visualTransform.gameObject, true);
                string stateName = animationName + "Loop";
                string folder = AnimationRoot + "/" + animationName;
                string clipPath = folder + "/" + stateName + ".anim";
                string controllerPath = folder + "/" + animationName + ".controller";
                EnsureAssetFolder(folder);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null) clip = CreateLoopingSpriteClip(clipPath, stateName, frames, framesPerSecond);
                var controller = EnsureAnimatorController(controllerPath, stateName, clip);

                ZeroPitch(visualTransform);
                var spriteRenderer = visualTransform.GetComponent<SpriteRenderer>();
                bool createdRenderer = spriteRenderer == null;
                if (createdRenderer) spriteRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
                if (createdRenderer)
                {
                    spriteRenderer.color = appearance.color;
                    spriteRenderer.sortingOrder = appearance.sortingOrder;
                }
                spriteRenderer.sprite = frames[0];
                ConfigureAnimator(visualTransform.gameObject, controller);

                EditorUtility.SetDirty(spriteRenderer);
                SavePrefabOrThrow(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void MigrateArrowRain()
        {
            var root = LoadPrefab(ArrowRainPrefabPath);
            try
            {
                RemoveMissingScriptsRecursively(root, ArrowRainPrefabPath);
                var areaVisual = root.GetComponent<ArrowRainAreaVisual>();
                if (areaVisual == null) throw new InvalidOperationException("ArrowRainAreaVisual is missing: " + ArrowRainPrefabPath);
                var serialized = new SerializedObject(areaVisual);

                var frames = ReadSpriteArray(serialized.FindProperty("frames"));
                if (frames.Length == 0)
                {
                    var names = new string[8];
                    for (int i = 0; i < names.Length; i++) names[i] = "ArrowRainFrame_" + i + ".png";
                    frames = LoadSprites(names);
                }
                RequireFrames(frames, "ArrowRain");

                float framesPerSecond = ReadFloat(serialized, "framesPerSecond", 8f);
                float travel = ReadFloat(serialized, "arrowFallTravel", 1.8f);
                float cyclesPerSecond = ReadFloat(serialized, "arrowFallCyclesPerSecond", 2f);
                float desync = ReadFloat(serialized, "arrowFallDesync", 0.85f);
                float heightJitter = ReadFloat(serialized, "arrowHeightJitter", 0.45f);
                var visuals = ReadVisualTransforms(serialized, root.transform);
                if (visuals.Count != 7)
                {
                    throw new InvalidOperationException("Arrow Rain requires exactly seven serialized visual children. found=" + visuals.Count);
                }

                string folder = AnimationRoot + "/ArrowRain";
                string clipPath = folder + "/ArrowRainFall.anim";
                string controllerPath = folder + "/ArrowRain.controller";
                EnsureAssetFolder(folder);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = CreateArrowRainClip(clipPath, frames, framesPerSecond, visuals, root.transform,
                        travel, cyclesPerSecond, desync, heightJitter);
                }
                ApplyArrowRainLandingLayout(root.transform, clip, visuals);
                var controller = EnsureAnimatorController(controllerPath, "ArrowRainFall", clip);

                for (int i = 0; i < visuals.Count; i++)
                {
                    var target = visuals[i];
                    var appearance = ReadLegacyAppearance(target.gameObject, frames[i % frames.Length], Color.white,
                        WeaponSortingOrders.Projectile);
                    RemoveLegacyVisualComponents(target.gameObject, true);
                    ZeroPitch(target);
                    var spriteRenderer = target.GetComponent<SpriteRenderer>();
                    bool createdRenderer = spriteRenderer == null;
                    if (createdRenderer) spriteRenderer = target.gameObject.AddComponent<SpriteRenderer>();
                    if (createdRenderer)
                    {
                        spriteRenderer.sprite = appearance.sprite;
                        spriteRenderer.color = appearance.color;
                        spriteRenderer.sortingOrder = appearance.sortingOrder;
                    }
                    else if (spriteRenderer.sprite == null)
                    {
                        spriteRenderer.sprite = appearance.sprite;
                    }
                    EditorUtility.SetDirty(spriteRenderer);
                }

                ZeroPitch(root.transform);
                ConfigureAnimator(root, controller);
                ClearArray(serialized, "frames");
                ClearArray(serialized, "arrowVisuals");
                ClearObjectReference(serialized, "arrowVisual");
                SetTransformArrayIfPresent(serialized, "animatorVisuals", visuals);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                SavePrefabOrThrow(root, ArrowRainPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static AnimationClip CreateSlashClip(string path, string name, Sprite[] frames, float frameSeconds, float startAlpha)
        {
            var clip = NewClip(path, name);
            frameSeconds = Mathf.Max(0.01f, frameSeconds);
            float duration = frameSeconds * frames.Length;
            var spriteKeys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
            {
                spriteKeys[i] = new ObjectReferenceKeyframe { time = frameSeconds * i, value = frames[i] };
            }
            spriteKeys[frames.Length] = new ObjectReferenceKeyframe { time = duration, value = frames[frames.Length - 1] };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), spriteKeys);

            var alphaKeys = new Keyframe[frames.Length + 1];
            var positionKeys = new Keyframe[frames.Length + 1];
            for (int i = 0; i <= frames.Length; i++)
            {
                float t = frameSeconds * i;
                float normalized = i / (float)Mathf.Max(1, frames.Length);
                alphaKeys[i] = new Keyframe(t, Mathf.Lerp(startAlpha, 0.32f, normalized));
                positionKeys[i] = new Keyframe(t, 0.035f * i);
            }
            SetLinearCurve(clip, string.Empty, typeof(SpriteRenderer), "m_Color.a", alphaKeys);
            SetLinearCurve(clip, string.Empty, typeof(Transform), "m_LocalPosition.x", positionKeys);
            SetLooping(clip, false);
            SaveClip(clip, path);
            return clip;
        }

        static AnimationClip EnsureSwordRushFrameClip(string folder, string stateName, Sprite frame,
            float frameSeconds, float alpha)
        {
            string path = folder + "/" + stateName + ".anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing;

            var clip = NewClip(path, stateName);
            float duration = Mathf.Max(0.01f, frameSeconds);
            var spriteKeys = new[]
            {
                new ObjectReferenceKeyframe { time = 0f, value = frame },
                new ObjectReferenceKeyframe { time = duration, value = frame }
            };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), spriteKeys);
            SetLinearCurve(clip, string.Empty, typeof(SpriteRenderer), "m_Color.a",
                new[] { new Keyframe(0f, alpha), new Keyframe(duration, alpha) });
            SetLinearCurve(clip, string.Empty, typeof(Transform), "m_LocalPosition.x",
                new[] { new Keyframe(0f, 0f), new Keyframe(duration, 0.035f) });
            SetLooping(clip, false);
            SaveClip(clip, path);
            return clip;
        }

        static AnimationClip CreateLoopingSpriteClip(string path, string name, Sprite[] frames, float framesPerSecond)
        {
            var clip = NewClip(path, name);
            float frameSeconds = 1f / Mathf.Max(0.1f, framesPerSecond);
            var keys = new ObjectReferenceKeyframe[frames.Length + 1];
            for (int i = 0; i < frames.Length; i++)
            {
                keys[i] = new ObjectReferenceKeyframe { time = frameSeconds * i, value = frames[i] };
            }
            keys[frames.Length] = new ObjectReferenceKeyframe
            {
                time = frameSeconds * frames.Length,
                value = frames[frames.Length - 1]
            };
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(SpriteRenderer), "m_Sprite"), keys);
            SetLooping(clip, true);
            SaveClip(clip, path);
            return clip;
        }

        static AnimationClip CreateArrowRainClip(string path, Sprite[] frames, float framesPerSecond,
            IReadOnlyList<Transform> visuals, Transform animatorRoot, float travel, float cyclesPerSecond,
            float desync, float heightJitter)
        {
            var clip = NewClip(path, "ArrowRainFall");
            float duration = Mathf.Max(1f, frames.Length / Mathf.Max(0.1f, framesPerSecond));
            int sampleCount = 64;

            for (int visualIndex = 0; visualIndex < visuals.Count; visualIndex++)
            {
                var visual = visuals[visualIndex];
                string hierarchyPath = AnimationUtility.CalculateTransformPath(visual, animatorRoot);
                var spriteKeys = new ObjectReferenceKeyframe[frames.Length + 1];
                for (int frameIndex = 0; frameIndex < frames.Length; frameIndex++)
                {
                    spriteKeys[frameIndex] = new ObjectReferenceKeyframe
                    {
                        time = frameIndex / Mathf.Max(0.1f, framesPerSecond),
                        value = frames[(frameIndex + visualIndex) % frames.Length]
                    };
                }
                spriteKeys[frames.Length] = new ObjectReferenceKeyframe
                {
                    time = duration,
                    value = frames[visualIndex % frames.Length]
                };
                AnimationUtility.SetObjectReferenceCurve(clip,
                    EditorCurveBinding.PPtrCurve(hierarchyPath, typeof(SpriteRenderer), "m_Sprite"), spriteKeys);

                SetConstantCurve(clip, hierarchyPath, typeof(Transform), "m_LocalPosition.x",
                    visual.localPosition.x, duration);

                float orderedOffset = visualIndex / (float)visuals.Count;
                float randomOffset = Hash01(visualIndex, 17);
                float phaseOffset = Mathf.Lerp(orderedOffset, randomOffset, Mathf.Clamp01(desync));
                float travelMultiplier = Mathf.Lerp(1f,
                    Mathf.Lerp(0.68f, 1.24f, Hash01(visualIndex, 43)), Mathf.Clamp01(heightJitter));
                var yKeys = new Keyframe[sampleCount + 1];
                for (int sample = 0; sample <= sampleCount; sample++)
                {
                    float time = duration * sample / sampleCount;
                    float phase = Mathf.Repeat(time * Mathf.Max(0.1f, cyclesPerSecond) + phaseOffset, 1f);
                    float y = visual.localPosition.y + Mathf.Lerp(Mathf.Max(0f, travel) * travelMultiplier, 0f, phase);
                    yKeys[sample] = new Keyframe(time, y);
                }
                SetLinearCurve(clip, hierarchyPath, typeof(Transform), "m_LocalPosition.y", yKeys);
            }

            SetLooping(clip, true);
            SaveClip(clip, path);
            return clip;
        }

        static void ApplyArrowRainLandingLayout(Transform animatorRoot, AnimationClip clip,
            IReadOnlyList<Transform> visuals)
        {
            if (visuals == null || visuals.Count != ArrowRainVisualCount)
            {
                throw new InvalidOperationException("Arrow Rain landing layout requires exactly seven visuals.");
            }

            float duration = Mathf.Max(0.01f, clip.length);
            for (int i = 0; i < visuals.Count; i++)
            {
                var visual = visuals[i];
                string expectedName = ArrowRainVisualPrefix + (i + 1).ToString("00");
                if (visual == null || visual.name != expectedName)
                {
                    throw new InvalidOperationException("Arrow Rain visual order is invalid at index " + i +
                        ". expected=" + expectedName + " actual=" + (visual != null ? visual.name : "null"));
                }

                Vector2 landingPosition = ArrowRainLandingPosition(i);
                Vector3 previousPosition = visual.localPosition;
                string hierarchyPath = AnimationUtility.CalculateTransformPath(visual, animatorRoot);
                ShiftCurveValue(clip, hierarchyPath, typeof(Transform), "m_LocalPosition.y",
                    landingPosition.y - previousPosition.y);
                SetConstantCurve(clip, hierarchyPath, typeof(Transform), "m_LocalPosition.x",
                    landingPosition.x, duration);

                visual.localPosition = new Vector3(landingPosition.x, landingPosition.y, previousPosition.z);
                EditorUtility.SetDirty(visual);
            }
        }

        static void ShiftCurveValue(AnimationClip clip, string path, Type type, string propertyName, float delta)
        {
            var binding = EditorCurveBinding.FloatCurve(path, type, propertyName);
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            if (curve == null)
            {
                throw new InvalidOperationException("Required AnimationClip curve is missing: " + path +
                    " / " + propertyName);
            }
            if (Mathf.Approximately(delta, 0f)) return;

            for (int i = 0; i < curve.length; i++)
            {
                Keyframe key = curve[i];
                key.value += delta;
                curve.MoveKey(i, key);
            }
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }

        static AnimationClip NewClip(string path, string name)
        {
            var clip = new AnimationClip { name = name, frameRate = 100f };
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        static void SaveClip(AnimationClip clip, string path)
        {
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        static void SetLinearCurve(AnimationClip clip, string path, Type type, string propertyName, Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, propertyName), curve);
        }

        static void SetConstantCurve(AnimationClip clip, string path, Type type, string propertyName, float value, float duration)
        {
            SetLinearCurve(clip, path, type, propertyName, new[]
            {
                new Keyframe(0f, value),
                new Keyframe(Mathf.Max(0.01f, duration), value)
            });
        }

        static void SetLooping(AnimationClip clip, bool loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
        }

        static AnimatorController EnsureAnimatorController(string path, string stateName, AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null) return controller;
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.AddState(stateName);
            state.motion = clip;
            state.writeDefaultValues = true;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorController EnsureSwordRushAnimatorController(string path, AnimationClip frame0Clip,
            AnimationClip frame1Clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var stateMachine = controller.layers[0].stateMachine;
            AnimatorState frame0State = null;
            AnimatorState frame1State = null;
            var states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                var state = states[i].state;
                if (state.name == SwordRushFrame0StateName) frame0State = state;
                else if (state.name == SwordRushFrame1StateName) frame1State = state;
                else stateMachine.RemoveState(state);
            }
            if (frame0State == null) frame0State = stateMachine.AddState(SwordRushFrame0StateName);
            if (frame1State == null) frame1State = stateMachine.AddState(SwordRushFrame1StateName);
            frame0State.motion = frame0Clip;
            frame1State.motion = frame1Clip;
            frame0State.writeDefaultValues = true;
            frame1State.writeDefaultValues = true;
            stateMachine.defaultState = frame0State;
            EditorUtility.SetDirty(frame0State);
            EditorUtility.SetDirty(frame1State);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        static Animator ConfigureAnimator(GameObject target, RuntimeAnimatorController controller)
        {
            var animator = target.GetComponent<Animator>();
            if (animator == null) animator = target.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
            EditorUtility.SetDirty(animator);
            return animator;
        }

        static List<Transform> ReadVisualTransforms(SerializedObject serialized, Transform root)
        {
            var results = new List<Transform>();
            var array = serialized.FindProperty("arrowVisuals");
            if (array != null && array.isArray)
            {
                for (int i = 0; i < array.arraySize; i++)
                {
                    var visual = array.GetArrayElementAtIndex(i).objectReferenceValue as Component;
                    if (visual != null && !results.Contains(visual.transform)) results.Add(visual.transform);
                }
            }
            if (results.Count > 0) return results;

            var animatorArray = serialized.FindProperty("animatorVisuals");
            if (animatorArray != null && animatorArray.isArray)
            {
                for (int i = 0; i < animatorArray.arraySize; i++)
                {
                    var visual = animatorArray.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (visual != null && !results.Contains(visual)) results.Add(visual);
                }
            }
            if (results.Count > 0) return results;

            for (int i = 1; i <= 7; i++)
            {
                var child = root.Find(ArrowRainVisualPrefix + i.ToString("00"));
                if (child != null) results.Add(child);
            }
            return results;
        }

        static LegacyAppearance ReadLegacyAppearance(GameObject target, Sprite fallbackSprite, Color fallbackColor, int fallbackOrder)
        {
            var legacy = FindBehaviour(target, "PaperMeshVisual", false);
            if (legacy == null) return new LegacyAppearance(fallbackSprite, fallbackColor, fallbackOrder);
            var serialized = new SerializedObject(legacy);
            var sprite = serialized.FindProperty("sourceSprite")?.objectReferenceValue as Sprite;
            var colorProperty = serialized.FindProperty("tint");
            var orderProperty = serialized.FindProperty("sortingOrder");
            return new LegacyAppearance(
                sprite != null ? sprite : fallbackSprite,
                colorProperty != null ? colorProperty.colorValue : fallbackColor,
                orderProperty != null ? orderProperty.intValue : fallbackOrder);
        }

        static void RemoveLegacyVisualComponents(GameObject target, bool removeMeshComponents)
        {
            RemoveBehaviour(target, "PaperMeshSpriteAnimator");
            RemoveBehaviour(target, "PaperBillboard");
            RemoveBehaviour(target, "PaperMeshVisual");
            if (!removeMeshComponents) return;
            var meshFilter = target.GetComponent<MeshFilter>();
            var meshRenderer = target.GetComponent<MeshRenderer>();
            if (meshFilter != null) UnityEngine.Object.DestroyImmediate(meshFilter, true);
            if (meshRenderer != null) UnityEngine.Object.DestroyImmediate(meshRenderer, true);
        }

        static void RemoveMissingScriptsRecursively(GameObject root, string assetPath)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var gameObject = transforms[i].gameObject;
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
                {
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameObject);
                }
                int remaining = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                if (remaining != 0)
                {
                    throw new InvalidOperationException("Failed to remove missing scripts: " + assetPath +
                        " object=" + gameObject.name + " remaining=" + remaining);
                }
            }
        }

        static void SavePrefabOrThrow(GameObject root, string prefabPath)
        {
            RemoveMissingScriptsRecursively(root, prefabPath);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            if (saved == null)
            {
                throw new InvalidOperationException("Failed to save migrated prefab: " + prefabPath);
            }
        }

        static bool RemoveBehaviour(GameObject target, string typeName)
        {
            var behaviour = FindBehaviour(target, typeName, false);
            if (behaviour == null) return false;
            UnityEngine.Object.DestroyImmediate(behaviour, true);
            return true;
        }

        static MonoBehaviour FindBehaviour(GameObject root, string typeName, bool includeChildren = true)
        {
            var behaviours = includeChildren
                ? root.GetComponentsInChildren<MonoBehaviour>(true)
                : root.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().Name == typeName) return behaviour;
            }
            return null;
        }

        static Sprite[] ReadSpriteArray(SerializedProperty property)
        {
            if (property == null || !property.isArray || property.arraySize == 0) return Array.Empty<Sprite>();
            var frames = new List<Sprite>(property.arraySize);
            for (int i = 0; i < property.arraySize; i++)
            {
                var sprite = property.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                if (sprite != null) frames.Add(sprite);
            }
            return frames.ToArray();
        }

        static Sprite[] LoadSprites(IReadOnlyList<string> names)
        {
            var result = new Sprite[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteRoot + "/" + names[i]);
            }
            return result;
        }

        static void RequireFrames(Sprite[] frames, string label)
        {
            if (frames == null || frames.Length == 0) throw new InvalidOperationException(label + " animation frames are missing.");
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null) throw new InvalidOperationException(label + " animation frame is missing at index " + i);
            }
        }

        static T ObjectReference<T>(SerializedObject serialized, string propertyName) where T : UnityEngine.Object
        {
            return serialized.FindProperty(propertyName)?.objectReferenceValue as T;
        }

        static float ReadFloat(SerializedObject serialized, string propertyName, float fallback)
        {
            var property = serialized.FindProperty(propertyName);
            return property != null ? property.floatValue : fallback;
        }

        static int ReadInt(SerializedObject serialized, string propertyName, int fallback)
        {
            var property = serialized.FindProperty(propertyName);
            return property != null ? property.intValue : fallback;
        }

        static Color ReadColor(SerializedObject serialized, string propertyName, Color fallback)
        {
            var property = serialized.FindProperty(propertyName);
            return property != null ? property.colorValue : fallback;
        }

        static void ClearArray(SerializedObject serialized, string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null && property.isArray) property.arraySize = 0;
        }

        static void ClearObjectReference(SerializedObject serialized, string propertyName)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
            {
                property.objectReferenceValue = null;
            }
        }

        static void SetObjectReferenceIfPresent(SerializedObject serialized, string propertyName,
            UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.ObjectReference)
            {
                property.objectReferenceValue = value;
            }
        }

        static void SetTransformArrayIfPresent(SerializedObject serialized, string propertyName,
            IReadOnlyList<Transform> values)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray) return;
            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        static GameObject LoadPrefab(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                throw new InvalidOperationException("Prefab is missing: " + path);
            }
            return PrefabUtility.LoadPrefabContents(path);
        }

        static Transform FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
            }
            return null;
        }

        static Transform FindDescendant(Transform root, string name)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == name) return transforms[i];
            }
            return null;
        }

        static void ZeroPitch(Transform target)
        {
            float z = target.localEulerAngles.z;
            target.localRotation = Quaternion.Euler(0f, 0f, z);
        }

        static void EnsureAssetFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string name = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException("Invalid asset folder path: " + folderPath);
            }
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static float Hash01(int value, int salt)
        {
            uint x = unchecked((uint)value * 747796405u + (uint)salt * 2891336453u);
            x = ((x >> 16) ^ x) * 2246822519u;
            x = ((x >> 13) ^ x) * 3266489917u;
            x = (x >> 16) ^ x;
            return (x & 0x00FFFFFF) / 16777215f;
        }

        readonly struct LegacyAppearance
        {
            public readonly Sprite sprite;
            public readonly Color color;
            public readonly int sortingOrder;

            public LegacyAppearance(Sprite sprite, Color color, int sortingOrder)
            {
                this.sprite = sprite;
                this.color = color;
                this.sortingOrder = sortingOrder;
            }
        }
    }
}
