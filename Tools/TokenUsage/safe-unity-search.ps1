<#
.SYNOPSIS
Runs the fixed Scene/Prefab search Reporter for one query.

.DESCRIPTION
Formal usage: safe-unity-search.ps1 -Query <object-or-field-name> [-PrintOutput]
The Reporter owns its Scene/Prefab scope. This wrapper does not accept -Path.
It connects to Unity and executes an Editor Menu Reporter; do not use it in a
subtask whose contract forbids Unity or Menu execution.
#>
param([Parameter(Mandatory = $true)][string]$Query, [switch]$PrintOutput)
& "$PSScriptRoot\Invoke-AreaUnitySearch.ps1" -Query $Query -PrintOutput:$PrintOutput
