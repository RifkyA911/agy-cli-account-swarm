; ==============================================================================
; Inno Setup 6 Script for Agy CLI Account Swarm
; Produces: Agy-CLI-Account-Swarm-Setup-v{#MyAppVersion}.exe with Uninstaller
; ==============================================================================

#define MyAppName "Agy CLI Account Swarm"
#define MyAppVersion "0.9.3-beta"
#define MyAppPublisher "RifkyA911"
#define MyAppURL "https://github.com/RifkyA911/agy-cli-account-swarm"
#define MyAppExeName "AgyAccountSwarm.exe"

[Setup]
AppId={{D37E88A1-4192-4C10-912A-B962631580E1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=Agy-CLI-Account-Swarm-Setup-v{#MyAppVersion}
SetupIconFile=..\Resources\favicon.ico
UninstallDisplayIcon={app}\Resources\favicon.ico
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\favicon.ico"; Comment: "Launch Agy CLI Account Swarm"
Name: "{autoprograms}\{#MyAppName}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\Resources\favicon.ico"; Comment: "Uninstall Agy CLI Account Swarm"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\favicon.ico"; Tasks: desktopicon; Comment: "Launch Agy CLI Account Swarm"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
