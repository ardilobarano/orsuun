# Launches the Windows playtest build, waits, saves a PNG of its window and closes it.
#   tools\screenshot.ps1 -Out artifacts\lane.png -Wait 14
#   tools\screenshot.ps1 -Out artifacts\forge.png -GameArgs '-forge'
param(
    [string]$Out = "artifacts\screenshot.png",
    [int]$Wait = 12,
    [string[]]$GameArgs = @()
)

$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "client\Builds\Windows\Orsuun.exe"
$outPath = if ([IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path $root $Out }

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class OrsuunWin {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    public struct RECT { public int L, T, R, B; }
}
"@
[OrsuunWin]::SetProcessDPIAware() | Out-Null

$p = Start-Process $exe -ArgumentList (@('-screen-fullscreen', '0', '-screen-width', '540', '-screen-height', '960') + $GameArgs) -PassThru
Start-Sleep -Seconds $Wait
$p.Refresh()
[OrsuunWin]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 600

$r = New-Object OrsuunWin+RECT
[OrsuunWin]::GetWindowRect($p.MainWindowHandle, [ref]$r) | Out-Null
$bmp = New-Object System.Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size)
$bmp.Save($outPath)
$g.Dispose(); $bmp.Dispose()
Stop-Process -Id $p.Id -Force
$outPath
