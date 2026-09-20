; Instalador de Rebith 2 (C#). Compilar con Inno Setup despues de correr compilar.bat.
[Setup]
AppId={{B7F9E2A1-3C6D-4A8F-9E12-202609180002}}
AppName=Rebith
AppVersion=2.0.0
DefaultDirName={autopf}\Rebith
DefaultGroupName=Rebith
OutputDir=Output
OutputBaseFilename=Rebith_2.0.0_instalador
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
WizardStyle=modern
; .NET Framework 4.8 ya viene en Windows 10 (1903+) y Windows 11.

[Files]
Source: "Rebith.App\bin\Release\net48\Rebith.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Rebith.App\bin\Release\net48\Rebith.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "Rebith.App\bin\Release\net48\Rebith.Core.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Rebith"; Filename: "{app}\Rebith.exe"
Name: "{autodesktop}\Rebith"; Filename: "{app}\Rebith.exe"

[Run]
Filename: "{app}\Rebith.exe"; Description: "Abrir Rebith"; Flags: nowait postinstall skipifsilent
