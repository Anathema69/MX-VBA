[Setup]
AppName=Sistema de Gestión de Proyectos
AppVersion=2.2.0
AppPublisher=IMA Mecatrónica
DefaultDirName={autopf}\SistemaGestionProyectos
DefaultGroupName=Sistema de Gestión
OutputDir=.\installer
OutputBaseFilename=SistemaGestionProyectos-v2.2.0-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"

[Files]
; Certificado de firma (se instala antes que los binarios)
Source: "ima-dev-cert.pfx"; DestDir: "{tmp}"; Flags: deleteafterinstall
; Binarios de la aplicacion
Source: "bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Sistema de Gestión"; Filename: "{app}\SistemaGestionProyectos2.exe"
Name: "{autodesktop}\Sistema de Gestión"; Filename: "{app}\SistemaGestionProyectos2.exe"; Tasks: desktopicon

[Run]
; Instalar certificado en stores confiables (silencioso, antes de ejecutar la app)
Filename: "certutil.exe"; Parameters: "-f -p ima2026 -importpfx TrustedPublisher ""{tmp}\ima-dev-cert.pfx"""; Flags: runhidden waituntilterminated; StatusMsg: "Instalando certificado de confianza..."
Filename: "certutil.exe"; Parameters: "-f -p ima2026 -importpfx Root ""{tmp}\ima-dev-cert.pfx"""; Flags: runhidden waituntilterminated; StatusMsg: "Configurando certificado raíz..."
; Ejecutar la aplicacion al finalizar
Filename: "{app}\SistemaGestionProyectos2.exe"; Description: "Ejecutar aplicación"; Flags: nowait postinstall skipifsilent
