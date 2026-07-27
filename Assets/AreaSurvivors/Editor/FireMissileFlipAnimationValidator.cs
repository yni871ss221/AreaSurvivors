using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class FireMissileFlipAnimationValidator
    {
        const string PrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/FireMissile.prefab";
        const string ClipPath = "Assets/AreaSurvivors/Animations/Weapons/FireMissileFlip.anim";
        const string ControllerPath = "Assets/AreaSurvivors/Animations/Weapons/FireMissileFlip.controller";
        const string MarkerRelativePath = "Library/AreaSafeUnity/fire-missile-flip-animation-validator.ok";
        const string VisualName = "Paper Visual";
        const float Epsilon = 0.0001f;

        [MenuItem("Area Survivors/Validate/Fire Missile Flip Animation")]
        public static void ValidateMenu()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string markerPath = Path.Combine(projectRoot, MarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            int errors = ValidateAssets();
            if (errors != 0)
            {
                throw new InvalidOperationException("Fire Missile flip animation validation failed. errors=" + errors);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Fire Missile flip animation validator: passed. Local-Y side flip toggles every 0.2 seconds.");
        }

        public static int ValidateAssets()
        {
            int errors = 0;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (clip == null)
            {
                Error("Fire Missile flip AnimationClip is missing.", ref errors);
            }
            else
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                var scaleXCurve = AnimationUtility.GetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.x"));
                var scaleYCurve = AnimationUtility.GetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.y"));
                var scaleZCurve = AnimationUtility.GetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.z"));
                var oldForwardAxisFlipCurve = AnimationUtility.GetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(PaperMeshVisual), "flipHorizontal"));
                if (!settings.loopTime || Mathf.Abs(clip.length - 0.4f) > Epsilon ||
                    bindings.Length != 3 ||
                    !IsConstantAxisCurve(scaleXCurve) ||
                    !IsConstantAxisCurve(scaleZCurve) ||
                    scaleYCurve == null || scaleYCurve.length != 3 ||
                    !KeyMatches(scaleYCurve, 0, 0f, 1f) ||
                    !KeyMatches(scaleYCurve, 1, 0.2f, -1f) ||
                    !KeyMatches(scaleYCurve, 2, 0.4f, 1f) ||
                    oldForwardAxisFlipCurve != null)
                {
                    Error("Fire Missile flip clip must preserve local X/Z and loop local Y at 1/-1/1 on 0.0/0.2/0.4 seconds.", ref errors);
                }

                if (!HasConstantTangents(scaleXCurve) ||
                    !HasConstantTangents(scaleYCurve) ||
                    !HasConstantTangents(scaleZCurve))
                {
                    Error("Fire Missile flip keys must use constant tangents.", ref errors);
                }
            }

            if (controller == null)
            {
                Error("Fire Missile flip Animator Controller is missing.", ref errors);
            }
            else
            {
                var stateMachine = controller.layers[0].stateMachine;
                var states = stateMachine.states;
                if (states.Length != 1 || stateMachine.defaultState == null ||
                    stateMachine.defaultState.motion != clip)
                {
                    Error("Fire Missile flip controller must have one default state using the flip clip.", ref errors);
                }
            }

            var visual = prefab != null ? prefab.transform.Find(VisualName) : null;
            var animator = visual != null ? visual.GetComponent<Animator>() : null;
            if (prefab == null || visual == null || visual.GetComponent<PaperMeshVisual>() == null ||
                prefab.GetComponent<Animator>() != null || animator == null ||
                animator.runtimeAnimatorController != controller || animator.applyRootMotion ||
                animator.cullingMode != AnimatorCullingMode.AlwaysAnimate ||
                !Approximately(visual.localScale, Vector3.one) ||
                Mathf.Abs(Mathf.DeltaAngle(visual.localEulerAngles.x, 0f)) > Epsilon ||
                Mathf.Abs(Mathf.DeltaAngle(visual.localEulerAngles.y, 0f)) > Epsilon)
            {
                Error("Fire Missile Animator must be saved only on the unchanged Paper Visual child.", ref errors);
            }

            return errors;
        }

        static bool KeyMatches(AnimationCurve curve, int index, float time, float value)
        {
            return index >= 0 && index < curve.length &&
                Mathf.Abs(curve[index].time - time) <= Epsilon &&
                Mathf.Abs(curve[index].value - value) <= Epsilon;
        }

        static bool IsConstantAxisCurve(AnimationCurve curve)
        {
            return curve != null && curve.length == 2 &&
                KeyMatches(curve, 0, 0f, 1f) &&
                KeyMatches(curve, 1, 0.4f, 1f);
        }

        static bool HasConstantTangents(AnimationCurve curve)
        {
            if (curve == null) return false;
            for (int i = 0; i < curve.length; i++)
            {
                if (AnimationUtility.GetKeyLeftTangentMode(curve, i) != AnimationUtility.TangentMode.Constant ||
                    AnimationUtility.GetKeyRightTangentMode(curve, i) != AnimationUtility.TangentMode.Constant)
                {
                    return false;
                }
            }
            return true;
        }

        static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= Epsilon * Epsilon;
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError(message);
        }
    }
}
