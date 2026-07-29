$ErrorActionPreference = "Stop"

$toolsRoot = Split-Path $PSScriptRoot -Parent
$projectRoot = Split-Path (Split-Path $toolsRoot -Parent) -Parent
$runtimeRoot = Join-Path $projectRoot "Assets\AreaSurvivors\Scripts\Game\Runtime"

$responsibilities = @(
    @{
        File = "GameManager.LevelProgression.cs"
        Anchor = "public void AddExperience(int amount)"
    },
    @{
        File = "GameManager.UpgradeChoices.cs"
        Anchor = "List<RunUpgradeChoice> RollUpgrades()"
    },
    @{
        File = "GameManager.LevelUpPanel.cs"
        Anchor = "void ApplyRunUpgrade(RunUpgradeChoice choice)"
    },
    @{
        File = "GameManager.RunStage.cs"
        Anchor = "public void GameOver()"
    },
    @{
        File = "GameManager.RelicModal.cs"
        Anchor = "public void ShowAnnouncement(string message)"
    },
    @{
        File = "GameManager.RunEnd.cs"
        Anchor = "void EndRun(bool clear)"
    },
    @{
        File = "GameManager.StageHud.cs"
        Anchor = "void UpdateHud()"
    }
)

$corePath = Join-Path $runtimeRoot "GameManager.cs"
$coreText = [System.IO.File]::ReadAllText($corePath)
$coreLines = [System.IO.File]::ReadAllLines($corePath).Count
if ($coreLines -gt 650) {
    throw "GameManager.cs exceeded the 650-line facade limit: $coreLines"
}
if (-not $coreText.Contains("public sealed partial class GameManager : MonoBehaviour")) {
    throw "GameManager.cs must remain the MonoBehaviour partial-class facade."
}

foreach ($responsibility in $responsibilities) {
    $path = Join-Path $runtimeRoot $responsibility.File
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "GameManager responsibility file is missing: $($responsibility.File)"
    }

    $text = [System.IO.File]::ReadAllText($path)
    $lineCount = [System.IO.File]::ReadAllLines($path).Count
    if ($lineCount -gt 400) {
        throw "$($responsibility.File) exceeded the 400-line responsibility limit: $lineCount"
    }
    if (-not $text.Contains("public sealed partial class GameManager") -or
        -not $text.Contains($responsibility.Anchor)) {
        throw "GameManager responsibility anchor is missing: $($responsibility.File)"
    }
    if ($coreText.Contains($responsibility.Anchor)) {
        throw "GameManager responsibility leaked back into GameManager.cs: $($responsibility.Anchor)"
    }
}

$obsoleteReporter = Join-Path $toolsRoot "game-manager-responsibility-report.ps1"
if (Test-Path -LiteralPath $obsoleteReporter) {
    throw "The obsolete single-file GameManager reporter must not be restored."
}

$validatorRoutes = @{
    "RelicDropEligibilityValidator.cs" = @(
        "GameManager.RunStage.cs",
        "GameManager.RelicModal.cs",
        "GameManager.LevelProgression.cs"
    )
    "RunWeaponUpgradeDiminishingValidator.cs" = @(
        "GameManager.UpgradeChoices.cs",
        "GameManager.LevelUpPanel.cs"
    )
    "StageTransitionEnemyDefeatValidator.cs" = @(
        "GameManager.RunStage.cs",
        "GameManager.LevelProgression.cs",
        "GameManager.LevelUpPanel.cs"
    )
    "TokenRuntimeServiceValidator.cs" = @(
        "GameManager.RunStage.cs",
        "GameManager.RunEnd.cs"
    )
}
$editorRoot = Join-Path $projectRoot "Assets\AreaSurvivors\Editor"
foreach ($route in $validatorRoutes.GetEnumerator()) {
    $validatorText = [System.IO.File]::ReadAllText(
        (Join-Path $editorRoot $route.Key)
    )
    foreach ($responsibilityFile in $route.Value) {
        if (-not $validatorText.Contains($responsibilityFile)) {
            throw "Validator responsibility route is missing: $($route.Key) -> $responsibilityFile"
        }
    }
}

Write-Output "command_tool_test_module: game-manager-structure passed"
