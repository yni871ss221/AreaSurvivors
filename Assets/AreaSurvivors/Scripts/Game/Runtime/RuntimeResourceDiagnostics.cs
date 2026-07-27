using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

namespace AreaSurvivors
{
    internal sealed class RuntimeResourceDiagnostics : IDisposable
    {
        ProfilerRecorder materialCountRecorder;
        ProfilerRecorder meshCountRecorder;
        ProfilerRecorder textureCountRecorder;
        ProfilerRecorder gameObjectCountRecorder;
        ProfilerRecorder totalUsedMemoryRecorder;

        public void Start()
        {
            StartProfilerRecorder(ref materialCountRecorder, "Material Count");
            StartProfilerRecorder(ref meshCountRecorder, "Mesh Count");
            StartProfilerRecorder(ref textureCountRecorder, "Texture Count");
            StartProfilerRecorder(ref gameObjectCountRecorder, "Game Object Count");
            StartProfilerRecorder(ref totalUsedMemoryRecorder, "Total Used Memory");
        }

        public void Dispose()
        {
            DisposeProfilerRecorder(ref materialCountRecorder);
            DisposeProfilerRecorder(ref meshCountRecorder);
            DisposeProfilerRecorder(ref textureCountRecorder);
            DisposeProfilerRecorder(ref gameObjectCountRecorder);
            DisposeProfilerRecorder(ref totalUsedMemoryRecorder);
        }

        public void LogSnapshot(string label)
        {
            var rendererMaterialStats = CollectRendererMaterialStats();
            Debug.Log(
                "Runtime object snapshot before scene transition"
                + $" ({label}): "
                + $"GameObject={FormatRecorderValue(gameObjectCountRecorder)}, "
                + $"Mesh={FormatRecorderValue(meshCountRecorder)}, "
                + $"Material={FormatRecorderValue(materialCountRecorder)}, "
                + $"Texture={FormatRecorderValue(textureCountRecorder)}, "
                + $"TotalUsedMemory={FormatBytesRecorderValue(totalUsedMemoryRecorder)}, "
                + $"Renderer={rendererMaterialStats.rendererCount}, "
                + $"RendererMaterialSlots={rendererMaterialStats.materialSlotCount}, "
                + $"UniqueSharedMaterials={rendererMaterialStats.uniqueSharedMaterialCount}, "
                + $"NullMaterialSlots={rendererMaterialStats.nullMaterialSlotCount}, "
                + $"PaperMeshVisual={CountSceneObjects<PaperMeshVisual>()}, "
                + $"RuntimeSpriteOutline={CountSceneObjects<RuntimeSpriteOutline>()}, "
                + $"PixelBurstEffect={CountSceneObjects<PixelBurstEffect>()}, "
                + $"Projectile={CountSceneObjects<Projectile>()}, "
                + $"TokenOrb={CountSceneObjects<TokenOrb>()}");
        }

        static void StartProfilerRecorder(ref ProfilerRecorder recorder, string statName)
        {
            try
            {
                recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, statName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to start profiler recorder '{statName}': {exception.Message}");
            }
        }

        static void DisposeProfilerRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid) recorder.Dispose();
            recorder = default;
        }

        static string FormatRecorderValue(ProfilerRecorder recorder)
        {
            return recorder.Valid ? recorder.LastValue.ToString() : "unavailable";
        }

        static string FormatBytesRecorderValue(ProfilerRecorder recorder)
        {
            if (!recorder.Valid) return "unavailable";
            return $"{recorder.LastValue / (1024f * 1024f):0.0}MB";
        }

        static int CountSceneObjects<T>() where T : UnityEngine.Object
        {
            return UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None).Length;
        }

        static RendererMaterialStats CollectRendererMaterialStats()
        {
            var stats = new RendererMaterialStats();
            var uniqueMaterialIds = new HashSet<int>();
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            stats.rendererCount = renderers.Length;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                var materials = renderer.sharedMaterials;
                stats.materialSlotCount += materials.Length;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null)
                    {
                        stats.nullMaterialSlotCount++;
                        continue;
                    }

                    uniqueMaterialIds.Add(material.GetInstanceID());
                }
            }

            stats.uniqueSharedMaterialCount = uniqueMaterialIds.Count;
            return stats;
        }

        struct RendererMaterialStats
        {
            public int rendererCount;
            public int materialSlotCount;
            public int uniqueSharedMaterialCount;
            public int nullMaterialSlotCount;
        }
    }
}
