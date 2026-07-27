param(
    [string]$ProjectRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$resolvedRoot = (Resolve-Path -LiteralPath $ProjectRoot).Path
$editorRoot = Join-Path $resolvedRoot "Assets/AreaSurvivors/Editor"
if (-not (Test-Path -LiteralPath $editorRoot -PathType Container)) {
    throw "AreaSurvivors Editor folder was not found: $editorRoot"
}

$codeRoots = @(
    $editorRoot,
    (Join-Path $resolvedRoot "Assets/AreaSurvivors/Scripts")
) | Where-Object { Test-Path -LiteralPath $_ -PathType Container }

$codeFiles = @(
    foreach ($codeRoot in $codeRoots) {
        Get-ChildItem -LiteralPath $codeRoot -Filter "*.cs" -File -Recurse
    }
)

$sourceByPath = @{}
foreach ($codeFile in $codeFiles) {
    $sourceByPath[$codeFile.FullName] = [System.IO.File]::ReadAllText($codeFile.FullName)
}

$rows = @(
    Get-ChildItem -LiteralPath $editorRoot -Filter "*Migration.cs" -File |
        Sort-Object Name |
        ForEach-Object {
            $migrationFile = $_
            $source = $sourceByPath[$migrationFile.FullName]
            $featureStem = $migrationFile.BaseName -replace 'Migration$', ''
            $classMatch = [regex]::Match(
                $source,
                '(?m)\b(?:public|internal)\s+static\s+class\s+([A-Za-z_][A-Za-z0-9_]*)')
            $className = if ($classMatch.Success) { $classMatch.Groups[1].Value } else { $migrationFile.BaseName }
            $classPattern = '\b' + [regex]::Escape($className) + '\b'

            $referenceFiles = @(
                foreach ($candidate in $codeFiles) {
                    if ($candidate.FullName -eq $migrationFile.FullName) {
                        continue
                    }
                    if ([regex]::IsMatch($sourceByPath[$candidate.FullName], $classPattern)) {
                        $candidate.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
                    }
                }
            )

            $menuPaths = @(
                [regex]::Matches($source, 'MenuItem\(\s*"([^"]+)"') |
                    ForEach-Object { $_.Groups[1].Value } |
                    Sort-Object -Unique
            )

            $literalTargets = @(
                [regex]::Matches(
                    $source,
                    'Assets/AreaSurvivors/[^"''\r\n]+?\.(?:unity|prefab|asset|controller|anim)') |
                    ForEach-Object { $_.Value.Replace('\', '/') } |
                    Sort-Object -Unique
            )

            $validatorFiles = @(
                foreach ($candidate in $codeFiles | Where-Object { $_.Name -like "*Validator.cs" }) {
                    $candidateSource = $sourceByPath[$candidate.FullName]
                    $sameStem = $candidate.BaseName -like ($featureStem + "*Validator")
                    $referencesClass = [regex]::IsMatch($candidateSource, $classPattern)
                    if ($sameStem -or $referencesClass) {
                        $candidate.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
                    }
                }
            ) | Sort-Object -Unique

            $writesAssets =
                $source.Contains("AssetDatabase.CreateAsset") -or
                $source.Contains("PrefabUtility.SaveAsPrefabAsset") -or
                $source.Contains("EditorSceneManager.SaveScene") -or
                $source.Contains("AssetDatabase.CopyAsset")

            [pscustomobject]@{
                file = $migrationFile.FullName.Substring($resolvedRoot.Length + 1).Replace('\', '/')
                class = $className
                lines = ($source -split "`r?`n").Count
                menus = $menuPaths
                reference_files = $referenceFiles
                validators = $validatorFiles
                literal_targets = $literalTargets
                writes_assets = $writesAssets
            }
        }
)

if ($Json) {
    $rows | ConvertTo-Json -Depth 6
    exit 0
}

foreach ($row in $rows) {
    $references = if ($row.reference_files.Count -gt 0) { $row.reference_files -join ", " } else { "-" }
    $validators = if ($row.validators.Count -gt 0) { $row.validators -join ", " } else { "-" }
    $targets = if ($row.literal_targets.Count -gt 0) { $row.literal_targets -join ", " } else { "-" }
    $menus = if ($row.menus.Count -gt 0) { $row.menus -join ", " } else { "-" }

    Write-Output ("[{0}] lines={1} writes_assets={2}" -f $row.file, $row.lines, $row.writes_assets)
    Write-Output ("  menus: {0}" -f $menus)
    Write-Output ("  references: {0}" -f $references)
    Write-Output ("  validators: {0}" -f $validators)
    Write-Output ("  targets: {0}" -f $targets)
}
