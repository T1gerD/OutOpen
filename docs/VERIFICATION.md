# 技术验证记录

本文件记录 OutOpen MVP 在真实 Windows 环境中的验证过程与结论，用来区分「代码写完了」「单元测试过了」和「在真机上确实能接管文件」。

- 验证日期：2026-11-22 ~ 2026-11-23
- 验证机器：Windows 10 专业版 19045.6466（x64）
- 运行时：.NET SDK 11.0.100，目标框架 `net10.0-windows`，运行时 `Microsoft.WindowsDesktop.App 10.0.15`
- 代码状态：`dotnet build OutOpen.sln` 0 警告 0 错误；`dotnet test OutOpen.sln` 通过 76、失败 0

## 1. 单元测试

```
dotnet test OutOpen.sln -v q --nologo
已通过! - 失败: 0，通过: 76，已跳过: 0，总计: 76
```

覆盖：类型识别与签名、临时文件/占用/稳定性校验、规则优先级、去重窗口、配置损坏恢复与数值 clamp、查看器可用/无关联/失效/稍后提醒、流水线全部状态（含回退与失败）、目录解析、路径脱敏、日志保留、宿主事件与配置重载。

测试中的外部启动全部使用假实现（`IFileLauncher`、`IDefaultProgramResolver`），不启动真实程序。

## 2. 本机环境探测

```
dotnet run --project src/OutOpen.Cli -- status
```

| 项目 | 结果 |
| --- | --- |
| 微信 PC 版 | 目录存在：`C:\Users\33502\Documents\WeChat Files\wxid_x6eaxxil577p22\FileStorage\File`（3.x 布局） |
| QQ（NT） | 目录存在：`C:\Users\33502\Documents\Tencent Files\3350202397\nt_qq\nt_data\File` |
| 钉钉 / 百度网盘 / 阿里云盘 / Telegram | 本机未安装或目录不同，界面显示「未启动 / 未找到」，没有假装可接管 |
| OneDrive | 目录存在（`C:\Users\33502\OneDrive`），默认关闭 |

## 3. 文件识别与查看器检测

对微信目录里一份真实 Word 文档执行 `outopen check`：

```
文件：C:\Users\33502\Documents\WeChat Files\wxid_x6eaxxil577p22\FileStorage\File\2025-06\实践活动1.docx
存在：True
扩展名：.docx
识别类型：Word 文档（Document）
MIME：application/vnd.openxmlformats-officedocument.wordprocessingml.document
文件签名：ZIP / OOXML
签名匹配：是
支持类型：是
完整性校验：通过
规则结论：ExternalOpen — Word 文档 的默认策略：外部打开
查看器：Available — 已关联默认程序：Word
```

`outopen viewers .md`：本机 `.md` 已关联 MaxInk（`C:\Users\33502\AppData\Local\MaxInk\maxink.exe`），同时列出 MaxInk / Notepad++ / Obsidian / VS Code 的官方入口与核验日期。`.pdf` 本机默认程序是 PDFGear。

## 4. 端到端接管验证（微信）

在微信接收目录投放一个 Markdown 文件：

```
C:\Users\33502\Documents\WeChat Files\wxid_x6eaxxil577p22\FileStorage\File\2025-06\OutOpen-接管验证-20261122.md
```

约 6 秒后（稳定性等待 2 秒 + 去抖）日志出现：

```
2026-11-22 23:53:27.015 +08:00 [INFO ] [pipeline] 已打开：wechat
  %USERPROFILE%\Documents\WeChat Files\wxid_***\FileStorage\File\2025-06\OutOpen-接管验证-***.md
  （pid 30232，系统默认程序）
```

MaxInk 被拉起并打开该文件；概览页「处理记录」+1，事件页显示「已打开」。这条记录同时证明：目录监听 → 稳定性等待 → 识别 → 规则 → Shell 启动 → 日志脱敏全链路可用。

## 5. 暂停与恢复

| 操作 | 日志/结果 |
| --- | --- |
| 点击「暂停接管」 | `23:56:39.767 [host] 已暂停接管`，概览页变为橙色「已暂停」胶囊 |
| 暂停期间投放 `OutOpen-pause-check.md` | 没有被打开，也没有产生打开记录 |
| 点击「恢复接管」 | `23:57:21.468 [host] 已恢复接管` |
| 恢复后投放 `OutOpen-third-check.md`、`OutOpen-resume-immediate.md` | 分别被打开（pid 29736 / 1344） |

说明：恢复后约 1 秒内立即写入的文件有一次未被捕获，之后两次复测均正常。这属于监听器重启后的极短窗口，已记录在案，不把它当成「已完全验证」的时序保证。

## 6. 重复事件

先投放 `OutOpen-dup-check.md`（被正常打开），删除后 1 秒内重建同名文件，同一去重窗口（8 秒）内：

```
23:59:36  重复忽略  OutOpen-dup-check.md  短时间内的重复事件，已忽略
```

日志中没有第二次启动外部程序的记录，符合「同一文件短时间只处理一次」的设计。

## 6.1 校验与回退实测（补充）

**不完整文件不打开**：向微信接收目录写入 0 字节的 `OutOpen-empty-check.md`，等待稳定性检查后：

```
00:49:42 [WARN ] [pipeline] 校验失败：wechat %USERPROFILE%\...\OutOpen-empty-check.md → 文件为空（0 字节），可能尚未写入完成
```

事件页显示「校验未通过 / 文件为空（0 字节），可能尚未写入完成」，没有启动任何外部程序（截图 `docs/screenshots/13-validation-events.png`）。临时后缀（`.part`、`.crdownload` 等）的文件在候选阶段就被过滤，不会触发打开。

**失败安全回退**：把 `config.json` 中 `.md` 的 `targetProgram` 指向不存在的 `C:\NoSuchFolder\missing-viewer.exe`，重启后投放 `OutOpen-fallback-check.md`：

```
00:51:19 [WARN ] [pipeline] 指定程序启动失败，已回退系统默认程序：wechat %USERPROFILE%\...\OutOpen-fallback-check.md（指定的程序不存在：C:\NoSuchFolder\missing-viewer.exe）
```

事件页显示「回退打开 / 指定的打开程序不可用，已回退到系统默认程序打开」，文件最终由 MaxInk 打开（截图 `docs/screenshots/15-fallback-events.png`）。测试后已把配置恢复为原值。

## 6.2 安装向导与卸载实测（补充）

用 Inno Setup 6.7.3 编译 `installer/OutOpen.iss`，生成 `dist/OutOpen-0.1.2-setup.exe`（约 2.6 MB，内置简体中文语言文件与自绘向导图）。

**安装向导选项**：图形安装时在「附加选项」页分别验证两个选项的独立效果。

| 选项 | 勾选 | 不勾选 |
| --- | --- | --- |
| 创建桌面快捷方式 | 生成 `%USERPROFILE%\Desktop\OutOpen.lnk` | 不生成（实测该路径不存在） |
| 开机自动启动（静默运行在系统托盘） | `HKCU\...\CurrentVersion\Run\OutOpen` = `"C:\Users\...\Programs\OutOpen\OutOpen.exe" --tray` | 无该注册表值 |

安装完成后 `%LOCALAPPDATA%\Programs\OutOpen` 与开始菜单三个快捷方式（OutOpen、OutOpen 命令行、卸载 OutOpen）总是创建；静默安装（`/VERYSILENT`）按设计不创建桌面快捷方式与自启动项。

**卸载**：程序运行中卸载时，卸载器通过 `OutOpen.exe --quit` 让实例退出，exit 0；程序未运行时同样 exit 0（约 5 秒）。卸载会清理程序目录、开始菜单/桌面快捷方式与自启动项，但保留 `%LOCALAPPDATA%\OutOpen` 下的配置、日志与推荐库。

**修复记录**：早期 `--quit` 只在「已有实例」分支生效，没有实例时会被当成一次正常启动，导致无窗口的卸载流程挂起。已改为在创建互斥量之前处理 `--quit`，并用 20×100ms 重试等待对方完成启动；修复后 `dotnet build` 0 警告 0 错误、`dotnet test` 76 通过。

截图：`docs/screenshots/20-installer-welcome.png`、`21-installer-license.png`、`22-installer-options.png`、`23-installer-finish.png`。

## 6.3 GUI 启动方式与安装布局（0.1.2 修复）

用户反馈：双击安装后的 EXE 先出现控制台窗口，关闭终端时软件本体也退出。排查确认这是**打包布局缺陷**，不是启动代码问题：

- GUI 是 `src/OutOpen.App`（`OutputType=WinExe`，PE subsystem=2）；CLI 是 `src/OutOpen.Cli`（AssemblyName `outopen`，PE subsystem=3）。
- 早期 Inno 脚本把 `publish\cli\outopen.exe` 装到 `{app}`，与 GUI 的 `{app}\OutOpen.exe` **只差大小写**。Windows 文件系统不区分大小写，安装时 CLI 覆盖了 GUI —— 安装目录里只剩控制台程序 `outopen.exe`。
- 实测（0.1.1 安装目录）：`%LOCALAPPDATA%\Programs\OutOpen\OutOpen.exe` 的 PE subsystem = **3**，`OutOpen.deps.json` 属于 GUI 而可执行文件属于 CLI，双击自然先弹控制台。

修复内容（0.1.2）：

1. CLI 安装到 `{app}\cli\outopen.exe`，开始菜单「OutOpen 命令行」指向该路径；卸载时清理 `{app}\cli`。
2. `App.OnStartup` 最先调用 `FreeConsole()`：即便被宿主挂到控制台，也主动脱离，保证进程与终端生命周期无关。
3. GUI 日志只走 `FileAppLog`（`%LOCALAPPDATA%\OutOpen\logs\outopen-yyyyMMdd.log`），不依赖 `Console`；`ConsoleAppLog` 仅供 CLI 使用。
4. 新增 `scripts/run-app.ps1`：构建后用 `Start-Process` 独立启动，开发时不再用 `dotnet run` 当日常入口。
5. 新增 `scripts/verify-packaging.ps1`，在 CI 的 installer job 中校验：GUI 产物 subsystem 必须为 2、CLI 必须为 3。

验证结果（0.1.2）：

- `dotnet build OutOpen.sln` 0 警告 0 错误；`dotnet test` 76 通过。
- `pwsh scripts/verify-packaging.ps1`：`OK  GUI 子系统：publish/app/OutOpen.exe`、`OK  控制台子系统：publish/cli/outopen.exe`。
- 安装后目录同时存在 `OutOpen.exe`（subsystem=2）与 `cli\outopen.exe`（subsystem=3）；双击 GUI 只出现主窗口，任务管理器里没有 `conhost.exe` 子进程。
- 从命令行启动 GUI 后关闭该终端，GUI 继续运行；`OutOpen.exe --quit` 可正常退出。
- `cli\outopen.exe status` 正常工作，输出与源码构建一致。
- 自启项指向桌面宿主：`cli\outopen.exe autostart on` 写入的是 `"...\Programs\OutOpen\OutOpen.exe" --tray`，而不是 CLI 自身（CLI 会解析 `..\OutOpen.exe`；早期版本误把 `cli\outopen.exe` 写进启动项，会拉起控制台进程，已修复）。
- 卸载回归：静默卸载 `exit 0`，程序目录、开始菜单、桌面快捷方式与 Run 键都清理，`%LOCALAPPDATA%\OutOpen`（config/logs/viewer-catalog）保留。

## 6.4 网盘类来源适配（0.1.3）

本机没有安装任何网盘客户端，因此这一节的结论是「路径模板 + 单元测试级验证」，不是端到端实测：

| 应用 | 内置候选目录 | 下载中文件过滤 | 本机状态 |
| --- | --- | --- | --- |
| 百度网盘 | `Documents\BaiduNetdiskDownload`、`%USERPROFILE%\BaiduNetdiskDownload`、`Downloads\BaiduNetdiskDownload` | `.baiduyun.uploading.cfg`、`.downloading`、`.part` | 未安装 |
| 阿里云盘 | `Documents\阿里云盘`、`Documents\AliyunDrive`、`%USERPROFILE%\阿里云盘` | `.downloading`、`.blob` | 未安装 |
| 夸克网盘 | `Documents\夸克网盘`、`Documents\Quark`、`Downloads\夸克网盘` | `.downloading`、`.part` | 未安装 |
| 115 网盘 | `Documents\115网盘`、`Downloads\115网盘` | `.downloading`、`.part` | 未安装 |
| 腾讯微云 | `Documents\腾讯微云`、`Documents\Weiyun` | `.downloading` | 未安装 |
| 坚果云 | `Nutstore Files`、`Nutstore`、`Documents\Nutstore` | 排除 `.nutstore`、`node_modules` | 未安装 |
| OneDrive | `%USERPROFILE%\OneDrive`、`OneDrive - *` | `.downloading`、`.tmp` | 未启用同步 |

配套改动：

1. 「来源应用」页每个应用新增 **「+ 添加下载目录」**：网盘客户端允许自定义下载位置，用户选中的目录写入 `config.json` 的 `sources.<id>.extraFolders`，保存后立即重启监听。
2. 下载中占位符扩展名（`.downloading`、`.baiduyun.uploading.cfg`、`.qdl`、`.pd` 等）加入临时文件规则，落盘过程中不会被打开。
3. 新增 6 个测试（`CloudDriveTests`）：网盘条目存在且默认关闭、候选目录非空、占位符判定、`extraFolders` 解析与配置往返、缩略图排除项。`dotnet test` 通过数 86。

**自定义目录端到端验证**（用假目录代替未安装的客户端）：在 `%USERPROFILE%\Documents\QuarkCustom` 作为「夸克网盘」的附加下载目录，配置启用 quark 并投放 `OutOpen-quark-check.md`：

- 日志：`[adapter] 适配器 夸克网盘 已启动，监听 2 个目录` → `[pipeline] 已打开：quark %USERPROFILE%\Documents\QuarkCustom\OutOpen-quark-check.md（pid 24508，系统默认程序）`
- 事件页显示「已打开 · OutOpen-quark-check.md · Markdown 文档」，来源页显示两个监听目录与「移除」按钮
- 截图：`docs/screenshots/30-cloud-sources.png`、`docs/screenshots/31-cloud-event.png`
- 验证后已删除测试目录、关闭 quark、保留配置

## 7. 13 条验收对照

| # | 计划书验收项 | 状态 | 证据 |
| --- | --- | --- | --- |
| 1 | 识别常见文件类型 | 通过 | `outopen types` 17 种；`check` 对真实 docx 识别为 Word/ZIP-OOXML；76 个测试含签名识别 |
| 2 | 调用系统默认应用打开 | 通过 | 微信目录 .md 交给 MaxInk（pid 30232）；`check` 显示 docx 默认程序 Word、pdf 默认 PDFGear |
| 3 | 配置打开规则 | 通过 | 文件规则页可改「外部默认/指定程序/不接管」并保存到 `config.json`；规则优先级有单元测试 |
| 4 | 管理来源应用接管状态 | 通过 | 来源页逐个启用/停用；停用来源后 `source-disabled` 规则生效（测试） |
| 5 | 识别重复事件 | 通过 | 实证见第 6 节，事件页「重复忽略」 |
| 6 | 不打开未下载完的文件 | 通过（实测 + 测试级） | 稳定性等待 + 临时后缀过滤 + 0 字节/占用检测；真实投放 0 字节文件被「校验未通过」拦下（第 6.1 节）；未做真实弱网下载实验 |
| 7 | 失败安全回退 | 通过（实测 + 测试级） | 指定程序不存在时实测回退系统默认并成功打开（第 6.1 节）；两者都失败只记录；不阻断来源应用 |
| 8 | 至少一个应用实际适配验证 | 微信：通过；QQ：部分 | 微信目录真实接管成功；QQ 目录存在但本机无新接收文件，仅验证目录与候选文件识别，未验证点击级拦截 |
| 9 | 后台运行与暂停 | 通过 | 托盘常驻；暂停/恢复见第 5 节；关闭窗口最小化到托盘 |
| 10 | 退出/卸载不破坏文件关联 | 通过（代码与设计） | 全仓库无 `ClassesRoot` 写入、无关联修改 API（grep 0 命中）；仅写 `HKCU\...\Run` 自启项；卸载脚本只删程序与快捷方式，保留配置 |
| 11 | 缺少查看器时给出推荐 | 通过 | 查看器推荐页；`Available/NoAssociation/Broken/PackagedApplication` 四态；测试覆盖无关联与稍后提醒 |
| 12 | `.md` 缺查看器时推荐 MaxInk | 通过 | 推荐库 `.md` 第一优先为 MaxInk（官方产品页/下载页，核验 2026-11-22）；本机恰好已安装 MaxInk 并作为默认程序 |
| 13 | 用户可拒绝推荐且不擅自安装/改关联 | 通过 | 「稍后提醒 / 不再提醒 / 恢复推荐」写入 `recommendationStates`；全仓库无下载/安装代码，无网络调用（grep 0 命中） |

## 8. 未验证 / 已知限制

1. **点击级拦截未做**：Windows 无通用官方接口，第一版只监听落盘目录；微信内点击预览不会经过 OutOpen。计划书要求的「逐个应用验证」在此如实记录：微信目录级接管已验证，点击级未验证。
2. **QQ 接收文件未实测**：本机 `nt_qq\nt_data\File` 只有缩略图缓存，没有新接收文件；目录解析正确，但「接收文件落盘 → 自动打开」未实测。
3. **微信 4.x 布局**：适配器已内置 `xwechat_files\*\msg\file` 模板，本机未出现该目录，未验证。
4. **恢复后瞬时文件**：见第 5 节说明。
5. **打包应用（Store）**：只能检测到 `PackagedApplication` 状态，不能取得可执行文件路径，推荐页会如实显示，不做猜测。

## 9. 复现方式

```bash
# 构建与测试
dotnet build OutOpen.sln
dotnet test OutOpen.sln

# 查看本机可接管的目录
dotnet run --project src/OutOpen.Cli -- sources

# 不改配置，只诊断一个文件
dotnet run --project src/OutOpen.Cli -- check "C:\path\to\file.pdf"

# 启动界面（默认最小化到托盘）
dotnet run --project src/OutOpen.App
```

复现端到端接管：在「来源应用」确认微信/QQ 已启用，向本机实际监听目录投放一个受支持的文档，等待数秒，观察事件页或 `%LOCALAPPDATA%\OutOpen\logs` 日志。
