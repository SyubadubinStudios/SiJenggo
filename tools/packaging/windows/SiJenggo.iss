; Windows installer for Si Jenggo — Preman Berhati Malaikat.
; Built by tools/packaging/build_release.py, which passes the publish folder and version:
;   ISCC.exe /DAppVersion=0.1.0 /DSourceDir=<publish dir> /DOutputDir=<dir> SiJenggo.iss
; Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\..\artifacts\publish\win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\..\artifacts\release"
#endif

[Setup]
AppId={{6B0E3D7A-5C1F-4E7B-9A0D-51A7E6C0B3F2}
AppName=Si Jenggo
AppVerName=Si Jenggo {#AppVersion}
AppVersion={#AppVersion}
AppPublisher=Syubadubin Studios
AppPublisherURL=https://github.com/SyubadubinStudios/SiJenggo
AppSupportURL=https://github.com/SyubadubinStudios/SiJenggo/issues
AppCopyright=Dibuat oleh Ariana Mischa Fadhila dari Syubadubin Studios
DefaultDirName={autopf}\Si Jenggo
DefaultGroupName=Si Jenggo
DisableProgramGroupPage=yes
; Installs per user without admin rights unless the player chooses "all users".
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename=SiJenggo-{#AppVersion}-win-x64-setup
SetupIconFile=..\..\..\src\SiJenggo.App\Assets\sijenggo.ico
UninstallDisplayIcon={app}\SiJenggo.exe
LicenseFile=..\..\..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Si Jenggo"; Filename: "{app}\SiJenggo.exe"
Name: "{group}\Si Jenggo (grafis rendah · low graphics)"; Filename: "{app}\SiJenggo.exe"; Parameters: "--quality 0"
Name: "{group}\{cm:UninstallProgram,Si Jenggo}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Si Jenggo"; Filename: "{app}\SiJenggo.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SiJenggo.exe"; Description: "{cm:LaunchProgram,Si Jenggo}"; Flags: nowait postinstall skipifsilent
