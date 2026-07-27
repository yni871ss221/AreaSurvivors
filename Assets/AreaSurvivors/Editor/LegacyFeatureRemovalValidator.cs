using System;
using System.IO;
using System.Reflection;
using AreaSurvivors.Testing;
using UnityEditor;
using UnityEngine;

namespace AreaSurvivors.EditorTools
{
    public static class LegacyFeatureRemovalValidator
    {
        const string MenuPath = "Area Survivors/Validate/Legacy Feature Removal";
        const string SuccessMarkerPath = "Library/AreaSafeUnity/legacy-feature-removal-validator.success";
        const BindingFlags AnyMember =
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly string[] RemovedUpgradeNames =
        {
            "WorkSpeed",
            "ResourceGain",
            "StartingWood",
            "StartingStone",
            "UnlockCarpenterHut",
            "UnlockAutoBuild",
            "AutoBuildSpeed",
            "UnlockWorkerHut",
            "AutoResourceInterval",
            "AutoResourceGain"
        };

        [MenuItem(MenuPath)]
        public static void ValidateMenu()
        {
            if (File.Exists(SuccessMarkerPath)) File.Delete(SuccessMarkerPath);

            var runtimeAssembly = typeof(GameManager).Assembly;
            foreach (var typeName in new[]
            {
                "AreaSurvivors.ResourceType",
                "AreaSurvivors.HarvestableResource",
                "AreaSurvivors.HarvestResourcePopup",
                "AreaSurvivors.LandmarkMaterialType",
                "AreaSurvivors.ClearableLandmark",
                "AreaSurvivors.NaturalLandmarkSpawner",
                "AreaSurvivors.BuildPlacementController",
                "AreaSurvivors.BuildingUpgradeController",
                "AreaSurvivors.BuildingPersistentState",
                "AreaSurvivors.MapSessionMode",
                "AreaSurvivors.SavedBuildingKind",
                "AreaSurvivors.StageBuildingSet",
                "AreaSurvivors.SavedBuildingData"
            })
            {
                Require(runtimeAssembly.GetType(typeName) == null, typeName + " still exists.");
            }

            Require(runtimeAssembly.GetType("AreaSurvivors.FixedBuildingLayoutService") != null, "FixedBuildingLayoutService is missing.");
            Require(runtimeAssembly.GetType("AreaSurvivors.BuildingRevivalState") != null, "BuildingRevivalState is missing.");
            Require(runtimeAssembly.GetType("AreaSurvivors.BuildingUpgradeTarget") != null, "BuildingUpgradeTarget is missing.");

            RequireMissingMembers(
                typeof(GameManager),
                "Wood",
                "Stone",
                "AddResource",
                "HasResources",
                "TrySpendResources",
                "SyncPersistentResources",
                "AddPersistentResourcesForTesting",
                "buildPlacement",
                "buildingUpgrade",
                "SessionMode");
            RequireMissingMembers(typeof(SaveData), "wood", "stone", "stageBuildings");
            RequireMissingMembers(typeof(RunResult), "woodEarned", "stoneEarned");
            RequireMissingMembers(
                typeof(GameConfig),
                "towerUpgradeWoodCost",
                "towerUpgradeStoneCost",
                "startingWood",
                "startingStone",
                "woodenWallWoodCost",
                "woodenWallStoneCost",
                "ballistaWoodCost",
                "ballistaStoneCost",
                "watchTowerWoodCost",
                "watchTowerStoneCost",
                "roundEndWoodReward",
                "roundEndStoneReward",
                "baseResourceGainBonus",
                "resourceGainPerUpgradeLevel",
                "runResourceGainBonus",
                "startingBallistaStock",
                "startingWallStock",
                "landmarkClearIntervalSeconds",
                "landmarkClearAmountPerTick",
                "landmarkDurability1Cell",
                "landmarkDurability2Cell",
                "landmarkDurability4Cell",
                "landmarkDurability8Cell",
                "baseWorkSpeedMultiplier",
                "workSpeedMultiplierPerUpgradeLevel",
                "runWorkSpeedMultiplierBonus",
                "autoBuildSpeedPerUpgradeLevel");
            RequireMissingMembers(
                typeof(ProgressionStore),
                "HasPersistentResources",
                "TrySpendPersistentResources",
                "AddPersistentResources",
                "GetStageBuildings",
                "ReplaceStageBuildings",
                "ReviveStageBuildings");
            RequireMissingMembers(
                typeof(GameplayTestScenario.SystemSettings),
                "enableNaturalLandmarkSpawner",
                "enableBuildPlacement",
                "clearExistingNaturalLandmarks");
            RequireMissingMembers(typeof(GameplayTestScenario), "landmarks");

            foreach (var upgradeName in RemovedUpgradeNames)
            {
                Require(!Enum.IsDefined(typeof(UpgradeType), upgradeName), "Removed UpgradeType still exists: " + upgradeName);
            }

            foreach (var path in new[]
            {
                "Assets/AreaSurvivors/Scripts/Game/Map/BuildPlacementController.cs",
                "Assets/AreaSurvivors/Scripts/Game/Map/NaturalLandmarkSpawner.cs",
                "Assets/AreaSurvivors/Scripts/Game/Map/ClearableLandmark.cs",
                "Assets/AreaSurvivors/Scripts/Core/LandmarkMaterialType.cs",
                "Assets/AreaSurvivors/Editor/NaturalLandmarkSpawnerEditor.cs",
                "Assets/AreaSurvivors/Sprites/Generated/UI/WoodIcon.png",
                "Assets/AreaSurvivors/Sprites/Generated/UI/StoneIcon.png",
                "Assets/AreaSurvivors/Sprites/Generated/StatIcons/StatResource.png",
                "Assets/AreaSurvivors/Sprites/Generated/StatIcons/StatWork.png",
                "Assets/AreaSurvivors/Sprites/Generated/Buildings/UpgradeBuildingIcon.png"
            })
            {
                RequireMissingAsset(path);
            }

            RequireTextMissing(
                "Assets/AreaSurvivors/Resources/GeneratedSpriteCatalog.asset",
                "WoodIcon",
                "StoneIcon",
                "StatResource",
                "StatWork",
                "UpgradeBuildingIcon");
            RequireTextMissing(
                "Assets/AreaSurvivors/Scenes/05_Game.unity",
                "m_Name: Wood Resource",
                "m_Name: Stone Resource",
                "m_Name: Test Add Wood Button",
                "m_Name: Test Add Stone Button",
                "m_Name: Construction Menu",
                "m_Name: Build Backplate",
                "m_Name: Build Status",
                "m_Name: Build Preview Tilemap");
            RequireTextMissing(
                "Assets/AreaSurvivors/Resources/Config/GameConfig.asset",
                "startingBallistaStock:",
                "startingWallStock:",
                "landmarkClearIntervalSeconds:",
                "landmarkClearAmountPerTick:",
                "landmarkDurability1Cell:",
                "landmarkDurability2Cell:",
                "landmarkDurability4Cell:",
                "landmarkDurability8Cell:",
                "baseWorkSpeedMultiplier:",
                "workSpeedMultiplierPerUpgradeLevel:",
                "runWorkSpeedMultiplierBonus:",
                "autoBuildSpeedPerUpgradeLevel:");

            Directory.CreateDirectory(Path.GetDirectoryName(SuccessMarkerPath));
            File.WriteAllText(SuccessMarkerPath, DateTime.UtcNow.ToString("O"));
            Debug.Log("Legacy feature removal validation: passed.");
        }

        static void RequireMissingMembers(Type type, params string[] memberNames)
        {
            foreach (var memberName in memberNames)
            {
                Require(
                    type.GetMember(memberName, AnyMember).Length == 0,
                    type.FullName + "." + memberName + " still exists.");
            }
        }

        static void RequireMissingAsset(string path)
        {
            Require(
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) == null,
                "Legacy asset still exists: " + path);
        }

        static void RequireTextMissing(string path, params string[] values)
        {
            var text = File.ReadAllText(path);
            foreach (var value in values)
            {
                Require(text.IndexOf(value, StringComparison.Ordinal) < 0, path + " still contains " + value);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
