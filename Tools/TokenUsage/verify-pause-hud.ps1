param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Normal", "Pause", "Resume")]
    [string]$Mode
)

[Console]::Error.WriteLine(
    "guard_code: 29; verify-pause-hud.ps1 mode '$Mode' is retired because Play Mode Eval can trigger Domain Reload and leave UniCLI permanently busy. Verify pause HUD through normal game input and a screenshot, or implement a precompiled validation hook before automating this check.")
exit 29
