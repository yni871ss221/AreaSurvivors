param(
    [switch]$IncludeCommandLine,
    [ValidateRange(64, 4096)]
    [int]$MaxCommandLineLength = 512,
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

function Protect-CommandLineSecrets {
    param([string]$CommandLine)

    if ([string]::IsNullOrEmpty($CommandLine)) { return "" }
    $secretArgumentPattern =
        '(?i)(?<prefix>--?(?:accessToken|apiKey|password|secret|token)(?:\s+|=))(?<value>"[^"]*"|\S+)'
    return [regex]::Replace(
        $CommandLine,
        $secretArgumentPattern,
        {
            param($match)
            return $match.Groups["prefix"].Value + "<redacted>"
        })
}

if ($SelfTest) {
    $sample = 'Unity.exe -projectPath C:\Project -accessToken sample-value --apiKey=sample-key'
    $protected = Protect-CommandLineSecrets -CommandLine $sample
    if ($protected.Contains("sample-value") -or
        $protected.Contains("sample-key") -or
        -not $protected.Contains("-accessToken <redacted>") -or
        -not $protected.Contains("--apiKey=<redacted>") -or
        -not $protected.Contains("-projectPath C:\Project")) {
        throw "unity-process-report secret redaction self-test failed."
    }
    Write-Output "unity_process_report_self_test: passed"
    exit 0
}

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
        $commandLine = Protect-CommandLineSecrets -CommandLine ([string]$process.CommandLine)
        if ($commandLine.Length -gt $MaxCommandLineLength) {
            $commandLine = $commandLine.Substring(0, $MaxCommandLineLength) + "..."
        }
        $record.CommandLine = $commandLine
    }
    [pscustomobject]$record
}
