# OutOpen 架构

OutOpen 是一个单进程 Windows 桌面应用：WPF 界面与后台引擎同进程运行，引擎不依赖 UI，可被 CLI 复用（诊断命令直接构造 `OutOpenHost`）。全部状态保存在本机 `%LOCALAPPDATA%\OutOpen`，不访问网络。

## 模块划分

计划书里的 A–H 模块与本仓库的对应关系：

| 模块 | 职责 | 代码位置 |
| --- | --- | --- |
| A Detection Layer | 扩展名/MIME/文件头签名识别 | `src/OutOpen.Core/Detection/FileTypeDetector.cs`、`Catalog/SupportedTypes.cs` |
| B Intercept Engine | 监听来源目录、去抖、稳定性等待、事件入队 | `Adapters/FolderWatchAdapter.cs`、`Adapters/FolderResolver.cs` |
| C Application Adapters | 微信/QQ/钉钉/网盘的目录定义与验证状态 | `Catalog/SourceApps.cs` |
| D File Pipeline | 校验 → 去重 → 规则 → 启动 → 结果 | `Pipeline/InterceptPipeline.cs` |
| E Rules Engine | 暂停/来源/类型/高级规则综合决策 | `Rules/RulesEngine.cs`、`Rules/DuplicateDetector.cs` |
| F Windows Shell Launcher | ShellExecute 调默认程序、指定程序启动 | `Launching/ShellLauncher.cs`、`Interop/NativeMethods.cs` |
| G Desktop UI | 6 页 WPF 界面、托盘、单实例、设置 | `src/OutOpen.App/` |
| H Viewer Recommendation | 关联状态检测与官方推荐库 | `Viewers/ViewerAdvisor.cs`、`Viewers/ViewerCatalog.cs`、`Launching/DefaultProgramResolver.cs` |

## 数据流

```
来源应用落盘
   │  FileSystemWatcher(Created/Renamed, 递归)
   ▼
FolderWatchAdapter ── 250ms 去抖 ── 稳定性等待(大小+时间连续 N 秒不变)
   │  FileArrived(OpenRequest)
   ▼
InterceptPipeline.Handle(request, config)
   ├─ config.Paused?                     → SkippedPaused
   ├─ 支持的扩展名?                      → SkippedUnsupportedType
   ├─ DuplicateDetector（时间窗口）        → SkippedDuplicate
   ├─ RulesEngine.Decide                 → Skip / ExternalOpen / SystemDefault
   ├─ FileValidationService.Validate      → ValidationFailed（含原因）
   ├─ ViewerAdvisor.Assess（未指定程序时）  → ViewerMissing
   ├─ IFileLauncher.OpenWithProgram / OpenWithDefault
   └─ InterceptResult + 脱敏日志
   ▼
OutOpenHost.ResultProduced → UI 概览/事件页/托盘气泡
```

### 状态与结果

`InterceptStatus`：`Opened`、`OpenedWithFallback`、`SkippedPaused`、`SkippedByRule`、`SkippedDuplicate`、`SkippedUnsupportedType`、`ValidationFailed`、`ViewerMissing`、`LaunchFailed`、`Error`。

界面把它映射成中文标签与颜色：成功绿色、跳过灰色/黄色、失败红色。

### 失败回退

- 指定程序启动失败 → 自动回退系统默认程序，结果记为 `OpenedWithFallback`；
- 回退也失败 → `LaunchFailed`，只写日志，**不重启、不阻断来源应用**；
- 校验失败（未下载完、被占用、0 字节、过大、签名不符）→ `ValidationFailed`，同样只记录。

## 监听与「拦截」的边界

Windows 没有通用的官方接口去拦截第三方应用内部的「点击预览」。因此第一版采用**文件目录监听**：应用把接收文件保存到磁盘后，OutOpen 在文件稳定时接管打开动作。

- 不注入进程、不做全局 Hook、不修改应用安装目录；
- 监听目录来自 `SourceApps` 的模板（支持环境变量与 `*` 通配）+ 用户补充目录；
- 目录不存在时适配器状态为 `Unavailable`，界面如实显示「未找到」，不会假装已接管；
- 目录变化（应用升级）可在设置中补充 `extraFolders`。

## 核心契约

```csharp
public interface IFileLauncher
{
    LaunchResult OpenWithDefault(string filePath);
    LaunchResult OpenWithProgram(string programPath, string filePath);
}

public interface IDefaultProgramResolver
{
    DefaultProgramInfo Resolve(string extension);
}

public interface IAppLog
{
    void Info(string message, string category = "app");
    void Warn(string message, string category = "app");
    void Error(string message, string category = "app");
}
```

- `ShellLauncher` 用 `ShellExecuteExW`（`SEE_MASK_NOCLOSEPROCESS | SEE_MASK_FLAG_NO_UI`）调用系统默认程序，返回 pid；
- 指定程序用 `ProcessStartInfo.ArgumentList`，参数为文件路径本身；
- `WindowsDefaultProgramResolver` 用 `AssocQueryStringW(ASSOCSTR_EXECUTABLE / ASSOCSTR_FRIENDLYAPPNAME)` 读取关联，能区分：

| 状态 | 判定 |
| --- | --- |
| `Available` | 有可执行文件且文件存在 |
| `NoAssociation` | `HRESULT 0x80070483`，或指向 `OpenWith.exe` / `OpenWithHost.exe` |
| `Broken` | 关联路径不存在 |
| `PackagedApplication` | 位于 `\WindowsApps\` 或 `ApplicationFrameHost.exe` |
| `Error` | 其它 HRESULT，带错误描述 |

## 文件识别与校验

- `FileTypeDetector` 读取文件头 512 字节，识别 PDF、ZIP/OOXML、OLE、RAR、7Z、JPEG、PNG、GIF；
- 扩展名 → MIME/类别/默认动作的映射集中在 `SupportedTypes`；
- `FileValidationService.Validate` 依次检查：空路径、不存在、目录、路径长度、临时文件、0 字节、大小上限、独占打开（被占用）、可选签名一致；
- `WaitUntilStableAsync` 要求连续快照（大小 + 最后写入时间）一致累计达到配置秒数，超时返回 null，适配器直接丢弃该事件。

## 配置

`config.json`（`ConfigStore` 负责读写、clamp 与损坏恢复）：

```jsonc
{
  "version": 1,
  "paused": false,
  "settings": {
    "stabilitySeconds": 2.0,
    "dedupeWindowSeconds": 8,
    "maxFileSizeMb": 512,
    "requireSignatureMatch": false,
    "autoOpenOnArrival": true,
    "startWithWindows": false,
    "minimizeToTray": true,
    "showNotifications": true,
    "logRetentionDays": 14,
    "scanExistingFilesOnStart": false
  },
  "sources": { "wechat": { "enabled": true, "extraFolders": [] } },
  "extensions": { ".pdf": { "enabled": true, "action": "ExternalOpen", "targetProgram": null } },
  "rules": [],
  "recommendationStates": { ".md": { "ignored": false, "snoozeUntil": null } }
}
```

数值范围会被 clamp（稳定性 0–60 秒、去重 0–600 秒、大小 1–10240 MB、日志 1–365 天）；写入先写临时文件再替换。

`viewer-catalog.json` 是查看器推荐库，首次运行生成，包含每个条目的官方介绍页、下载页、来源核验时间与优先级。

## 宿主生命周期

`OutOpenHost` 是引擎门面：

```csharp
using var host = new OutOpenHost();   // 默认 %LOCALAPPDATA%\OutOpen
host.Start();                          // 读取配置、创建适配器、可选扫描最近文件
host.SetPaused(true);                  // 暂停：适配器保持监听但忽略事件
host.HandleManual(path);               // 手动打开（来源 = manual）
host.ReloadFromDisk();                 // 设置页保存后重载
host.ResultProduced += ...;            // UI 订阅处理结果
```

- 单实例由 `App.xaml.cs` 的 `Local\OutOpen.SingleInstance` Mutex 保证；
- 关闭窗口默认只隐藏到托盘（可在设置关闭该行为）；
- 退出时停止适配器、释放日志与托盘图标，不改动任何系统关联。

## 测试

`tests/OutOpen.Core.Tests` 覆盖：类型识别、签名校验、临时文件/占用/稳定性、规则顺序（暂停、来源禁用、类型禁用、高级规则优先级）、去重窗口、配置损坏恢复与 clamp、查看器可见/隐藏/稍后提醒、流水线各状态、目录解析与临时文件过滤、路径脱敏、日志保留、宿主事件与配置重载。

测试不启动真实外部程序：`IFileLauncher` 与 `IDefaultProgramResolver` 在测试中是假实现，Shell 调用只在真实运行中发生。
