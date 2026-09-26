# Configure from your PC

This replaces the earlier phone/hotspot/static-website setup. The ESP32 starts directly in playback mode. It does not run DHCP, DNS, or an HTTP setup server. No public website or client secret is required.

1. In your Spotify developer application, register exactly `http://127.0.0.1:8000/callback` as a redirect URI.
2. Run `./Setup-PC.ps1` from the repository folder. On the first run it creates `device-config.json` without overwriting any existing file.
3. Edit `device-config.json`: enter `WifiSsid`, `WifiPassword`, and your Spotify `ClientId`. Use a 2.4 GHz home network. Keep the supplied `RedirectUri`; leave `RefreshToken` and `AuthorizedClientId` empty. Do not enter a Spotify account password or client secret.
4. Run `./Setup-PC.ps1` again. It temporarily listens on your PC's loopback port 8000 and opens Spotify in your browser. Sign in and approve playback access. Keep the terminal running until it confirms completion. The listener then closes automatically.
5. Run `./tools/Build.ps1` to build all five buttons. Open the solution in Visual Studio, select Debug and your nanoFramework device, then press F5. If Visual Studio already has the solution open, reload it after running the build script. Disconnect power before wiring the switch between GPIO25 and GND.
6. The board may restart once to disable its previous hotspot. It connects to home Wi-Fi and imports the PC settings. Start music in Spotify on another device, then press GPIO25 to pause/resume. The PC is no longer needed.

Set each button GPIO using the named constants in `NanoDevice/DeviceOptions.cs`: PlayPauseButtonPin, NextTrackButtonPin, PreviousTrackButtonPin, VolumeUpButtonPin, and VolumeDownButtonPin. Defaults are 25, 26, 27, 32, and 33 respectively. Connect each switch to its GPIO and GND. There is no single-button test mode.

## Change settings

Edit the private config, run `./Setup-PC.ps1`, rebuild and redeploy. Ordinary reruns reuse the saved authorization. After Spotify revokes access, or when changing the Spotify account, run `./Setup-PC.ps1 -Authorize` for a fresh approval, then rebuild and deploy.

Authorization is imported once per newly obtained refresh token. Power cycles and Wi-Fi-only changes preserve any refresh token rotated on the ESP32. A revoked authorization is not silently resurrected from the firmware on reboot. Flash erasure requires fresh PC authorization if the original embedded token has since rotated.

The config, generated `NanoDevice/ProvisionedConfig.cs`, and firmware contain credentials. They are ignored by Git; keep them private. Unicode escapes in generated C# are source escaping, not encryption. Preserve `NanoDevice/DeviceSecrets.cs`: its storage key decrypts the board's saved settings. No credentials are printed by the setup tool.

The tool requires the .NET 10 SDK. Port 8000 must be free. If authorization fails, correct the configuration/Spotify redirect registration and run it again. No firmware flashing, flash erasure, or device deployment is performed by the PC tool.
