$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
[xml]$manifest = Get-Content (Join-Path $root 'NanoDevice/packages.config')
foreach ($package in $manifest.packages.package) {
    $id = [string]$package.id
    $version = [string]$package.version
    $target = Join-Path $root "packages/$id.$version"
    if (Test-Path (Join-Path $target '.restored')) { continue }
    New-Item -ItemType Directory -Force $target | Out-Null
    $lower = $id.ToLowerInvariant()
    $archive = Join-Path $target 'package.zip'
    Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/$lower/$version/$lower.$version.nupkg" -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $target -Force
    Remove-Item -LiteralPath $archive
    Set-Content (Join-Path $target '.restored') $version
}
Write-Output 'Pinned nanoFramework packages restored.'
