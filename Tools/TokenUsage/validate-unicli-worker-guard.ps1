[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$manifestPath = Join-Path $projectRoot 'Packages\manifest.json'
$bootstrapPath = Join-Path $projectRoot 'Packages\com.yucchiy.unicli-server\Editor\UniCliServerBootstrap.cs'
$packageJsonPath = Join-Path $projectRoot 'Packages\com.yucchiy.unicli-server\package.json'

foreach ($path in @($manifestPath, $bootstrapPath, $packageJsonPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required UniCLI guard file is missing: $path"
    }
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$dependency = $manifest.dependencies.'com.yucchiy.unicli-server'
if ([string]::IsNullOrWhiteSpace($dependency)) {
    throw 'UniCLI must remain a direct project dependency so the embedded package overrides it.'
}

$package = Get-Content -LiteralPath $packageJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($package.name -ne 'com.yucchiy.unicli-server') {
    throw "Unexpected embedded package name: $($package.name)"
}

$bootstrap = Get-Content -LiteralPath $bootstrapPath -Raw -Encoding UTF8
$constructorPattern = '(?s)static UniCliServerBootstrap\(\)\s*\{(?<body>.*?)\n\s*\}'
$constructorMatch = [regex]::Match($bootstrap, $constructorPattern)
if (-not $constructorMatch.Success) {
    throw 'UniCliServerBootstrap static constructor was not found.'
}

$constructorBody = $constructorMatch.Groups['body'].Value
$workerGuardIndex = $constructorBody.IndexOf('AssetDatabase.IsAssetImportWorkerProcess()', [StringComparison]::Ordinal)
$pidWriteIndex = $constructorBody.IndexOf('EnsurePidFile()', [StringComparison]::Ordinal)
if ($workerGuardIndex -lt 0 -or $pidWriteIndex -lt 0 -or $workerGuardIndex -gt $pidWriteIndex) {
    throw 'AssetImportWorker guard must execute before EnsurePidFile().'
}

$startServerPattern = '(?s)public static void StartServer\(\)\s*\{(?<body>.*?)\n\s*\}'
$startServerMatch = [regex]::Match($bootstrap, $startServerPattern)
if (-not $startServerMatch.Success -or
    $startServerMatch.Groups['body'].Value.IndexOf('AssetDatabase.IsAssetImportWorkerProcess()', [StringComparison]::Ordinal) -lt 0) {
    throw 'StartServer must reject AssetImportWorker processes.'
}

[PSCustomObject]@{
    valid = $true
    package = $package.name
    version = $package.version
    direct_dependency = $dependency
    embedded_override = $true
    worker_guard_before_pid_write = $true
    worker_guard_in_start_server = $true
}
