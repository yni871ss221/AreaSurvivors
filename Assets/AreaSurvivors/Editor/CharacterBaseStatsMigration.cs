using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class CharacterBaseStatsMigration
    {
        const string ConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const string MigrationMarkerPath = "Library/AreaSafeUnity/character-base-stats-migration.success";
        const string ValidatorMarkerPath = "Library/AreaSafeUnity/character-base-stats-validator.success";

        [MenuItem("Area Survivors/Migrate/Character Base Stats")]
        public static void Migrate()
        {
            DeleteMarker(MigrationMarkerPath);
            var config = RequireConfig();
            config.knightBaseStats = CharacterBaseStatsDefinition.Create(40, 2.1f, 1, 6f, 3, 1.1f, 0);
            config.archerBaseStats = CharacterBaseStatsDefinition.Create(30, 2.4f, 1, 6f, 1, 1f, 0);
            config.mageBaseStats = CharacterBaseStatsDefinition.Create(20, 1.8f, 2, 6f, 0, 1.3f, 1);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            WriteMarker(MigrationMarkerPath);
            Debug.Log("Character base stats migration completed.");
        }

        [MenuItem("Area Survivors/Validate/Character Base Stats")]
        public static void Validate()
        {
            DeleteMarker(ValidatorMarkerPath);
            var config = RequireConfig();
            int errors = 0;
            ValidateProfile(config, CharacterType.Knight, 40, 2.1f, 1, 6f, 3, 1.1f, 0, ref errors);
            ValidateProfile(config, CharacterType.Archer, 30, 2.4f, 1, 6f, 1, 1f, 0, ref errors);
            ValidateProfile(config, CharacterType.Mage, 20, 1.8f, 2, 6f, 0, 1.3f, 1, ref errors);
            if (!Mathf.Approximately(config.autoRegenIntervalSeconds, 2f)) Error("Auto regeneration interval must be 2 seconds.", ref errors);

            if (errors > 0) throw new InvalidOperationException($"Character base stats validation failed with {errors} error(s).");
            WriteMarker(ValidatorMarkerPath);
            Debug.Log("Character base stats validation passed.");
        }

        static void ValidateProfile(
            GameConfig config,
            CharacterType type,
            int maxHp,
            float moveSpeed,
            int paintRadius,
            float reviveSeconds,
            int defense,
            float xpGainMultiplier,
            int autoRegen,
            ref int errors)
        {
            var profile = config.GetCharacterBaseStats(type);
            if (profile == null)
            {
                Error(type + " base stats are missing.", ref errors);
                return;
            }

            if (profile.maxHp != maxHp) Error(type + " max HP is incorrect.", ref errors);
            if (!Mathf.Approximately(profile.moveSpeed, moveSpeed)) Error(type + " move speed is incorrect.", ref errors);
            if (profile.paintRadius != paintRadius) Error(type + " paint radius is incorrect.", ref errors);
            if (!Mathf.Approximately(profile.reviveSeconds, reviveSeconds)) Error(type + " revive time is incorrect.", ref errors);
            if (profile.defense != defense) Error(type + " defense is incorrect.", ref errors);
            if (!Mathf.Approximately(profile.xpGainMultiplier, xpGainMultiplier)) Error(type + " XP multiplier is incorrect.", ref errors);
            if (profile.autoRegen != autoRegen) Error(type + " auto regeneration is incorrect.", ref errors);
        }

        static GameConfig RequireConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null) throw new InvalidOperationException("GameConfig is missing: " + ConfigPath);
            return config;
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError("[Character Base Stats] " + message);
        }

        static void DeleteMarker(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        static void WriteMarker(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, DateTime.UtcNow.ToString("o"));
        }
    }
}
