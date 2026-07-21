[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot),
    [Parameter(Mandatory = $false)]
    [string]$ReleaseTag,
    [Parameter(Mandatory = $false)]
    [switch]$AllowPrivateAlphaWithoutRootLicense
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$versionPropsPath = Join-Path $root 'Version.props'
$versionJsonPath = Join-Path $root 'eng\release\version.json'
$manifestPath = Join-Path $root 'src\NexaStation.App\Package.appxmanifest'

if (-not (Test-Path -LiteralPath (Join-Path $root 'LICENSE'))) {
    if ($AllowPrivateAlphaWithoutRootLicense) {
        Write-Warning 'No owner-approved root LICENSE file exists. Public release remains blocked; private Alpha tester packaging may continue.'
    }
    else {
        throw 'RELEASE BLOCKED: no owner-approved root LICENSE file exists.'
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.md'))) {
    throw 'RELEASE BLOCKED: THIRD-PARTY-NOTICES.md is missing.'
}

[xml]$versionProps = Get-Content -LiteralPath $versionPropsPath
$versionJson = Get-Content -LiteralPath $versionJsonPath -Raw | ConvertFrom-Json
$version = [string]$versionProps.Project.PropertyGroup.NexaStationVersion
$packageVersion = [string]$versionProps.Project.PropertyGroup.NexaStationPackageVersion
$channel = [string]$versionProps.Project.PropertyGroup.NexaStationReleaseChannel
if ($versionJson.semanticVersion -ne $version -or $versionJson.packageVersion -ne $packageVersion -or $versionJson.releaseChannel -ne $channel) {
    throw 'RELEASE BLOCKED: Version.props and eng/release/version.json disagree.'
}
if ($channel -ne 'Alpha' -or $versionJson.architecture -ne 'x64') {
    throw 'RELEASE BLOCKED: private Alpha packaging must use channel Alpha and architecture x64.'
}
[xml]$manifest = Get-Content -LiteralPath $manifestPath
$manifestVersion = [string]$manifest.Package.Identity.Version
if ($manifestVersion -ne $packageVersion) {
    throw "RELEASE BLOCKED: manifest version '$manifestVersion' does not match '$packageVersion'."
}
if ([string]$manifest.Package.Identity.Name -ne $versionJson.packageName -or [string]$manifest.Package.Identity.Publisher -ne $versionJson.publisher) {
    throw 'RELEASE BLOCKED: package identity does not match private Alpha release metadata.'
}
if ([string]$manifest.Package.Properties.PublisherDisplayName -ne $versionJson.publisherDisplayName) {
    throw 'RELEASE BLOCKED: package PublisherDisplayName does not match private Alpha release metadata.'
}
foreach ($capability in $manifest.Package.Capabilities.ChildNodes) {
    if ($capability.LocalName -eq 'Capability' -and [string]$capability.GetAttribute('Name') -ne 'runFullTrust') {
        throw "RELEASE BLOCKED: unsupported package capability '$($capability.GetAttribute('Name'))' is declared."
    }
}
if ($ReleaseTag -and $ReleaseTag -like 'v*' -and $ReleaseTag -ne "v$version") {
    throw "RELEASE BLOCKED: tag '$ReleaseTag' must be 'v$version'."
}

$assets = Join-Path $root 'src\NexaStation.App\Assets'
foreach ($name in @('Square44x44Logo', 'Square150x150Logo', 'Wide310x150Logo', 'StoreLogo')) {
    if (-not (Get-ChildItem -LiteralPath $assets -Filter "$name*.png" -File)) {
        throw "RELEASE BLOCKED: package asset '$name' is missing."
    }
}

Write-Output "Release readiness metadata passed for NexaStation $version."
