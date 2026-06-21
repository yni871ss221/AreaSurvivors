param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("hud-layout", "construction-menu-layout", "building-prefab-visuals", "asset-references")]
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

function Convert-ToCharLiteralList {
    param([string]$Text)
    return ($Text.ToCharArray() | ForEach-Object {
        switch ($_ ) {
            "'" { "'\''" }
            "\" { "'\\'" }
            default { "'$_'" }
        }
    }) -join ","
}

$typeChars = Convert-ToCharLiteralList $typeName
$methodChars = Convert-ToCharLiteralList $methodName
# Prefer Menu.Execute in daily use. Keep the reflected method route as a fallback
# because some report names are easier to keep stable in one place here.
$code = @"
System.Reflection.Assembly.Load(new string(new[]{'A','s','s','e','m','b','l','y','-','C','S','h','a','r','p','-','E','d','i','t','o','r'}))
    .GetType(new string(new[]{$typeChars}))
    .GetMethod(new string(new[]{$methodChars}), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
    .Invoke(null, null);
"@

Write-Output "Run report: $Report"
Write-Output "Menu: $menu"
Write-Output "Type: $typeName"
Write-Output "Method: $methodName"
& unicli exec Eval --code $code
