using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class CombatAnimatorMigrationValidator
    {
        const string AnimationRoot = "Assets/AreaSurvivors/Animations/Weapons";
        const string SpriteRoot = "Assets/AreaSurvivors/Sprites/Generated/Weapons";
        const string SlashPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/Slash.prefab";
        const string SwordRushPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/SwordRushSlash.prefab";
        const string FrostPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/FrostArea.prefab";
        const string FrostStormPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/FrostStormSpike.prefab";
        const string ArrowRainPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/ArrowRainArea.prefab";
        const string SlashSourcePath = "Assets/AreaSurvivors/Scripts/Game/Weapons/SlashView.cs";
        const string ArrowRainSourcePath = "Assets/AreaSurvivors/Scripts/Game/Weapons/ArrowRainAreaVisual.cs";
        const string LegacySpriteAnimatorPath = "Assets/AreaSurvivors/Scripts/Game/Visuals/PaperMeshSpriteAnimator.cs";
        const string CompletionMarkerRelativePath = "Library/AreaSafeUnity/combat-animator-migration-validator.ok";

        [MenuItem("Area Survivors/Validate/Combat Animator Migration")]
        public static void ValidateMenu()
        {
            string markerPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                CompletionMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            int errors = 0;
            var slashFrames = LoadSprites(new[] { "Slash_0.png", "Slash_1.png", "Slash_2.png" }, ref errors);
            var swordRushFrames = LoadSprites(new[] { "SwordRushSlashEffect.png", "SwordRushSlashEffectAlt.png" }, ref errors);
            var frostFrames = LoadSprites(new[] { "FrostAreaTexture.png", "FrostAreaTextureAlt.png" }, ref errors);
            var arrowRainNames = new string[8];
            for (int i = 0; i < arrowRainNames.Length; i++) arrowRainNames[i] = "ArrowRainFrame_" + i + ".png";
            var arrowRainFrames = LoadSprites(arrowRainNames, ref errors);

            ValidateSlash(SlashPrefabPath, "Slash", slashFrames, ref errors);
            ValidateSlash(SwordRushPrefabPath, "SwordRush", swordRushFrames, ref errors);
            ValidateFrost(FrostPrefabPath, "Frost", frostFrames, ref errors);
            ValidateFrost(FrostStormPrefabPath, "FrostStorm", frostFrames, ref errors);
            ValidateArrowRain(arrowRainFrames, ref errors);
            ValidateRuntimeCleanup(ref errors);

            if (errors != 0)
            {
                throw new InvalidOperationException("Combat Animator migration validation failed. errors=" + errors);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Combat Animator migration validator: passed. Slash/SwordRush/Frost/FrostStorm/ArrowRain are prefab-owned Animator visuals and legacy runtime frame paths are absent.");
        }

        static void ValidateSlash(string prefabPath, string animationName, Sprite[] expectedFrames, ref int errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!ValidatePrefabExists(prefab, prefabPath, ref errors)) return;

            string folder = AnimationRoot + "/" + animationName;
            AnimationClip runtimeClip;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + "/" + animationName + ".controller");
            var visualRoot = prefab.transform.Find("Visual");
            var animatorTransform = visualRoot != null
                ? visualRoot.Find(CombatAnimatorMigration.SlashAnimatorObjectName)
                : null;
            ValidateAnimatorVisual(animatorTransform, controller, expectedFrames.Length > 0 ? expectedFrames[0] : null,
                prefabPath, ref errors);
            if (animationName == "SwordRush")
            {
                var frame0Clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/" +
                    CombatAnimatorMigration.SwordRushFrame0StateName + ".anim");
                var frame1Clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/" +
                    CombatAnimatorMigration.SwordRushFrame1StateName + ".anim");
                runtimeClip = frame0Clip;
                ValidateSwordRushClip(frame0Clip, expectedFrames.Length > 0 ? expectedFrames[0] : null,
                    CombatAnimatorMigration.SwordRushFrame0StateName, ref errors);
                ValidateSwordRushClip(frame1Clip, expectedFrames.Length > 1 ? expectedFrames[1] : null,
                    CombatAnimatorMigration.SwordRushFrame1StateName, ref errors);
                ValidateSwordRushController(controller, frame0Clip, frame1Clip, ref errors);
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/SwordRush.anim") != null)
                {
                    Error("Obsolete SwordRush.anim must not remain after two-state migration.", ref errors);
                }
            }
            else
            {
                runtimeClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/" + animationName + ".anim");
                ValidateClipController(runtimeClip, controller, false, animationName, ref errors);
                ValidateObjectReferenceFrames(runtimeClip, string.Empty, expectedFrames, animationName, ref errors);
            }

            if (visualRoot == null)
            {
                Error(animationName + " visual root is missing.", ref errors);
                return;
            }
            ValidateNoLegacyVisual(visualRoot.gameObject, animationName + " visual root", ref errors);
            var slashView = prefab.GetComponent<SlashView>();
            if (slashView != null)
            {
                var serialized = new SerializedObject(slashView);
                ValidateEmptyArray(serialized, "animationFrames", animationName, ref errors);
                ValidateNullReference(serialized, "visual", animationName, ref errors);
                ValidateNullReference(serialized, "billboard", animationName, ref errors);
                ValidateExpectedReference(serialized, "animator",
                    animatorTransform != null ? animatorTransform.GetComponent<Animator>() : null,
                    animationName + " SlashView Animator", ref errors);
                ValidateExpectedReference(serialized, "animationClip", runtimeClip,
                    animationName + " SlashView AnimationClip", ref errors);
                ValidateExpectedReference(serialized, "spriteRenderer",
                    animatorTransform != null ? animatorTransform.GetComponent<SpriteRenderer>() : null,
                    animationName + " SlashView SpriteRenderer", ref errors);
            }
        }

        static void ValidateFrost(string prefabPath, string animationName, Sprite[] expectedFrames, ref int errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!ValidatePrefabExists(prefab, prefabPath, ref errors)) return;
            string label = Path.GetFileNameWithoutExtension(prefabPath);
            string folder = AnimationRoot + "/" + animationName;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "/" + animationName + "Loop.anim");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(folder + "/" + animationName + ".controller");
            var visual = prefab.transform.Find(CombatAnimatorMigration.FrostVisualObjectName);
            ValidateAnimatorVisual(visual, controller, expectedFrames.Length > 0 ? expectedFrames[0] : null,
                label, ref errors);
            ValidateClipController(clip, controller, true, label, ref errors);
            ValidateObjectReferenceFrames(clip, string.Empty, expectedFrames, label, ref errors);
            if (visual != null) ValidateNoLegacyVisual(visual.gameObject, label, ref errors);
            if (FindBehaviour(prefab, "PaperMeshSpriteAnimator") != null)
            {
                Error(label + " still contains PaperMeshSpriteAnimator.", ref errors);
            }
        }

        static void ValidateArrowRain(Sprite[] expectedFrames, ref int errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowRainPrefabPath);
            if (!ValidatePrefabExists(prefab, ArrowRainPrefabPath, ref errors)) return;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationRoot + "/ArrowRain/ArrowRainFall.anim");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimationRoot + "/ArrowRain/ArrowRain.controller");
            var animator = prefab.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController != controller || !animator.enabled)
            {
                Error("ArrowRain root Animator or controller reference is invalid.", ref errors);
            }
            ValidateZeroPitch(prefab.transform, "ArrowRain root", ref errors);
            ValidateClipController(clip, controller, true, "ArrowRain", ref errors);

            var expectedPaths = new HashSet<string>(StringComparer.Ordinal);
            var expectedXByPath = new Dictionary<string, float>(StringComparer.Ordinal);
            var expectedYByPath = new Dictionary<string, float>(StringComparer.Ordinal);
            for (int i = 1; i <= CombatAnimatorMigration.ArrowRainVisualCount; i++)
            {
                string objectName = CombatAnimatorMigration.ArrowRainVisualPrefix + i.ToString("00");
                var child = prefab.transform.Find(objectName);
                if (child == null)
                {
                    Error("ArrowRain visual child is missing: " + objectName, ref errors);
                    continue;
                }
                string hierarchyPath = AnimationUtility.CalculateTransformPath(child, prefab.transform);
                expectedPaths.Add(hierarchyPath);
                var renderer = child.GetComponent<SpriteRenderer>();
                Sprite expectedFirst = expectedFrames.Length > 0 ? expectedFrames[(i - 1) % expectedFrames.Length] : null;
                if (renderer == null || renderer.sprite != expectedFirst)
                {
                    Error("ArrowRain SpriteRenderer or first frame is invalid: " + objectName, ref errors);
                }
                Vector2 expectedLanding = CombatAnimatorMigration.ArrowRainLandingPosition(i - 1);
                Vector2 actualLanding = new Vector2(child.localPosition.x, child.localPosition.y);
                if (Vector2.Distance(actualLanding, expectedLanding) > 0.001f)
                {
                    Error("ArrowRain visual does not use the evenly distributed landing position: " + objectName +
                        " expected=" + expectedLanding + " actual=" + actualLanding, ref errors);
                }
                expectedXByPath[hierarchyPath] = expectedLanding.x;
                expectedYByPath[hierarchyPath] = expectedLanding.y;
                ValidateZeroPitch(child, objectName, ref errors);
                ValidateNoLegacyVisual(child.gameObject, objectName, ref errors);
            }

            if (clip != null)
            {
                var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                var positionBindings = AnimationUtility.GetCurveBindings(clip);
                var spritePaths = new HashSet<string>(StringComparer.Ordinal);
                var xPaths = new HashSet<string>(StringComparer.Ordinal);
                var yPaths = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < objectBindings.Length; i++)
                {
                    var binding = objectBindings[i];
                    if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
                    {
                        spritePaths.Add(binding.path);
                        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                        if (keys == null || keys.Length < expectedFrames.Length + 1)
                        {
                            Error("ArrowRain Sprite curve has too few keys: " + binding.path, ref errors);
                        }
                    }
                }
                for (int i = 0; i < positionBindings.Length; i++)
                {
                    var binding = positionBindings[i];
                    if (binding.type == typeof(Transform) && binding.propertyName == "m_LocalPosition.x")
                    {
                        xPaths.Add(binding.path);
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        if (!expectedXByPath.TryGetValue(binding.path, out float expectedX) || curve == null ||
                            !CurvePreservesValue(curve, expectedX))
                        {
                            Error("ArrowRain clip X curve does not preserve prefab spread: " + binding.path, ref errors);
                        }
                    }
                    if (binding.type == typeof(Transform) && binding.propertyName == "m_LocalPosition.y")
                    {
                        yPaths.Add(binding.path);
                        var curve = AnimationUtility.GetEditorCurve(clip, binding);
                        if (!expectedYByPath.TryGetValue(binding.path, out float expectedY) || curve == null ||
                            !CurveMinimumMatchesValue(curve, expectedY, 0.1f))
                        {
                            Error("ArrowRain clip Y curve does not land near the prefab position: " + binding.path,
                                ref errors);
                        }
                    }
                }
                if (!spritePaths.SetEquals(expectedPaths) || !xPaths.SetEquals(expectedPaths) ||
                    !yPaths.SetEquals(expectedPaths))
                {
                    Error("ArrowRain clip must animate Sprite, fixed local X, and falling local Y for all seven prefab children.", ref errors);
                }
            }

            var areaVisual = prefab.GetComponent<ArrowRainAreaVisual>();
            if (areaVisual == null)
            {
                Error("ArrowRainAreaVisual is missing after migration.", ref errors);
            }
            else
            {
                var serialized = new SerializedObject(areaVisual);
                ValidateObjectReference(serialized, "fillMeshFilter", "ArrowRain area fill MeshFilter", ref errors);
                ValidateObjectReference(serialized, "fillRenderer", "ArrowRain area fill MeshRenderer", ref errors);
                ValidateObjectReference(serialized, "outlineRenderer", "ArrowRain area outline", ref errors);
                ValidateEmptyArray(serialized, "frames", "ArrowRain", ref errors);
                ValidateEmptyArray(serialized, "arrowVisuals", "ArrowRain", ref errors);
                ValidateNullReference(serialized, "arrowVisual", "ArrowRain", ref errors);
                ValidateTransformArray(serialized, "animatorVisuals", prefab.transform, ref errors);
            }
        }

        static void ValidateClipController(AnimationClip clip, AnimatorController controller, bool expectedLoop,
            string label, ref int errors)
        {
            if (clip == null)
            {
                Error(label + " AnimationClip is missing.", ref errors);
                return;
            }
            if (controller == null)
            {
                Error(label + " AnimatorController is missing.", ref errors);
                return;
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime != expectedLoop)
            {
                Error(label + " loop setting is invalid. expected=" + expectedLoop, ref errors);
            }
            if (AnimationUtility.GetAnimationEvents(clip).Length != 0)
            {
                Error(label + " clip must not use Animation Events for gameplay timing.", ref errors);
            }
            if (controller.layers == null || controller.layers.Length != 1 ||
                controller.layers[0].stateMachine.defaultState == null ||
                controller.layers[0].stateMachine.defaultState.motion != clip)
            {
                Error(label + " controller default state does not reference the expected clip.", ref errors);
            }
        }

        static void ValidateObjectReferenceFrames(AnimationClip clip, string expectedPath, Sprite[] expectedFrames,
            string label, ref int errors)
        {
            if (clip == null) return;
            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length != 1 || bindings[0].path != expectedPath ||
                bindings[0].type != typeof(SpriteRenderer) || bindings[0].propertyName != "m_Sprite")
            {
                Error(label + " clip SpriteRenderer.m_Sprite binding is invalid.", ref errors);
                return;
            }
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            if (keys == null || keys.Length < expectedFrames.Length + 1)
            {
                Error(label + " clip has too few Sprite keys.", ref errors);
                return;
            }
            for (int i = 0; i < expectedFrames.Length; i++)
            {
                if (keys[i].value != expectedFrames[i])
                {
                    Error(label + " clip Sprite key mismatch at index " + i, ref errors);
                }
            }
        }

        static void ValidateSwordRushClip(AnimationClip clip, Sprite expectedSprite, string stateName, ref int errors)
        {
            if (clip == null)
            {
                Error(stateName + " AnimationClip is missing.", ref errors);
                return;
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime) Error(stateName + " must not loop.", ref errors);
            if (AnimationUtility.GetAnimationEvents(clip).Length != 0)
            {
                Error(stateName + " must not use Animation Events.", ref errors);
            }

            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (objectBindings.Length != 1 || objectBindings[0].path != string.Empty ||
                objectBindings[0].type != typeof(SpriteRenderer) || objectBindings[0].propertyName != "m_Sprite")
            {
                Error(stateName + " must contain exactly one SpriteRenderer.m_Sprite curve.", ref errors);
            }
            else
            {
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, objectBindings[0]);
                if (keys == null || keys.Length < 2)
                {
                    Error(stateName + " Sprite curve must hold one image for the full Slash duration.", ref errors);
                }
                else
                {
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (keys[i].value != expectedSprite)
                        {
                            Error(stateName + " must not switch to another Sprite inside one Slash.", ref errors);
                            break;
                        }
                    }
                }
            }

            bool hasAlpha = false;
            bool hasLocalX = false;
            var floatBindings = AnimationUtility.GetCurveBindings(clip);
            for (int i = 0; i < floatBindings.Length; i++)
            {
                var binding = floatBindings[i];
                if (binding.path != string.Empty) continue;
                if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Color.a") hasAlpha = true;
                if (binding.type == typeof(Transform) && binding.propertyName == "m_LocalPosition.x") hasLocalX = true;
            }
            if (!hasAlpha || !hasLocalX)
            {
                Error(stateName + " must retain the Slash alpha and local movement curves.", ref errors);
            }
        }

        static void ValidateSwordRushController(AnimatorController controller, AnimationClip frame0Clip,
            AnimationClip frame1Clip, ref int errors)
        {
            if (controller == null)
            {
                Error("SwordRush AnimatorController is missing.", ref errors);
                return;
            }
            var states = controller.layers[0].stateMachine.states;
            if (states.Length != 2)
            {
                Error("SwordRush AnimatorController must contain exactly two states.", ref errors);
                return;
            }
            AnimatorState frame0State = null;
            AnimatorState frame1State = null;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == CombatAnimatorMigration.SwordRushFrame0StateName)
                    frame0State = states[i].state;
                else if (states[i].state.name == CombatAnimatorMigration.SwordRushFrame1StateName)
                    frame1State = states[i].state;
            }
            if (frame0State == null || frame0State.motion != frame0Clip ||
                frame1State == null || frame1State.motion != frame1Clip)
            {
                Error("SwordRush Frame0/Frame1 state clip references are invalid.", ref errors);
            }
            if (controller.layers[0].stateMachine.defaultState != frame0State)
            {
                Error("SwordRushFrame0 must be the default Animator state.", ref errors);
            }
        }

        static void ValidateAnimatorVisual(Transform target, RuntimeAnimatorController controller, Sprite firstFrame,
            string label, ref int errors)
        {
            if (target == null)
            {
                Error(label + " Animator visual is missing.", ref errors);
                return;
            }
            var renderer = target.GetComponent<SpriteRenderer>();
            var animator = target.GetComponent<Animator>();
            if (renderer == null || renderer.sprite != firstFrame)
            {
                Error(label + " SpriteRenderer or first frame is invalid.", ref errors);
            }
            if (animator == null || !animator.enabled || animator.runtimeAnimatorController != controller ||
                animator.applyRootMotion || animator.cullingMode != AnimatorCullingMode.AlwaysAnimate)
            {
                Error(label + " Animator serialized settings are invalid.", ref errors);
            }
            ValidateZeroPitch(target, label, ref errors);
        }

        static bool ValidatePrefabExists(GameObject prefab, string path, ref int errors)
        {
            if (prefab == null)
            {
                Error("Prefab is missing: " + path, ref errors);
                return false;
            }
            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transforms[i].gameObject);
                if (missing != 0)
                {
                    Error("Prefab contains missing scripts: " + path + " object=" +
                        transforms[i].name + " count=" + missing, ref errors);
                }
            }
            return true;
        }

        static void ValidateNoLegacyVisual(GameObject root, string label, ref int errors)
        {
            string[] forbidden = { "PaperMeshSpriteAnimator", "PaperMeshVisual", "PaperBillboard" };
            for (int i = 0; i < forbidden.Length; i++)
            {
                if (FindBehaviour(root, forbidden[i]) != null)
                {
                    Error(label + " still contains legacy component " + forbidden[i] + ".", ref errors);
                }
            }
        }

        static bool CurvePreservesValue(AnimationCurve curve, float expected)
        {
            if (curve == null || curve.keys.Length < 2) return false;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!Mathf.Approximately(keys[i].value, expected)) return false;
            }
            return true;
        }

        static bool CurveMinimumMatchesValue(AnimationCurve curve, float expected, float tolerance)
        {
            if (curve == null || curve.keys.Length < 2) return false;
            float minimum = float.PositiveInfinity;
            var keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                minimum = Mathf.Min(minimum, keys[i].value);
            }
            return Mathf.Abs(minimum - expected) <= Mathf.Max(0f, tolerance);
        }

        static void ValidateRuntimeCleanup(ref int errors)
        {
            ValidateSourceDoesNotContain(SlashSourcePath,
                new[] { "EnsureDefaultFrames(", "animationFrames", "visual.sprite =" }, ref errors);
            ValidateSourceDoesNotContain(ArrowRainSourcePath,
                new[] { "framesPerSecond", "ApplyFallAnimation(", "ApplyFrame(", "arrowVisuals", "arrowVisual" }, ref errors);
            if (File.Exists(LegacySpriteAnimatorPath))
            {
                Error("Legacy PaperMeshSpriteAnimator.cs must be removed after Frost prefab migration.", ref errors);
            }
        }

        static void ValidateSourceDoesNotContain(string assetPath, IReadOnlyList<string> forbidden,
            ref int errors)
        {
            if (!File.Exists(assetPath))
            {
                Error("Runtime source is missing: " + assetPath, ref errors);
                return;
            }
            string source = File.ReadAllText(assetPath);
            for (int i = 0; i < forbidden.Count; i++)
            {
                if (source.IndexOf(forbidden[i], StringComparison.Ordinal) >= 0)
                {
                    Error(assetPath + " still contains legacy runtime animation token: " + forbidden[i], ref errors);
                }
            }
        }

        static Sprite[] LoadSprites(IReadOnlyList<string> names, ref int errors)
        {
            var result = new Sprite[names.Count];
            for (int i = 0; i < names.Count; i++)
            {
                string path = SpriteRoot + "/" + names[i];
                result[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (result[i] == null) Error("Animation Sprite is missing: " + path, ref errors);
            }
            return result;
        }

        static void ValidateZeroPitch(Transform target, string label, ref int errors)
        {
            Vector3 euler = target.localEulerAngles;
            float x = Mathf.DeltaAngle(0f, euler.x);
            float y = Mathf.DeltaAngle(0f, euler.y);
            if (!Mathf.Approximately(x, 0f) || !Mathf.Approximately(y, 0f))
            {
                Error(label + " local Rotation X/Y must be zero. actual=" + x + "/" + y, ref errors);
            }
        }

        static void ValidateEmptyArray(SerializedObject serialized, string propertyName, string label, ref int errors)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null && property.isArray && property.arraySize != 0)
            {
                Error(label + " legacy serialized array is not empty: " + propertyName, ref errors);
            }
        }

        static void ValidateNullReference(SerializedObject serialized, string propertyName, string label, ref int errors)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null && property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue != null)
            {
                Error(label + " legacy serialized reference is not null: " + propertyName, ref errors);
            }
        }

        static void ValidateObjectReference(SerializedObject serialized, string propertyName, string label, ref int errors)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference ||
                property.objectReferenceValue == null)
            {
                Error(label + " reference is missing.", ref errors);
            }
        }

        static void ValidateExpectedReference(SerializedObject serialized, string propertyName,
            UnityEngine.Object expected, string label, ref int errors)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference ||
                property.objectReferenceValue != expected || expected == null)
            {
                Error(label + " serialized reference is missing or incorrect. Required field=" + propertyName, ref errors);
            }
        }

        static void ValidateTransformArray(SerializedObject serialized, string propertyName, Transform root,
            ref int errors)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null || !property.isArray || property.arraySize != 7)
            {
                Error("ArrowRain animatorVisuals must contain exactly seven serialized Transform references.", ref errors);
                return;
            }
            for (int i = 0; i < property.arraySize; i++)
            {
                var expected = root.Find(CombatAnimatorMigration.ArrowRainVisualPrefix + (i + 1).ToString("00"));
                var actual = property.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (actual == null || actual != expected)
                {
                    Error("ArrowRain animatorVisuals reference mismatch at index " + i, ref errors);
                }
            }
        }

        static MonoBehaviour FindBehaviour(GameObject root, string typeName)
        {
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().Name == typeName) return behaviour;
            }
            return null;
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError("Combat Animator Migration Validator: " + message);
        }
    }
}
