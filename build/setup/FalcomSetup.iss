#define FalcomPublish "C:\Projekte\Hundhausen 2026\Falcom\publish\Falcom"
#define FalcomWpfPublish "C:\Projekte\Hundhausen 2026\Falcom\publish\FalcomWpf"

[Setup]
AppId={{80EEBE8A-1D30-466A-B501-E9677A2F4AC4}
AppName=FALCOM und FALCOM WPF
AppVersion=1.0
AppPublisher=IROPA Elektrotechnik GmbH
DefaultDirName={autopf}\IROPA\FALCOM
DisableDirPage=yes
Uninstallable=no
PrivilegesRequired=admin
OutputDir=..\..\..\Setups\FalcomSetup
OutputBaseFilename=FalcomSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "{#FalcomPublish}\*"; DestDir: "{code:GetFalcomInstallDir}"; Excludes: "appsettings.json,appsettings.Local.json,appsettings.Development.json,CertificateStores\*,logs\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#FalcomPublish}\appsettings.json"; DestDir: "{code:GetFalcomInstallDir}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallFalcomAppSettings
Source: "{#FalcomPublish}\appsettings.Local.json"; DestDir: "{code:GetFalcomInstallDir}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallFalcomLocalSettings
Source: "{#FalcomPublish}\appsettings.Development.json"; DestDir: "{code:GetFalcomInstallDir}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallFalcomDevelopmentSettings

Source: "{#FalcomWpfPublish}\*"; DestDir: "{code:GetFalcomWpfInstallDir}"; Excludes: "appsettings.json,appsettings.Local.json,appsettings.Development.json,CertificateStores\*,logs\*"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#FalcomWpfPublish}\appsettings.json"; DestDir: "{code:GetFalcomWpfInstallDir}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallFalcomWpfAppSettings
Source: "{#FalcomWpfPublish}\appsettings.Local.json"; DestDir: "{code:GetFalcomWpfInstallDir}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallFalcomWpfLocalSettings
Source: "{#FalcomWpfPublish}\appsettings.Development.json"; DestDir: "{code:GetFalcomWpfInstallDir}"; Flags: ignoreversion skipifsourcedoesntexist; Check: ShouldInstallFalcomWpfDevelopmentSettings

[Code]
const
  SettingsKey = 'SOFTWARE\IROPA\FALCOM Setup';

var
  RootDirPage: TInputDirWizardPage;
  StoredRootDir: String;

procedure InitializeWizard;
begin
  RegQueryStringValue(HKLM, SettingsKey, 'InstallRoot', StoredRootDir);

  RootDirPage := CreateInputDirPage(wpWelcome,
    'Zielordner',
    'Gemeinsamer Zielordner fuer FALCOM',
    'Waehle den uebergeordneten Ordner. Das Setup legt darin Falcom und FalcomWpf parallel an.',
    False, '');
  RootDirPage.Add('Uebergeordneter Programmordner:');
  if StoredRootDir <> '' then
    RootDirPage.Values[0] := StoredRootDir
  else
    RootDirPage.Values[0] := ExpandConstant('{autopf}\IROPA');
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = RootDirPage.ID) and (StoredRootDir <> '');
end;

function GetFalcomInstallDir(Param: String): String;
begin
  Result := AddBackslash(RootDirPage.Values[0]) + 'Falcom';
end;

function GetFalcomWpfInstallDir(Param: String): String;
begin
  Result := AddBackslash(RootDirPage.Values[0]) + 'FalcomWpf';
end;

function ShouldInstallFalcomAppSettings: Boolean;
begin
  Result := not FileExists(AddBackslash(GetFalcomInstallDir('')) + 'appsettings.json');
end;

function ShouldInstallFalcomLocalSettings: Boolean;
begin
  Result := not FileExists(AddBackslash(GetFalcomInstallDir('')) + 'appsettings.Local.json');
end;

function ShouldInstallFalcomDevelopmentSettings: Boolean;
begin
  Result := not FileExists(AddBackslash(GetFalcomInstallDir('')) + 'appsettings.Development.json');
end;

function ShouldInstallFalcomWpfAppSettings: Boolean;
begin
  Result := not FileExists(AddBackslash(GetFalcomWpfInstallDir('')) + 'appsettings.json');
end;

function ShouldInstallFalcomWpfLocalSettings: Boolean;
begin
  Result := not FileExists(AddBackslash(GetFalcomWpfInstallDir('')) + 'appsettings.Local.json');
end;

function ShouldInstallFalcomWpfDevelopmentSettings: Boolean;
begin
  Result := not FileExists(AddBackslash(GetFalcomWpfInstallDir('')) + 'appsettings.Development.json');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    RegWriteStringValue(HKLM, SettingsKey, 'InstallRoot', RootDirPage.Values[0]);
end;
