using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class GroundStrikeEvolutionValidator
    {
        const string ConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const string ArrowShowerPrefabPath = "Assets/AreaSurvivors/Prefabs/Weapons/ArrowShowerStrike.prefab";
        const string FrameRoot = "Assets/AreaSurvivors/Sprites/Generated/Weapons/ArrowShowerImpactFrame";
        const string AnimationClipPath = "Assets/AreaSurvivors/Animations/Weapons/ArrowShower/ArrowShowerFall.anim";
        const string AnimatorControllerPath = "Assets/AreaSurvivors/Animations/Weapons/ArrowShower/ArrowShower.controller";
        const string AnimatorVisualObjectName = "Arrow Shower Animator Visual";
        const string CompletionMarkerRelativePath = "Library/AreaSafeUnity/ground-strike-evolution-validator.ok";
        static readonly Vector2 ExpectedImpactPivot = new Vector2(90f / 192f, 22f / 192f);

        [MenuItem("Area Survivors/Validate/Ground Strike Range And Animation")]
        public static void ValidateMenu()
        {
            string markerPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, CompletionMarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);
            int errors = 0;
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null || !Mathf.Approximately(config.evolvedGroundStrikeTargetRadiusCells, 15f))
            {
                Error("Ground strike target radius must be 15 cells.", ref errors);
            }

            float cellWidth = TileGrid.DefaultCellSize;
            Vector2 origin = Vector2.zero;
            if (!AdvancedWeaponRuntime.IsWithinGroundStrikeTargetRadius(origin, Vector2.right * 15f * cellWidth, cellWidth, 15f) ||
                AdvancedWeaponRuntime.IsWithinGroundStrikeTargetRadius(origin, Vector2.right * 15.01f * cellWidth, cellWidth, 15f))
            {
                Error("Ground strike fixed-radius boundary contract is invalid.", ref errors);
            }

            var expectedFrames = new Sprite[3];
            for (int i = 0; i < expectedFrames.Length; i++)
            {
                string path = FrameRoot + (i + 1).ToString("00") + ".png";
                expectedFrames[i] = ValidateFrame(path, ref errors);
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowShowerPrefabPath);
            var rangeVisual = prefab != null ? prefab.GetComponent<ArrowRainAreaVisual>() : null;
            if (prefab == null)
            {
                Error("ArrowShowerStrike prefab is missing.", ref errors);
            }
            else
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) != 0)
                {
                    Error("ArrowShowerStrike must not contain the removed legacy runtime animator script.", ref errors);
                }
                if (prefab.GetComponentsInChildren<PaperMeshVisual>(true).Length != 0)
                {
                    Error("ArrowShowerStrike must not retain the legacy PaperMeshVisual animation object.", ref errors);
                }
            }
            ValidateAnimatorPlayback(prefab, expectedFrames, ref errors);
            ValidateRangeVisual(prefab, rangeVisual, ref errors);

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(FrameRoot + "04.png") != null)
            {
                Error("Obsolete Arrow Shower water-splash frame 4 must be removed.", ref errors);
            }

            if (errors != 0) throw new InvalidOperationException("Ground strike range and animation validation failed. errors=" + errors);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Ground strike range and animation validator: passed. Animator-only fall=6 keys, legacy runtime animation=removed, radius=15 cells.");
        }

        static void ValidateAnimatorPlayback(GameObject prefab, Sprite[] expectedFrames, ref int errors)
        {
            var visualTransform = prefab != null ? prefab.transform.Find(AnimatorVisualObjectName) : null;
            var spriteRenderer = visualTransform != null ? visualTransform.GetComponent<SpriteRenderer>() : null;
            var unityAnimator = visualTransform != null ? visualTransform.GetComponent<Animator>() : null;
            var playback = visualTransform != null ? visualTransform.GetComponent<GroundStrikeAnimatorPlayback>() : null;
            var billboard = visualTransform != null ? visualTransform.GetComponent<PaperBillboard>() : null;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationClipPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);

            if (visualTransform == null || spriteRenderer == null || unityAnimator == null || playback == null || billboard != null ||
                !visualTransform.gameObject.activeSelf || !ApproximatelyScale(visualTransform.localScale, 2f) ||
                !ApproximatelyZeroPitch(visualTransform.localEulerAngles) ||
                spriteRenderer.sprite != expectedFrames[0] || !unityAnimator.enabled || unityAnimator.runtimeAnimatorController != controller ||
                !playback.enabled || playback.Animator != unityAnimator || playback.AnimationClip != clip)
            {
                Error("Arrow Shower Animator visual prefab references or transform settings are invalid.", ref errors);
                return;
            }

            if (clip == null || clip.isLooping || !Mathf.Approximately(clip.frameRate, 100f) ||
                !Mathf.Approximately(clip.length, 0.41f) || !Mathf.Approximately(playback.ImpactDelaySeconds, 0.32f))
            {
                Error("Arrow Shower Animator clip timing or loop settings are invalid.", ref errors);
                return;
            }

            var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            if (bindings.Length != 1 || bindings[0].path != string.Empty || bindings[0].type != typeof(SpriteRenderer) ||
                bindings[0].propertyName != "m_Sprite")
            {
                Error("Arrow Shower Animator clip must contain one SpriteRenderer.m_Sprite binding.", ref errors);
                return;
            }

            var keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
            if (keys == null || keys.Length != 6 || keys[0].value != expectedFrames[0] || keys[1].value != expectedFrames[0] ||
                keys[2].value != expectedFrames[1] || keys[3].value != expectedFrames[1] ||
                keys[4].value != expectedFrames[2] || keys[5].value != expectedFrames[2] ||
                !Mathf.Approximately(keys[0].time, 0f) || !Mathf.Approximately(keys[1].time, 0.08f) ||
                !Mathf.Approximately(keys[2].time, 0.16f) || !Mathf.Approximately(keys[3].time, 0.24f) ||
                !Mathf.Approximately(keys[4].time, 0.32f) || !Mathf.Approximately(keys[5].time, 0.4f))
            {
                Error("Arrow Shower Animator sprite keyframes must contain six staged fall/impact keys.", ref errors);
            }

            var floatBindings = AnimationUtility.GetCurveBindings(clip);
            if (floatBindings.Length != 1 || floatBindings[0].path != string.Empty || floatBindings[0].type != typeof(Transform) ||
                floatBindings[0].propertyName != "m_LocalPosition.y")
            {
                Error("Arrow Shower Animator clip must animate only Transform.m_LocalPosition.y in addition to the Sprite.", ref errors);
            }
            else
            {
                var positionCurve = AnimationUtility.GetEditorCurve(clip, floatBindings[0]);
                var positionKeys = positionCurve != null ? positionCurve.keys : null;
                float[] expectedTimes = { 0f, 0.08f, 0.16f, 0.24f, 0.32f, 0.4f };
                float[] expectedHeights = { 1.6f, 1.25f, 0.9f, 0.45f, 0f, 0f };
                if (positionKeys == null || positionKeys.Length != expectedTimes.Length)
                {
                    Error("Arrow Shower Animator position curve must contain six fall-height keys.", ref errors);
                }
                else
                {
                    for (int i = 0; i < positionKeys.Length; i++)
                    {
                        if (!Mathf.Approximately(positionKeys[i].time, expectedTimes[i]) ||
                            !Mathf.Approximately(positionKeys[i].value, expectedHeights[i]))
                        {
                            Error("Arrow Shower Animator position key mismatch: " + i, ref errors);
                        }
                    }
                }
            }

            var events = AnimationUtility.GetAnimationEvents(clip);
            if (events.Length != 0)
            {
                Error("Arrow Shower Animator clip must not depend on non-persistent Animation Events.", ref errors);
            }
        }

        static void ValidateRangeVisual(GameObject prefab, ArrowRainAreaVisual rangeVisual, ref int errors)
        {
            if (rangeVisual == null)
            {
                Error("ArrowShowerStrike must contain the Arrow Rain attack-range visual.", ref errors);
                return;
            }

            var serialized = new SerializedObject(rangeVisual);
            var fillFilter = serialized.FindProperty("fillMeshFilter")?.objectReferenceValue as MeshFilter;
            var fillRenderer = serialized.FindProperty("fillRenderer")?.objectReferenceValue as MeshRenderer;
            var outline = serialized.FindProperty("outlineRenderer")?.objectReferenceValue as LineRenderer;
            var arrowVisual = serialized.FindProperty("arrowVisual")?.objectReferenceValue;
            var arrowVisuals = serialized.FindProperty("arrowVisuals");
            var frames = serialized.FindProperty("frames");
            var rangeContainer = prefab != null ? prefab.transform.Find("Arrow Shower Attack Range Visual Mesh Only") : null;
            var legacyRangeContainer = prefab != null ? prefab.transform.Find("Arrow Shower Attack Range Visual") : null;
            var legacyRootSprite = prefab != null ? prefab.transform.Find("Ellipse Range Outline") : null;
            var area = prefab != null ? prefab.GetComponent<AdvancedWeaponArea>() : null;
            var scaleRoot = area != null
                ? new SerializedObject(area).FindProperty("visualScaleRoot")?.objectReferenceValue as Transform
                : null;
            if (fillFilter == null || fillRenderer == null || outline == null || arrowVisual != null ||
                rangeContainer == null || legacyRangeContainer != null || legacyRootSprite != null ||
                rangeContainer.GetComponentsInChildren<PaperMeshVisual>(true).Length != 0 ||
                rangeContainer.GetComponentsInChildren<PaperBillboard>(true).Length != 0 ||
                scaleRoot != rangeContainer || !ApproximatelyOne(rangeContainer.localScale) ||
                (fillFilter != null && !fillFilter.transform.IsChildOf(rangeContainer)) ||
                (fillRenderer != null && !fillRenderer.transform.IsChildOf(rangeContainer)) ||
                (outline != null && !outline.transform.IsChildOf(rangeContainer)) ||
                (arrowVisuals != null && arrowVisuals.arraySize != 0) || (frames != null && frames.arraySize != 0))
            {
                Error("ArrowShowerStrike range visual must be the sole radius-scaled root and contain only the Arrow Rain fill/outline.", ref errors);
            }
        }

        static bool ApproximatelyOne(Vector3 scale)
        {
            return Vector3.SqrMagnitude(scale - Vector3.one) <= 0.000001f;
        }

        static bool ApproximatelyScale(Vector3 scale, float expected)
        {
            return Vector3.SqrMagnitude(scale - Vector3.one * expected) <= 0.000001f;
        }

        static bool ApproximatelyZeroPitch(Vector3 euler)
        {
            return Mathf.Abs(Mathf.DeltaAngle(0f, euler.x)) <= 0.001f &&
                Mathf.Abs(Mathf.DeltaAngle(0f, euler.y)) <= 0.001f;
        }

        static Sprite ValidateFrame(string path, ref int errors)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            var settings = new TextureImporterSettings();
            if (importer != null) importer.ReadTextureSettings(settings);
            if (sprite == null || texture == null || texture.width != 192 || texture.height != 192 || importer == null ||
                importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 384f) ||
                settings.spriteAlignment != (int)SpriteAlignment.Custom ||
                Vector2.Distance(settings.spritePivot, ExpectedImpactPivot) > 0.0001f || importer.mipmapEnabled ||
                importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                !importer.alphaIsTransparency)
            {
                Error("Arrow Shower animation frame import settings are invalid: " + path, ref errors);
            }
            return sprite;
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError(message);
        }
    }
}
