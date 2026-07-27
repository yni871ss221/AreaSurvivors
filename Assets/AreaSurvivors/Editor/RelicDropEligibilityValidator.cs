using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.Editor
{
    public static class RelicDropEligibilityValidator
    {
        const string SuccessMarkerPath =
            "TokenReports/Validation/relic-drop-eligibility-validator.success";

        [MenuItem("Area Survivors/Validate/Relic Drop Eligibility")]
        public static void ValidateFromMenu()
        {
            DeleteSuccessMarker();
            var failures = new List<string>();
            Predicate<RelicType> ownsNothing = _ => false;
            ValidateRaincallerPlume(failures);

            var beforeThreshold = RelicCatalog.GetDropEligibleDefinitions(
                RelicCatalog.StrongRelicMinimumOwnedCount - 1,
                ownsNothing);
            for (int i = 0; i < beforeThreshold.Length; i++)
            {
                var definition = beforeThreshold[i];
                if (definition.rarity == RelicRarity.Legendary)
                {
                    failures.Add(
                        $"Legendary relic {definition.type} must not appear below 10 owned relics.");
                }
                if (definition.type == RelicType.SolitaryBlade)
                {
                    failures.Add("Solitary Blade must not appear below 10 owned relics.");
                }
            }

            var atThreshold = RelicCatalog.GetDropEligibleDefinitions(
                RelicCatalog.StrongRelicMinimumOwnedCount,
                ownsNothing);
            RequireCandidate(
                atThreshold,
                RelicType.SolitaryBlade,
                "Solitary Blade must unlock at 10 owned relics.",
                failures);
            for (int i = 0; i < RelicCatalog.All.Length; i++)
            {
                var definition = RelicCatalog.All[i];
                if (definition.rarity != RelicRarity.Legendary) continue;
                RequireCandidate(
                    atThreshold,
                    definition.type,
                    $"Legendary relic {definition.type} must unlock at 10 owned relics.",
                    failures);
            }

            var ownedTypes = new HashSet<RelicType>
            {
                RelicType.WarriorCharm,
                RelicType.SolitaryBlade,
                RelicType.TriBladeCrest
            };
            var withoutOwned = RelicCatalog.GetDropEligibleDefinitions(
                RelicCatalog.StrongRelicMinimumOwnedCount,
                ownedTypes.Contains);
            for (int i = 0; i < withoutOwned.Length; i++)
            {
                if (ownedTypes.Contains(withoutOwned[i].type))
                {
                    failures.Add($"Owned relic {withoutOwned[i].type} remained in the drop pool.");
                }
            }

            var noCandidates = RelicCatalog.GetDropEligibleDefinitions(
                RelicCatalog.All.Length,
                _ => true);
            if (noCandidates.Length != 0)
            {
                failures.Add("A fully owned relic catalog must produce no duplicate candidates.");
            }

            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Relic drop eligibility validation failed:\n- " +
                    string.Join("\n- ", failures));
            }

            WriteSuccessMarker();
            Debug.Log(
                "Relic drop eligibility validation passed. " +
                "Owned relics are excluded, Legendary relics plus Solitary Blade unlock at 10 owned relics, " +
                "and Raincaller Plume range bonus is 0.35.");
        }

        static void ValidateRaincallerPlume(List<string> failures)
        {
            var definition = RelicCatalog.Get(RelicType.RaincallerPlume);
            const string japaneseEffect = "アローレインの攻撃範囲 +0.35";
            const string englishEffect = "Arrow Rain area +0.35";
            if (definition == null ||
                definition.effectKind != RelicEffectKind.WeaponRangeBonus ||
                definition.targetWeapon != WeaponType.ArrowRain ||
                !Mathf.Approximately(definition.value, 0.35f) ||
                definition.effectTextSource != japaneseEffect)
            {
                failures.Add("Raincaller Plume must add exactly 0.35 range to Arrow Rain.");
                return;
            }

            if (LocalizationTextCatalog.Translate(japaneseEffect, GameLanguage.English) != englishEffect)
            {
                failures.Add("Raincaller Plume English effect text must match the 0.35 range bonus.");
            }
        }

        static void RequireCandidate(
            RelicDefinition[] definitions,
            RelicType requiredType,
            string failure,
            List<string> failures)
        {
            for (int i = 0; i < definitions.Length; i++)
            {
                if (definitions[i].type == requiredType) return;
            }
            failures.Add(failure);
        }

        static void DeleteSuccessMarker()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);
        }

        static void WriteSuccessMarker()
        {
            string directory = Path.GetDirectoryName(SuccessMarkerPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(
                SuccessMarkerPath,
                DateTime.UtcNow.ToString("O") + Environment.NewLine);
        }
    }
}
