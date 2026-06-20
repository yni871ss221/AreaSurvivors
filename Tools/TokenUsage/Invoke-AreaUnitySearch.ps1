param(
    [Parameter(Mandatory = $true)]
    [string]$Query,
    [switch]$PrintOutput
)

$ErrorActionPreference = "Stop"

$escaped = $Query.Replace("'", "''")
& "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action Eval -EvalCode "UnityEditor.EditorPrefs.SetString('AreaSurvivors.Report.SearchQuery', '$escaped');" | Out-Null
& "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action Menu -MenuPath "Area Survivors/Reports/Scene Prefab Search" -PrintOutput:$PrintOutput

$latest = Get-ChildItem -LiteralPath "TokenReports\UnityReports" -Filter "scene-prefab-search-*.md" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($latest) {
    Write-Output ("search_query: {0}" -f $Query)
    Write-Output ("report: {0}" -f $latest.FullName)
    Write-Output ("bytes: {0}" -f $latest.Length)
}
