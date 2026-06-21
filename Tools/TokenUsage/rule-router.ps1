param(
    [Parameter(Mandatory = $true)][string]$Task,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$rules = New-Object System.Collections.Generic.List[string]
$files = New-Object System.Collections.Generic.List[string]

function New-UString {
    param([int[]]$CodePoints)
    return -join ($CodePoints | ForEach-Object { [char]$_ })
}

function Test-ContainsAny {
    param([string]$Text, [string[]]$Needles)
    foreach ($needle in $Needles) {
        if ($Text.IndexOf($needle, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) { return $true }
    }
    return $false
}

function Add-Unique {
    param($List, [string]$Value)
    if (-not [string]::IsNullOrWhiteSpace($Value) -and -not $List.Contains($Value)) { $List.Add($Value) | Out-Null }
}

function Add-Rule { param([string]$Value) Add-Unique $rules $Value }
function Add-File { param([string]$Value) Add-Unique $files $Value }

$uiWords = @("hud", "ui", "button", "lobby", "title", "upgrade", (New-UString @(0x753B,0x9762)), (New-UString @(0x30DC,0x30BF,0x30F3)), (New-UString @(0x8868,0x793A)))
$buildWords = @("build", "construction", "cost", "stock", (New-UString @(0x5EFA,0x9020)), (New-UString @(0x914D,0x7F6E)), (New-UString @(0x58C1)), (New-UString @(0x57CE,0x9580)))
$resourceWords = @("wood", "stone", "resource", "token", "progression", "save", (New-UString @(0x6728,0x6750)), (New-UString @(0x77F3,0x6750)), (New-UString @(0x8CC7,0x6E90)), (New-UString @(0x30C8,0x30FC,0x30AF,0x30F3)), (New-UString @(0x6C38,0x7D9A)))
$assetWords = @("sprite", "prefab", "visual", "asset", "icon", "image", (New-UString @(0x753B,0x50CF)), (New-UString @(0x7D20,0x6750)), (New-UString @(0x5DEE,0x3057,0x66FF,0x3048)))
$combatWords = @("attack", "enemy", "damage", "collider", "combat", "projectile", (New-UString @(0x653B,0x6483)), (New-UString @(0x6575)), (New-UString @(0x7206,0x767A)), (New-UString @(0x5F3E)))
$testWords = @("map", "scene", "gameplaytest", "test", "unity", "compile", (New-UString @(0x30B7,0x30FC,0x30F3)), (New-UString @(0x30C6,0x30B9,0x30C8)), (New-UString @(0x691C,0x8A3C)))
$tokenWords = @("token", "safe-", "rtk", "reports", "diff", "search", (New-UString @(0x30C8,0x30FC,0x30AF,0x30F3,0x6D88,0x8CBB)), (New-UString @(0x691C,0x7D22)))
$closeoutWords = @("obsidian", "memory", "closeout", "commit", "push", (New-UString @(0x7DE0,0x3081)), (New-UString @(0x8A18,0x61B6)), (New-UString @(0x5C65,0x6B74)), (New-UString @(0x4F5C,0x696D,0x7D42,0x4E86)))
$modelWords = @("model", "reasoning", "context", "chat", (New-UString @(0x30E2,0x30C7,0x30EB)), (New-UString @(0x63A8,0x8AD6)), (New-UString @(0x9577,0x3044,0x30B9,0x30EC,0x30C3,0x30C9)), (New-UString @(0x65B0,0x898F,0x30C1,0x30E3,0x30C3,0x30C8)))

Add-Rule "Docs/AgentRules/core-files.md"

if (Test-ContainsAny $Task $uiWords) {
    Add-Rule "Docs/AgentRules/ui-and-hud.md"
    Add-File "Assets/AreaSurvivors/Scripts/Game/GameManager.cs"
    Add-File "Assets/AreaSurvivors/Scripts/Game/BuildPlacementController.cs"
}
if (Test-ContainsAny $Task $resourceWords) {
    Add-File "Assets/AreaSurvivors/Scripts/Core/ProgressionStore.cs"
    Add-File "Assets/AreaSurvivors/Scripts/Core/SaveData.cs"
    Add-File "Assets/AreaSurvivors/Scripts/Core/GameConfig.cs"
}
if (Test-ContainsAny $Task $buildWords) {
    Add-File "Assets/AreaSurvivors/Scripts/Game/BuildPlacementController.cs"
    Add-File "Assets/AreaSurvivors/Resources/Config/GameConfig.asset"
}
if (Test-ContainsAny $Task $assetWords) { Add-Rule "Docs/AgentRules/assets-and-visuals.md" }
if (Test-ContainsAny $Task $combatWords) {
    Add-Rule "Docs/AgentRules/combat.md"
    Add-File "Assets/AreaSurvivors/Scripts/Game/BuildingUpgradeController.cs"
}
if (Test-ContainsAny $Task $testWords) {
    Add-Rule "Docs/AgentRules/map-and-testing.md"
    Add-File "Assets/AreaSurvivors/Scripts/Testing/GameplayTestScenario.cs"
    Add-File "Assets/AreaSurvivors/Scripts/Testing/GameplayTestRunner.cs"
}
if (Test-ContainsAny $Task $tokenWords) { Add-Rule "Docs/AgentRules/token-tools.md" }
if (Test-ContainsAny $Task $closeoutWords) { Add-Rule "Docs/AgentRules/closeout.md" }
if (Test-ContainsAny $Task $modelWords) { Add-Rule "Docs/AgentRules/model-and-context.md" }

if ($rules.Count -eq 0) { Add-Rule "AGENTS.md" }
if ($files.Count -eq 0) { Add-File "AGENTS.md" }

$result = [pscustomobject]@{
    task = $Task
    rules = @($rules)
    core_files = @($files)
}

if ($Json) {
    $result | ConvertTo-Json -Depth 4
    exit 0
}

Write-Output "Rules:"
foreach ($rule in $rules) { Write-Output ("- {0}" -f $rule) }
Write-Output ""
Write-Output "Core files:"
foreach ($file in $files) { Write-Output ("- {0}" -f $file) }
