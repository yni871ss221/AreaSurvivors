param(
    [string]$ProjectRoot = "",
    [ValidateRange(0, 600)][int]$WaitTimeoutSeconds = 0,
    [ValidateRange(100, 10000)][int]$PollIntervalMilliseconds = 1000
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
}
$resolvedRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$assetsRoot = Join-Path $resolvedRoot "Assets"
$assembliesRoot = Join-Path $resolvedRoot "Library\ScriptAssemblies"
$beeArtifactsRoot = Join-Path $resolvedRoot "Library\Bee\artifacts"
$runtimeAssemblyPath = Join-Path $assembliesRoot "Assembly-CSharp.dll"
$editorAssemblyPath = Join-Path $assembliesRoot "Assembly-CSharp-Editor.dll"

if (-not (Test-Path -LiteralPath $assetsRoot -PathType Container)) {
    throw "Assets directory does not exist: $assetsRoot"
}

$sources = @(Get-ChildItem -LiteralPath $assetsRoot -Filter "*.cs" -File -Recurse)
$runtimeSources = @($sources | Where-Object { $_.FullName -notmatch '[\\/]Editor[\\/]' })
$editorSources = @($sources | Where-Object { $_.FullName -match '[\\/]Editor[\\/]' })

function Assert-AssemblyCurrent {
    param(
        [Parameter(Mandatory = $true)][System.IO.FileInfo[]]$SourceFiles,
        [Parameter(Mandatory = $true)][string]$AssemblyPath,
        [Parameter(Mandatory = $true)][string]$BeeArtifactsRoot,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if ($SourceFiles.Count -eq 0) {
        Write-Output "$Label`: no source files"
        return
    }
    if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) {
        throw "$Label assembly is missing: $AssemblyPath"
    }

    $latestSource = $SourceFiles | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    $assembly = Get-Item -LiteralPath $AssemblyPath
    if ($assembly.LastWriteTimeUtc -lt $latestSource.LastWriteTimeUtc) {
        $assemblyName = Split-Path $AssemblyPath -Leaf
        $latestBeeArtifact = $null
        if (Test-Path -LiteralPath $BeeArtifactsRoot -PathType Container) {
            $latestBeeArtifact = Get-ChildItem -LiteralPath $BeeArtifactsRoot -Filter $assemblyName -File -Recurse -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTimeUtc -Descending |
                Select-Object -First 1
        }

        if ($null -ne $latestBeeArtifact -and $latestBeeArtifact.LastWriteTimeUtc -ge $latestSource.LastWriteTimeUtc) {
            $assemblyHash = (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
            $beeArtifactHash = (Get-FileHash -LiteralPath $latestBeeArtifact.FullName -Algorithm SHA256).Hash
            if ($assemblyHash -eq $beeArtifactHash) {
                Write-Output "$Label`: current via Bee artifact hash"
                Write-Output "  latest_source: $($latestSource.FullName)"
                Write-Output "  source_utc: $($latestSource.LastWriteTimeUtc.ToString('o'))"
                Write-Output "  assembly_utc: $($assembly.LastWriteTimeUtc.ToString('o'))"
                Write-Output "  bee_artifact_utc: $($latestBeeArtifact.LastWriteTimeUtc.ToString('o'))"
                return
            }
        }

        $beeEvidence = if ($null -eq $latestBeeArtifact) {
            "bee_artifact=missing"
        } else {
            "bee_artifact=$($latestBeeArtifact.FullName); bee_artifact_utc=$($latestBeeArtifact.LastWriteTimeUtc.ToString('o'))"
        }
        throw "guard_code: 41; $Label assembly is stale. safe-unity Compile is verification-only and does not import externally changed C# files. Import every changed script through the approved AssetImport or RegisterAndRun path before the next compile verification. latest_source=$($latestSource.FullName); source_utc=$($latestSource.LastWriteTimeUtc.ToString('o')); assembly_utc=$($assembly.LastWriteTimeUtc.ToString('o')); $beeEvidence"
    }

    Write-Output "$Label`: current"
    Write-Output "  latest_source: $($latestSource.FullName)"
    Write-Output "  source_utc: $($latestSource.LastWriteTimeUtc.ToString('o'))"
    Write-Output "  assembly_utc: $($assembly.LastWriteTimeUtc.ToString('o'))"
}

$deadlineUtc = [DateTime]::UtcNow.AddSeconds($WaitTimeoutSeconds)
while ($true) {
    try {
        $runtimeEvidence = @(Assert-AssemblyCurrent -SourceFiles $runtimeSources -AssemblyPath $runtimeAssemblyPath -BeeArtifactsRoot $beeArtifactsRoot -Label "runtime")
        $editorEvidence = @(Assert-AssemblyCurrent -SourceFiles $editorSources -AssemblyPath $editorAssemblyPath -BeeArtifactsRoot $beeArtifactsRoot -Label "editor")
        $runtimeEvidence | Write-Output
        $editorEvidence | Write-Output
        Write-Output "unity_script_compilation_verification: passed"
        break
    } catch {
        $isStaleAssembly = $_.Exception.Message.Contains("guard_code: 41;")
        if (-not $isStaleAssembly -or [DateTime]::UtcNow -ge $deadlineUtc) { throw }
        Start-Sleep -Milliseconds $PollIntervalMilliseconds
    }
}
