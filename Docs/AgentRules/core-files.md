# Core Files By Task

- 建造UI、HUD、資源表示: `Assets/AreaSurvivors/Scripts/Game/GameManager.cs`、`Assets/AreaSurvivors/Scripts/Game/BuildPlacementController.cs`、必要時のみ `Assets/AreaSurvivors/Editor/AreaSurvivorsBootstrap.cs`
- 資源、トークン、永続進行: `Assets/AreaSurvivors/Scripts/Core/ProgressionStore.cs`、`Assets/AreaSurvivors/Scripts/Core/SaveData.cs`、`Assets/AreaSurvivors/Scripts/Core/GameConfig.cs`
- 建造物配置、コスト、ストック: `Assets/AreaSurvivors/Scripts/Game/BuildPlacementController.cs`、`Assets/AreaSurvivors/Resources/Config/GameConfig.asset`
- 建造物強化: `Assets/AreaSurvivors/Scripts/Game/BuildingUpgradeController.cs`、`Assets/AreaSurvivors/Scripts/Game/WoodenBarrier.cs`、対象Prefabのみ
- ロビー、タイトル、アップグレード画面: `Assets/AreaSurvivors/Scripts/UI/LobbyScreen.cs`、`Assets/AreaSurvivors/Scripts/UI/LobbyUiFactory.cs`、`Assets/AreaSurvivors/Scripts/UI/UpgradeScreen.cs`
- GameplayTest: `Assets/AreaSurvivors/Scripts/Testing/GameplayTestScenario.cs`、`Assets/AreaSurvivors/Scripts/Testing/GameplayTestRunner.cs`、対象Scenario Assetのみ
- Scene/Prefab内検索: Scene YAML全文ではなく `Tools/TokenUsage/safe-unity-search.ps1 -Query <対象名>`、またはUnity Reporterを使う。
