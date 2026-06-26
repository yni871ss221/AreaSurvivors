param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("hud-layout", "construction-menu-layout", "skill-tree-layout", "building-prefab-visuals", "asset-references")]
    [string]$Report
)

$ErrorActionPreference = "Stop"

$reportSpecByName = @{
    "hud-layout" = @{
        Menu = "Area Survivors/Reports/HUD Layout"
        Type = "AreaSurvivors.Editor.HudLayoutReporter, Assembly-CSharp-Editor"
        Method = "LogHudLayout"
    }
    "construction-menu-layout" = @{
        Menu = "Area Survivors/Reports/Construction Menu Layout"
        Type = "AreaSurvivors.Editor.HudLayoutReporter, Assembly-CSharp-Editor"
        Method = "LogConstructionMenuLayout"
    }
    "skill-tree-layout" = @{
        Menu = "Area Survivors/Reports/Skill Tree Layout"
        Type = "AreaSurvivors.Editor.SkillTreeLayoutReporter, Assembly-CSharp-Editor"
        Method = "LogSkillTreeLayout"
    }
    "building-prefab-visuals" = @{
        Menu = "Area Survivors/Reports/Building Prefab Visuals"
        Type = "AreaSurvivors.Editor.BuildingPrefabVisualReporter, Assembly-CSharp-Editor"
        Method = "LogBuildingPrefabVisuals"
    }
    "asset-references" = @{
        Menu = "Area Survivors/Reports/Asset References"
        Type = "AreaSurvivors.Editor.AssetReferenceReporter, Assembly-CSharp-Editor"
        Method = "LogAssetReferences"
    }
}

$spec = $reportSpecByName[$Report]
if ($null -eq $spec) {
    throw "Unknown report: $Report"
}

$menu = $spec.Menu
$typeName = $spec.Type
$methodName = $spec.Method

Write-Output "Run report: $Report"
Write-Output "Menu: $menu"
Write-Output "Type: $typeName"
Write-Output "Method: $methodName"
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $projectRoot
try {
    & unicli exec Menu.Execute --menuItemPath $menu
}
finally {
    Pop-Location
}
