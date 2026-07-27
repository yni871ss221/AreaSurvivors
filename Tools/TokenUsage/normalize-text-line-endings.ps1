param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string]$ExpectedSha256,
    [ValidateSet("LF", "CRLF")]
    [string]$LineEnding = "LF"
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

$allowedExtensions = @(".cs", ".ps1", ".md", ".shader")
$extension = [System.IO.Path]::GetExtension($resolvedPath)
if ($extension -notin $allowedExtensions)
{
    throw "Line-ending normalization is not allowed for extension: $extension"
}

$bytes = [System.IO.File]::ReadAllBytes($resolvedPath)
$actualSha = [System.BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)).
    Replace("-", "").
    ToLowerInvariant()
if ($actualSha -ne $ExpectedSha256.ToLowerInvariant())
{
    throw "File changed after inspection. expected=$ExpectedSha256 actual=$actualSha"
}
if ($bytes -contains 0)
{
    throw "Line-ending normalization refuses files containing NUL bytes."
}

$hasUtf8Bom = $bytes.Length -ge 3 -and
    $bytes[0] -eq 0xEF -and
    $bytes[1] -eq 0xBB -and
    $bytes[2] -eq 0xBF
$offset = if ($hasUtf8Bom) { 3 } else { 0 }
$utf8 = New-Object System.Text.UTF8Encoding($false, $true)
$text = $utf8.GetString($bytes, $offset, $bytes.Length - $offset)
if ([regex]::IsMatch($text, "`r(?!`n)"))
{
    throw "Line-ending normalization refuses bare CR characters."
}

$normalized = $text.Replace("`r`n", "`n")
if ($LineEnding -eq "CRLF")
{
    $normalized = $normalized.Replace("`n", "`r`n")
}
[System.IO.File]::WriteAllText($resolvedPath, $normalized, $utf8)

$updatedBytes = [System.IO.File]::ReadAllBytes($resolvedPath)
$updatedSha = [System.BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash($updatedBytes)).
    Replace("-", "").
    ToLowerInvariant()
Write-Output "normalized_path: $resolvedPath"
Write-Output "line_ending: $LineEnding"
Write-Output "previous_sha256: $actualSha"
Write-Output "updated_sha256: $updatedSha"
