; OutOpen 安装脚本（Inno Setup 6）
;
; 构建步骤：
;   dotnet publish src/OutOpen.App -c Release -r win-x64 --self-contained false -o publish/app
;   dotnet publish src/OutOpen.Cli -c Release -r win-x64 --self-contained false -o publish/cli
;   iscc installer/OutOpen.iss
;
; 说明：
; - 按当前用户安装，不需要管理员权限；
; - 不注册、不修改任何文件关联（OutOpen 只读取关联信息）；
; - 卸载时保留 %LOCALAPPDATA%\OutOpen 下的配置、日志与查看器推荐库。

#define AppName "OutOpen"
#define AppVersion "0.1.0"
#define AppPublisher "T1gerD"
#define AppExeName "OutOpen.exe"

[Setup]
AppId={{6C1F6B29-7F4F-4C41-9B57-0A8A9C0E8D21}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=OutOpen-{#AppVersion}-setup
SetupIconFile=..\assets\outopen.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: unchecked

[Files]
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\cli\outopen.exe"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\OutOpen"; Filename: "{app}\{#AppExeName}"
Name: "{group}\OutOpen 诊断命令"; Filename: "{cmd}"; Parameters: "/k ""{app}\outopen.exe"" status"; Flags: skipifsourcedoesntexist
Name: "{group}\卸载 OutOpen"; Filename: "{uninstallexe}"
Name: "{autodesktop}\OutOpen"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "启动 OutOpen"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExeName}"; Parameters: "--quit"; Flags: runhidden skipifdoesntexist; RunOnceId: "StopOutOpen"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// 卸载不删除 %LOCALAPPDATA%\OutOpen（配置/日志/推荐库），避免用户设置被静默清空。
