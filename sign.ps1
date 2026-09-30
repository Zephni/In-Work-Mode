param(
    [string]$FilePath = (Join-Path $PSScriptRoot 'Work Mode.exe'),
    [switch]$InstallCertificate
)

$ErrorActionPreference = 'Stop'
$certificateSubject = 'CN=Work Mode Local Development'

function Get-SigningCertificate {
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        [System.Security.Cryptography.X509Certificates.StoreName]::My,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
    )

    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        @($store.Certificates) |
            Where-Object {
                $enhancedKeyUsage = $_.Extensions |
                    Where-Object { $_.Oid.Value -eq '2.5.29.37' } |
                    Select-Object -First 1

                $_.Subject -eq $certificateSubject -and
                $_.NotAfter -gt (Get-Date) -and
                $_.HasPrivateKey -and
                $enhancedKeyUsage.EnhancedKeyUsages.Value -contains '1.3.6.1.5.5.7.3.3'
            } |
            Sort-Object NotAfter -Descending |
            Select-Object -First 1
    }
    finally {
        $store.Dispose()
    }
}

function Add-ToCertificateStore {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [System.Security.Cryptography.X509Certificates.StoreName]$StoreName
    )

    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
        $StoreName,
        [System.Security.Cryptography.X509Certificates.StoreLocation]::CurrentUser
    )

    try {
        $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
        $store.Add($Certificate)
    }
    finally {
        $store.Dispose()
    }
}

$certificate = Get-SigningCertificate

if ($InstallCertificate) {
    if (-not $certificate) {
        $certificate = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject $certificateSubject `
            -CertStoreLocation Cert:\CurrentUser\My `
            -HashAlgorithm SHA256 `
            -KeyAlgorithm RSA `
            -KeyLength 3072 `
            -KeyExportPolicy NonExportable `
            -NotAfter (Get-Date).AddYears(3)
    }

    Add-ToCertificateStore -Certificate $certificate -StoreName Root
    Add-ToCertificateStore -Certificate $certificate -StoreName TrustedPublisher
    Write-Host "Installed local signing certificate: $($certificate.Thumbprint)"
    exit 0
}

if (-not $certificate) {
    throw 'No local signing certificate found. Run sign.ps1 -InstallCertificate once, then rebuild.'
}

if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) {
    throw "Cannot sign missing file: $FilePath"
}

$signature = Set-AuthenticodeSignature `
    -LiteralPath $FilePath `
    -Certificate $certificate `
    -HashAlgorithm SHA256

if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
    throw "The Authenticode signature is not valid: $($signature.StatusMessage)"
}

Write-Host "Signed $FilePath"
Write-Host "Signer: $($certificate.Subject)"