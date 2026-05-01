# Copyright 2024 Keyfactor
#
# Generates unique self-signed cert + key pairs for the DataPower test setup
# and writes them as Postman Collection Runner iteration-data files.
#
# Output (relative to this script):
#   data/pubcert-data.json     -  10 rows: { certPemB64 }
#   data/sharedcert-data.json  -  10 rows: { certPemB64 }
#   data/perdomain-data.json   - 100 rows: { certPemB64, keyPemB64 }
#
# In Postman Collection Runner, drop the matching JSON file into the "Data"
# slot when running each folder; iteration count is taken from the file.

param(
    [int]$ValidDays = 365
)

$ErrorActionPreference = 'Stop'

function ConvertTo-Pem {
    param([byte[]]$Bytes, [string]$Header)
    $b64 = [Convert]::ToBase64String($Bytes)
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("-----BEGIN $Header-----")
    for ($i = 0; $i -lt $b64.Length; $i += 64) {
        $len = [Math]::Min(64, $b64.Length - $i)
        [void]$sb.AppendLine($b64.Substring($i, $len))
    }
    [void]$sb.AppendLine("-----END $Header-----")
    return $sb.ToString()
}

function New-CertKeyPair {
    param([string]$Subject, [int]$Days)

    $rsa = [System.Security.Cryptography.RSA]::Create(2048)
    try {
        $req = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new(
            $Subject,
            $rsa,
            [System.Security.Cryptography.HashAlgorithmName]::SHA256,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1
        )

        # DataPower's CryptoCertificate loader rejects barebones certs ("unreadable,
        # corrupt, or invalid certificate file") - it expects a real end-entity TLS cert
        # with the usual extensions. Add BasicConstraints, KeyUsage, EKU, and SKI.
        $req.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension]::new(
                $false, $false, 0, $true))
        $req.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509KeyUsageExtension]::new(
                ([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::DigitalSignature -bor
                 [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyEncipherment),
                $true))
        $ekuOids = [System.Security.Cryptography.OidCollection]::new()
        [void]$ekuOids.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.1'))  # serverAuth
        [void]$ekuOids.Add([System.Security.Cryptography.Oid]::new('1.3.6.1.5.5.7.3.2'))  # clientAuth
        $req.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new(
                $ekuOids, $false))
        $req.CertificateExtensions.Add(
            [System.Security.Cryptography.X509Certificates.X509SubjectKeyIdentifierExtension]::new(
                $req.PublicKey, $false))

        $notBefore = [DateTimeOffset]::UtcNow.AddMinutes(-5)
        $notAfter  = [DateTimeOffset]::UtcNow.AddDays($Days)
        $cert = $req.CreateSelfSigned($notBefore, $notAfter)

        $certBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
        $certPem = ConvertTo-Pem -Bytes $certBytes -Header 'CERTIFICATE'

        # PKCS#8 unencrypted (-----BEGIN PRIVATE KEY-----). DataPower's filestore
        # validator rejects PKCS#1 keys (-----BEGIN RSA PRIVATE KEY-----) with 400.
        $keyBytes = $rsa.ExportPkcs8PrivateKey()
        $keyPem = ConvertTo-Pem -Bytes $keyBytes -Header 'PRIVATE KEY'

        return [PSCustomObject]@{
            CertPemB64 = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($certPem))
            KeyPemB64  = [Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($keyPem))
        }
    }
    finally {
        $rsa.Dispose()
    }
}

$dataDir = Join-Path $PSScriptRoot 'data'
New-Item -ItemType Directory -Force -Path $dataDir | Out-Null

Write-Host "Generating 10 pubcert pairs..." -ForegroundColor Cyan
$pubcert = @(1..10 | ForEach-Object {
    $p = New-CertKeyPair -Subject "CN=Pubcert-Test-$($_.ToString('00'))" -Days $ValidDays
    [PSCustomObject]@{ certPemB64 = $p.CertPemB64 }
})
$pubcert | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $dataDir 'pubcert-data.json') -Encoding ASCII

Write-Host "Generating 10 sharedcert pairs..." -ForegroundColor Cyan
$sharedcert = @(1..10 | ForEach-Object {
    $p = New-CertKeyPair -Subject "CN=Sharedcert-Test-$($_.ToString('00'))" -Days $ValidDays
    [PSCustomObject]@{ certPemB64 = $p.CertPemB64; keyPemB64 = $p.KeyPemB64 }
})
$sharedcert | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $dataDir 'sharedcert-data.json') -Encoding ASCII

Write-Host "Generating 100 per-domain cert+key pairs..." -ForegroundColor Cyan
$perdomain = @(1..100 | ForEach-Object {
    $p = New-CertKeyPair -Subject "CN=Perdomain-Test-$($_.ToString('000'))" -Days $ValidDays
    [PSCustomObject]@{ certPemB64 = $p.CertPemB64; keyPemB64 = $p.KeyPemB64 }
})
$perdomain | ConvertTo-Json -Depth 5 | Set-Content -Path (Join-Path $dataDir 'perdomain-data.json') -Encoding ASCII

Write-Host ""
Write-Host "Done. Wrote:" -ForegroundColor Green
Write-Host "  $dataDir\pubcert-data.json    (10 rows)"
Write-Host "  $dataDir\sharedcert-data.json (10 rows)"
Write-Host "  $dataDir\perdomain-data.json  (100 rows)"
