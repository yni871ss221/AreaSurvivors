param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Compile", "ConsoleLogs", "ConsoleErrors", "ConsoleWarnings", "Menu", "MenuExists", "Eval", "AssetImport", "AssetRefresh", "Screenshot", "PlayEnter", "PlayExit", "PlayStatus")]
    [string]$Action,
    [string]$MenuPath = "",
    [string]$EvalCode = "",
    [string]$AssetPath = "",
    [string]$ScreenshotPath = "",
    [int]$MaxCount = 30,
    [int]$TimeoutSeconds = 0,
    [ValidateRange(0, 600)][int]$CompileWaitSeconds = 0,
    [int]$PlayExitCooldownSeconds = 20,
    [int]$CompileAfterRefreshCooldownSeconds = 30,
    [int]$CommandLockTimeoutSeconds = 2,
    [switch]$PrintOutput,
    [switch]$AllowHighOutput
)

& "$PSScriptRoot\Invoke-AreaSafeUnity.ps1" -Action $Action -MenuPath $MenuPath -EvalCode $EvalCode -AssetPath $AssetPath -ScreenshotPath $ScreenshotPath -MaxCount $MaxCount -TimeoutSeconds $TimeoutSeconds -CompileWaitSeconds $CompileWaitSeconds -PlayExitCooldownSeconds $PlayExitCooldownSeconds -CompileAfterRefreshCooldownSeconds $CompileAfterRefreshCooldownSeconds -CommandLockTimeoutSeconds $CommandLockTimeoutSeconds -PrintOutput:$PrintOutput -AllowHighOutput:$AllowHighOutput
$safeExitCode = $LASTEXITCODE
if ($safeExitCode -ne 0) { exit $safeExitCode }
