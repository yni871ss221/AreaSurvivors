using System.Collections.Generic;
using AreaSurvivors.Testing;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    [CustomEditor(typeof(GameplayTestScenario))]
    public sealed class GameplayTestScenarioEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var scenario = (GameplayTestScenario)target;
            EditorGUILayout.Space(8f);
            foreach (string warning in Validate(scenario)) EditorGUILayout.HelpBox(warning, MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use In Gameplay Test")) GameplayTestTools.UseScenarioAsset(scenario);
                if (GUILayout.Button("Run Scenario"))
                {
                    GameplayTestTools.UseScenarioAsset(scenario);
                    EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
                }
            }
        }

        static List<string> Validate(GameplayTestScenario scenario)
        {
            var warnings = new List<string>();
            if (scenario.assertions == null || scenario.assertions.Length == 0)
                warnings.Add("Assertionsがありません。再現だけでなく、期待結果も設定することを推奨します。");

            foreach (var placement in scenario.prefabs ?? System.Array.Empty<GameplayTestScenario.PrefabPlacement>())
            {
                if (placement != null && placement.prefab == null) warnings.Add("Prefab Placementに未設定のPrefabがあります。");
            }

            foreach (var placement in scenario.landmarks ?? System.Array.Empty<GameplayTestScenario.LandmarkPlacement>())
            {
                if (placement != null && string.IsNullOrWhiteSpace(placement.landmarkName))
                    warnings.Add("Landmark Placementに名前未設定の項目があります。");
            }

            foreach (var configOverride in scenario.configOverrides ?? System.Array.Empty<GameplayTestScenario.ConfigOverride>())
            {
                if (configOverride != null && string.IsNullOrWhiteSpace(configOverride.fieldName))
                    warnings.Add("Config OverrideにField Name未設定の項目があります。");
            }

            foreach (var action in scenario.scheduledActions ?? System.Array.Empty<GameplayTestScenario.ScheduledAction>())
            {
                if (action != null && RequiresTargetObject(action.type) && string.IsNullOrWhiteSpace(action.objectName))
                    warnings.Add("Scheduled ActionにObject Name未設定の項目があります。");
            }

            return warnings;
        }

        static bool RequiresTargetObject(GameplayTestActionType type)
        {
            switch (type)
            {
                case GameplayTestActionType.LevelUpSlashWeapon:
                case GameplayTestActionType.LevelUpArrowWeapon:
                case GameplayTestActionType.LevelUpFireballWeapon:
                    return false;
                default:
                    return true;
            }
        }
    }
}
