; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "Valeport Removable Pressure Transducer"
#define MyAppVersion "1.0.0.7"
#define MyAppPublisher "Valeport Limited"
#define MyAppURL "http://www.valeport.co.uk"
#define MyAppExeName "Pressure Calibration Software.exe"
#define MyProgramDataFolder GetEnv('PROGRAMDATA')

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{AE37AD9A-33AC-4EA8-8F60-B16A195D692E}
AppName=Removable Pressure Transducer
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName=C:\Valeport Software\Removable Pressure Transducer
DisableDirPage=no
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=C:\valeport_csharp\projects\Valeport Removable Pressure Sensor
OutputBaseFilename=vpRPTSetup
SetupIconFile=C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\Icon3.ico
Compression=lzma
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
Name: "{app}\bin\private\data"

[Files]
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\Pressure Calibration Software.exe"; DestDir: "{app}\bin"; Flags: ignoreversion     
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\TMS.Cloud.dll"; DestDir: "{app}\bin"; Flags: ignoreversion  
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\FlexCel.dll"; DestDir: "{app}\bin"; Flags: ignoreversion  
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\FlexCel.xml"; DestDir: "{app}\bin"; Flags: ignoreversion      
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\es\FlexCel.resources.dll"; DestDir: "{app}\bin"; Flags: ignoreversion              
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\NModbus4.dll"; DestDir: "{app}\bin"; Flags: ignoreversion  
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\NModbus4.xml"; DestDir: "{app}\bin"; Flags: ignoreversion  
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\Syncfusion.Chart.Base.dll"; DestDir: "{app}\bin"; Flags: ignoreversion  
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\Syncfusion.Chart.Base.xml"; DestDir: "{app}\bin"; Flags: ignoreversion    
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\Syncfusion.Chart.Windows.dll"; DestDir: "{app}\bin"; Flags: ignoreversion  
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\Syncfusion.Chart.Windows.xml"; DestDir: "{app}\bin"; Flags: ignoreversion 
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\Syncfusion.Shared.Base.dll"; DestDir: "{app}\bin"; Flags: ignoreversion  
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\Syncfusion.Shared.Base.xml"; DestDir: "{app}\bin"; Flags: ignoreversion           
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\MySql.Data.dll"; DestDir: "{app}\bin"; Flags: ignoreversion 
Source: "C:\valeport_csharp\projects\Valeport Removable Pressure Sensor\Valeport Removable Pressure Sensor\bin\Release\MySql.Data.xml"; DestDir: "{app}\bin"; Flags: ignoreversion 
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\bin\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\bin\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\bin\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent