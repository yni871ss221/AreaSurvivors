using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class RunWeaponUpgradeDiminishingValidator
    {
        const string GameManagerPath = "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.cs";
        const string MarkerRelativePath = "Library/AreaSafeUnity/run-weapon-upgrade-diminishing-validator.ok";
        const float Epsilon = 0.0001f;

        [MenuItem("Area Survivors/Validate/Run Weapon Upgrade Diminishing Returns")]
        public static void ValidateMenu()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string markerPath = Path.Combine(projectRoot, MarkerRelativePath);
            if (File.Exists(markerPath)) File.Delete(markerPath);

            int errors = ValidateAll();
            if (errors != 0)
            {
                throw new InvalidOperationException(
                    "Run weapon upgrade diminishing validation failed. errors=" + errors);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log(
                "Run weapon upgrade diminishing validator: passed. Repeated range, knockback, cooldown, and slow upgrades retain 80% per same weapon/stat selection.");
        }

        public static int ValidateAll()
        {
            int errors = 0;
            ValidateCalculation(ref errors);
            ValidateWeaponAndStatIsolation(ref errors);
            ValidateLevelUpChoiceCoverage(ref errors);
            return errors;
        }

        static void ValidateCalculation(ref int errors)
        {
            if (!Approximately(RunWeaponUpgradeDiminishing.Factor(0), 1f) ||
                !Approximately(RunWeaponUpgradeDiminishing.Factor(1), 0.8f) ||
                !Approximately(RunWeaponUpgradeDiminishing.Factor(2), 0.64f) ||
                !Approximately(RunWeaponUpgradeDiminishing.AdditiveAmount(10f, 0), 10f) ||
                !Approximately(RunWeaponUpgradeDiminishing.AdditiveAmount(10f, 1), 8f) ||
                !Approximately(RunWeaponUpgradeDiminishing.AdditiveAmount(10f, 2), 6.4f) ||
                !Approximately(RunWeaponUpgradeDiminishing.CumulativeAdditiveAmount(10f, 3), 24.4f) ||
                !Approximately(RunWeaponUpgradeDiminishing.CooldownMultiplier(0.9f, 0), 0.9f) ||
                !Approximately(RunWeaponUpgradeDiminishing.CooldownMultiplier(0.9f, 1), 0.92f) ||
                !Approximately(RunWeaponUpgradeDiminishing.CooldownMultiplier(0.9f, 2), 0.936f) ||
                !Approximately(RunWeaponUpgradeDiminishing.CumulativeCooldownMultiplier(0.9f, 2), 0.828f))
            {
                Error("Diminishing calculations must follow 100%, 80%, 64% retention.", ref errors);
            }
        }

        static void ValidateWeaponAndStatIsolation(ref int errors)
        {
            var root = new GameObject("Run Weapon Upgrade Diminishing Validator")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            try
            {
                var controller = root.AddComponent<WeaponController>();
                controller.RegisterRunUpgradeSelection(WeaponType.Slash, RunWeaponUpgradeStat.Range);
                if (controller.GetRunUpgradeSelectionCount(WeaponType.Slash, RunWeaponUpgradeStat.Range) != 1 ||
                    controller.GetRunUpgradeSelectionCount(WeaponType.Slash, RunWeaponUpgradeStat.Knockback) != 0 ||
                    controller.GetRunUpgradeSelectionCount(WeaponType.Arrow, RunWeaponUpgradeStat.Range) != 0 ||
                    !Approximately(
                        controller.GetDiminishedAdditiveUpgrade(
                            WeaponType.Slash,
                            RunWeaponUpgradeStat.Range,
                            10f),
                        8f) ||
                    !Approximately(
                        controller.GetDiminishedAdditiveUpgrade(
                            WeaponType.Slash,
                            RunWeaponUpgradeStat.Knockback,
                            10f),
                        10f))
                {
                    Error("Diminishing counts must be independent per weapon and stat.", ref errors);
                }

                controller.RegisterRunUpgradeSelection(
                    WeaponType.FireMissile,
                    RunWeaponUpgradeStat.ExplosionRange);
                if (controller.GetRunUpgradeSelectionCount(
                        WeaponType.Fireball,
                        RunWeaponUpgradeStat.ExplosionRange) != 1)
                {
                    Error("Evolution weapons must continue the base weapon diminishing history.", ref errors);
                }

                var reset = typeof(WeaponController).GetMethod(
                    "ResetRunWeaponUpgrades",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                reset?.Invoke(controller, null);
                if (reset == null ||
                    controller.GetRunUpgradeSelectionCount(
                        WeaponType.Slash,
                        RunWeaponUpgradeStat.Range) != 0 ||
                    controller.GetRunUpgradeSelectionCount(
                        WeaponType.Fireball,
                        RunWeaponUpgradeStat.ExplosionRange) != 0)
                {
                    Error("Diminishing counts must reset at the start of each run.", ref errors);
                }
            }
            catch (Exception exception)
            {
                Error(
                    "Weapon/stat isolation execution failed: " +
                    exception.GetBaseException().Message,
                    ref errors);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void ValidateLevelUpChoiceCoverage(ref int errors)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string gameManagerFullPath = Path.Combine(projectRoot, GameManagerPath);
            string source = File.ReadAllText(gameManagerFullPath);
            int additiveChoiceOccurrences = CountOccurrences(
                source,
                "CreateDiminishingAdditiveChoice(");
            int cooldownChoiceOccurrences = CountOccurrences(
                source,
                "CreateDiminishingCooldownChoice(");

            if (additiveChoiceOccurrences != 17 ||
                cooldownChoiceOccurrences != 9 ||
                !source.Contains(
                    "RegisterRunUpgradeSelection(choice.weaponType, choice.diminishingStat)") ||
                !source.Contains(
                    "RunWeaponUpgradeStat diminishingStat = RunWeaponUpgradeStat.None"))
            {
                Error(
                    "All 16 additive and 8 cooldown level-up choices must use the shared diminishing path.",
                    ref errors);
            }
        }

        static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
        }

        static bool Approximately(float actual, float expected)
        {
            return Mathf.Abs(actual - expected) <= Epsilon;
        }

        static void Error(string message, ref int errors)
        {
            errors++;
            Debug.LogError(message);
        }
    }
}
