$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$target = Join-Path $root 'NanoDevice/DeviceSecrets.cs'
if (Test-Path $target) { throw 'DeviceSecrets.cs already exists. Keep it to preserve the storage key needed to read existing settings.' }
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$keyBytes = New-Object byte[] 32
$rng.GetBytes($keyBytes)
$storageKey = [Convert]::ToBase64String($keyBytes)
$rng.Dispose()
@"
namespace SpotifyClicker
{
    internal static class DeviceSecrets
    {
        public const string StorageKey = "$storageKey";
    }
}
"@ | Set-Content -LiteralPath $target -Encoding UTF8
Write-Output 'Generated NanoDevice/DeviceSecrets.cs. Open it privately and save a private backup of the storage key. No credentials were printed.'
