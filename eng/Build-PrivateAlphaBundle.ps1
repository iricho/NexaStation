[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$MsixPath,
    [Parameter(Mandatory = $true)]
    [string]$CertificatePath,
    [Parameter(Mandatory = $false)]
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\private-alpha'),
    [Parameter(Mandatory = $false)]
    [string]$GitCommit = 'unknown',
    [Parameter(Mandatory = $false)]
    [switch]$Signed,
    [Parameter(Mandatory = $false)]
    [switch]$SelfContained,
    [Parameter(Mandatory = $false)]
    [string[]]$KnownLimitation = @(
        'Clean-machine installation must be recorded on the actual tester machine before claiming readiness.',
        'Windows App Certification Kit results are not implied by this bundle.',
        'The optional .appinstaller update channel is disabled until HTTPS package URLs are configured.'
    )
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$versionPath = Join-Path $root 'eng\release\version.json'
if (-not (Test-Path -LiteralPath $MsixPath)) { throw "MSIX not found: $MsixPath" }
if (-not (Test-Path -LiteralPath $CertificatePath)) { throw "Certificate not found: $CertificatePath" }
if (([IO.Path]::GetExtension($CertificatePath)) -ne '.cer') { throw 'The tester bundle must include the public .cer, not a private .pfx.' }

$version = Get-Content -LiteralPath $versionPath -Raw | ConvertFrom-Json
$output = [IO.Path]::GetFullPath($OutputDirectory)
$stage = Join-Path $output 'NexaStation-Private-Alpha'
$zipPath = Join-Path $output ("NexaStation-{0}-Private-Alpha-x64.zip" -f $version.semanticVersion)
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

$msixName = "NexaStation-$($version.semanticVersion)-alpha-x64.msix"
Copy-Item -LiteralPath $MsixPath -Destination (Join-Path $stage $msixName)
Copy-Item -LiteralPath $CertificatePath -Destination (Join-Path $stage 'NexaStation-Alpha-Test.cer')

$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificatePath)
$msixHash = (Get-FileHash -LiteralPath (Join-Path $stage $msixName) -Algorithm SHA256).Hash.ToLowerInvariant()
$manifest = [ordered]@{
    productName = 'NexaStation'
    displayVersion = $version.semanticVersion
    packageVersion = $version.packageVersion
    releaseChannel = $version.releaseChannel
    architecture = $version.architecture
    gitCommit = $GitCommit
    buildTimestampUtc = (Get-Date).ToUniversalTime().ToString('O')
    databaseSchemaVersion = 64
    minimumWindowsVersion = $version.minimumWindowsVersion
    packageFilename = $msixName
    packageSha256 = $msixHash
    certificateSubject = $cert.Subject
    certificateThumbprint = $cert.Thumbprint
    signed = [bool]$Signed
    selfContained = [bool]$SelfContained
    includedDependencies = @()
    knownLimitations = $KnownLimitation
    diagnosticsFormatVersion = $version.diagnosticsFormatVersion
}

@"
NexaStation Private Alpha installation

Version: $($version.semanticVersion)
Package version: $($version.packageVersion)
Certificate subject: $($cert.Subject)
Certificate thumbprint: $($cert.Thumbprint)

1. Extract this ZIP.
2. Verify the expected version and certificate thumbprint above.
3. Open NexaStation-Alpha-Test.cer.
4. Install the certificate for Current User.
5. Place it in the Trusted People certificate store.
6. Double-click the .msix.
7. Verify the publisher is NexaStation Alpha.
8. Select Install.
9. Launch NexaStation from the Start menu.

Install only the certificate included with the package received directly from the NexaStation developer. Do not trust similarly named certificates from other sources. Do not disable Windows security globally.

Troubleshooting: if Windows reports publisher, trust, architecture, dependency, App Installer, policy, older-package, or in-progress-install errors, report the exact message and do not bypass unrelated security protections.
"@ | Set-Content -LiteralPath (Join-Path $stage 'INSTALL.txt') -Encoding UTF8

@"
Install a newer NexaStation Alpha package with the same identity and a higher package version. Data under %LocalAppData%\NexaStation should be preserved. Downgrades are not supported. The .appinstaller update channel is inactive unless real HTTPS URLs are documented for this release.
"@ | Set-Content -LiteralPath (Join-Path $stage 'UPDATE.txt') -Encoding UTF8

@"
Uninstall through Windows Settings > Apps > Installed Apps > NexaStation > Uninstall. Games, applications, ROMs, emulators, photos, videos, launcher manifests, external artwork, and Steam/Epic/GOG data are not deleted by package uninstall. Local NexaStation data may remain under %LocalAppData%\NexaStation.
"@ | Set-Content -LiteralPath (Join-Path $stage 'UNINSTALL.txt') -Encoding UTF8

@"
NexaStation is a private Alpha local-first Windows frontend for games, apps, emulators, and media. Export diagnostics only when reporting a problem; diagnostics are not uploaded automatically. Do not upload ROMs, games, photos, videos, API keys, credentials, full databases, or private Gallery paths. Report Windows version, controller type, display resolution, provider, reproduction steps, Safe Mode status, and diagnostics correlation ID.
"@ | Set-Content -LiteralPath (Join-Path $stage 'TESTER-GUIDE.txt') -Encoding UTF8

($KnownLimitation -join [Environment]::NewLine) | Set-Content -LiteralPath (Join-Path $stage 'KNOWN-ISSUES.txt') -Encoding UTF8
"NexaStation $($version.semanticVersion) private Alpha tester build." | Set-Content -LiteralPath (Join-Path $stage 'CHANGELOG.txt') -Encoding UTF8
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stage 'release-manifest.json') -Encoding UTF8

Get-ChildItem -LiteralPath $stage -File | Sort-Object Name | ForEach-Object {
    "{0}  {1}" -f ((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()), $_.Name
} | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding ASCII

if (Get-ChildItem -LiteralPath $stage -Recurse -Include '*.pfx','*.db','*.jsonl' -File) {
    throw 'Tester bundle contains a forbidden private key, database, or log file.'
}

if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
Compress-Archive -LiteralPath $stage -DestinationPath $zipPath
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Add-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Value ("{0}  {1}" -f $zipHash, (Split-Path -Leaf $zipPath))

Write-Output "Tester bundle: $zipPath"
Write-Output "Certificate thumbprint: $($cert.Thumbprint)"
