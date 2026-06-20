param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Compile", "ConsoleErrors", "Menu", "Eval")]
    [string]$Action,
    [string]$MenuPath = "",
    [string]$EvalCode = "",
    [int]$MaxCount = 30,
    [switch]$PrintOutput,
    [switch]$AllowHighOutput
)

& "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action $Action -MenuPath $MenuPath -EvalCode $EvalCode -MaxCount $MaxCount -PrintOutput:$PrintOutput -AllowHighOutput:$AllowHighOutput
