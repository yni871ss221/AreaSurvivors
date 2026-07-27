using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class NormalEnemyPlayerTargetingValidator
    {
        public const string MenuPath = "Area Survivors/Validate/Normal Enemy Player Targeting";
        const string ConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const string MarkerRelativePath = "Library/AreaSafeUnity/normal-enemy-player-targeting-validator.success";

        [MenuItem(MenuPath)]
        public static void Validate()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string markerPath = Path.Combine(projectRoot, MarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) throw new InvalidOperationException("GameConfig asset was not found.");
            if (!Mathf.Approximately(config.normalEnemyPlayerAggroRangeCells, 5f))
            {
                throw new InvalidOperationException("Normal enemy player aggro range must be 5 cells.");
            }

            Vector2 cellSize = new Vector2(2f, 1f);
            AssertDecision(true, false, false, Vector2.zero, new Vector2(10f, 0f), new Vector2(15f, 0f), cellSize, "exactly 5 cells and closer");
            AssertDecision(true, false, false, Vector2.zero, new Vector2(0f, 4f), new Vector2(0f, 8f), cellSize, "inside range and closer");
            AssertDecision(false, false, false, Vector2.zero, new Vector2(10.01f, 0f), new Vector2(15f, 0f), cellSize, "outside 5 cells");
            AssertDecision(false, false, false, Vector2.zero, new Vector2(8f, 0f), new Vector2(6f, 0f), cellSize, "tower is closer");
            AssertDecision(false, false, false, Vector2.zero, new Vector2(8f, 0f), new Vector2(-8f, 0f), cellSize, "equal distance");
            AssertDecision(false, true, false, Vector2.zero, new Vector2(2f, 0f), new Vector2(20f, 0f), cellSize, "boss remains on tower");
            AssertDecision(false, false, true, Vector2.zero, new Vector2(2f, 0f), new Vector2(20f, 0f), cellSize, "elite remains on tower");

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, "passed");
            Debug.Log("Normal enemy player targeting validation passed.");
        }

        static void AssertDecision(
            bool expected,
            bool isBoss,
            bool isElite,
            Vector2 enemyPosition,
            Vector2 playerPosition,
            Vector2 towerPosition,
            Vector2 cellSize,
            string label)
        {
            bool actual = EnemyController.ShouldChasePlayer(
                isBoss,
                isElite,
                enemyPosition,
                playerPosition,
                towerPosition,
                cellSize,
                5f);
            if (actual != expected)
            {
                throw new InvalidOperationException($"Player targeting decision failed: {label}. Expected={expected}, Actual={actual}");
            }
        }
    }
}
