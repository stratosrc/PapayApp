#define AppName "PapayApp"
#define AppVersion "1.0.2"
#define AppPublisher "PapayApp"
#define AppExeName "PapayApp.UI.exe"

[Setup]
AppId={{6A8A0625-724C-4415-B8B0-6E8D734BC0A4}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\PapayApp
DefaultGroupName=PapayApp
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=PapayApp-{#AppVersion}-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=app\appicon.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "app\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\PapayApp"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\PapayApp"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos adicionales:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Abrir PapayApp"; Flags: nowait postinstall skipifsilent
