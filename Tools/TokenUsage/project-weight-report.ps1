param(
    [int]$Top = 20,
    [int]$ReferenceSample = 20,
    [switch]$SkipReferenceCandidates,
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$root = "Assets/AreaSurvivors"
$exclude = @("\Library\", "/Library/", "\Temp\", "/Temp/", "\Obj\", "/Obj/", "\.git\", "/.git/", "\TokenReports\", "/TokenReports/")

function Test-ExcludedPath {
    param([string]$Path)
    $normalized = $Path.Replace("/", "\")
    foreach ($part in $exclude) {
        if ($normalized.Contains($part.Replace("/", "\"))) { return $true }
    }
    return $false
}

function Get-RelativePath {
    param([string]$Path)
    return (Resolve-Path -LiteralPath $Path -Relative).TrimStart(".", "\", "/")
}

$files = @(
    Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { -not (Test-ExcludedPath $_.FullName) }
)

$largest = @(
    $files |
        Sort-Object Length -Descending |
        Select-Object -First $Top @{Name="KB";Expression={[math]::Round($_.Length / 1KB, 1)}}, @{Name="Path";Expression={Get-RelativePath $_.FullName}}
)

$largeUnity = @(
    $files |
        Where-Object { $_.Extension -in @(".unity", ".prefab", ".asset") } |
        Sort-Object Length -Descending |
        Select-Object -First $Top @{Name="KB";Expression={[math]::Round($_.Length / 1KB, 1)}}, @{Name="Path";Expression={Get-RelativePath $_.FullName}}
)

$byExtension = @(
    $files |
        Group-Object Extension |
        Sort-Object Count -Descending |
        Select-Object -First $Top Count, @{Name="Extension";Expression={if ([string]::IsNullOrWhiteSpace($_.Name)) { "(none)" } else { $_.Name }}}
)

$referenceCandidates = @()
if (-not $SkipReferenceCandidates) {
    $referenceRoots = @(
        "Assets/AreaSurvivors/Sprites/Generated",
        "Assets/AreaSurvivors/Sprites/External",
        "Assets/AreaSurvivors/Resources",
        "Assets/AreaSurvivors/Prefabs"
    )

    $candidateAssets = @(
        foreach ($dir in $referenceRoots) {
            if (Test-Path -LiteralPath $dir) {
                Get-ChildItem -LiteralPath $dir -Recurse -File -ErrorAction SilentlyContinue |
                    Where-Object { $_.Extension -in @(".png", ".prefab", ".asset") } |
                    Sort-Object Length -Descending |
                    Select-Object -First $ReferenceSample
            }
        }
    )

    foreach ($asset in $candidateAssets) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($asset.Name)
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        $pattern = [regex]::Escape($name)
        $hit = & rg -l --hidden -g '!Library/**' -g '!Temp/**' -g '!Obj/**' -g '!.git/**' -g '!TokenReports/**' -g '!*.png' -g '!*.jpg' -g '!*.jpeg' -g '!*.dll' -g '!*.pdb' $pattern Assets/AreaSurvivors 2>$null | Select-Object -First 2
        $referenceCandidates += [pscustomobject]@{
            KB = [math]::Round($asset.Length / 1KB, 1)
            Path = Get-RelativePath $asset.FullName
            Name = $name
            ReferenceHits = @($hit).Count
            Candidate = if (@($hit).Count -eq 0) { "unreferenced-name" } else { "referenced-name" }
        }
    }
}

$followUp = [pscustomobject]@{
    asset_reference_report = "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/run-unity-report.ps1 -Report asset-references"
    reporter_list = "powershell -ExecutionPolicy Bypass -File Tools/TokenUsage/reporter-candidates.ps1"
    note = if ($SkipReferenceCandidates) { "Name-hit sample skipped. Run asset reference reporter when Unity validation is needed." } else { "Name-hit sample is heuristic only. Confirm deletion candidates with asset reference reporter." }
}

$result = [pscustomobject]@{
    largest_files = $largest
    largest_unity_files = $largeUnity
    files_by_extension = $byExtension
    asset_reference_candidates = @($referenceCandidates | Sort-Object Candidate, KB -Descending | Select-Object -First $Top)
    follow_up = $followUp
}

if ($Json) {
    $result | ConvertTo-Json -Depth 5
    exit 0
}

Write-Output "Largest files:"
$largest | Format-Table -AutoSize
Write-Output ""
Write-Output "Largest Unity text assets:"
$largeUnity | Format-Table -AutoSize
Write-Output ""
Write-Output "Files by extension:"
$byExtension | Format-Table -AutoSize
Write-Output ""
if (-not $SkipReferenceCandidates) {
    Write-Output "Asset reference candidates (name search sample; review before deleting):"
    $result.asset_reference_candidates | Format-Table -AutoSize
    Write-Output ""
}
Write-Output "Follow-up commands:"
$followUp | Format-List
