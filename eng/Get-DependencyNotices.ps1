[CmdletBinding()]
param([string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot))

$packages = @{}
Get-ChildItem -LiteralPath (Join-Path $RepositoryRoot 'src') -Recurse -Filter project.assets.json | ForEach-Object {
    $assets = Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
    $assets.libraries.PSObject.Properties | ForEach-Object {
        if ($_.Value.type -eq 'package') {
            $id, $version = $_.Name -split '/'
            $packages[$id] = $version
        }
    }
}

$packages.GetEnumerator() | Sort-Object Name | ForEach-Object {
    $nuspec = Join-Path $env:USERPROFILE ".nuget\packages\$($_.Name.ToLowerInvariant())\$($_.Value)\$($_.Name).nuspec"
    $license = 'Review package contents'
    if (Test-Path -LiteralPath $nuspec) {
        [xml]$metadata = Get-Content -LiteralPath $nuspec
        if ($metadata.package.metadata.license.'#text') { $license = $metadata.package.metadata.license.'#text' }
    }
    "| $($_.Name) | $($_.Value) | $license |"
}
