param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Capture", "Click", "SendEscape", "StopPlay")]
    [string]$Action,
    [Parameter(Mandatory = $true)][int]$ProcessId,
    [string]$ExpectedTitle = "AreaSurvivors",
    [string]$OutputPath = "Temp/AreaSurvivors/unity-window.png",
    [int]$WindowX = -1,
    [int]$WindowY = -1
)

$ErrorActionPreference = "Stop"

$process = Get-Process -Id $ProcessId -ErrorAction Stop
if ($process.ProcessName -ne "Unity" -or $process.MainWindowHandle -eq 0) {
    throw "Process $ProcessId is not a Unity process with a main window."
}
if ([string]::IsNullOrWhiteSpace($process.MainWindowTitle) -or
    $process.MainWindowTitle.IndexOf($ExpectedTitle, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
    throw "Unity window title did not match '$ExpectedTitle': $($process.MainWindowTitle)"
}

Add-Type -AssemblyName Microsoft.VisualBasic
[Microsoft.VisualBasic.Interaction]::AppActivate($ProcessId)
Start-Sleep -Milliseconds 750

if ($Action -eq "SendEscape") {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait("{ESC}")
    Start-Sleep -Milliseconds 1000
    Write-Output "unity_escape_sent: true"
    Write-Output ("process_id: {0}" -f $ProcessId)
    Write-Output ("window_title: {0}" -f $process.MainWindowTitle)
    exit 0
}

if ($Action -eq "StopPlay") {
    Add-Type -AssemblyName System.Windows.Forms
    [System.Windows.Forms.SendKeys]::SendWait("^p")
    Start-Sleep -Milliseconds 1000
    Write-Output "unity_stop_shortcut_sent: true"
    Write-Output ("process_id: {0}" -f $ProcessId)
    Write-Output ("window_title: {0}" -f $process.MainWindowTitle)
    exit 0
}

if (-not $OutputPath.Replace("\", "/").StartsWith("Temp/", [System.StringComparison]::Ordinal) -or
    -not $OutputPath.EndsWith(".png", [System.StringComparison]::OrdinalIgnoreCase) -or
    $OutputPath.Replace("\", "/").Split("/", [System.StringSplitOptions]::RemoveEmptyEntries) -contains "..") {
    throw "Capture OutputPath must be a project-relative PNG path under Temp/."
}

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class AreaUnityWindowNative
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")]
    public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
"@

$rect = New-Object AreaUnityWindowNative+Rect
if (-not [AreaUnityWindowNative]::GetWindowRect([IntPtr]$process.MainWindowHandle, [ref]$rect)) {
    throw "GetWindowRect failed for Unity process $ProcessId."
}
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -le 0 -or $height -le 0) {
    throw "Unity window bounds are invalid: ${width}x${height}."
}

if ($Action -eq "Click") {
    if ($WindowX -lt 0 -or $WindowY -lt 0 -or $WindowX -ge $width -or $WindowY -ge $height) {
        throw "Click coordinates must be inside the validated Unity window: (${WindowX}, ${WindowY}) for ${width}x${height}."
    }
    $screenX = $rect.Left + $WindowX
    $screenY = $rect.Top + $WindowY
    if (-not [AreaUnityWindowNative]::SetCursorPos($screenX, $screenY)) {
        throw "SetCursorPos failed for Unity window coordinate (${WindowX}, ${WindowY})."
    }
    [AreaUnityWindowNative]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [AreaUnityWindowNative]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 1000
    Write-Output "unity_window_click_sent: true"
    Write-Output ("window_coordinate: {0},{1}" -f $WindowX, $WindowY)
    Write-Output ("process_id: {0}" -f $ProcessId)
    Write-Output ("window_title: {0}" -f $process.MainWindowTitle)
    exit 0
}

$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$absolutePath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot "Temp"))
if (-not $absolutePath.StartsWith($allowedRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Capture output escaped the project Temp directory."
}
[System.IO.Directory]::CreateDirectory((Split-Path $absolutePath -Parent)) | Out-Null

$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
try {
    $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
    $bitmap.Save($absolutePath, [System.Drawing.Imaging.ImageFormat]::Png)
} finally {
    $graphics.Dispose()
    $bitmap.Dispose()
}

Write-Output ("unity_window_capture: {0}" -f $absolutePath)
Write-Output ("resolution: {0}x{1}" -f $width, $height)
