using System;
using System.Collections.Generic;
using System.IO;
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
        public const string CompletionMarkerRelativePath =
            "Library/AreaSafeUnity/player-directional-animator-migration.ok";
        public const float FramesPerSecond = 8f;
        const int ArcherWalkFrameSize = 384;
        const int ArcherLegRegionTopY = 307;

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

        sealed class ArcherVerticalWalkBuild
        {
            public string targetPath;
            public int width;
            public int height;
            public Color32[] outputPixels;
        }

        [MenuItem(MenuPath)]
        public static void Migrate()
        {
            string markerPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                CompletionMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            EnsureFolder(AnimationRootPath);
            EnsureFolder(BaseFolderPath);
            RebuildArcherVerticalWalkFrames();

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
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
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

                bool mageCharacter = string.Equals(characterName, "Mage", StringComparison.Ordinal);
                bool archerCharacter = string.Equals(characterName, "Archer", StringComparison.Ordinal);
                bool verticalDirection =
                    string.Equals(state.DirectionName, "Up", StringComparison.Ordinal) ||
                    string.Equals(state.DirectionName, "Down", StringComparison.Ordinal);
                bool twoPoseWalkCycle = mageCharacter || (archerCharacter && !verticalDirection);
                bool threePoseWalkCycle = archerCharacter && state.Moving && verticalDirection;
                bool writesFlipCurve = mageCharacter || archerCharacter;
                bool alternateFinalStep = mageCharacter && state.Moving && verticalDirection;
                int idleFrameIndex = archerCharacter ? 0 : 1;

                result.Add(
                    state.StateName,
                    EnsureSpriteClip(
                        CharacterClipPath(characterName, state.StateName),
                        frames,
                        state.Moving,
                        false,
                        twoPoseWalkCycle,
                        threePoseWalkCycle,
                        writesFlipCurve,
                        alternateFinalStep,
                        idleFrameIndex));
            }

            return result;
        }

        static AnimationClip EnsureSpriteClip(
            string assetPath,
            Sprite[] frames,
            bool moving,
            bool placeholder,
            bool twoPoseWalkCycle = false,
            bool threePoseWalkCycle = false,
            bool writesFlipCurve = false,
            bool alternateFinalStep = false,
            int idleFrameIndex = 1)
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
                if (threePoseWalkCycle)
                {
                    keys = new[]
                    {
                        new ObjectReferenceKeyframe { time = 0f, value = frames[0] },
                        new ObjectReferenceKeyframe { time = frameDuration, value = frames[1] },
                        new ObjectReferenceKeyframe { time = frameDuration * 2f, value = frames[0] },
                        new ObjectReferenceKeyframe { time = frameDuration * 3f, value = frames[2] },
                    };
                }
                else if (twoPoseWalkCycle)
                {
                    keys = new[]
                    {
                        new ObjectReferenceKeyframe { time = 0f, value = frames[0] },
                        new ObjectReferenceKeyframe { time = frameDuration, value = frames[1] },
                        new ObjectReferenceKeyframe { time = frameDuration * 2f, value = frames[0] },
                        new ObjectReferenceKeyframe { time = frameDuration * 3f, value = frames[1] },
                    };
                }
                else
                {
                    keys = new[]
                    {
                        new ObjectReferenceKeyframe { time = 0f, value = frames[0] },
                        new ObjectReferenceKeyframe { time = frameDuration, value = frames[1] },
                        new ObjectReferenceKeyframe { time = frameDuration * 2f, value = frames[2] },
                        new ObjectReferenceKeyframe { time = frameDuration * 3f, value = frames[2] },
                    };
                }
            }
            else
            {
                keys = new[]
                {
                    new ObjectReferenceKeyframe { time = 0f, value = frames[idleFrameIndex] },
                    new ObjectReferenceKeyframe { time = frameDuration, value = frames[idleFrameIndex] },
                };
            }

            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(PaperMeshVisual), "sourceSprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
            if (writesFlipCurve)
            {
                var flipKeys = new Keyframe[keys.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    float value = alternateFinalStep && i == keys.Length - 1 ? 1f : 0f;
                    flipKeys[i] = new Keyframe(keys[i].time, value);
                }

                var flipCurve = new AnimationCurve(flipKeys);
                for (int i = 0; i < flipCurve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(flipCurve, i, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(flipCurve, i, AnimationUtility.TangentMode.Constant);
                }

                var flipBinding = EditorCurveBinding.FloatCurve(string.Empty, typeof(PaperMeshVisual), "flipHorizontal");
                AnimationUtility.SetEditorCurve(clip, flipBinding, flipCurve);
            }
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return clip;
        }

        static void RebuildArcherVerticalWalkFrames()
        {
            var builds = new List<ArcherVerticalWalkBuild>();
            foreach (string directionName in new[] { "Down", "Up" })
            {
                PreflightArcherWalkSource(ArcherNeutralSourcePath(directionName));
                for (int frameIndex = 1; frameIndex <= 2; frameIndex++)
                    PreflightArcherWalkSource(ArcherWalkSourcePath(directionName, frameIndex));
            }

            foreach (string directionName in new[] { "Down", "Up" })
            {
                string cleanNeutralPath = ArcherNeutralSourcePath(directionName);
                var cleanNeutralTexture = LoadPngTexture(cleanNeutralPath);
                try
                {
                    ValidateArcherWalkSource(cleanNeutralTexture, cleanNeutralPath);
                    var cleanNeutralPixels = cleanNeutralTexture.GetPixels32();
                    builds.Add(new ArcherVerticalWalkBuild
                    {
                        targetPath = GeneratedFramePath("Archer", directionName, 0),
                        width = cleanNeutralTexture.width,
                        height = cleanNeutralTexture.height,
                        outputPixels = cleanNeutralPixels
                    });

                    for (int frameIndex = 1; frameIndex <= 2; frameIndex++)
                    {
                        string sourcePath = ArcherWalkSourcePath(directionName, frameIndex);
                        var sourceTexture = LoadPngTexture(sourcePath);
                        try
                        {
                            ValidateArcherWalkSource(sourceTexture, sourcePath);
                            if (sourceTexture.width != cleanNeutralTexture.width ||
                                sourceTexture.height != cleanNeutralTexture.height)
                            {
                                throw new InvalidOperationException(
                                    $"Archer vertical walk source must match the clean neutral frame: {cleanNeutralPath} / {sourcePath}");
                            }

                            var outputPixels = (Color32[])cleanNeutralPixels.Clone();
                            int lowerRegionPixelCount =
                                cleanNeutralTexture.width * (cleanNeutralTexture.height - ArcherLegRegionTopY);
                            Array.Copy(
                                sourceTexture.GetPixels32(),
                                0,
                                outputPixels,
                                0,
                                lowerRegionPixelCount);
                            builds.Add(new ArcherVerticalWalkBuild
                            {
                                targetPath = GeneratedFramePath("Archer", directionName, frameIndex),
                                width = cleanNeutralTexture.width,
                                height = cleanNeutralTexture.height,
                                outputPixels = outputPixels
                            });
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(sourceTexture);
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cleanNeutralTexture);
                }
            }

            var changedTargetPaths = new List<string>();
            foreach (var build in builds)
            {
                if (PngPixelsEqual(build.targetPath, build.width, build.height, build.outputPixels)) continue;

                var outputTexture = new Texture2D(build.width, build.height, TextureFormat.RGBA32, false);
                try
                {
                    outputTexture.SetPixels32(build.outputPixels);
                    outputTexture.Apply(false, false);
                    File.WriteAllBytes(Path.GetFullPath(build.targetPath), outputTexture.EncodeToPNG());
                    changedTargetPaths.Add(build.targetPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(outputTexture);
                }
            }

            // Import only after every raw PNG write has completed. Importing a source or
            // target between writes can leave another generated PNG memory-mapped by the
            // Windows Asset Pipeline and make the next File.WriteAllBytes fail with IO 1224.
            foreach (string changedTargetPath in changedTargetPaths)
            {
                AssetDatabase.ImportAsset(
                    changedTargetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Debug.Log("Rebuilt Archer vertical walk frame from authored regions: " + changedTargetPath);
            }
        }

        static void PreflightArcherWalkSource(string sourcePath)
        {
            if (!File.Exists(Path.GetFullPath(sourcePath)))
                throw new FileNotFoundException("Archer walk source PNG is missing.", sourcePath);

            if (AssetImporter.GetAtPath(sourcePath) == null)
                throw new InvalidOperationException(
                    "Archer walk source must already be imported with its .meta before migration: " + sourcePath);
        }

        static void ValidateArcherWalkSource(Texture2D texture, string sourcePath)
        {
            if (texture.width != ArcherWalkFrameSize || texture.height != ArcherWalkFrameSize)
                throw new InvalidOperationException("Archer walk source must be 384x384: " + sourcePath);

            var pixels = texture.GetPixels32();
            bool hasVisiblePixel = false;
            bool hasTransparentPixel = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                hasVisiblePixel |= pixels[i].a > 0;
                hasTransparentPixel |= pixels[i].a < byte.MaxValue;
            }

            if (!hasVisiblePixel || !hasTransparentPixel)
                throw new InvalidOperationException("Archer walk source requires visible RGBA art and transparency: " + sourcePath);
        }

        static bool PngPixelsEqual(string assetPath, int width, int height, Color32[] expectedPixels)
        {
            if (!File.Exists(Path.GetFullPath(assetPath))) return false;

            var texture = LoadPngTexture(assetPath);
            try
            {
                if (texture.width != width || texture.height != height) return false;
                var actualPixels = texture.GetPixels32();
                if (actualPixels.Length != expectedPixels.Length) return false;
                for (int i = 0; i < actualPixels.Length; i++)
                {
                    if (!SameColor(actualPixels[i], expectedPixels[i])) return false;
                }
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static Texture2D LoadPngTexture(string assetPath)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) throw new FileNotFoundException("Player walk PNG is missing.", fullPath);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(File.ReadAllBytes(fullPath), false)) return texture;

            UnityEngine.Object.DestroyImmediate(texture);
            throw new InvalidOperationException("Player walk PNG could not be decoded: " + assetPath);
        }

        static bool SameColor(Color32 left, Color32 right)
        {
            return left.r == right.r && left.g == right.g && left.b == right.b && left.a == right.a;
        }

        static string GeneratedFramePath(string characterName, string directionName, int frameIndex)
        {
            return $"Assets/AreaSurvivors/Sprites/Generated/Walk/{characterName}/{directionName}_{frameIndex}.png";
        }

        static string ArcherWalkSourcePath(string directionName, int frameIndex)
        {
            return $"Assets/AreaSurvivors/Sprites/External/Archer{directionName}Walk{frameIndex}Source.png";
        }

        static string ArcherNeutralSourcePath(string directionName)
        {
            return $"Assets/AreaSurvivors/Sprites/External/Archer{directionName}NeutralCleanSource.png";
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
