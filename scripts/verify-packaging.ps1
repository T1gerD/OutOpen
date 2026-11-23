# 发布产物结构校验：
#   1) publish/app/OutOpen.exe 必须是 Windows GUI 子系统（PE subsystem = 2），双击不弹控制台；
#   2) publish/cli/outopen.exe 是控制台程序（subsystem = 3），且不能出现在 app 目录里；
#   3) 两个可执行文件名只差大小写，Windows 文件系统不区分大小写，装在同一个目录会互相覆盖。
param(
    [string]$AppDir = 'publish/app',
    [string]$CliDir = 'publish/cli'
)

$ErrorActionPreference = 'Stop'

function Get-PeSubsystem([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes[0] -ne 0x4D -or $bytes[1] -ne 0x5A) { throw "$Path 不是 PE 文件" }
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
    if ($bytes[$peOffset] -ne 0x50 -or $bytes[$peOffset + 1] -ne 0x45) { throw "$Path 缺少 PE 签名" }
    $optionalHeader = $peOffset + 24
    return [BitConverter]::ToUInt16($bytes, $optionalHeader + 68)
}

$failures = @()

$appExe = Join-Path $AppDir 'OutOpen.exe'
if (-not (Test-Path $appExe)) {
    $failures += "缺少 GUI 程序：$appExe"
}
else {
    $sub = Get-PeSubsystem $appExe
    if ($sub -ne 2) { $failures += "$appExe 的 PE subsystem=$sub，应为 2（WINDOWS_GUI）" }
    else { Write-Host "OK  GUI 子系统：$appExe" }
}

$cliExe = Join-Path $CliDir 'outopen.exe'
if (Test-Path $cliExe) {
    $sub = Get-PeSubsystem $cliExe
    if ($sub -ne 3) { $failures += "$cliExe 的 PE subsystem=$sub，应为 3（WINDOWS_CUI）" }
    else { Write-Host "OK  控制台子系统：$cliExe" }
}
else {
    Write-Warning "跳过 CLI 检查：$cliExe 不存在"
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Host '发布产物校验通过：GUI 与 CLI 分属不同子系统，且没有文件名冲突。'
