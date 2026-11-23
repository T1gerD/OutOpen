Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W2 {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  public struct RECT { public int Left, Top, Right, Bottom; }
}
"@
$p = Get-Process OutOpen | Select-Object -First 1
$h = $p.MainWindowHandle
[W2]::SetWindowPos($h, [IntPtr]::Zero, 40, 20, 1260, 1180, 0x0040) | Out-Null
Start-Sleep -Milliseconds 900
$r = New-Object W2+RECT
[W2]::GetWindowRect($h, [ref]$r) | Out-Null
$bmp = New-Object System.Drawing.Bitmap ($r.Right-$r.Left), ($r.Bottom-$r.Top)
$g = [System.Drawing.Graphics]::FromImage($bmp); $hdc=$g.GetHdc(); [W2]::PrintWindow($h,$hdc,2)|Out-Null; $g.ReleaseHdc($hdc); $g.Dispose()
$bmp.Save('C:\Users\33502\Documents\OutOpen-evidence\quark-sources-tall.png',[System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
'restored'
[W2]::SetWindowPos($h, [IntPtr]::Zero, 40, 20, 1200, 800, 0x0040) | Out-Null
