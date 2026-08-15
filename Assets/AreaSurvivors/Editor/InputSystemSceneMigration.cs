using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace AreaSurvivors.Editor
{
    public static class InputSystemSceneMigration
    {
        const string SceneDirectory = "Assets/AreaSurvivors/Scenes";

        [MenuItem("Area Survivors/Migrate/Input System Event Systems")]
        public static void Migrate()
        {
            EnsureOpenScenesAreClean();
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            int replaced = 0;

            try
            {
                foreach (string scenePath in FindScenePaths())
                {
                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    bool changed = false;
                    foreach (StandaloneInputModule legacyModule in FindInScene<StandaloneInputModule>(scene))
                    {
                        GameObject target = legacyModule.gameObject;
                        UnityEngine.Object.DestroyImmediate(legacyModule);
                        var inputModule = target.GetComponent<InputSystemUIInputModule>();
                        if (inputModule == null) inputModule = target.AddComponent<InputSystemUIInputModule>();
                        inputModule.UnassignActions();
                        EditorUtility.SetDirty(inputModule);
                        changed = true;
                        replaced++;
                    }

                    foreach (InputSystemUIInputModule inputModule in FindInScene<InputSystemUIInputModule>(scene))
                    {
                        if (inputModule.GetComponent<AreaInputSystemUiConfigurator>() != null) continue;
                        inputModule.gameObject.AddComponent<AreaInputSystemUiConfigurator>();
                        changed = true;
                    }

                    if (changed && !EditorSceneManager.SaveScene(scene))
                    {
                        throw new InvalidOperationException("Failed to save Input System migration scene: " + scenePath);
                    }
                }
            }
            finally
            {
                if (originalSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Input System EventSystem migration completed. replaced={replaced}");
        }

        [MenuItem("Area Survivors/Validate/Input System Migration")]
        public static void Validate()
        {
            EnsureOpenScenesAreClean();
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var errors = new List<string>();

            try
            {
                foreach (string scenePath in FindScenePaths())
                {
                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    int eventSystemCount = FindInScene<EventSystem>(scene).Count;
                    int legacyCount = FindInScene<StandaloneInputModule>(scene).Count;
                    int inputSystemCount = FindInScene<InputSystemUIInputModule>(scene).Count;
                    int configuratorCount = FindInScene<AreaInputSystemUiConfigurator>(scene).Count;
                    if (legacyCount != 0)
                    {
                        errors.Add($"{scenePath}: StandaloneInputModule remains ({legacyCount}).");
                    }

                    if (eventSystemCount > 0 && inputSystemCount != eventSystemCount)
                    {
                        errors.Add($"{scenePath}: EventSystem={eventSystemCount}, InputSystemUIInputModule={inputSystemCount}.");
                    }

                    if (eventSystemCount > 0 && configuratorCount != eventSystemCount)
                    {
                        errors.Add($"{scenePath}: EventSystem={eventSystemCount}, AreaInputSystemUiConfigurator={configuratorCount}.");
                    }
                }
            }
            finally
            {
                if (originalSetup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }

            if (errors.Count > 0)
            {
                foreach (string error in errors) Debug.LogError(error);
                throw new InvalidOperationException("Input System migration validation failed. errors=" + errors.Count);
            }

            Debug.Log("Input System migration validation passed.");
        }

        [MenuItem("Area Survivors/Debug/Log Input System Devices")]
        public static void LogInputSystemDevices()
        {
            InputSystem.Update();
            string devices = InputSystem.devices.Count == 0
                ? "(none)"
                : string.Join("\n", InputSystem.devices.Select(device =>
                    $"name={device.name}, layout={device.layout}, class={device.description.deviceClass}, " +
                    $"interface={device.description.interfaceName}, manufacturer={device.description.manufacturer}, " +
                    $"product={device.description.product}, enabled={device.enabled}"));
            var unsupportedDevices = InputSystem.GetUnsupportedDevices();
            string unsupported = unsupportedDevices.Count == 0
                ? "(none)"
                : string.Join("\n", unsupportedDevices.Select(device =>
                    $"class={device.deviceClass}, interface={device.interfaceName}, " +
                    $"manufacturer={device.manufacturer}, product={device.product}, " +
                    $"capabilitiesLength={device.capabilities?.Length ?? 0}"));
            string supportedLayouts = InputSystem.settings.supportedDevices.Count == 0
                ? "(all)"
                : string.Join(", ", InputSystem.settings.supportedDevices);
            Debug.Log($"Input System devices:\n{devices}\nSupported layouts: {supportedLayouts}\nUnsupported devices:\n{unsupported}");
        }

        static string[] FindScenePaths()
        {
            return AssetDatabase.FindAssets("t:Scene", new[] { SceneDirectory })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        static List<T> FindInScene<T>(Scene scene) where T : Component
        {
            var result = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                result.AddRange(root.GetComponentsInChildren<T>(true));
            }

            return result;
        }

        static void EnsureOpenScenesAreClean()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.isDirty)
                {
                    throw new InvalidOperationException("Save or discard current Scene changes before Input System migration: " + scene.path);
                }
            }
        }
    }
}
