param([switch]$Json)

$ErrorActionPreference = "Stop"

$gameScriptsRoot = "Assets/AreaSurvivors/Scripts/Game"
$candidates = @(
    Get-ChildItem -LiteralPath $gameScriptsRoot -Recurse -File -Filter "GameManager.cs" -ErrorAction Stop
)
if ($candidates.Count -ne 1) {
    $candidatePaths = @($candidates | ForEach-Object { $_.FullName })
    throw "Expected exactly one GameManager.cs below ${gameScriptsRoot}; found $($candidates.Count): $($candidatePaths -join ', ')"
}
$path = (Resolve-Path -LiteralPath $candidates[0].FullName -Relative).TrimStart(".", "\", "/")

$lines = Get-Content -LiteralPath $path -Encoding UTF8
$methods = @()
for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i].Trim()
    if ($line -match '^(public|private|protected|internal|static|void|int|bool|float|string|IEnumerator|Button|Text|Canvas|RectTransform|GameObject|PlayerController|TowerController|Color)\s+.*\)\s*$') {
        if ($line -notmatch '^(if|for|foreach|while|switch|catch)\b') {
            $methods += [pscustomobject]@{ Line = $i + 1; Signature = $line }
        }
    }
}

$areas = @(
    @{ Name = "HUD/UI"; Pattern = "Hud|UI|Button|Panel|Text|Canvas|Image|Slider|BuildConstructionMenu|TowerPanel|PlayerPanel" },
    @{ Name = "Resources"; Pattern = "Wood|Stone|Resource|Token|ProgressionStore|Persistent" },
    @{ Name = "Stage/Run"; Pattern = "Stage|Round|Timer|Boss|Enemy|Kill|Experience|Level" },
    @{ Name = "Player/Tower"; Pattern = "Player|Tower|Health|Upgrade|Weapon" },
    @{ Name = "Build Mode"; Pattern = "Build|Construction|Placement|Lobby" },
    @{ Name = "Scene/Setup"; Pattern = "Start|Awake|Scene|Canvas|Configure|Bind|Create" }
)

$summary = foreach ($area in $areas) {
    $matches = @($lines | Select-String -Pattern $area.Pattern)
    [pscustomobject]@{
        Area = $area.Name
        Hits = $matches.Count
        FirstLine = if ($matches.Count -gt 0) { $matches[0].LineNumber } else { 0 }
    }
}

$result = [pscustomobject]@{
    path = $path
    total_lines = $lines.Count
    method_count = $methods.Count
    responsibility_summary = $summary
    first_methods = @($methods | Select-Object -First 30)
    all_methods = @($methods)
    extracted_components = @(
        "GameHudController: gameplay HUD lifecycle, player/tower/stage panels"
        "TokenRuntimeService: RunTokens and token-source accounting"
        "RuntimeResourceDiagnostics: profiler recorders and scene-transition snapshots"
    )
    split_candidates = @(
        "RunStageController: stage timer, boss, round transitions",
        "FixedBuildingLayoutService: fixed building slot definitions and stage restoration layout",
        "LevelUpController: XP progression, choice generation, reroll/skip and level-up panel",
        "GameHudController follow-up: split player/tower panels only if independent maintenance is needed"
    )
}

if ($Json) {
    $result | ConvertTo-Json -Depth 5
    exit 0
}

Write-Output ("GameManager responsibility report: {0} lines, {1} method-like entries" -f $result.total_lines, $result.method_count)
Write-Output ""
Write-Output "Responsibility summary:"
$summary | Sort-Object Hits -Descending | Format-Table -AutoSize
Write-Output ""
Write-Output "Extracted components:"
foreach ($component in $result.extracted_components) { Write-Output ("- {0}" -f $component) }
Write-Output ""
Write-Output "Split candidates:"
foreach ($candidate in $result.split_candidates) { Write-Output ("- {0}" -f $candidate) }
