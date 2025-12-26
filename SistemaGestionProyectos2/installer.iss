[Setup]
AppName=Sistema de Gestión de Proyectos
AppVersion=1.0.9
AppPublisher=IMA Mecatrónica
DefaultDirName={autopf}\SistemaGestionProyectos
DefaultGroupName=Sistema de Gestión
OutputDir=.\installer
OutputBaseFilename=SistemaGestionProyectos-v1.0.9-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Sistema de Gestión"; Filename: "{app}\SistemaGestionProyectos2.exe"
Name: "{autodesktop}\Sistema de Gestión"; Filename: "{app}\SistemaGestionProyectos2.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SistemaGestionProyectos2.exe"; Description: "Ejecutar aplicación"; Flags: nowait postinstall skipifsilent