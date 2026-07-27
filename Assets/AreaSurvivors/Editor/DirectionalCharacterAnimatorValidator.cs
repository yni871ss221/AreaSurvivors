using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class DirectionalCharacterAnimatorValidator
    {
        public const string MenuPath = "Area Survivors/Validate/Player Directional Animator Migration";
        const string CompletionMarkerRelativePath = "Library/AreaSafeUnity/player-directional-animator-validator.ok";
        const string MigrationScriptPath =
            "Assets/AreaSurvivors/Editor/DirectionalCharacterAnimatorMigration.cs";
        const int ArcherWalkFrameSize = 384;
        const int ArcherLegRegionTopY = 307;

        [MenuItem(MenuPath)]
        public static void Validate()
        {
            string markerPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                CompletionMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            int errors = 0;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DirectionalCharacterAnimatorMigration.PlayerPrefabPath);
            if (prefab == null)
            {
                Error("Player prefab is missing.", ref errors);
                ThrowIfFailed(errors);
                return;
            }

            var player = prefab.GetComponent<PlayerController>();
            if (player == null)
            {
                Error("PlayerController is missing from Player prefab.", ref errors);
                ThrowIfFailed(errors);
                return;
            }

            var playerSerialized = new SerializedObject(player);
            var expectedFrames = ResolveExpectedFrames(playerSerialized, ref errors);
            ValidateArcherMigrationWriteOrdering(ref errors);
            ValidateArcherVerticalFrameComposition(ref errors);
            var placeholderClips = ValidatePlaceholderClips(ref errors);
            var characterClips = ValidateCharacterClips(expectedFrames, ref errors);
            var baseController = ValidateBaseController(placeholderClips, ref errors);
            var overrideControllers = ValidateOverrideControllers(baseController, placeholderClips, characterClips, ref errors);
            ValidatePrefab(prefab, playerSerialized, overrideControllers, ref errors);
            ValidateLegacyRemoval(playerSerialized, prefab, ref errors);

            ThrowIfFailed(errors);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Player directional Animator validation passed: 8 states, 8 placeholder clips, 24 character clips, 3 override controllers, Prefab references saved, legacy Player animator removed.");
        }

        static void ValidateArcherMigrationWriteOrdering(ref int errors)
        {
            string fullPath = Path.GetFullPath(MigrationScriptPath);
            if (!File.Exists(fullPath))
            {
                Error("Directional Animator migration source is missing: " + MigrationScriptPath, ref errors);
                return;
            }

            string source = File.ReadAllText(fullPath);
            int firstWriteIndex = source.IndexOf("File.WriteAllBytes(", StringComparison.Ordinal);
            int lastWriteIndex = source.LastIndexOf("File.WriteAllBytes(", StringComparison.Ordinal);
            int firstImportIndex = source.IndexOf("AssetDatabase.ImportAsset(", StringComparison.Ordinal);
            if (firstWriteIndex < 0 || firstImportIndex < 0 || firstImportIndex < lastWriteIndex)
            {
                Error(
                    "Archer migration must write every generated PNG before starting AssetDatabase.ImportAsset; interleaving can fail with Windows IO 1224.",
                    ref errors);
            }
        }

        static Dictionary<string, Dictionary<string, Sprite[]>> ResolveExpectedFrames(
            SerializedObject playerSerialized,
            ref int errors)
        {
            var result = new Dictionary<string, Dictionary<string, Sprite[]>>(StringComparer.Ordinal);
            foreach (var characterName in DirectionalCharacterAnimatorMigration.CharacterNames)
            {
                var directions = new Dictionary<string, Sprite[]>(StringComparer.Ordinal);
                foreach (var directionName in DirectionNames())
                {
                    string propertyName = char.ToLowerInvariant(characterName[0]) + characterName.Substring(1) + directionName + "Frames";
                    var property = playerSerialized.FindProperty(propertyName);
                    Sprite[] frames;
                    if (property != null)
                    {
                        frames = ReadSerializedFrames(property, propertyName, ref errors);
                    }
                    else
                    {
                        frames = LoadFramesFromGeneratedAssets(characterName, directionName, ref errors);
                    }
                    directions.Add(directionName, frames);
                }
                result.Add(characterName, directions);
            }
            return result;
        }

        static Sprite[] ReadSerializedFrames(SerializedProperty property, string propertyName, ref int errors)
        {
            if (!property.isArray || property.arraySize != 3)
            {
                Error("Legacy Player frame property must contain exactly 3 sprites while it exists: " + propertyName, ref errors);
                return new Sprite[3];
            }

            var frames = new Sprite[3];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = property.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                if (frames[i] == null) Error($"Legacy Player frame is missing: {propertyName}[{i}]", ref errors);
            }
            return frames;
        }

        static Sprite[] LoadFramesFromGeneratedAssets(string characterName, string directionName, ref int errors)
        {
            var frames = new Sprite[3];
            for (int i = 0; i < frames.Length; i++)
            {
                string path = $"Assets/AreaSurvivors/Sprites/Generated/Walk/{characterName}/{directionName}_{i}.png";
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[i] == null) Error("Generated Player walk sprite is missing: " + path, ref errors);
            }
            return frames;
        }

        static Dictionary<string, AnimationClip> ValidatePlaceholderClips(ref int errors)
        {
            var result = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (var state in DirectionalCharacterAnimatorMigration.States)
            {
                string path = DirectionalCharacterAnimatorMigration.PlaceholderClipPath(state.StateName);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                result.Add(state.StateName, clip);
                if (clip == null)
                {
                    Error("Player Animator placeholder clip is missing: " + path, ref errors);
                    continue;
                }

                var keys = ValidateClipContract(clip, path, false, ref errors);
                if (keys == null) continue;
                if (keys.Length != 2) Error("Placeholder clip must contain exactly 2 timing keys: " + path, ref errors);
                foreach (var key in keys)
                {
                    if (key.value != null) Error("Placeholder clip must not contain a character Sprite: " + path, ref errors);
                }
            }
            return result;
        }

        static Dictionary<string, Dictionary<string, AnimationClip>> ValidateCharacterClips(
            Dictionary<string, Dictionary<string, Sprite[]>> expectedFrames,
            ref int errors)
        {
            var result = new Dictionary<string, Dictionary<string, AnimationClip>>(StringComparer.Ordinal);
            foreach (var characterName in DirectionalCharacterAnimatorMigration.CharacterNames)
            {
                var clips = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
                foreach (var state in DirectionalCharacterAnimatorMigration.States)
                {
                    string path = DirectionalCharacterAnimatorMigration.CharacterClipPath(characterName, state.StateName);
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    clips.Add(state.StateName, clip);
                    if (clip == null)
                    {
                        Error("Player directional AnimationClip is missing: " + path, ref errors);
                        continue;
                    }

                    bool mageCharacter = string.Equals(characterName, "Mage", StringComparison.Ordinal);
                    bool archerCharacter = string.Equals(characterName, "Archer", StringComparison.Ordinal);
                    bool verticalDirection =
                        string.Equals(state.DirectionName, "Up", StringComparison.Ordinal) ||
                        string.Equals(state.DirectionName, "Down", StringComparison.Ordinal);
                    bool twoPoseCharacter = mageCharacter || archerCharacter;
                    bool archerVerticalWalk = archerCharacter && state.Moving && verticalDirection;
                    var keys = ValidateClipContract(clip, path, twoPoseCharacter, ref errors);
                    if (keys == null) continue;
                    var frames = expectedFrames[characterName][state.DirectionName];
                    if (state.Moving)
                    {
                        if (keys.Length != 4)
                        {
                            Error("Walk clip must contain exactly four Sprite keys: " + path, ref errors);
                        }
                        else
                        {
                            ValidateKeySprite(keys[0], frames[0], path, 0, ref errors);
                            ValidateKeySprite(keys[1], frames[1], path, 1, ref errors);
                            ValidateKeySprite(keys[2], twoPoseCharacter ? frames[0] : frames[2], path, 2, ref errors);
                            ValidateKeySprite(
                                keys[3],
                                archerVerticalWalk ? frames[2] : twoPoseCharacter ? frames[1] : frames[2],
                                path,
                                3,
                                ref errors);
                        }
                    }
                    else
                    {
                        if (keys.Length != 2)
                        {
                            Error("Idle clip must contain exactly 2 hold keys: " + path, ref errors);
                        }
                        else
                        {
                            var idleFrame = archerCharacter ? frames[0] : frames[1];
                            ValidateKeySprite(keys[0], idleFrame, path, 0, ref errors);
                            ValidateKeySprite(keys[1], idleFrame, path, 1, ref errors);
                        }
                    }

                    if (twoPoseCharacter)
                        ValidateCharacterFlipCurve(clip, path, characterName, state, ref errors);
                }
                result.Add(characterName, clips);
            }
            return result;
        }

        static void ValidateArcherVerticalFrameComposition(ref int errors)
        {
            foreach (string directionName in new[] { "Down", "Up" })
            {
                string neutralPath = GeneratedFramePath(directionName, 0);
                string cleanNeutralPath = ArcherNeutralSourcePath(directionName);
                var neutralTexture = LoadPngTexture(neutralPath, ref errors);
                var cleanNeutralTexture = LoadPngTexture(cleanNeutralPath, ref errors);
                if (neutralTexture == null || cleanNeutralTexture == null)
                {
                    if (neutralTexture != null) UnityEngine.Object.DestroyImmediate(neutralTexture);
                    if (cleanNeutralTexture != null) UnityEngine.Object.DestroyImmediate(cleanNeutralTexture);
                    continue;
                }

                try
                {
                    if (neutralTexture.width != ArcherWalkFrameSize ||
                        neutralTexture.height != ArcherWalkFrameSize ||
                        cleanNeutralTexture.width != ArcherWalkFrameSize ||
                        cleanNeutralTexture.height != ArcherWalkFrameSize ||
                        neutralTexture.width != cleanNeutralTexture.width ||
                        neutralTexture.height != cleanNeutralTexture.height)
                    {
                        Error(
                            $"Archer neutral target/source must both be 384x384: {neutralPath} / {cleanNeutralPath}",
                            ref errors);
                        continue;
                    }

                    var cleanNeutralPixels = cleanNeutralTexture.GetPixels32();
                    if (!HasVisibleAndTransparentPixels(cleanNeutralPixels))
                    {
                        Error(
                            "Archer clean neutral source requires visible RGBA art and transparency: " + cleanNeutralPath,
                            ref errors);
                        continue;
                    }

                    var neutralPixels = neutralTexture.GetPixels32();
                    int neutralMismatchCount = 0;
                    for (int pixelIndex = 0; pixelIndex < neutralPixels.Length; pixelIndex++)
                    {
                        if (!SameColor(neutralPixels[pixelIndex], cleanNeutralPixels[pixelIndex]))
                            neutralMismatchCount++;
                    }
                    if (neutralMismatchCount > 0)
                    {
                        Error(
                            $"Archer neutral frame must exactly match its clean source: {neutralPath}, mismatches={neutralMismatchCount}",
                            ref errors);
                    }

                    for (int frameIndex = 1; frameIndex <= 2; frameIndex++)
                    {
                        string targetPath = GeneratedFramePath(directionName, frameIndex);
                        string sourcePath = ArcherWalkSourcePath(directionName, frameIndex);
                        var targetTexture = LoadPngTexture(targetPath, ref errors);
                        var sourceTexture = LoadPngTexture(sourcePath, ref errors);
                        if (targetTexture == null || sourceTexture == null)
                        {
                            if (targetTexture != null) UnityEngine.Object.DestroyImmediate(targetTexture);
                            if (sourceTexture != null) UnityEngine.Object.DestroyImmediate(sourceTexture);
                            continue;
                        }

                        try
                        {
                            if (targetTexture.width != ArcherWalkFrameSize ||
                                targetTexture.height != ArcherWalkFrameSize ||
                                sourceTexture.width != ArcherWalkFrameSize ||
                                sourceTexture.height != ArcherWalkFrameSize ||
                                targetTexture.width != neutralTexture.width ||
                                targetTexture.height != neutralTexture.height ||
                                sourceTexture.width != neutralTexture.width ||
                                sourceTexture.height != neutralTexture.height)
                            {
                                Error(
                                    $"Archer vertical walk target/source must match the 384x384 neutral frame: {targetPath} / {sourcePath}",
                                    ref errors);
                                continue;
                            }

                            var sourcePixels = sourceTexture.GetPixels32();
                            if (!HasVisibleAndTransparentPixels(sourcePixels))
                            {
                                Error(
                                    "Archer walk source requires visible RGBA art and transparency: " + sourcePath,
                                    ref errors);
                                continue;
                            }

                            var targetPixels = targetTexture.GetPixels32();
                            int lowerRegionPixelCount =
                                neutralTexture.width * (neutralTexture.height - ArcherLegRegionTopY);
                            int upperMismatchCount = 0;
                            int lowerMismatchCount = 0;
                            for (int pixelIndex = 0; pixelIndex < targetPixels.Length; pixelIndex++)
                            {
                                if (pixelIndex < lowerRegionPixelCount)
                                {
                                    if (!SameColor(targetPixels[pixelIndex], sourcePixels[pixelIndex]))
                                        lowerMismatchCount++;
                                }
                                else if (!SameColor(targetPixels[pixelIndex], neutralPixels[pixelIndex]))
                                {
                                    upperMismatchCount++;
                                }
                            }

                            if (upperMismatchCount > 0 || lowerMismatchCount > 0)
                            {
                                Error(
                                    $"Archer vertical walk frame composition is invalid: {targetPath}, upper neutral mismatches={upperMismatchCount}, lower source mismatches={lowerMismatchCount}",
                                    ref errors);
                            }
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(sourceTexture);
                            UnityEngine.Object.DestroyImmediate(targetTexture);
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cleanNeutralTexture);
                    UnityEngine.Object.DestroyImmediate(neutralTexture);
                }
            }
        }

        static Texture2D LoadPngTexture(string assetPath, ref int errors)
        {
            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                Error("Player walk PNG is missing: " + assetPath, ref errors);
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(File.ReadAllBytes(fullPath), false)) return texture;

            UnityEngine.Object.DestroyImmediate(texture);
            Error("Player walk PNG could not be decoded: " + assetPath, ref errors);
            return null;
        }

        static bool HasVisibleAndTransparentPixels(Color32[] pixels)
        {
            bool hasVisiblePixel = false;
            bool hasTransparentPixel = false;
            for (int i = 0; i < pixels.Length; i++)
            {
                hasVisiblePixel |= pixels[i].a > 0;
                hasTransparentPixel |= pixels[i].a < byte.MaxValue;
            }

            return hasVisiblePixel && hasTransparentPixel;
        }

        static bool SameColor(Color32 left, Color32 right)
        {
            return left.r == right.r && left.g == right.g && left.b == right.b && left.a == right.a;
        }

        static string GeneratedFramePath(string directionName, int frameIndex)
        {
            return $"Assets/AreaSurvivors/Sprites/Generated/Walk/Archer/{directionName}_{frameIndex}.png";
        }

        static string ArcherWalkSourcePath(string directionName, int frameIndex)
        {
            return $"Assets/AreaSurvivors/Sprites/External/Archer{directionName}Walk{frameIndex}Source.png";
        }

        static string ArcherNeutralSourcePath(string directionName)
        {
            return $"Assets/AreaSurvivors/Sprites/External/Archer{directionName}NeutralCleanSource.png";
        }

        static ObjectReferenceKeyframe[] ValidateClipContract(
            AnimationClip clip,
            string path,
            bool expectTwoPoseFlipCurve,
            ref int errors)
        {
            if (Mathf.Abs(clip.frameRate - DirectionalCharacterAnimatorMigration.FramesPerSecond) > 0.001f)
                Error("Player directional AnimationClip frame rate must be 8: " + path, ref errors);

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (!settings.loopTime) Error("Player directional AnimationClip must loop: " + path, ref errors);
            if (AnimationUtility.GetAnimationEvents(clip).Length != 0)
                Error("Player directional AnimationClip must not use Animation Events: " + path, ref errors);
            var floatBindings = AnimationUtility.GetCurveBindings(clip);
            if (expectTwoPoseFlipCurve)
            {
                if (floatBindings.Length != 1 ||
                    !string.IsNullOrEmpty(floatBindings[0].path) ||
                    floatBindings[0].type != typeof(PaperMeshVisual) ||
                    floatBindings[0].propertyName != "flipHorizontal")
                {
                    Error("Directional AnimationClip must contain only PaperMeshVisual.flipHorizontal: " + path, ref errors);
                }
            }
            else if (floatBindings.Length != 0)
            {
                Error("Player directional AnimationClip must not modify float/Transform properties: " + path, ref errors);
            }

            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length != 1)
            {
                Error("Player directional AnimationClip must contain exactly one Sprite curve: " + path, ref errors);
                return null;
            }

            var binding = bindings[0];
            if (!string.IsNullOrEmpty(binding.path) || binding.type != typeof(PaperMeshVisual) || binding.propertyName != "sourceSprite")
            {
                Error("Player directional AnimationClip must target PaperMeshVisual.sourceSprite on the Animator GameObject: " + path, ref errors);
            }
            return AnimationUtility.GetObjectReferenceCurve(clip, binding);
        }

        static void ValidateCharacterFlipCurve(
            AnimationClip clip,
            string path,
            string characterName,
            DirectionalCharacterAnimatorMigration.StateSpec state,
            ref int errors)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            if (bindings.Length != 1) return;

            var curve = AnimationUtility.GetEditorCurve(clip, bindings[0]);
            int expectedLength = state.Moving ? 4 : 2;
            if (curve == null || curve.length != expectedLength)
            {
                Error("Directional flip curve key count is invalid: " + path, ref errors);
                return;
            }

            bool alternateFinalStep = string.Equals(characterName, "Mage", StringComparison.Ordinal) &&
                state.Moving &&
                (string.Equals(state.DirectionName, "Up", StringComparison.Ordinal) ||
                 string.Equals(state.DirectionName, "Down", StringComparison.Ordinal));
            float frameDuration = 1f / DirectionalCharacterAnimatorMigration.FramesPerSecond;
            for (int i = 0; i < curve.length; i++)
            {
                float expectedValue = alternateFinalStep && i == curve.length - 1 ? 1f : 0f;
                if (Mathf.Abs(curve.keys[i].time - frameDuration * i) > 0.001f ||
                    Mathf.Abs(curve.keys[i].value - expectedValue) > 0.001f)
                {
                    Error($"Directional flip curve key {i} is invalid: {path}", ref errors);
                }
            }
        }

        static void ValidateKeySprite(
            ObjectReferenceKeyframe key,
            Sprite expected,
            string path,
            int keyIndex,
            ref int errors)
        {
            if (expected == null || key.value != expected)
                Error($"Player directional AnimationClip Sprite mismatch: {path} key={keyIndex}", ref errors);
        }

        static AnimatorController ValidateBaseController(
            Dictionary<string, AnimationClip> placeholderClips,
            ref int errors)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(DirectionalCharacterAnimatorMigration.BaseControllerPath);
            if (controller == null)
            {
                Error("Player base Animator Controller is missing.", ref errors);
                return null;
            }
            if (controller.parameters.Length != 0) Error("Player base Animator Controller must not use parameters.", ref errors);
            if (controller.layers.Length != 1)
            {
                Error("Player base Animator Controller must contain exactly one layer.", ref errors);
                return controller;
            }

            var stateMachine = controller.layers[0].stateMachine;
            if (stateMachine.states.Length != DirectionalCharacterAnimatorMigration.States.Length)
                Error("Player base Animator Controller must contain exactly 8 states.", ref errors);
            if (stateMachine.anyStateTransitions.Length != 0 || stateMachine.entryTransitions.Length != 0)
                Error("Player base Animator Controller must not contain AnyState/Entry transitions.", ref errors);
            if (stateMachine.stateMachines.Length != 0)
                Error("Player base Animator Controller must not contain nested state machines.", ref errors);

            var statesByName = new Dictionary<string, AnimatorState>(StringComparer.Ordinal);
            foreach (var child in stateMachine.states)
            {
                if (statesByName.ContainsKey(child.state.name))
                    Error("Player base Animator Controller contains a duplicate state: " + child.state.name, ref errors);
                else
                    statesByName.Add(child.state.name, child.state);
            }

            foreach (var stateSpec in DirectionalCharacterAnimatorMigration.States)
            {
                if (!statesByName.TryGetValue(stateSpec.StateName, out var state))
                {
                    Error("Player base Animator state is missing: " + stateSpec.StateName, ref errors);
                    continue;
                }
                if (state.transitions.Length != 0) Error("Player base Animator states must not contain transitions: " + state.name, ref errors);
                if (!placeholderClips.TryGetValue(stateSpec.StateName, out var placeholder) || state.motion != placeholder)
                    Error("Player base Animator state has an unexpected Motion: " + state.name, ref errors);
            }

            if (!statesByName.TryGetValue("IdleDown", out var idleDown) || stateMachine.defaultState != idleDown)
                Error("Player base Animator default state must be IdleDown.", ref errors);
            return controller;
        }

        static Dictionary<string, AnimatorOverrideController> ValidateOverrideControllers(
            AnimatorController baseController,
            Dictionary<string, AnimationClip> placeholderClips,
            Dictionary<string, Dictionary<string, AnimationClip>> characterClips,
            ref int errors)
        {
            var result = new Dictionary<string, AnimatorOverrideController>(StringComparer.Ordinal);
            foreach (var characterName in DirectionalCharacterAnimatorMigration.CharacterNames)
            {
                string path = DirectionalCharacterAnimatorMigration.OverrideControllerPath(characterName);
                var overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
                result.Add(characterName, overrideController);
                if (overrideController == null)
                {
                    Error("Player Animator Override Controller is missing: " + path, ref errors);
                    continue;
                }
                if (overrideController.runtimeAnimatorController != baseController)
                    Error("Player Animator Override Controller does not reference the shared base Controller: " + path, ref errors);

                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrideController.GetOverrides(overrides);
                if (overrides.Count != DirectionalCharacterAnimatorMigration.States.Length)
                    Error("Player Animator Override Controller must contain exactly 8 overrides: " + path, ref errors);

                foreach (var state in DirectionalCharacterAnimatorMigration.States)
                {
                    var placeholder = placeholderClips[state.StateName];
                    AnimationClip actual = null;
                    foreach (var pair in overrides)
                    {
                        if (pair.Key == placeholder)
                        {
                            actual = pair.Value;
                            break;
                        }
                    }
                    if (actual != characterClips[characterName][state.StateName])
                        Error($"Player Animator override is missing or incorrect: {characterName}/{state.StateName}", ref errors);
                }
            }
            return result;
        }

        static void ValidatePrefab(
            GameObject prefab,
            SerializedObject playerSerialized,
            Dictionary<string, AnimatorOverrideController> overrideControllers,
            ref int errors)
        {
            var visual = prefab.GetComponentInChildren<PaperMeshVisual>(true);
            var animator = visual != null ? visual.GetComponent<Animator>() : null;
            var driver = prefab.GetComponent<DirectionalAnimatorDriver>();
            if (visual == null) Error("Player prefab PaperMeshVisual is missing.", ref errors);
            if (animator == null) Error("Player Paper Visual must contain Animator directly.", ref errors);
            if (prefab.GetComponent<Animator>() != null) Error("Player root must not contain Animator; it belongs on Paper Visual.", ref errors);
            if (driver == null) Error("Player root DirectionalAnimatorDriver is missing.", ref errors);

            ValidateZeroXYRotation(prefab.transform, "Player root", ref errors);
            if (visual != null) ValidateZeroXYRotation(visual.transform, "Player Paper Visual", ref errors);

            if (animator != null)
            {
                if (animator.runtimeAnimatorController != overrideControllers["Knight"])
                    Error("Player Paper Visual Animator must default to the Knight Override Controller.", ref errors);
                if (animator.applyRootMotion) Error("Player Paper Visual Animator must not apply root motion.", ref errors);
                if (animator.updateMode != AnimatorUpdateMode.Normal) Error("Player Paper Visual Animator updateMode must be Normal.", ref errors);
                if (animator.cullingMode != AnimatorCullingMode.CullUpdateTransforms)
                    Error("Player Paper Visual Animator cullingMode must be CullUpdateTransforms.", ref errors);
            }

            if (driver != null)
            {
                var driverSerialized = new SerializedObject(driver);
                var animatorProperty = driverSerialized.FindProperty("animator");
                if (animatorProperty == null || animatorProperty.objectReferenceValue != animator)
                    Error("DirectionalAnimatorDriver must serialize the Player Paper Visual Animator reference.", ref errors);
            }

            ValidatePlayerReference(playerSerialized, "directionalAnimatorDriver", driver, ref errors);
            ValidatePlayerReference(playerSerialized, "knightAnimatorController", overrideControllers["Knight"], ref errors);
            ValidatePlayerReference(playerSerialized, "archerAnimatorController", overrideControllers["Archer"], ref errors);
            ValidatePlayerReference(playerSerialized, "mageAnimatorController", overrideControllers["Mage"], ref errors);

            if (visual != null)
            {
                foreach (var behaviour in visual.GetComponents<MonoBehaviour>())
                {
                    if (behaviour == null || behaviour.GetType().Name != "PaperBillboard") continue;
                    var billboardSerialized = new SerializedObject(behaviour);
                    var faceCamera = billboardSerialized.FindProperty("faceCamera");
                    if (faceCamera != null && faceCamera.boolValue)
                        Error("Player Animator Paper Visual must not use PaperBillboard.faceCamera.", ref errors);
                }
            }
        }

        static void ValidateLegacyRemoval(SerializedObject playerSerialized, GameObject prefab, ref int errors)
        {
            var legacyReference = playerSerialized.FindProperty("directionalAnimator");
            if (legacyReference != null)
                Error("PlayerController must not retain the legacy directionalAnimator serialized field.", ref errors);

            foreach (var characterName in DirectionalCharacterAnimatorMigration.CharacterNames)
            {
                foreach (var directionName in DirectionNames())
                {
                    string fieldName = char.ToLowerInvariant(characterName[0]) + characterName.Substring(1) + directionName + "Frames";
                    if (playerSerialized.FindProperty(fieldName) != null)
                        Error("PlayerController must not retain the legacy frame-array field: " + fieldName, ref errors);
                }
            }

            bool hasLegacyComponent = false;
            foreach (var behaviour in prefab.GetComponents<MonoBehaviour>())
            {
                if (behaviour != null && behaviour.GetType().Name == "DirectionalSpriteAnimator")
                {
                    hasLegacyComponent = true;
                    break;
                }
            }
            if (hasLegacyComponent)
                Error("Player Prefab must not retain the legacy DirectionalSpriteAnimator component.", ref errors);
        }

        static void ValidatePlayerReference(
            SerializedObject playerSerialized,
            string propertyName,
            UnityEngine.Object expected,
            ref int errors)
        {
            var property = playerSerialized.FindProperty(propertyName);
            if (property == null)
            {
                Error("PlayerController serialized field is missing: " + propertyName, ref errors);
                return;
            }
            if (property.objectReferenceValue != expected)
                Error("PlayerController serialized reference is incorrect: " + propertyName, ref errors);
        }

        static void ValidateZeroXYRotation(Transform transform, string label, ref int errors)
        {
            var euler = transform.localEulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)) > 0.01f || Mathf.Abs(Mathf.DeltaAngle(0f, euler.y)) > 0.01f)
                Error(label + " must have Rotation X/Y = 0.", ref errors);
        }

        static IEnumerable<string> DirectionNames()
        {
            yield return "Down";
            yield return "Left";
            yield return "Right";
            yield return "Up";
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError(message);
        }

        static void ThrowIfFailed(int errors)
        {
            if (errors != 0) throw new InvalidOperationException("Player directional Animator validation failed. errors=" + errors);
        }
    }
}
