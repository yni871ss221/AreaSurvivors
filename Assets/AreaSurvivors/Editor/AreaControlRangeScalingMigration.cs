using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class AreaControlRangeScalingMigration
    {
        const string GameConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const string MarkerRelativePath = "Library/AreaSafeUnity/area-control-range-scaling-migration.ok";

        [MenuItem("Area Survivors/Migrations/Apply Area Control Range Scaling")]
        public static void Apply()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string markerPath = Path.Combine(projectRoot, MarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
            {
                throw new InvalidOperationException("GameConfig is missing: " + GameConfigPath);
            }

            config.areaControlRangeScaleStartRatio = 0.5f;
            config.areaControlRangeScaleFullRatio = 1f;
            config.areaControlRangeScaleMaxMultiplier = 2f;
            config.areaControlRangeEvaluationIntervalSeconds = 1f;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Area control range scaling migration: applied x1 through 50%, linear scaling to x2 at 100%, evaluated once per second.");
        }
    }
}
