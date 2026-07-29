[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [ValidateSet("All", "CSharp", "PowerShell")]
    [string]$Language = "All",
    [ValidateRange(1, 50)][int]$MaxResults = 20,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$indexPath = Join-Path $PSScriptRoot "structure-index.ps1"
$indexArguments = @{
    Action = "Query"
    Path = $Path
    Language = $Language
    MaxResults = $MaxResults
}
if ($Force) {
    $indexArguments.Force = $true
}
& $indexPath @indexArguments

$cachePath = Join-Path $PSScriptRoot "semantic-summary-cache.ps1"
& $cachePath -Action Query -Path $Path
