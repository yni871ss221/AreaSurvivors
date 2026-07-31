using System;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class UiButtonFocusSfxValidator
    {
        const string ClipResourcePath = "Audio/SFX/cursor_move_2";
        const string ClipAssetPath = "Assets/AreaSurvivors/Resources/Audio/SFX/cursor_move_2.wav";
        const float OnsetThreshold = 0.001f;
        const float MaximumOnsetMilliseconds = 2f;

        [MenuItem("Area Survivors/Validate/UI Button Focus SFX")]
        public static void ValidateFromMenu()
        {
            try
            {
                if (AudioCatalog.SfxPath(SfxTrack.ButtonFocus) != ClipResourcePath)
                    throw new InvalidOperationException("ButtonFocus must map to " + ClipResourcePath + ".");

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipAssetPath);
                if (clip == null)
                    throw new InvalidOperationException("UI button focus SFX is missing at " + ClipAssetPath + ".");
                if (clip.length <= 0f)
                    throw new InvalidOperationException("UI button focus SFX must contain audio data.");

                var importer = AssetImporter.GetAtPath(ClipAssetPath) as AudioImporter;
                if (importer == null)
                    throw new InvalidOperationException("UI button focus SFX AudioImporter is missing.");
                var settings = importer.defaultSampleSettings;
                if (settings.loadType != AudioClipLoadType.DecompressOnLoad || !settings.preloadAudioData)
                    throw new InvalidOperationException("UI button focus SFX must preload with Decompress On Load.");
                if (settings.compressionFormat != AudioCompressionFormat.PCM)
                    throw new InvalidOperationException("UI button focus SFX must use PCM compression.");
                if (importer.loadInBackground)
                    throw new InvalidOperationException("UI button focus SFX must not load in the background.");

                float onsetMilliseconds = FindOnsetMilliseconds(clip);
                if (onsetMilliseconds < 0f || onsetMilliseconds > MaximumOnsetMilliseconds)
                    throw new InvalidOperationException(
                        "UI button focus SFX onset must be within " + MaximumOnsetMilliseconds +
                        "ms, but was " + onsetMilliseconds + "ms.");

                Debug.Log("UI Button Focus SFX validator passed.");
            }
            catch (Exception exception)
            {
                Debug.LogError("UI Button Focus SFX validator failed: " + exception.Message);
            }
        }

        static float FindOnsetMilliseconds(AudioClip clip)
        {
            if (!clip.LoadAudioData()) return -1f;
            int channels = Mathf.Max(1, clip.channels);
            var samples = new float[clip.samples * channels];
            if (!clip.GetData(samples, 0)) return -1f;

            int frames = samples.Length / channels;
            for (int frame = 0; frame < frames; frame++)
            {
                int offset = frame * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    if (Mathf.Abs(samples[offset + channel]) >= OnsetThreshold)
                        return frame * 1000f / clip.frequency;
                }
            }

            return -1f;
        }
    }
}
