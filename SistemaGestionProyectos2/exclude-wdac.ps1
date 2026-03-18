# ============================================
# Excluir carpeta de build de WDAC/SmartScreen
# EJECUTAR COMO ADMINISTRADOR
# ============================================

$paths = @(
    "$PSScriptRoot\bin",
    "$PSScriptRoot\obj",
    "$env:USERPROFILE\source\repos"
)

foreach ($path in $paths) {
    try {
        Add-MpPreference -ExclusionPath $path -ErrorAction Stop
        Write-Host "[OK] Excluido de Windows Defender: $path" -ForegroundColor Green
    } catch {
        Write-Host "[WARN] No se pudo excluir: $path - $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Exclusiones aplicadas. Recompila y ejecuta la app." -ForegroundColor Cyan
Read-Host "Presiona Enter para cerrar"
