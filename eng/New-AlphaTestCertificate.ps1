[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Subject = 'CN=NexaStation Alpha',
    [Parameter(Mandatory = $false)]
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\signing'),
    [Parameter(Mandatory = $false)]
    [securestring]$PfxPassword,
    [Parameter(Mandatory = $false)]
    [int]$YearsValid = 2
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $PfxPassword) {
    $PfxPassword = Read-Host -AsSecureString 'Enter a password for the private Alpha PFX'
}

New-Item -ItemType Directory -Force -Path $output | Out-Null
$certificate = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -KeyExportPolicy Exportable `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -NotAfter (Get-Date).AddYears($YearsValid)

$cerPath = Join-Path $output 'NexaStation-Alpha-Test.cer'
$pfxPath = Join-Path $output 'NexaStation-Alpha-Test.private.pfx'
Export-Certificate -Cert $certificate -FilePath $cerPath | Out-Null
Export-PfxCertificate -Cert $certificate -FilePath $pfxPath -Password $PfxPassword | Out-Null

$notice = @"
NexaStation Alpha test certificate created.

Subject:    $($certificate.Subject)
Thumbprint: $($certificate.Thumbprint)
Public CER: $cerPath
Private PFX: $pfxPath

Keep the PFX and password out of source control and chat logs.
Give testers only the .cer that is bundled with the signed MSIX.
"@
$noticePath = Join-Path $output 'NexaStation-Alpha-Test-Certificate.txt'
$notice | Set-Content -LiteralPath $noticePath -Encoding UTF8
Write-Output $notice
