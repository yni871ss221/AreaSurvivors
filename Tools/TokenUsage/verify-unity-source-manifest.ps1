[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [string]$BeeRoot = "",
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

function Find-MissingCompiledCSharpSource {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$ArtifactsRoot
    )

    if (-not (Test-Path -LiteralPath $ArtifactsRoot -PathType Container)) {
        return @()
    }

    $missing = New-Object System.Collections.Generic.HashSet[string](
        [System.StringComparer]::OrdinalIgnoreCase
    )
    $responseFiles = @(
        Get-ChildItem -LiteralPath $ArtifactsRoot -Filter "*.rsp" -File -Recurse
    )
    foreach ($responseFile in $responseFiles) {
        foreach ($line in [System.IO.File]::ReadLines($responseFile.FullName)) {
            $candidate = $line.Trim().Trim('"')
            if (-not $candidate.EndsWith(
                    ".cs",
                    [System.StringComparison]::OrdinalIgnoreCase
                )) {
                continue
            }

            $resolved = if ([System.IO.Path]::IsPathRooted($candidate)) {
                $candidate
            } else {
                Join-Path $Root $candidate
            }
            if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
                [void]$missing.Add($candidate)
            }
        }
    }
    return @($missing | Sort-Object)
}

if ($SelfTest) {
    $testRoot = Join-Path (
        [System.IO.Path]::GetTempPath()
    ) ("area-unity-manifest-" + [guid]::NewGuid().ToString("N"))
    try {
        $artifacts = Join-Path $testRoot "Library\Bee\artifacts\self-test"
        $existingSource = Join-Path $testRoot "Assets\Existing.cs"
        New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
        New-Item -ItemType Directory -Path (Split-Path $existingSource -Parent) -Force |
            Out-Null
        [System.IO.File]::WriteAllText($existingSource, "sealed class Existing {}")
        [System.IO.File]::WriteAllLines(
            (Join-Path $artifacts "Assembly-CSharp.rsp"),
            @(
                '"Assets/Existing.cs"',
                '"Assets/Missing.cs"',
                '-reference:"Library/NotSource.dll"'
            )
        )

        $result = @(
            Find-MissingCompiledCSharpSource `
                -Root $testRoot `
                -ArtifactsRoot (Join-Path $testRoot "Library\Bee\artifacts")
        )
        if ($result.Count -ne 1 -or $result[0] -ne "Assets/Missing.cs") {
            throw "Unity source manifest self-test did not isolate the missing source."
        }
        Write-Output "unity_source_manifest_self_test: passed"
        return
    } finally {
        if (Test-Path -LiteralPath $testRoot) {
            Remove-Item -LiteralPath $testRoot -Recurse -Force
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
}
if ([string]::IsNullOrWhiteSpace($BeeRoot)) {
    $BeeRoot = Join-Path $ProjectRoot "Library\Bee\artifacts"
}

$missingSources = @(
    Find-MissingCompiledCSharpSource `
        -Root $ProjectRoot `
        -ArtifactsRoot $BeeRoot
)
if ($missingSources.Count -gt 0) {
    $sample = ($missingSources | Select-Object -First 5) -join "; "
    throw (
        "guard_code: 46; Unity compile manifest references missing C# source(s): " +
        "$sample. Use invoke-unity-editor-runner.ps1 -BatchRefresh so " +
        "AssetDatabase acknowledges deletions before Compile."
    )
}

Write-Output "unity_source_manifest: current"
