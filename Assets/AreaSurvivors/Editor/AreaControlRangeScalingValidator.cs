using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class AreaControlRangeScalingValidator
    {
        const string GameConfigPath = "Assets/AreaSurvivors/Resources/Config/GameConfig.asset";
        const string MarkerRelativePath = "Library/AreaSafeUnity/area-control-range-scaling-validator.ok";
        const string DescriptionSource = "エリア占有率50%までは攻撃範囲1倍、50%を超えると増加し100%で2倍";
        const string EnglishDescription = "Attack area stays at x1 through 50% area control, then scales up to x2 at 100%";
        const float Epsilon = 0.0001f;

        [MenuItem("Area Survivors/Validate/Area Control Range Scaling")]
        public static void ValidateMenu()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string markerPath = Path.Combine(projectRoot, MarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            int errors = ValidateAssets();
            if (errors != 0)
            {
                throw new InvalidOperationException("Area control range scaling validation failed. errors=" + errors);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("o"));
            Debug.Log("Area control range scaling validator: passed.");
        }

        public static int ValidateAssets()
        {
            int errors = 0;
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
            {
                Error("GameConfig is missing.", ref errors);
                return errors;
            }

            if (!Approximately(config.areaControlRangeScaleStartRatio, 0.5f) ||
                !Approximately(config.areaControlRangeScaleFullRatio, 1f) ||
                !Approximately(config.areaControlRangeScaleMaxMultiplier, 2f) ||
                !Approximately(config.areaControlRangeEvaluationIntervalSeconds, 1f))
            {
                Error("Area control range scaling config must be 50% start, 100% full, x2 maximum, and 1-second evaluation.", ref errors);
            }

            ValidateMultiplier(0f, 1f, ref errors);
            ValidateMultiplier(0.5f, 1f, ref errors);
            ValidateMultiplier(0.75f, 1.5f, ref errors);
            ValidateMultiplier(1f, 2f, ref errors);

            var scaledTypes = new[]
            {
                WeaponType.Flag,
                WeaponType.GoddessBlessing,
                WeaponType.AuraSword,
                WeaponType.Excalibur,
                WeaponType.ArrowRain,
                WeaponType.ArrowShower,
                WeaponType.Frost,
                WeaponType.FrostStorm
            };
            for (int i = 0; i < scaledTypes.Length; i++)
            {
                var type = scaledTypes[i];
                if (!WeaponController.UsesAreaControlRangeScaling(type) ||
                    WeaponCatalog.AreaControlSpecialEffectDescriptionSource(type) != DescriptionSource)
                {
                    Error("Area control range scaling target or description is invalid: " + type, ref errors);
                }
            }

            if (WeaponController.UsesAreaControlRangeScaling(WeaponType.Slash) ||
                WeaponController.UsesAreaControlRangeScaling(WeaponType.ThunderBall))
            {
                Error("Area control range scaling must be limited to Flag, Aura Sword, Arrow Rain, Frost, and their evolutions.", ref errors);
            }

            if (LocalizationTextCatalog.Translate(DescriptionSource, GameLanguage.English) != EnglishDescription)
            {
                Error("Area control range scaling English localization is invalid.", ref errors);
            }

            return errors;
        }

        static void ValidateMultiplier(float ratio, float expected, ref int errors)
        {
            float actual = WeaponController.CalculateAreaControlRangeMultiplier(ratio, 0.5f, 1f, 2f);
            if (!Approximately(actual, expected))
            {
                Error("Area control range multiplier is invalid at ratio=" + ratio + ". actual=" + actual, ref errors);
            }
        }

        static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= Epsilon;
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError(message);
        }
    }
}
