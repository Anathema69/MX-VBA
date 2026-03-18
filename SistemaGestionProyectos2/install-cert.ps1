# ============================================
# Instalar certificado IMA en stores confiables
# EJECUTAR COMO ADMINISTRADOR
# ============================================

$pfxPath = Join-Path $PSScriptRoot "ima-dev-cert.pfx"
$password = ConvertTo-SecureString -String "ima2026" -Force -AsPlainText

if (-not (Test-Path $pfxPath)) {
    Write-Host "ERROR: No se encontro $pfxPath" -ForegroundColor Red
    Write-Host "Ejecuta primero create-cert.ps1" -ForegroundColor Yellow
    exit 1
}

try {
    # Importar como Trusted Publisher (elimina aviso de SmartScreen)
    Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\LocalMachine\TrustedPublisher -Password $password | Out-Null
    Write-Host "[OK] Certificado importado a Trusted Publishers" -ForegroundColor Green

    # Importar como Root CA (WDAC lo reconoce como confiable)
    Import-PfxCertificate -FilePath $pfxPath -CertStoreLocation Cert:\LocalMachine\Root -Password $password | Out-Null
    Write-Host "[OK] Certificado importado a Trusted Root CA" -ForegroundColor Green

    Write-Host ""
    Write-Host "Certificado instalado correctamente!" -ForegroundColor Green
    Write-Host "Ahora puedes compilar y ejecutar la app sin bloqueos de WDAC." -ForegroundColor Cyan
} catch {
    Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Asegurate de ejecutar este script como ADMINISTRADOR:" -ForegroundColor Yellow
    Write-Host "  Click derecho en PowerShell -> Ejecutar como administrador" -ForegroundColor Yellow
}

Read-Host "Presiona Enter para cerrar"
