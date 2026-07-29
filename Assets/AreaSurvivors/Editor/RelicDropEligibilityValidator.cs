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
        const string RelicChestSourcePath =
            "Assets/AreaSurvivors/Scripts/Game/Pickups/RelicChest.cs";
        static readonly string[] GameManagerSourcePaths =
        {
            "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.RunStage.cs",
            "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.RelicModal.cs",
            "Assets/AreaSurvivors/Scripts/Game/Runtime/GameManager.LevelProgression.cs"
        };
        const string RelicPanelSourcePath =
            "Assets/AreaSurvivors/Scripts/UI/RelicAcquisitionPanel.cs";

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

            var noUnownedCandidates = RelicCatalog.GetDropEligibleDefinitions(
                RelicCatalog.All.Length,
                _ => true);
            if (noUnownedCandidates.Length != 0)
            {
                failures.Add("A fully owned relic catalog must produce no duplicate candidates.");
            }
            ValidateFullOwnershipFallback(failures);
            ValidateDuplicateTokenRewards(failures);
            ValidateRewardConsumerContracts(failures);
            ValidateRelicModalInputPriority(failures);

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
                "full ownership restores the weighted duplicate pool, duplicate rarity tokens are configured, " +
                "and Raincaller Plume range bonus is 0.35.");
        }

        static void ValidateFullOwnershipFallback(List<string> failures)
        {
            var lastUnownedType = RelicCatalog.All[0].type;
            var oneRemainingPool = RelicCatalog.GetRandomDropPoolDefinitions(
                RelicCatalog.All.Length - 1,
                type => type != lastUnownedType);
            if (oneRemainingPool.Length != 1 ||
                oneRemainingPool[0].type != lastUnownedType)
            {
                failures.Add(
                    "Duplicate relics must remain excluded while even one unowned relic remains.");
            }

            var fullyOwnedPool = RelicCatalog.GetRandomDropPoolDefinitions(
                RelicCatalog.All.Length,
                _ => true);
            var uniqueTypes = new HashSet<RelicType>();
            for (int i = 0; i < fullyOwnedPool.Length; i++)
            {
                uniqueTypes.Add(fullyOwnedPool[i].type);
            }
            if (fullyOwnedPool.Length != RelicCatalog.All.Length ||
                uniqueTypes.Count != RelicCatalog.All.Length)
            {
                failures.Add(
                    "After full ownership, every relic must return to the weighted duplicate pool exactly once.");
            }

            if (RelicCatalog.GetDropWeight(RelicRarity.Common) != 50 ||
                RelicCatalog.GetDropWeight(RelicRarity.Uncommon) != 30 ||
                RelicCatalog.GetDropWeight(RelicRarity.Rare) != 15 ||
                RelicCatalog.GetDropWeight(RelicRarity.Legendary) != 5)
            {
                failures.Add(
                    "The full-ownership duplicate pool must retain rarity weights 50/30/15/5.");
            }
        }

        static void ValidateDuplicateTokenRewards(List<string> failures)
        {
            if (RelicCatalog.GetDuplicateTokenReward(RelicRarity.Common) != 5 ||
                RelicCatalog.GetDuplicateTokenReward(RelicRarity.Uncommon) != 10 ||
                RelicCatalog.GetDuplicateTokenReward(RelicRarity.Rare) != 30 ||
                RelicCatalog.GetDuplicateTokenReward(RelicRarity.Legendary) != 50)
            {
                failures.Add(
                    "Duplicate relic conversion must grant 5/10/30/50 tokens by rarity.");
            }
        }

        static void ValidateRewardConsumerContracts(List<string> failures)
        {
            string[] requiredPaths =
            {
                RelicChestSourcePath,
                RelicPanelSourcePath
            };
            for (int i = 0; i < requiredPaths.Length; i++)
            {
                if (!File.Exists(requiredPaths[i]))
                {
                    failures.Add($"Relic reward source is missing: {requiredPaths[i]}");
                    return;
                }
            }

            string chestSource = File.ReadAllText(RelicChestSourcePath);
            string managerSource = ReadGameManagerSource();
            string panelSource = File.ReadAllText(RelicPanelSourcePath);
            if (!chestSource.Contains("RelicCatalog.TryAcquireReward") ||
                !chestSource.Contains("duplicateTokenReward") ||
                !managerSource.Contains("RelicCatalog.TryAcquireReward") ||
                !managerSource.Contains("duplicateTokenReward"))
            {
                failures.Add(
                    "Field chests and direct boss relic rewards must share duplicate conversion handling.");
            }
            if (!panelSource.Contains("openButton.gameObject.SetActive(true)") ||
                !panelSource.Contains("DuplicateMessageOrDescription") ||
                !panelSource.Contains("変換トークン +"))
            {
                failures.Add(
                    "Duplicate relics must still use the chest-open panel and show their token conversion.");
            }
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

        static void ValidateRelicModalInputPriority(List<string> failures)
        {
            if (!File.Exists(RelicPanelSourcePath))
            {
                failures.Add("Relic modal input-priority sources are missing.");
                return;
            }

            string managerSource = ReadGameManagerSource();
            string panelSource = File.ReadAllText(RelicPanelSourcePath);
            if (!managerSource.Contains("BeginRelicAcquisitionModal();") ||
                !managerSource.Contains("EndRelicAcquisitionModal();") ||
                !managerSource.Contains("IsLevelUpInputBlockedByFrontModal()") ||
                !managerSource.Contains("Time.frameCount <= levelUpInputBlockedThroughFrame") ||
                !managerSource.Contains("activeRelicAcquisitionModalCount > 0"))
            {
                failures.Add(
                    "The relic panel must block level-up selection and actions until the front modal closes.");
            }

            if (!panelSource.Contains("Time.timeScale = Mathf.Max(0f, previousTimeScale);"))
            {
                failures.Add(
                    "Closing a relic panel must restore an existing paused modal without unpausing it.");
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

        static string ReadGameManagerSource()
        {
            string source = string.Empty;
            for (int i = 0; i < GameManagerSourcePaths.Length; i++)
            {
                if (!File.Exists(GameManagerSourcePaths[i]))
                {
                    throw new FileNotFoundException(
                        "GameManager responsibility source is missing.",
                        GameManagerSourcePaths[i]);
                }
                source += File.ReadAllText(GameManagerSourcePaths[i]);
            }
            return source;
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
