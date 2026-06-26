param([switch]$Json)

$ErrorActionPreference = "Stop"

$existing = @(
    Get-ChildItem -LiteralPath "Assets/AreaSurvivors/Editor" -Filter "*Reporter*.cs" -File -ErrorAction SilentlyContinue |
        Select-Object @{Name="Path";Expression={$_.FullName.Replace((Get-Location).Path + "\", "")}}
)

$candidates = @(
    [pscustomobject]@{ Name = "HUD Layout Reporter"; Target = "05_Game.unity HUD"; Output = "RectTransform table, overlaps, missing Sprite references" },
    [pscustomobject]@{ Name = "Construction Menu Reporter"; Target = "Construction Menu"; Output = "slots, buttons, labels, icons, bound GameObjects" },
    [pscustomobject]@{ Name = "Resource HUD Reporter"; Target = "Wood/Stone/Token HUD"; Output = "amount labels, icons, anchors, active state" },
    [pscustomobject]@{ Name = "Skill Tree Layout Reporter"; Target = "Skill tree"; Output = "node overlap, link angle, duplicate IDs, prerequisite errors" },
    [pscustomobject]@{ Name = "Building Prefab Visual Reporter"; Target = "Building prefabs"; Output = "sprite refs, footprint, collider, visual bounds, missing upgrade images" },
    [pscustomobject]@{ Name = "Asset Reference Reporter"; Target = "Sprites/Prefabs/Resources"; Output = "guid refs, catalog refs, code name refs" }
)

$runCommands = @(
    [pscustomobject]@{ Report = "hud-layout"; Command = "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/run-unity-report.ps1 -Report hud-layout" },
    [pscustomobject]@{ Report = "construction-menu-layout"; Command = "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/run-unity-report.ps1 -Report construction-menu-layout" },
    [pscustomobject]@{ Report = "skill-tree-layout"; Command = "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/run-unity-report.ps1 -Report skill-tree-layout" },
    [pscustomobject]@{ Report = "building-prefab-visuals"; Command = "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/run-unity-report.ps1 -Report building-prefab-visuals" },
    [pscustomobject]@{ Report = "asset-references"; Command = "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/run-unity-report.ps1 -Report asset-references" }
)

$result = [pscustomobject]@{
    existing_reporters = $existing
    recommended_candidates = $candidates
    run_commands = $runCommands
}

if ($Json) {
    $result | ConvertTo-Json -Depth 5
    exit 0
}

Write-Output "Existing reporters:"
$existing | Format-Table -AutoSize
Write-Output ""
Write-Output "Recommended reporter candidates:"
$candidates | Format-Table -AutoSize
Write-Output ""
Write-Output "Run commands:"
$runCommands | Format-Table -AutoSize
