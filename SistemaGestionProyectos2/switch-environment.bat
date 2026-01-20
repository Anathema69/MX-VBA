@echo off
echo ============================================
echo  Cambiar Ambiente - IMA Mecatronica
echo ============================================
echo.
echo Selecciona el ambiente:
echo   1. Produccion (wjozxqldvypdtfmkamud)
echo   2. Staging (pruebas)
echo.
set /p choice="Ingresa tu opcion (1 o 2): "

if "%choice%"=="1" (
    echo.
    echo Cambiando a PRODUCCION...
    copy /Y appsettings.production.json appsettings.json
    echo.
    echo [OK] Ambiente: PRODUCCION
    echo [!] CUIDADO: Los cambios afectaran datos reales
) else if "%choice%"=="2" (
    echo.
    echo Cambiando a STAGING...
    copy /Y appsettings.staging.json appsettings.json
    echo.
    echo [OK] Ambiente: STAGING (pruebas)
    echo [i] Puedes probar sin afectar produccion
) else (
    echo.
    echo Opcion no valida. No se hicieron cambios.
)

echo.
pause
