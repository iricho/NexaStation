[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+([\-+][0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0',

    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',

    [switch]$PortableOnly,
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Invoke-DotNet {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Remove-GeneratedDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ArtifactsRoot
    )

    $resolvedPath = [IO.Path]::GetFullPath($Path)
    $resolvedArtifacts = [IO.Path]::GetFullPath($ArtifactsRoot)
    $prefix = $resolvedArtifacts.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the generated artifacts directory: $resolvedPath"
    }

    if (Test-Path -LiteralPath $resolvedPath) {
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }
}

function Find-InnoSetupCompiler {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command) {
        return $command.Source
    }

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $candidates += Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates += Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'
    }

    return $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

function Write-Checksums {
    param(
        [Parameter(Mandatory = $true)][string[]]$Files,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    $lines = foreach ($file in $Files) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            throw "Cannot hash a missing release file: $file"
        }
        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash
        '{0}  {1}' -f $hash, (Split-Path -Leaf $file)
    }
    $lines | Set-Content -LiteralPath $Destination -Encoding ascii
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'NexaStation.sln'
$projectPath = Join-Path $repositoryRoot 'src\NexaStation.App\NexaStation.App.csproj'
$installerDefinition = Join-Path $repositoryRoot 'installer\NexaStation.iss'
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = Join-Path $artifactsRoot "publish\$Runtime"
$releaseDirectory = Join-Path $artifactsRoot "release\$Version"
$portableStage = Join-Path $artifactsRoot "staging\NexaStation-Portable-$Version-$Runtime"
$applicationExecutable = 'NexaStation.App.exe'
$portableArchive = Join-Path $releaseDirectory "NexaStation-Portable-$Version-$Runtime.zip"
$installerOutput = Join-Path $releaseDirectory "NexaStation-Setup-$Version-$Runtime.exe"
$checksumsPath = Join-Path $releaseDirectory 'SHA256SUMS.txt'

foreach ($requiredPath in @($solutionPath, $projectPath, $installerDefinition)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required release input was not found: $requiredPath"
    }
}

$versionMatch = [regex]::Match($Version, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)')
$numericParts = @(
    [int]$versionMatch.Groups['major'].Value,
    [int]$versionMatch.Groups['minor'].Value,
    [int]$versionMatch.Groups['patch'].Value,
    0
)
if ($numericParts | Where-Object { $_ -gt 65535 }) {
    throw 'Each numeric version component must be between 0 and 65535 for Windows version metadata.'
}
$numericVersion = $numericParts -join '.'

Remove-GeneratedDirectory -Path $publishDirectory -ArtifactsRoot $artifactsRoot
Remove-GeneratedDirectory -Path $releaseDirectory -ArtifactsRoot $artifactsRoot
Remove-GeneratedDirectory -Path $portableStage -ArtifactsRoot $artifactsRoot
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

Write-Host "Restoring NexaStation dependencies..."
Invoke-DotNet @('restore', $solutionPath)
Invoke-DotNet @('restore', $projectPath, '-r', $Runtime, '-p:Platform=x64', '-p:NexaStationReleasePackaging=true', '-p:WindowsPackageType=None')

if (-not $SkipTests) {
    Write-Host 'Running the solution test suite...'
    Invoke-DotNet @('test', $solutionPath, '-c', 'Release', '--no-restore', '-m:1', '-v:minimal')
}

Write-Host "Publishing the self-contained $Runtime application..."
Invoke-DotNet @(
    'publish', $projectPath,
    '-c', 'Release',
    '-r', $Runtime,
    '-p:Platform=x64',
    '-p:NexaStationReleasePackaging=true',
    '-p:WindowsPackageType=None',
    '-p:SelfContained=true',
    '-p:PublishTrimmed=false',
    '-p:PublishSingleFile=false',
    '-p:AppxPackageSigningEnabled=false',
    "-p:Version=$Version",
    "-p:InformationalVersion=$Version",
    "-p:AssemblyVersion=$numericVersion",
    "-p:FileVersion=$numericVersion",
    '--no-restore',
    '--output', $publishDirectory
)

$publishedExecutable = Join-Path $publishDirectory $applicationExecutable
if (-not (Test-Path -LiteralPath $publishedExecutable -PathType Leaf)) {
    throw "Publish completed without the expected executable: $publishedExecutable"
}
if (Test-Path -LiteralPath (Join-Path $publishDirectory 'portable.flag')) {
    throw 'portable.flag must not exist in the installer source.'
}
if (Test-Path -LiteralPath (Join-Path $publishDirectory 'Data')) {
    throw 'The base publish output must not contain a writable Data directory.'
}

try {
    New-Item -ItemType Directory -Path $portableStage -Force | Out-Null
    Copy-Item -Path (Join-Path $publishDirectory '*') -Destination $portableStage -Recurse -Force
    New-Item -ItemType File -Path (Join-Path $portableStage 'portable.flag') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $portableStage 'Data') -Force | Out-Null

    Write-Host 'Creating the portable archive...'
    Compress-Archive -Path (Join-Path $portableStage '*') -DestinationPath $portableArchive -CompressionLevel Optimal
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $portableZip = [IO.Compression.ZipFile]::Open($portableArchive, [IO.Compression.ZipArchiveMode]::Update)
    try {
        if ($null -eq $portableZip.GetEntry('Data/')) {
            $null = $portableZip.CreateEntry('Data/')
        }
    }
    finally {
        $portableZip.Dispose()
    }
}
finally {
    Remove-GeneratedDirectory -Path $portableStage -ArtifactsRoot $artifactsRoot
}

if ($PortableOnly) {
    Write-Checksums -Files @($portableArchive) -Destination $checksumsPath
    Write-Host ''
    Write-Host 'Portable release complete:'
    Write-Host "  $portableArchive"
    Write-Host "  $checksumsPath"
    exit 0
}

$innoCompiler = Find-InnoSetupCompiler
if ([string]::IsNullOrWhiteSpace($innoCompiler)) {
    Write-Checksums -Files @($portableArchive) -Destination $checksumsPath
    Write-Warning "Portable ZIP created at '$portableArchive', but Inno Setup 6 (ISCC.exe) was not found. Install Inno Setup 6 or use -PortableOnly."
    throw 'The installer was skipped because Inno Setup 6 is not installed.'
}

Write-Host 'Compiling the per-user installer...'
& $innoCompiler "/DAppVersion=$Version" "/DAppNumericVersion=$numericVersion" "/DSourceDir=$publishDirectory" "/DOutputDir=$releaseDirectory" $installerDefinition
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath $installerOutput -PathType Leaf)) {
    throw "Inno Setup completed without the expected installer: $installerOutput"
}

Write-Checksums -Files @($portableArchive, $installerOutput) -Destination $checksumsPath

Write-Host ''
Write-Host "NexaStation $Version release complete:"
Write-Host "  Portable: $portableArchive"
Write-Host "  Installer: $installerOutput"
Write-Host "  Checksums: $checksumsPath"
