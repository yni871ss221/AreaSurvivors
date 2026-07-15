param(
    [Parameter(Mandatory = $true)][string]$VaultRoot,
    [Parameter(Mandatory = $true)][string]$RelativePath,
    [Parameter(Mandatory = $true)][string]$ContentPath,
    [switch]$WhatIf
)

$ErrorActionPreference = "Stop"

$resolvedVault = [System.IO.Path]::GetFullPath($VaultRoot).TrimEnd('\', '/')
if (-not (Test-Path -LiteralPath $resolvedVault -PathType Container)) {
    throw "Vault root does not exist: $resolvedVault"
}

if ([System.IO.Path]::IsPathRooted($RelativePath) -or [System.IO.Path]::GetExtension($RelativePath) -ne ".md") {
    throw "RelativePath must be a relative Markdown path: $RelativePath"
}

$target = [System.IO.Path]::GetFullPath((Join-Path $resolvedVault $RelativePath))
$vaultPrefix = $resolvedVault + [System.IO.Path]::DirectorySeparatorChar
if (-not $target.StartsWith($vaultPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Target must remain inside the vault: $target"
}

$resolvedContent = [System.IO.Path]::GetFullPath($ContentPath)
if (-not (Test-Path -LiteralPath $resolvedContent -PathType Leaf)) {
    throw "Content file does not exist: $resolvedContent"
}

if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw "Target note does not exist: $target"
}

$content = Get-Content -LiteralPath $resolvedContent -Raw -Encoding UTF8
if ([string]::IsNullOrWhiteSpace($content)) {
    throw "Content file is empty: $resolvedContent"
}

if ($WhatIf) {
    Write-Output "append_vault_note_whatif: passed"
    Write-Output "target: $target"
    Write-Output "content_chars: $($content.Length)"
    exit 0
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::AppendAllText($target, $content, $utf8NoBom)
Write-Output "append_vault_note: completed"
Write-Output "target: $target"
Write-Output "content_chars: $($content.Length)"
