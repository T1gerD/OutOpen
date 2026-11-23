# 启动 OutOpen 桌面程序（独立进程，不依赖终端/控制台）。
#
# 为什么不直接 `dotnet run`：
#   dotnet run 会把 GUI 进程挂成终端会话的子进程，关闭终端时可能一起结束；
#   而且终端本身就是一个黑色控制台窗口。这里先构建，再用 ShellExecute 独立启动 exe。
param(
    [switch]$NoBuild,
    [switch]$Tray,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src/OutOpen.App/OutOpen.App.csproj'
$exe = Join-Path $root "src/OutOpen.App/bin/$Configuration/net10.0-windows/OutOpen.exe"

if (-not $NoBuild) {
    dotnet build $project -c $Configuration -v q --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build 失败（exit $LASTEXITCODE）"
    }
}

if (-not (Test-Path $exe)) {
    throw "找不到 $exe，请先构建或去掉 -NoBuild。"
}

$arguments = @()
if ($Tray) { $arguments += '--tray' }

# Start-Process 默认走 ShellExecute：新进程不继承当前终端的控制台，终端关闭不影响它。
Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory (Split-Path $exe)
Write-Host "已启动：$exe"
