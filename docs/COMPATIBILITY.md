# 兼容性说明

本文件记录 MVP 版本（0.1.0）在操作系统、来源应用、文件类型和查看器推荐上的支持边界。表中「本机验证」指在 Windows 10 19045 上实际执行过的检查；「未验证」不等于不支持，而是没有拿到真实样本。

## 操作系统

| 系统 | 状态 | 说明 |
| --- | --- | --- |
| Windows 11（x64） | 目标 | 计划书目标平台；使用 Win32 Shell API 与 WPF，无版本专属依赖 |
| Windows 10 1809+（x64） | 已实测 | 本机 19045.6466 完成构建、单元测试与微信目录端到端接管 |
| Windows 10 ARM64 | 未验证 | 需用 `win-arm64` RID 重新发布；Shell 启动逻辑不依赖架构 |
| Windows 7/8.1 | 不支持 | .NET 10 与 WPF 运行时不再支持 |

其他兼容性事实：

- 进程为 `asInvoker`，不需要管理员权限；安装包按当前用户安装。
- 高 DPI：manifest 声明 PerMonitorV2。
- 长路径：manifest 开启 `longPathAware`，校验层也允许超过 260 字符的路径（上限 30000）。
- 不写入 `HKCU/HKLM` 的文件关联键；只读关联信息，另写一个 HKCU 自启项（用户显式开启时）。

## 来源应用

| 应用 | 默认 | 监听路径模板 | 本机验证 |
| --- | --- | --- | --- |
| 微信 PC 版（微信 3.x） | 启用 | `%USERPROFILE%\Documents\WeChat Files\*\FileStorage\File` | **已实测**（目录存在，真实文件落盘后自动打开） |
| 微信 PC 版（微信 4.x 布局） | 启用 | `%USERPROFILE%\Documents\xwechat_files\*\msg\file` | 未验证（本机无此目录） |
| QQ PC 版（NT） | 启用 | `%USERPROFILE%\Documents\Tencent Files\*\nt_qq\nt_data\File`、`...\FileRecv` | 目录已确认；接收文件未实测 |
| 钉钉 | 关闭 | 用户补充目录 | 未安装 |
| 百度网盘 | 关闭 | `%USERPROFILE%\Documents\BaiduNetdiskDownload` 等 | 未安装 |
| 阿里云盘 | 关闭 | 用户补充目录 | 未安装 |
| OneDrive | 关闭 | `%USERPROFILE%\OneDrive` | 目录存在；默认关闭，避免接管全部同步文件 |
| Telegram Desktop | 关闭 | 标记为不支持自动识别 | 未安装 |

说明：

- 每个应用的「监听目录」= 内置模板 + 用户在 `config.json` 里补充的 `extraFolders`；解析后只保留真实存在的目录。
- 排除目录（如 `Temp`、`Cache`、`Thumb`、`Image`、`avatar`、`Emoji`）不会触发。
- 应用升级可能改变缓存布局；找不到目录时界面显示「未找到」，不会把进程运行误报成「已接管」。
- 点击应用内部预览不会经过 OutOpen（详见 `ARCHITECTURE.md` 的边界说明）。

## 文件类型

| 扩展名 | 类型 | 类别 | MIME | 默认动作 |
| --- | --- | --- | --- | --- |
| `.pdf` | PDF 文档 | Document | `application/pdf` | 外部打开 |
| `.doc` / `.docx` | Word 文档 | Document | `application/msword` / OOXML | 外部打开 |
| `.xls` / `.xlsx` | Excel 表格 | Spreadsheet | `application/vnd.ms-excel` / OOXML | 外部打开 |
| `.ppt` / `.pptx` | PowerPoint 演示 | Presentation | `application/vnd.ms-powerpoint` / OOXML | 外部打开 |
| `.txt` | 纯文本 | Text | `text/plain` | 外部打开 |
| `.md` / `.markdown` | Markdown | Text | `text/markdown` | 外部打开 |
| `.csv` | CSV 表格 | Spreadsheet | `text/csv` | 外部打开 |
| `.jpg` / `.jpeg` | JPEG 图片 | Image | `image/jpeg` | 不接管 |
| `.png` | PNG 图片 | Image | `image/png` | 不接管 |
| `.gif` | GIF 图片 | Image | `image/gif` | 不接管 |
| `.zip` | ZIP 压缩包 | Archive | `application/zip` | 不接管 |
| `.7z` | 7-Zip 压缩包 | Archive | `application/x-7z-compressed` | 不接管 |
| `.rar` | RAR 压缩包 | Archive | `application/vnd.rar` | 不接管 |

签名识别覆盖：PDF（`%PDF`）、ZIP/OOXML（`PK\x03\x04`）、OLE（`D0CF11E0A1B11AE1`）、RAR（`Rar!\x1A\x07`）、7Z（`37 7A BC AF 27 1C`）、JPEG（`FF D8 FF`）、PNG（`89 50 4E 47 0D 0A 1A 0A`）、GIF（`GIF87a/GIF89a`）。

开启「要求文件签名与扩展名一致」后，签名不匹配的文件会被 `ValidationFailed` 拦下；不开启时只记录不一致，不阻断（这样 `.md` 等纯文本格式不会因为无签名被误伤）。

## 查看器推荐库

核验日期 2026-11-22（官方页面 HTTP 200）。推荐只提供入口，不自动下载、不静默安装、不修改默认关联。

| 扩展名 | 推荐项（优先级从高到低） | 官方入口 |
| --- | --- | --- |
| `.md` `.markdown` | MaxInk、Obsidian、Visual Studio Code | <https://www.runxun.com/ai/tools/maxink> |
| `.pdf` | Adobe Acrobat Reader、SumatraPDF | <https://get.adobe.com/reader/>、<https://www.sumatrapdfreader.org/download-free-pdf-viewer> |
| doc/xls/ppt/csv | WPS Office、LibreOffice | <https://www.wps.cn/>、<https://www.libreoffice.org/download/download-libreoffice/> |
| `.txt` `.md` `.csv` | Notepad++（补充项） | <https://notepad-plus-plus.org/downloads/> |
| zip/7z/rar | 7-Zip、WinRAR | <https://www.7-zip.org/download.html>、<https://www.rarlab.com/download.htm> |
| jpg/png/gif | Windows 照片 | <https://apps.microsoft.com/detail/9wzdncrfjbh4> |
| `.json` 等文本 | Visual Studio Code | <https://code.visualstudio.com/Download> |

MaxInk 事实（2026-11-22 核验）：润迅官方产品，Windows 10/11 x64，安装包约 15 MB，Certum 数字签名，免费；本机恰好安装并作为 `.md` 默认程序。

## 检测得到的查看器状态

| 状态 | 含义 | 界面行为 |
| --- | --- | --- |
| `Available` | 关联程序存在且可用 | 显示「系统已有关联程序」，推荐作为备选 |
| `NoAssociation` | 无关联，或指向 `OpenWith.exe` | 显示「没有可用查看器」，Primary 推荐置顶 |
| `Broken` | 关联的程序路径已不存在 | 显示「默认程序失效」，给出推荐 |
| `PackagedApplication` | 指向 Store 应用（`\WindowsApps\` / `ApplicationFrameHost.exe`） | 如实显示，无法取得 exe 路径 |
| `Error` | 其它 HRESULT | 显示错误信息，仍给推荐 |

## 日志与隐私

- 日志按天写到 `%LOCALAPPDATA%\OutOpen\logs\outopen-YYYYMMDD.log`，默认保留 14 天。
- 路径脱敏：`%USERPROFILE%` 前缀替换、`wxid_*` → `wxid_***`、连续 5 位以上数字 → `***`。
- 无网络请求、无遥测；文件名只写本机日志。
