param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$fixtureDir = (Resolve-Path -LiteralPath $Destination).Path
$fixtureRsa = [System.Security.Cryptography.RSA]::Create(2048)
try {
    $request = [System.Security.Cryptography.X509Certificates.CertificateRequest]::new('CN=Patchwork local fixture', $fixtureRsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $san = [System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder]::new()
    foreach ($name in @('blitzapp.gg','api.blitz.gg','dn0qt3r0xannq.cloudfront.net','live.primis.tech','science.v2.iesdev.com','www.google-analytics.com','www.googletagmanager.com','data.v2.iesdev.com')) { $san.AddDnsName($name) }
    $request.CertificateExtensions.Add($san.Build())
    $certificate = $request.CreateSelfSigned([DateTimeOffset]::UtcNow.AddMinutes(-5), [DateTimeOffset]::UtcNow.AddDays(1))
    try {
        [IO.File]::WriteAllText((Join-Path $fixtureDir 'fixture-cert.pem'), $certificate.ExportCertificatePem())
        [IO.File]::WriteAllText((Join-Path $fixtureDir 'fixture-key.pem'), $fixtureRsa.ExportPkcs8PrivateKeyPem())
    } finally { $certificate.Dispose() }
} finally { $fixtureRsa.Dispose() }
Write-Output 'Created one-day private fixture certificate; no certificate store was changed.'
