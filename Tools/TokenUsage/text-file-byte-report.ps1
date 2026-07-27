param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = "Stop"
$projectRoot = [System.IO.Path]::GetFullPath(
    (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))
$resolvedPath = [System.IO.Path]::GetFullPath(
    (Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path)
$projectPrefix = $projectRoot.TrimEnd('\', '/') +
    [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedPath.StartsWith(
        $projectPrefix,
        [System.StringComparison]::OrdinalIgnoreCase))
{
    throw "Path must remain inside the project: $resolvedPath"
}

$bytes = [System.IO.File]::ReadAllBytes($resolvedPath)
$hasUtf8Bom = $bytes.Length -ge 3 -and
    $bytes[0] -eq 0xEF -and
    $bytes[1] -eq 0xBB -and
    $bytes[2] -eq 0xBF
$offset = if ($hasUtf8Bom) { 3 } else { 0 }
$text = [System.Text.Encoding]::UTF8.GetString(
    $bytes,
    $offset,
    $bytes.Length - $offset)
$crlfCount = [regex]::Matches($text, "`r`n").Count
$bareLfCount = [regex]::Matches($text, "(?<!`r)`n").Count
$bareCrCount = [regex]::Matches($text, "`r(?!`n)").Count
$leadingWhitespaceKinds = @(
    $text.Split("`n") |
        Where-Object { $_ -match '^[\t ]+' } |
        ForEach-Object {
            if ($_ -match '^\t+') { "tab" } else { "space" }
        } |
        Sort-Object -Unique)
$sha256 = [System.BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)).
    Replace("-", "").
    ToLowerInvariant()

Write-Output "path: $resolvedPath"
Write-Output "bytes: $($bytes.Length)"
Write-Output "sha256: $sha256"
Write-Output "utf8_bom: $hasUtf8Bom"
Write-Output "crlf_count: $crlfCount"
Write-Output "bare_lf_count: $bareLfCount"
Write-Output "bare_cr_count: $bareCrCount"
Write-Output "nul_count: $([regex]::Matches($text, [char]0).Count)"
Write-Output "leading_whitespace: $($leadingWhitespaceKinds -join ',')"
