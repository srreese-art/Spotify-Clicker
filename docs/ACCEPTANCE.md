# Device acceptance checks

These checks require the physical ESP32 and Spotify account; automated tests cannot replace them.

- Confirm Wi-Fi and token refresh succeed with no setup hotspot.
- Start music on another device. Verify all five named button pins and their actions.
- Hold play/pause: one action. Hold volume: repeated changes. Track buttons act on release.
- Change playback in Spotify, then use the hardware button: it must read current state.
- Disconnect Wi-Fi: old queued presses must not replay on reconnection.
- Restart and redeploy: saved settings and any rotated refresh token must remain usable.
- Change only Wi-Fi in PC configuration: preserve the device's renewed token.
- Reauthorize using Setup-PC.ps1 -Authorize, rebuild and deploy: import the new authorization.
- Next and previous are now independent buttons; no setup chord remains.
