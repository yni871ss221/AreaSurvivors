param([switch]$Json)

$ErrorActionPreference = "Stop"

$path = "Assets/AreaSurvivors/Scripts/Game/GameManager.cs"
if (-not (Test-Path -LiteralPath $path)) { throw "GameManager.cs not found: $path" }

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
    split_candidates = @(
        "BuildModeHudBinder: BuildConstructionMenu, build slots, build mode buttons",
        "ResourceRuntimeService: Wood/Stone/Tokens and persistent sync",
        "RunStageController: stage timer, boss, round transitions",
        "PlayerHudController: player stats and weapon panel",
        "TowerHudController: tower status and damage popups"
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
Write-Output "Split candidates:"
foreach ($candidate in $result.split_candidates) { Write-Output ("- {0}" -f $candidate) }
