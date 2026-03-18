$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=IMA Mecatronica Dev, O=IMA Mecatronica" `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -CertStoreLocation Cert:\CurrentUser\My `
    -NotAfter (Get-Date).AddYears(5) `
    -FriendlyName "IMA Mecatronica Code Signing"

Write-Host "Certificado creado exitosamente!"
Write-Host "Thumbprint: $($cert.Thumbprint)"
Write-Host "Subject: $($cert.Subject)"
Write-Host "Expira: $($cert.NotAfter)"

# Export PFX for backup (sin password para simplificar dev)
$pfxPath = Join-Path $PSScriptRoot "ima-dev-cert.pfx"
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password (ConvertTo-SecureString -String "ima2026" -Force -AsPlainText) | Out-Null
Write-Host "PFX exportado a: $pfxPath"
Write-Host ""
Write-Host "SIGUIENTE PASO: Copia el Thumbprint y pegalo en el .csproj"
