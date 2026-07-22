#define AppName "NexaStation"
#define AppPublisher "NexaStation"
#define AppExecutable "NexaStation.App.exe"

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#ifndef AppNumericVersion
  #define AppNumericVersion "0.1.0.0"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\release\0.1.0"
#endif

[Setup]
AppId={{9AFC071D-292C-4D3B-921B-31B518BB2606}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\NexaStation
DefaultGroupName=NexaStation
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=NexaStation-Setup-{#AppVersion}-win-x64
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter={#AppExecutable}
RestartApplications=no
UninstallDisplayIcon={app}\{#AppExecutable}
UsePreviousAppDir=yes
SetupLogging=yes
VersionInfoVersion={#AppNumericVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=NexaStation Windows installer
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppNumericVersion}
VersionInfoProductTextVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Excludes: "portable.flag,Data\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\NexaStation"; Filename: "{app}\{#AppExecutable}"; WorkingDir: "{app}"
Name: "{autodesktop}\NexaStation"; Filename: "{app}\{#AppExecutable}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExecutable}"; Description: "Launch NexaStation"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
