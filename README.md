# Spotify Clicker

Standalone ESP32 Spotify controller using C# and .NET nanoFramework. Configure it once from your PC; thereafter it runs on power and Wi-Fi alone.

## Setup

1. Register `http://127.0.0.1:8000/callback` in your Spotify developer application.
2. Run `./Setup-PC.ps1` to create your private `device-config.json`.
3. Fill in your Wi-Fi name/password and Spotify client ID.
4. Run `./Setup-PC.ps1` again and approve access in the Spotify browser window.
5. Run `./tools/Build.ps1`, then deploy from Visual Studio with F5.

For play/pause, wire one switch between GPIO25 and GND while power is disconnected. Start music on another Spotify device, then press the button to pause/resume.

No device setup webpage, hotspot, hosted callback site, client secret, or continuously running PC is needed. The local callback listener runs only during authorization. The board refreshes its own Spotify tokens.

See [PC setup instructions](docs/PC-SETUP.md) for configuration changes, reauthorization, and the normal five-button build.

Keep `device-config.json`, `NanoDevice/ProvisionedConfig.cs`, `NanoDevice/DeviceSecrets.cs`, and built firmware private. The storage key in DeviceSecrets must be preserved to read existing device settings.

## Development

Requires Visual Studio with the nanoFramework extension and .NET 10 SDK for the PC tool/tests. Package versions are pinned to the existing ESP32 runtime; do not upgrade the filesystem package without checking native compatibility.

```powershell
dotnet run --project tools/PcSetup/PcSetup.csproj -- --self-test
dotnet run --project tests/Core.Tests.csproj
```

The firmware contains only playback, Wi-Fi, GPIO, and encrypted settings code. The PC tool handles initial Spotify authorization.