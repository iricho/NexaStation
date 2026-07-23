[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+([\-+][0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',

    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$packId = 'Iricho.NexaStation'
$packTitle = 'NexaStation'
$packAuthor = 'Iricho'
$channel = 'win'
$applicationExecutable = 'NexaStation.App.exe'

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

function Copy-ToCanonicalName {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination
    )

    if (-not [IO.Path]::GetFullPath($Source).Equals([IO.Path]::GetFullPath($Destination), [StringComparison]::OrdinalIgnoreCase)) {
        Copy-Item -LiteralPath $Source -Destination $Destination
    }
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
$toolManifest = Join-Path $repositoryRoot '.config\dotnet-tools.json'
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$publishDirectory = Join-Path $artifactsRoot "publish\$Runtime"
$releaseDirectory = Join-Path $artifactsRoot "releases\$Version"
$setupPath = Join-Path $releaseDirectory "$packId-Setup.exe"
$portablePath = Join-Path $releaseDirectory "$packId-Portable.zip"
$fullPackagePath = Join-Path $releaseDirectory "$packId-$Version-full.nupkg"
$releasesPath = Join-Path $releaseDirectory "releases.$channel.json"
$assetsPath = Join-Path $releaseDirectory "assets.$channel.json"
$checksumsPath = Join-Path $releaseDirectory 'SHA256SUMS.txt'

foreach ($requiredPath in @($solutionPath, $projectPath, $toolManifest)) {
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
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDirectory -Force | Out-Null

Push-Location $repositoryRoot
try {
    Write-Host 'Restoring the pinned Velopack tool and NexaStation dependencies...'
    Invoke-DotNet @('tool', 'restore')
    Invoke-DotNet @('restore', $solutionPath)
    Invoke-DotNet @('restore', $projectPath, '-r', $Runtime, '-p:Platform=x64', '-p:NexaStationReleasePackaging=true', '-p:WindowsPackageType=None')

    if (-not $SkipTests) {
        Write-Host 'Running the complete solution test suite...'
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
    $requiredPublishedFiles = @(
        $publishedExecutable,
        (Join-Path $publishDirectory 'coreclr.dll'),
        (Join-Path $publishDirectory 'Microsoft.WindowsAppRuntime.dll'),
        (Join-Path $publishDirectory 'NexaStation.UI.Themes.Nexa.dll'),
        (Join-Path $publishDirectory 'NexaStation.UI.Themes.Crossbar.dll'),
        (Join-Path $publishDirectory 'NexaStation.Input.dll'),
        (Join-Path $publishDirectory 'NexaStation.MediaPlayer.dll'),
        (Join-Path $publishDirectory 'NexaStation.Providers.Steam.dll'),
        (Join-Path $publishDirectory 'NexaStation.Providers.Emulation.dll'),
        (Join-Path $publishDirectory 'NexaStation.Providers.WindowsApps.dll'),
        (Join-Path $publishDirectory 'Assets\InputGlyphs\PlayStation\ps-cross.png')
    )
    foreach ($requiredPublishedFile in $requiredPublishedFiles) {
        if (-not (Test-Path -LiteralPath $requiredPublishedFile -PathType Leaf)) {
            throw "Publish completed without a required runtime file: $requiredPublishedFile"
        }
    }

    Write-Host 'Creating the Velopack installer, portable bundle, package, and release metadata...'
    $velopackArguments = @(
        'tool', 'run', 'vpk', 'pack',
        '--outputDir', $releaseDirectory,
        '--channel', $channel,
        '--runtime', $Runtime,
        '--packId', $packId,
        '--packVersion', $Version,
        '--packDir', $publishDirectory,
        '--packAuthors', $packAuthor,
        '--packTitle', $packTitle,
        '--mainExe', $applicationExecutable
    )
    $icon = Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src\NexaStation.App') -File -Filter '*.ico' -Recurse | Select-Object -First 1
    if ($null -eq $icon) {
        Write-Warning 'No NexaStation .ico file was found; Velopack will use its default installer icon.'
    }
    else {
        $velopackArguments += @('--icon', $icon.FullName)
    }
    Invoke-DotNet $velopackArguments
}
finally {
    Pop-Location
}

$generatedSetups = @(Get-ChildItem -LiteralPath $releaseDirectory -File -Filter '*Setup.exe')
$generatedPortables = @(Get-ChildItem -LiteralPath $releaseDirectory -File -Filter '*Portable.zip')
if ($generatedSetups.Count -ne 1 -or $generatedPortables.Count -ne 1) {
    throw "Velopack must produce exactly one installer and one portable bundle. Found $($generatedSetups.Count) installer(s) and $($generatedPortables.Count) portable bundle(s)."
}
$generatedSetup = $generatedSetups[0]
$generatedPortable = $generatedPortables[0]
Copy-ToCanonicalName -Source $generatedSetup.FullName -Destination $setupPath
Copy-ToCanonicalName -Source $generatedPortable.FullName -Destination $portablePath

$releaseFiles = @($setupPath, $portablePath, $fullPackagePath, $releasesPath, $assetsPath)
Write-Checksums -Files $releaseFiles -Destination $checksumsPath

Write-Host ''
Write-Host "NexaStation $Version Velopack release complete:"
foreach ($file in $releaseFiles + $checksumsPath) {
    Write-Host "  $file"
}
