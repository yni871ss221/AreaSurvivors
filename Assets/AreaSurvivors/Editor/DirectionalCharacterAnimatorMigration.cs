using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class DirectionalCharacterAnimatorMigration
    {
        public const string MenuPath = "Area Survivors/Migrate/Player Directional Animator";
        public const string PlayerPrefabPath = "Assets/AreaSurvivors/Prefabs/Characters/Player.prefab";
        public const string AnimationRootPath = "Assets/AreaSurvivors/Animations/Characters/Player";
        public const string BaseFolderPath = AnimationRootPath + "/Base";
        public const string BaseControllerPath = AnimationRootPath + "/PlayerDirectionalBase.controller";
        public const float FramesPerSecond = 8f;

        public static readonly string[] CharacterNames = { "Knight", "Archer", "Mage" };
        public static readonly StateSpec[] States =
        {
            new StateSpec("IdleDown", "Down", false),
            new StateSpec("IdleLeft", "Left", false),
            new StateSpec("IdleRight", "Right", false),
            new StateSpec("IdleUp", "Up", false),
            new StateSpec("WalkDown", "Down", true),
            new StateSpec("WalkLeft", "Left", true),
            new StateSpec("WalkRight", "Right", true),
            new StateSpec("WalkUp", "Up", true),
        };

        public readonly struct StateSpec
        {
            public StateSpec(string stateName, string directionName, bool moving)
            {
                StateName = stateName;
                DirectionName = directionName;
                Moving = moving;
            }

            public string StateName { get; }
            public string DirectionName { get; }
            public bool Moving { get; }
        }

        [MenuItem(MenuPath)]
        public static void Migrate()
        {
            EnsureFolder(AnimationRootPath);
            EnsureFolder(BaseFolderPath);

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var player = root.GetComponent<PlayerController>();
                if (player == null) throw new InvalidOperationException("PlayerController is missing: " + PlayerPrefabPath);

                var playerSerialized = new SerializedObject(player);
                var frameSets = ReadFrameSets(playerSerialized);
                var placeholderClips = EnsurePlaceholderClips();
                var baseController = EnsureBaseController(placeholderClips);

                var overrideControllers = new Dictionary<string, AnimatorOverrideController>(StringComparer.Ordinal);
                foreach (var characterName in CharacterNames)
                {
                    var clips = EnsureCharacterClips(characterName, frameSets[characterName]);
                    overrideControllers.Add(
                        characterName,
                        EnsureOverrideController(characterName, baseController, placeholderClips, clips));
                }

                ConfigurePlayerPrefab(root, player, overrideControllers);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Player directional Animator migration completed. Legacy DirectionalSpriteAnimator remains for Phase 2 removal.");
        }

        static Dictionary<string, Dictionary<string, Sprite[]>> ReadFrameSets(SerializedObject playerSerialized)
        {
            var result = new Dictionary<string, Dictionary<string, Sprite[]>>(StringComparer.Ordinal);
            foreach (var characterName in CharacterNames)
            {
                var directions = new Dictionary<string, Sprite[]>(StringComparer.Ordinal);
                foreach (var directionName in DirectionNames())
                {
                    string propertyName = char.ToLowerInvariant(characterName[0]) + characterName.Substring(1) + directionName + "Frames";
                    var property = playerSerialized.FindProperty(propertyName);
                    directions.Add(
                        directionName,
                        property != null
                            ? ReadSpriteArray(property, propertyName)
                            : LoadGeneratedFrames(characterName, directionName));
                }

                result.Add(characterName, directions);
            }

            return result;
        }

        static Sprite[] ReadSpriteArray(SerializedProperty property, string propertyName)
        {
            if (!property.isArray)
                throw new InvalidOperationException("Player prefab frame property is not an array: " + propertyName);
            if (property.arraySize != 3)
                throw new InvalidOperationException("Player prefab frame property must contain exactly 3 sprites: " + propertyName);

            var frames = new Sprite[property.arraySize];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = property.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                if (frames[i] == null)
                    throw new InvalidOperationException($"Player prefab frame is missing: {propertyName}[{i}]");
            }

            return frames;
        }

        static Sprite[] LoadGeneratedFrames(string characterName, string directionName)
        {
            var frames = new Sprite[3];
            for (int i = 0; i < frames.Length; i++)
            {
                string path = $"Assets/AreaSurvivors/Sprites/Generated/Walk/{characterName}/{directionName}_{i}.png";
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[i] == null)
                    throw new InvalidOperationException("Generated Player walk Sprite is missing: " + path);
            }
            return frames;
        }

        static Dictionary<string, AnimationClip> EnsurePlaceholderClips()
        {
            var result = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (var state in States)
            {
                string path = PlaceholderClipPath(state.StateName);
                result.Add(state.StateName, EnsureSpriteClip(path, null, state.Moving, true));
            }

            return result;
        }

        static Dictionary<string, AnimationClip> EnsureCharacterClips(
            string characterName,
            Dictionary<string, Sprite[]> directionFrames)
        {
            string characterFolder = CharacterFolderPath(characterName);
            EnsureFolder(characterFolder);

            var result = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (var state in States)
            {
                if (!directionFrames.TryGetValue(state.DirectionName, out var frames) || frames == null || frames.Length != 3)
                    throw new InvalidOperationException($"Directional frames are invalid: {characterName}/{state.DirectionName}");

                result.Add(
                    state.StateName,
                    EnsureSpriteClip(CharacterClipPath(characterName, state.StateName), frames, state.Moving, false));
            }

            return result;
        }

        static AnimationClip EnsureSpriteClip(string assetPath, Sprite[] frames, bool moving, bool placeholder)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(assetPath) };
                AssetDatabase.CreateAsset(clip, assetPath);
            }

            clip.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            clip.frameRate = FramesPerSecond;
            ClearClipCurves(clip);

            float frameDuration = 1f / FramesPerSecond;
            ObjectReferenceKeyframe[] keys;
            if (placeholder)
            {
                keys = new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = null },
                    new ObjectReferenceKeyframe { time = frameDuration, value = null },
                };
            }
            else if (moving)
            {
                keys = new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = frames[0] },
                    new ObjectReferenceKeyframe { time = frameDuration, value = frames[1] },
                    new ObjectReferenceKeyframe { time = frameDuration * 2f, value = frames[2] },
                    new ObjectReferenceKeyframe { time = frameDuration * 3f, value = frames[2] },
                };
            }
            else
            {
                keys = new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = frames[1] },
                    new ObjectReferenceKeyframe { time = frameDuration, value = frames[1] },
                };
            }

            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(PaperMeshVisual), "sourceSprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }

        static void ClearClipCurves(AnimationClip clip)
        {
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                AnimationUtility.SetEditorCurve(clip, binding, null);
        }

        static AnimatorController EnsureBaseController(Dictionary<string, AnimationClip> placeholderClips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(BaseControllerPath);
            if (controller.layers == null || controller.layers.Length != 1)
                throw new InvalidOperationException("Generated Player base Animator Controller must contain exactly one layer.");

            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            var stateMachine = controller.layers[0].stateMachine;
            if (stateMachine.stateMachines.Length != 0)
                throw new InvalidOperationException("Generated Player base Animator Controller must not contain nested state machines.");

            foreach (var transition in stateMachine.anyStateTransitions)
                stateMachine.RemoveAnyStateTransition(transition);
            foreach (var transition in stateMachine.entryTransitions)
                stateMachine.RemoveEntryTransition(transition);

            var expectedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var state in States) expectedNames.Add(state.StateName);

            var stateByName = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            foreach (var child in stateMachine.states)
            {
                if (!expectedNames.Contains(child.state.name) || stateByName.ContainsKey(child.state.name))
                {
                    stateMachine.RemoveState(child.state);
                    continue;
                }
                stateByName.Add(child.state.name, child.state);
            }

            AnimatorState idleDown = null;
            foreach (var stateSpec in States)
            {
                if (!stateByName.TryGetValue(stateSpec.StateName, out var state))
                {
                    state = stateMachine.AddState(stateSpec.StateName);
                    stateByName.Add(stateSpec.StateName, state);
                }

                foreach (var transition in state.transitions) state.RemoveTransition(transition);
                state.motion = placeholderClips[stateSpec.StateName];
                state.speed = 1f;
                state.writeDefaultValues = true;
                if (stateSpec.StateName == "IdleDown") idleDown = state;
            }

            stateMachine.defaultState = idleDown;
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
            return controller;
        }

        static AnimatorOverrideController EnsureOverrideController(
            string characterName,
            AnimatorController baseController,
            Dictionary<string, AnimationClip> placeholderClips,
            Dictionary<string, AnimationClip> characterClips)
        {
            string path = OverrideControllerPath(characterName);
            var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if (overrideController == null)
            {
                overrideController = new AnimatorOverrideController
                {
                    name = "Player" + characterName,
                    runtimeAnimatorController = baseController,
                };
                AssetDatabase.CreateAsset(overrideController, path);
            }
            else
            {
                overrideController.runtimeAnimatorController = baseController;
            }

            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrideController.GetOverrides(overrides);
            for (int i = 0; i < overrides.Count; i++)
            {
                var baseClip = overrides[i].Key;
                string stateName = null;
                foreach (var pair in placeholderClips)
                {
                    if (pair.Value == baseClip)
                    {
                        stateName = pair.Key;
                        break;
                    }
                }
                if (stateName == null || !characterClips.TryGetValue(stateName, out var clip))
                    throw new InvalidOperationException("Unexpected base clip in Player Animator Override Controller: " + (baseClip != null ? baseClip.name : "null"));
                overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(baseClip, clip);
            }
            overrideController.ApplyOverrides(overrides);

            foreach (var state in States)
            {
                if (!placeholderClips.ContainsKey(state.StateName) || !characterClips.ContainsKey(state.StateName))
                    throw new InvalidOperationException("Player Animator override mapping is incomplete: " + state.StateName);
            }

            EditorUtility.SetDirty(overrideController);
            AssetDatabase.SaveAssetIfDirty(overrideController);
            return overrideController;
        }

        static void ConfigurePlayerPrefab(
            GameObject root,
            PlayerController player,
            Dictionary<string, AnimatorOverrideController> overrideControllers)
        {
            var visual = root.GetComponentInChildren<PaperMeshVisual>(true);
            if (visual == null) throw new InvalidOperationException("Player PaperMeshVisual is missing.");
            AssertZeroXYRotation(root.transform, "Player root");
            AssertZeroXYRotation(visual.transform, "Player Paper Visual");

            var originalRootPosition = root.transform.localPosition;
            var originalRootRotation = root.transform.localRotation;
            var originalRootScale = root.transform.localScale;
            var originalVisualPosition = visual.transform.localPosition;
            var originalVisualRotation = visual.transform.localRotation;
            var originalVisualScale = visual.transform.localScale;

            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = overrideControllers["Knight"];
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            animator.enabled = true;

            var driver = root.GetComponent<DirectionalAnimatorDriver>();
            if (driver == null) driver = root.AddComponent<DirectionalAnimatorDriver>();
            var driverSerialized = new SerializedObject(driver);
            SetObjectReference(driverSerialized, "animator", animator);
            driverSerialized.ApplyModifiedPropertiesWithoutUndo();

            var playerSerialized = new SerializedObject(player);
            SetObjectReference(playerSerialized, "directionalAnimatorDriver", driver);
            SetObjectReference(playerSerialized, "knightAnimatorController", overrideControllers["Knight"]);
            SetObjectReference(playerSerialized, "archerAnimatorController", overrideControllers["Archer"]);
            SetObjectReference(playerSerialized, "mageAnimatorController", overrideControllers["Mage"]);
            playerSerialized.ApplyModifiedPropertiesWithoutUndo();

            RemoveLegacyDirectionalSpriteAnimator(root);

            AssertTransformUnchanged(root.transform, originalRootPosition, originalRootRotation, originalRootScale, "Player root");
            AssertTransformUnchanged(visual.transform, originalVisualPosition, originalVisualRotation, originalVisualScale, "Player Paper Visual");
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(driver);
            EditorUtility.SetDirty(player);
        }

        static void RemoveLegacyDirectionalSpriteAnimator(GameObject root)
        {
            foreach (var behaviour in root.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != "DirectionalSpriteAnimator") continue;
                UnityEngine.Object.DestroyImmediate(behaviour);
            }
        }

        static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException("Required serialized property is missing: " + propertyName);
            property.objectReferenceValue = value;
        }

        static void AssertZeroXYRotation(Transform transform, string label)
        {
            var euler = transform.localEulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)) > 0.01f || Mathf.Abs(Mathf.DeltaAngle(0f, euler.y)) > 0.01f)
                throw new InvalidOperationException(label + " must have Rotation X/Y = 0 before Animator migration.");
        }

        static void AssertTransformUnchanged(
            Transform transform,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            string label)
        {
            if (transform.localPosition != position || Quaternion.Angle(transform.localRotation, rotation) > 0.001f || transform.localScale != scale)
                throw new InvalidOperationException(label + " Transform changed during Animator migration.");
        }

        static IEnumerable<string> DirectionNames()
        {
            yield return "Down";
            yield return "Left";
            yield return "Right";
            yield return "Up";
        }

        public static string CharacterFolderPath(string characterName) => AnimationRootPath + "/" + characterName;
        public static string PlaceholderClipPath(string stateName) => BaseFolderPath + "/Placeholder_" + stateName + ".anim";
        public static string CharacterClipPath(string characterName, string stateName) => CharacterFolderPath(characterName) + "/" + stateName + ".anim";
        public static string OverrideControllerPath(string characterName) => CharacterFolderPath(characterName) + "/Player" + characterName + ".overrideController";

        static void EnsureFolder(string folderPath)
        {
            var parts = folderPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
