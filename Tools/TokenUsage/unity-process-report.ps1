param(
    [switch]$IncludeCommandLine,
    [ValidateRange(64, 4096)]
    [int]$MaxCommandLineLength = 512
)

$ErrorActionPreference = "Stop"

$processes = Get-CimInstance Win32_Process |
    Where-Object { $_.Name -eq "Unity.exe" } |
    Sort-Object ProcessId

foreach ($process in $processes) {
    $runtimeProcess = Get-Process -Id $process.ProcessId -ErrorAction SilentlyContinue
    $record = [ordered]@{
        ProcessId = $process.ProcessId
        MainWindowHandle = if ($runtimeProcess -ne $null) { $runtimeProcess.MainWindowHandle } else { 0 }
        MainWindowTitle = if ($runtimeProcess -ne $null) { $runtimeProcess.MainWindowTitle } else { "" }
        Responding = if ($runtimeProcess -ne $null) { $runtimeProcess.Responding } else { $false }
    }
    if ($IncludeCommandLine) {
        $commandLine = [string]$process.CommandLine
        if ($commandLine.Length -gt $MaxCommandLineLength) {
            $commandLine = $commandLine.Substring(0, $MaxCommandLineLength) + "..."
        }
        $record.CommandLine = $commandLine
    }
    [pscustomobject]$record
}
