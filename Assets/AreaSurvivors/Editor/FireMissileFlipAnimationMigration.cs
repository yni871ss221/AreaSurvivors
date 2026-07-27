using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class FireMissileFlipAnimationMigration
    {
        const string PrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/FireMissile.prefab";
        const string ClipPath = "Assets/AreaSurvivors/Animations/Weapons/FireMissileFlip.anim";
        const string ControllerPath = "Assets/AreaSurvivors/Animations/Weapons/FireMissileFlip.controller";
        const string MarkerRelativePath = "Library/AreaSafeUnity/fire-missile-flip-animation-migration.ok";
        const string VisualName = "Paper Visual";
        const string StateName = "FireMissileFlip";
        const float FlipIntervalSeconds = 0.2f;
        const float LoopDurationSeconds = FlipIntervalSeconds * 2f;

        [MenuItem("Area Survivors/Migrations/Apply Fire Missile Flip Animation")]
        public static void Apply()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string markerPath = Path.Combine(projectRoot, MarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = StateName };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            ConfigureClip(clip);

            var controller = ConfigureController(clip);
            ConfigurePrefab(controller);
            AssetDatabase.SaveAssets();

            var savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var savedVisual = savedPrefab != null ? savedPrefab.transform.Find(VisualName) : null;
            var savedAnimator = savedVisual != null ? savedVisual.GetComponent<Animator>() : null;
            if (savedAnimator == null || savedAnimator.runtimeAnimatorController != controller)
            {
                throw new InvalidOperationException("Fire Missile flip Animator was not saved to the visual child.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Fire Missile flip animation migration: applied 0.2-second local-Y side flip loop.");
        }

        static void ConfigureClip(AnimationClip clip)
        {
            clip.frameRate = 100f;
            clip.ClearCurves();

            var scaleXCurve = CreateConstantCurve(
                new Keyframe(0f, 1f),
                new Keyframe(LoopDurationSeconds, 1f));
            var scaleYCurve = CreateConstantCurve(
                new Keyframe(0f, 1f),
                new Keyframe(FlipIntervalSeconds, -1f),
                new Keyframe(LoopDurationSeconds, 1f));
            var scaleZCurve = CreateConstantCurve(
                new Keyframe(0f, 1f),
                new Keyframe(LoopDurationSeconds, 1f));

            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.x"),
                scaleXCurve);
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.y"),
                scaleYCurve);
            AnimationUtility.SetEditorCurve(
                clip,
                EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), "m_LocalScale.z"),
                scaleZCurve);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            AssetDatabase.ImportAsset(ClipPath, ImportAssetOptions.ForceUpdate);
        }

        static AnimationCurve CreateConstantCurve(params Keyframe[] keys)
        {
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }
            return curve;
        }

        static AnimatorController ConfigureController(AnimationClip clip)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            var stateMachine = controller.layers[0].stateMachine;
            var states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                stateMachine.RemoveState(states[i].state);
            }

            var state = stateMachine.AddState(StateName);
            state.motion = clip;
            state.writeDefaultValues = true;
            stateMachine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);
            return controller;
        }

        static void ConfigurePrefab(RuntimeAnimatorController controller)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var visual = root.transform.Find(VisualName);
                if (visual == null || visual.GetComponent<PaperMeshVisual>() == null)
                {
                    throw new InvalidOperationException("Fire Missile Paper Visual or PaperMeshVisual is missing.");
                }

                var animator = visual.GetComponent<Animator>();
                if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.enabled = true;
                EditorUtility.SetDirty(animator);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null)
                {
                    throw new InvalidOperationException("Failed to save Fire Missile prefab with flip Animator.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
