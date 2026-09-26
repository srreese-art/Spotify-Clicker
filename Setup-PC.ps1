param([switch]$Authorize)
$ErrorActionPreference = 'Stop'
$config = Join-Path $PSScriptRoot 'device-config.json'
if (!(Test-Path $config)) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'device-config.example.json') -Destination $config
    Write-Output 'Created device-config.json. Enter your home Wi-Fi details, then run .\Setup-PC.ps1 again. Register http://127.0.0.1:8000/callback in your Spotify developer app.'
    return
}
$arguments = @('run', '--project', (Join-Path $PSScriptRoot 'tools/PcSetup/PcSetup.csproj'), '--', $config, (Join-Path $PSScriptRoot 'NanoDevice/ProvisionedConfig.cs'))
if ($Authorize) { $arguments += '--authorize' }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'PC setup did not complete. Correct the reported issue and retry.' }
Write-Output 'Next: run .\tools\Build.ps1, then deploy with F5 in Visual Studio. No device webpage or hotspot is used.'
