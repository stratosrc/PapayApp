#define AppName "PapayApp"
#define AppVersion "1.0.1"
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
OutputBaseFilename=PapayApp-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin

[Files]
Source: "app-debug\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion
Source: "prerequisites\windowsdesktop-runtime-10.0.10-win-x64.exe"; Flags: dontcopy
Source: "prerequisites\WindowsAppRuntimeInstall-x64.exe"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\PapayApp"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\PapayApp"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos adicionales:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Abrir PapayApp"; Flags: nowait postinstall skipifsilent

[Code]
procedure InstallPrerequisite(const FileName, Parameters: String);
var
  ResultCode: Integer;
begin
  ExtractTemporaryFile(FileName);
  if not Exec(ExpandConstant('{tmp}\' + FileName), Parameters, '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    RaiseException('No se pudo ejecutar ' + FileName + '.');

  if (ResultCode <> 0) and (ResultCode <> 3010) then
    RaiseException(FileName + ' terminó con el código ' + IntToStr(ResultCode) + '.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    InstallPrerequisite('windowsdesktop-runtime-10.0.10-win-x64.exe', '/install /quiet /norestart');
    InstallPrerequisite('WindowsAppRuntimeInstall-x64.exe', '--msix --license --quiet --force');
  end;
end;
