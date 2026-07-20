using System.Collections.Generic;
using System.IO;
using AreaSurvivors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class LocalizationCoverageValidator
{
    const string SuccessMarkerPath = "TokenReports/Validation/localization-coverage-validator.success";

    [MenuItem("Area Survivors/Validate/Localization Coverage")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new System.InvalidOperationException(
                "Localization Coverage validation requires Edit Mode because it opens production Scenes.");
        }

        DeleteSuccessMarker();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
            {
                throw new System.InvalidOperationException("Save all open Scenes before running Localization Coverage.");
            }
        }

        var errors = new List<string>();
        var sceneSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (string scenePath in FindProductionScenes())
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                {
                    ValidateTexts(scenePath, root, errors);
                    ValidateSkillNodes(scenePath, root, errors);
                    ValidateWeaponEntries(scenePath, root, errors);
                }
            }

            foreach (string prefabPath in FindUiPrefabs())
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    ValidateTexts(prefabPath, root, errors);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            ValidateCatalogs(errors);
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);
        }

        if (errors.Count > 0)
        {
            foreach (string error in errors) Debug.LogError(error);
            throw new System.InvalidOperationException($"Localization coverage failed with {errors.Count} untranslated values.");
        }

        WriteSuccessMarker();
        Debug.Log("Localization coverage passed for all production Scenes, UI Prefabs, skill descriptions, and weapon descriptions.");
    }

    static void DeleteSuccessMarker()
    {
        if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
    }

    static void WriteSuccessMarker()
    {
        string directory = Path.GetDirectoryName(SuccessMarkerPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(SuccessMarkerPath, System.DateTime.UtcNow.ToString("O"));
    }

    static void ValidateCatalogs(List<string> errors)
    {
        foreach (var definition in RelicCatalog.All)
        {
            if (definition == null) continue;
            ValidateValue("RelicCatalog", null, "Relic name", definition.displayNameSource, errors);
            ValidateValue("RelicCatalog", null, "Relic description", definition.descriptionSource, errors);
            ValidateValue("RelicCatalog", null, "Relic effect", definition.effectTextSource, errors);
        }

        foreach (WeaponType type in System.Enum.GetValues(typeof(WeaponType)))
        {
            ValidateValue("WeaponCatalog", null, "Weapon name", WeaponCatalog.DisplayNameSource(type), errors);
        }
    }

    static void ValidateTexts(string assetPath, GameObject root, List<string> errors)
    {
        foreach (var text in root.GetComponentsInChildren<Text>(true))
        {
            ValidateValue(assetPath, text.transform, "Text", text.text, errors);
        }
    }

    static void ValidateSkillNodes(string assetPath, GameObject root, List<string> errors)
    {
        foreach (var node in root.GetComponentsInChildren<SkillNodeView>(true))
        {
            ValidateValue(assetPath, node.transform, "Skill title", node.title, errors);
            ValidateValue(assetPath, node.transform, "Skill description", node.description, errors);
        }
    }

    static void ValidateWeaponEntries(string assetPath, GameObject root, List<string> errors)
    {
        foreach (var entry in root.GetComponentsInChildren<WeaponBookEntryView>(true))
        {
            ValidateValue(assetPath, entry.transform, "Weapon name", entry.displayName, errors);
            ValidateValue(assetPath, entry.transform, "Weapon feature", entry.featureDescription, errors);
            ValidateValue(assetPath, entry.transform, "Weapon stats", entry.initialStatsText, errors);
            ValidateValue(assetPath, entry.transform, "Weapon special", entry.SpecialEffectDescriptionSource, errors);
        }
    }

    static void ValidateValue(string assetPath, Transform target, string field, string source, List<string> errors)
    {
        if (!LocalizationService.ContainsJapanese(source)) return;
        string english = LocalizationTextCatalog.Translate(source, GameLanguage.English);
        if (!LocalizationService.ContainsJapanese(english)) return;
        errors.Add($"[Localization] {assetPath} :: {HierarchyPath(target)} :: {field} :: {source}");
    }

    static IEnumerable<string> FindProductionScenes()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/AreaSurvivors/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileName(path);
            if (fileName.Length >= 3 && char.IsDigit(fileName[0]) && char.IsDigit(fileName[1])) yield return path;
        }
    }

    static IEnumerable<string> FindUiPrefabs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/AreaSurvivors/Prefabs/UI" }))
        {
            yield return AssetDatabase.GUIDToAssetPath(guid);
        }
    }

    static string HierarchyPath(Transform target)
    {
        if (target == null) return "<none>";
        var names = new List<string>();
        for (var current = target; current != null; current = current.parent) names.Add(current.name);
        names.Reverse();
        return string.Join("/", names);
    }
}
