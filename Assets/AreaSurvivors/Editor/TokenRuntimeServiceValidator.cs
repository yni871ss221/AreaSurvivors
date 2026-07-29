using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class TokenRuntimeServiceValidator
    {
        const string MenuPath = "Area Survivors/Validate/Token Runtime Service";
        const string SuccessMarkerPath = "Library/AreaSafeUnity/token-runtime-service-validator.success";

        [MenuItem(MenuPath)]
        public static void ValidateMenu()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);

            var serviceType = typeof(GameManager).Assembly.GetType("AreaSurvivors.TokenRuntimeService", true);
            var service = Activator.CreateInstance(serviceType, true);

            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.tokenKillsDivisor = 3;
                Require((int)Invoke(serviceType, service, "AwardKillTokens", false, config) == 0,
                    "First kill reward changed.");
                Require((int)Invoke(serviceType, service, "AwardKillTokens", false, config) == 0,
                    "Second kill reward changed.");
                Require((int)Invoke(serviceType, service, "AwardKillTokens", false, config) == 1,
                    "Kill threshold reward changed.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }

            Require((int)Invoke(serviceType, service, "AwardElapsedTimeTokens", 29.9f, false) == 0,
                "Elapsed reward occurred early.");
            Require((int)Invoke(serviceType, service, "AwardElapsedTimeTokens", 30f, false) == 1,
                "Elapsed reward threshold changed.");
            Require((int)Invoke(serviceType, service, "AwardElapsedTimeTokens", 91f, false) == 2,
                "Elapsed catch-up reward changed.");

            var tokenResult = Invoke(serviceType, service, "AddRunTokens", 9, RunTokenSource.TokenOrb);
            Require(Field<int>(tokenResult.GetType(), tokenResult, "gained") == 9, "Token gain amount changed.");
            Require(!Field<bool>(tokenResult.GetType(), tokenResult, "attackTierChanged"),
                "Attack tier changed below ten tokens.");
            tokenResult = Invoke(serviceType, service, "AddRunTokens", 1, RunTokenSource.KillMilestone);
            Require(Field<bool>(tokenResult.GetType(), tokenResult, "attackTierChanged"),
                "Attack tier did not change at ten tokens.");
            Require(Property<int>(serviceType, service, "RunTokens") == 10, "Run token balance changed.");
            Require(Property<int>(serviceType, service, "TokenOrbTokens") == 9, "Token orb breakdown changed.");
            Require(Property<int>(serviceType, service, "KillMilestoneTokens") == 1,
                "Kill milestone breakdown changed.");

            Invoke(serviceType, service, "AddRelicDuplicateTokens", 4);
            Invoke(serviceType, service, "AddRelicDuplicateTokens", -2);
            Require(Property<int>(serviceType, service, "RelicDuplicateTokens") == 4,
                "Duplicate relic token breakdown changed.");

            Invoke(serviceType, service, "SetElapsedTokenRewardSchedule", 61f);
            Require(Mathf.Approximately(
                    Property<float>(serviceType, service, "NextElapsedTokenRewardSeconds"),
                    90f),
                "Elapsed reward schedule changed.");

            ValidateDifficultyTelemetry();

            Directory.CreateDirectory(Path.GetDirectoryName(SuccessMarkerPath));
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log("Token runtime service validation: passed.");
        }

        static void ValidateDifficultyTelemetry()
        {
            var telemetryType = typeof(GameManager).Assembly.GetType("AreaSurvivors.RunDifficultyTelemetry", true);
            var telemetry = Activator.CreateInstance(telemetryType, true);
            Invoke(telemetryType, telemetry, "Reset");
            Invoke(telemetryType, telemetry, "RecordExperience", 14, 35);
            Invoke(
                telemetryType,
                telemetry,
                "RecordLevelUp",
                2,
                1,
                12f,
                4,
                1,
                8,
                2.5f,
                "experience");
            Invoke(telemetryType, telemetry, "RecordUpgrade", 2, 1, 12f, "Validator Upgrade");

            Require(Property<int>(telemetryType, telemetry, "BaseExperienceCollected") == 14,
                "Difficulty telemetry base XP aggregation changed.");
            Require(Property<int>(telemetryType, telemetry, "AppliedExperienceGained") == 35,
                "Difficulty telemetry applied XP aggregation changed.");
            var levels = (System.Collections.IList)Invoke(telemetryType, telemetry, "BuildLevelUps");
            var upgrades = (System.Collections.IList)Invoke(telemetryType, telemetry, "BuildUpgradeHistory");
            Require(levels.Count == 1, "Difficulty telemetry level history changed.");
            Require(upgrades.Count == 1, "Difficulty telemetry upgrade history changed.");

            var logEntry = new TokenRunLogEntry();
            string json = JsonUtility.ToJson(logEntry);
            Require(logEntry.schemaVersion == 3, "Token run log schema version changed.");
            Require(json.Contains("\"difficultyCheckpoints\""), "Difficulty checkpoint JSON field is missing.");
            Require(json.Contains("\"enemyStats\""), "Enemy stats JSON field is missing.");
            Require(json.Contains("\"buildingStats\""), "Building stats JSON field is missing.");
            Require(json.Contains("\"damageReport\""), "Damage report JSON field is missing.");

            RequireSourceContains(
                "Assets/AreaSurvivors/Scripts/Game/Characters/EnemySpawner.cs",
                "RegisterEnemySpawn(enemy);");
            RequireSourceContains(
                "Assets/AreaSurvivors/Scripts/Game/Characters/EnemyController.cs",
                "RegisterKill(this);");
            RequireSourceContains(
                "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.RunStage.cs",
                "RecordDifficultyCheckpoint(\"boss_spawn\")");
            RequireSourceContains(
                "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.RunEnd.cs",
                "difficultyCheckpoints = runDifficultyTelemetry.BuildCheckpoints()");
            RequireSourceContains(
                "Assets/AreaSurvivors/Scripts/Game/Characters/Health.cs",
                "LastDamageDealt = dealt;");
        }

        static object Invoke(Type type, object target, string methodName, params object[] arguments)
        {
            var method = type.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("Method is missing: " + methodName);
            return method.Invoke(target, arguments);
        }

        static T Property<T>(Type type, object target, string propertyName)
        {
            var property = type.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null) throw new InvalidOperationException("Property is missing: " + propertyName);
            return (T)property.GetValue(target);
        }

        static T Field<T>(Type type, object target, string fieldName)
        {
            var field = type.GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Field is missing: " + fieldName);
            return (T)field.GetValue(target);
        }

        static void RequireSourceContains(string path, string sentinel)
        {
            Require(File.Exists(path), "Telemetry source is missing: " + path);
            string source = File.ReadAllText(path);
            Require(source.Contains(sentinel), "Telemetry wiring is missing: " + sentinel);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
