; OutOpen 安装脚本（Inno Setup 6）
;
; 构建步骤：
;   dotnet publish src/OutOpen.App -c Release -r win-x64 --self-contained false -o publish/app
;   dotnet publish src/OutOpen.Cli -c Release -r win-x64 --self-contained false -o publish/cli
;   iscc installer/OutOpen.iss
;
; 注意：GUI 可执行文件是 OutOpen.exe，CLI 是 outopen.exe。Windows 文件系统不区分大小写，
; 两者绝不能装进同一个目录（早期版本因此把 GUI 覆盖成控制台程序）。CLI 固定放进 {app}\cli。
;
; 说明：
; - 按当前用户安装，不需要管理员权限；
; - 不注册文件关联、不注入第三方进程，只读取系统关联信息；
; - 卸载时保留 %LOCALAPPDATA%\OutOpen 下的配置、日志与查看器推荐库；
; - 安装向导中的「桌面快捷方式」「开机自启动（静默）」由自定义页面收集。

#define AppName "OutOpen"
#define AppVersion "0.1.3"
#define AppPublisher "T1gerD"
#define AppUrl "https://github.com/T1gerD/OutOpen"
#define AppExeName "OutOpen.exe"

[Setup]
AppId={{6C1F6B29-7F4F-4C41-9B57-0A8A9C0E8D21}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion=0.1.3.0
VersionInfoDescription={#AppName} 安装程序
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
PrivilegesRequired=lowest
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=OutOpen-{#AppVersion}-setup
SetupIconFile=..\assets\outopen.ico
WizardImageFile=..\assets\wizard-image.bmp
WizardSmallImageFile=..\assets\wizard-small.bmp
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[CustomMessages]
chinesesimplified.OptionsCaption=附加选项
chinesesimplified.OptionsDescription=选择安装完成后的行为。这些选项之后都可以在 OutOpen 的「设置」里修改。
chinesesimplified.OptionsSubCaption=只监听你勾选的来源应用目录，不改文件关联，也不注入任何进程。
chinesesimplified.OptionsDesktop=创建桌面快捷方式
chinesesimplified.OptionsAutostart=开机自动启动（静默运行在系统托盘）

[Files]
Source: "..\publish\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; CLI 与 GUI 的可执行文件名只差大小写，必须分目录，避免互相覆盖。
Source: "..\publish\cli\*"; DestDir: "{app}\cli"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\OutOpen"; Filename: "{app}\{#AppExeName}"
Name: "{group}\OutOpen 命令行"; Filename: "{cmd}"; Parameters: "/k ""{app}\cli\outopen.exe"" status"
Name: "{group}\卸载 OutOpen"; Filename: "{uninstallexe}"
Name: "{autodesktop}\OutOpen"; Filename: "{app}\{#AppExeName}"; Check: ShouldCreateDesktopIcon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "OutOpen"; ValueData: """{app}\{#AppExeName}"" --tray"; Flags: uninsdeletevalue; Check: ShouldAutostart

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\{#AppExeName}"; Parameters: "--quit"; Flags: runhidden skipifdoesntexist; RunOnceId: "StopOutOpen"

[UninstallDelete]
Type: files; Name: "{autodesktop}\OutOpen.lnk"
Type: filesandordirs; Name: "{app}\cli"

[Code]
var
  OptionsPage: TInputOptionWizardPage;

function ShouldCreateDesktopIcon: Boolean;
begin
  Result := (not WizardSilent) and OptionsPage.Values[0];
end;

function ShouldAutostart: Boolean;
begin
  Result := (not WizardSilent) and OptionsPage.Values[1];
end;

procedure InitializeWizard;
begin
  OptionsPage := CreateInputOptionPage(wpSelectDir,
    ExpandConstant('{cm:OptionsCaption}'),
    ExpandConstant('{cm:OptionsDescription}'),
    ExpandConstant('{cm:OptionsSubCaption}'),
    False, False);
  OptionsPage.Add(ExpandConstant('{cm:OptionsDesktop}'));
  OptionsPage.Add(ExpandConstant('{cm:OptionsAutostart}'));
  OptionsPage.Values[0] := True;
  OptionsPage.Values[1] := False;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKEY_CURRENT_USER,
      'Software\Microsoft\Windows\CurrentVersion\Run', 'OutOpen');
end;
