; InboxDock Windows Installer
; Requires Inno Setup 6.2+
; Usage: iscc /DAppVersion=0.3.0 installer/InboxDock.iss

#ifndef AppVersion
  #define AppVersion "0.3.0"
#endif

#define AppName "InboxDock"
#define AppPublisher "InboxDock"
#define AppURL "https://github.com/qianlangzi/InboxDock"
#define AppExeName "InboxDock.exe"

[Setup]
AppId={{8A7F-3B2C-4D5E-9F1A-InboxDock}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=InboxDock-Setup-{#AppVersion}
SetupIconFile=..\assets\inboxdock.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion}
LicenseFile=LICENSE.txt

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; 卸载时移除 InboxDock 创建的登录启动项
Filename: "{app}\{#AppExeName}"; Parameters: "--unregister-autostart"; Flags: runhidden; RunOnceId: "RemoveAutostart"

[UninstallDelete]
; 默认保留用户数据，不删除 %LocalAppData%\InboxDock
; 用户可手动在卸载后删除该目录以彻底清理

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
