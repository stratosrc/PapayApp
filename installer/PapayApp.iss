#define AppName "PapayApp"
#define AppVersion "1.0.6"
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
SetupIconFile=app-1.0.6\appicon.ico
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#AppExeName}
CloseApplications=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
Source: "app-1.0.6\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "prerequisites\WindowsAppRuntimeInstall-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[InstallDelete]
; Elimina bibliotecas de publicaciones anteriores antes de actualizar.
; Esto evita que componentes autocontenidos antiguos interfieran con el runtime oficial.
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.winmd"
Type: files; Name: "{app}\*.manifest"
Type: filesandordirs; Name: "{app}\Microsoft.UI.Xaml"

[Icons]
Name: "{autoprograms}\PapayApp"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\PapayApp"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos adicionales:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Abrir PapayApp"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  RuntimeInstaller: String;
begin
  if CurStep = ssPostInstall then
  begin
    RuntimeInstaller := ExpandConstant('{tmp}\WindowsAppRuntimeInstall-x64.exe');
    WizardForm.StatusLabel.Caption := 'Instalando componentes de Windows necesarios...';

    if not Exec(RuntimeInstaller, '--msix --license --quiet --force', '',
      SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      RaiseException('No se pudo iniciar la instalacion de Windows App Runtime.');
    end;

    if (ResultCode <> 0) and (ResultCode <> 3010) then
    begin
      RaiseException(Format(
        'No se pudo instalar Windows App Runtime (codigo %d).', [ResultCode]));
    end;
  end;
end;
