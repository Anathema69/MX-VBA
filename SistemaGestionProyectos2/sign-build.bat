@echo off
REM Firma Authenticode para WDAC - IMA Mecatronica
REM Thumbprint: DCC862F51EC0429FB37FE4A0910FE8805ED2596B

set SIGNTOOL="C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"
set THUMB=DCC862F51EC0429FB37FE4A0910FE8805ED2596B

if not exist %SIGNTOOL% (
    echo [SIGN] signtool.exe no encontrado, omitiendo firma
    exit /b 0
)

REM Firmar cada archivo pasado como argumento
:loop
if "%~1"=="" goto :done
if exist "%~1" (
    %SIGNTOOL% sign /sha1 %THUMB% /fd SHA256 /td SHA256 "%~1" >nul 2>&1
    if %errorlevel% equ 0 (
        echo [SIGN] Firmado: %~nx1
    ) else (
        echo [SIGN] No se pudo firmar: %~nx1 (no critico)
    )
)
shift
goto :loop

:done
exit /b 0
