using System;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class LichSummonAttackValidator
    {
        const string MenuPath = "Area Survivors/Validate/Lich Summon Attack";
        const string ConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const string EnemyPrefabPath = "Assets/AreaSurvivors/Prefabs/Characters/Enemy.prefab";
        const string ControllerPath = "Assets/AreaSurvivors/Scripts/Game/Weapons/LichSummonAttackController.cs";

        [MenuItem(MenuPath)]
        public static void Validate()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                throw new InvalidOperationException("GameConfig asset was not found.");
            }
            if (!Mathf.Approximately(config.lichSummonDistanceCells, 10f))
            {
                throw new InvalidOperationException("Lich summon distance must be 10 cells.");
            }

            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            var controller = enemyPrefab != null
                ? enemyPrefab.GetComponent<LichSummonAttackController>()
                : null;
            if (controller == null)
            {
                throw new InvalidOperationException("Enemy prefab needs LichSummonAttackController.");
            }
            if (controller.summonCirclePrefab == null)
            {
                throw new InvalidOperationException("Lich summon circle prefab reference is missing.");
            }

            var controllerScript = AssetDatabase.LoadAssetAtPath<MonoScript>(ControllerPath);
            if (controllerScript == null)
            {
                throw new InvalidOperationException("Lich summon attack script was not found.");
            }
            RequireSourceToken(
                controllerScript.text,
                "SpawnSummonCircle(transform.position);",
                "Lich-foot summon circle");
            RequireSourceToken(
                controllerScript.text,
                "SpawnSummonCircle(summonPosition);",
                "remote summon circle");
            RequireSourceToken(
                controllerScript.text,
                "SummonEnemies(summonPosition);",
                "remote enemy summon center");
            RequireSourceToken(
                controllerScript.text,
                "Random.Range(-90f, 90f)",
                "forward 180-degree random angle");
            RequireSourceToken(
                controllerScript.text,
                "enemy != null ? enemy.FacingDirection : direction",
                "movement-facing summon direction");

            var cellSize = new Vector2(1.5f, 0.75f);
            var forward = new Vector2(1f, 0.5f).normalized;
            var cellForward = new Vector2(
                forward.x / cellSize.x,
                forward.y / cellSize.y).normalized;
            float[] angles = { -90f, -45f, 0f, 45f, 90f };
            foreach (float angle in angles)
            {
                Vector2 offset = LichSummonAttackController.CalculateSummonOffset(
                    forward,
                    angle,
                    cellSize,
                    config.lichSummonDistanceCells);
                var cellOffset = new Vector2(
                    offset.x / cellSize.x,
                    offset.y / cellSize.y);
                if (!Mathf.Approximately(cellOffset.magnitude, 10f))
                {
                    throw new InvalidOperationException(
                        "Lich summon position must stay exactly 10 cells away.");
                }
                if (Vector2.Dot(cellForward, cellOffset.normalized) < -0.0001f)
                {
                    throw new InvalidOperationException(
                        "Lich summon angle must stay inside the forward 180-degree sector.");
                }
            }

            Debug.Log(
                "Lich summon attack validator: passed. distance=10 cells, forwardSector=180 degrees, circlePrefab=assigned.");
        }

        static void RequireSourceToken(string source, string token, string label)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException(
                    "Lich summon attack is missing " + label + ".");
            }
        }
    }
}
