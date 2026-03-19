@echo off
REM ============================================
REM Build Release - IMA Mecatronica
REM Publish + Sign + Installer
REM ============================================
setlocal enabledelayedexpansion

set VERSION=2.1.0
set PROJECT_DIR=%~dp0
set PUBLISH_DIR=%PROJECT_DIR%bin\Release\net8.0-windows\win-x64\publish
set SIGNTOOL="C:\Program Files (x86)\Microsoft SDKs\ClickOnce\SignTool\signtool.exe"
set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
set THUMB=DCC862F51EC0429FB37FE4A0910FE8805ED2596B

echo.
echo ========================================
echo  IMA Mecatronica - Build Release v%VERSION%
echo ========================================
echo.

REM --- Paso 1: Publish ---
echo [1/4] Publicando aplicacion...
dotnet publish "%PROJECT_DIR%SistemaGestionProyectos2.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true
if %errorlevel% neq 0 (
    echo [ERROR] Fallo el publish
    goto :fail
)
echo [OK] Publish completado
echo.

REM --- Paso 2: Firmar binarios ---
echo [2/4] Firmando binarios...
if not exist %SIGNTOOL% (
    echo [WARN] signtool.exe no encontrado, omitiendo firma
    goto :skip_sign
)

set SIGN_COUNT=0
for %%f in ("%PUBLISH_DIR%\SistemaGestionProyectos2.exe" "%PUBLISH_DIR%\SistemaGestionProyectos2.dll") do (
    if exist "%%f" (
        %SIGNTOOL% sign /sha1 %THUMB% /fd SHA256 /td SHA256 "%%f" >nul 2>&1
        if !errorlevel! equ 0 (
            echo   [SIGN] Firmado: %%~nxf
            set /a SIGN_COUNT+=1
        ) else (
            echo   [WARN] No se pudo firmar: %%~nxf
        )
    )
)
echo [OK] %SIGN_COUNT% archivos firmados

:skip_sign
echo.

REM --- Paso 3: Crear instalador ---
echo [3/4] Creando instalador...
if not exist %ISCC% (
    echo [ERROR] Inno Setup no encontrado en %ISCC%
    goto :fail
)
%ISCC% "%PROJECT_DIR%installer.iss"
if %errorlevel% neq 0 (
    echo [ERROR] Fallo la creacion del instalador
    goto :fail
)
echo [OK] Instalador creado
echo.

REM --- Paso 4: Firmar instalador ---
echo [4/4] Firmando instalador...
if exist %SIGNTOOL% (
    %SIGNTOOL% sign /sha1 %THUMB% /fd SHA256 /td SHA256 "%PROJECT_DIR%installer\SistemaGestionProyectos-v%VERSION%-Setup.exe" >nul 2>&1
    if %errorlevel% equ 0 (
        echo [OK] Instalador firmado
    ) else (
        echo [WARN] No se pudo firmar el instalador
    )
) else (
    echo [WARN] signtool.exe no encontrado, instalador sin firma
)

echo.
echo ========================================
echo  BUILD COMPLETADO
echo  Instalador: installer\SistemaGestionProyectos-v%VERSION%-Setup.exe
echo ========================================
goto :end

:fail
echo.
echo [BUILD FALLIDO]
pause
exit /b 1

:end
pause
exit /b 0
