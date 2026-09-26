param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (!(Test-Path (Join-Path $root 'NanoDevice/ProvisionedConfig.cs'))) {
    throw 'Run .\Setup-PC.ps1 and complete PC authorization before building. Edit device-config.json with your Wi-Fi settings.'
}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
if (!(Test-Path $vswhere)) { throw 'Install Visual Studio and the .NET nanoFramework extension first.' }
$installations = & $vswhere -all -products * -property installationPath
$msbuild = $null
foreach ($installation in $installations) {
    $candidate = Join-Path $installation 'MSBuild/Current/Bin/MSBuild.exe'
    $targets = Join-Path $installation 'MSBuild/nanoFramework/v1.0/NFProjectSystem.CSharp.targets'
    if ((Test-Path $candidate) -and (Test-Path $targets)) { $msbuild = $candidate; break }
}
if (!$msbuild) { throw 'No Visual Studio installation with nanoFramework build targets was found.' }
& (Join-Path $PSScriptRoot 'Restore-Packages.ps1')
if (!(Test-Path (Join-Path $root 'NanoDevice/DeviceSecrets.cs'))) {
    & (Join-Path $PSScriptRoot 'Initialize-Device.ps1')
}
# Build the project directly: Visual Studio may rewrite the solution configuration list.
# Rebuild also removes stale assemblies after a package downgrade or mode change.
& $msbuild (Join-Path $root 'NanoDevice/NanoDevice.nfproj') /t:Rebuild "/p:Configuration=$Configuration" '/p:Platform=AnyCPU' /v:minimal /nologo
if ($LASTEXITCODE -ne 0) { throw "Firmware build failed ($LASTEXITCODE)." }
Write-Output "Build complete: $Configuration. Existing private credentials were preserved. Nothing was deployed."
