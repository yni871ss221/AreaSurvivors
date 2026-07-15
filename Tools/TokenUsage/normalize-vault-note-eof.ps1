param(
    [Parameter(Mandatory = $true)][string]$VaultRoot,
    [Parameter(Mandatory = $true)][string]$RelativePath,
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
if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
    throw "Target note does not exist: $target"
}

$text = [System.IO.File]::ReadAllText($target, [System.Text.Encoding]::UTF8)
$normalized = [regex]::Replace($text, '(\r?\n)+\z', '') + "`n"
$changed = -not [string]::Equals($text, $normalized, [System.StringComparison]::Ordinal)

if ($WhatIf) {
    Write-Output "normalize_vault_note_eof_whatif: passed"
    Write-Output "target: $target"
    Write-Output "changed: $($changed.ToString().ToLowerInvariant())"
    exit 0
}

if ($changed) {
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($target, $normalized, $utf8NoBom)
}
Write-Output "normalize_vault_note_eof: completed"
Write-Output "target: $target"
Write-Output "changed: $($changed.ToString().ToLowerInvariant())"
