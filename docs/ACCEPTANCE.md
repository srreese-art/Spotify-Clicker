# Device acceptance checks

These checks require the physical ESP32 and Spotify account; automated tests cannot replace them.

- Confirm Wi-Fi and token refresh succeed with no setup hotspot.
- Start music on another device. Verify all five named button pins and their actions.
- Hold play/pause: one action. Hold volume: repeated changes. Track buttons act on release.
- Change playback in Spotify, wait for the next background status update, then verify the hardware button uses the updated state.
- Confirm idle playback checks continue (at least 10 seconds between completed normal checks). Button presses send only the action request, never a playback GET or token refresh.
- Press volume repeatedly and toggle play/pause twice: successful actions must update the cached state immediately.
- During a background GET, press a button: the current GET finishes before the action starts. No HTTPS requests overlap, and the button sends only its action. Waiting commands take priority over another poll; commands older than the queue age limit are discarded.
- Before the first status update, or with status over 60 seconds old, presses must wait for background recovery without initiating a GET.
- Disconnect Wi-Fi: old queued presses must not replay on reconnection.
- Restart and redeploy: saved settings and any rotated refresh token must remain usable.
- Change only Wi-Fi in PC configuration: preserve the device's renewed token.
- Reauthorize using Setup-PC.ps1 -Authorize, rebuild and deploy: import the new authorization.
- Next and previous are now independent buttons; no setup chord remains.

- Hold volume or repeatedly press buttons when a normal poll is due: no new normal poll should start until the queue drains and controls have been idle for two seconds. A request already in flight must finish before the action.
- Continue input past the 60-second cache age limit: background recovery must still be possible; invalid status retries must be spaced by at least three seconds.
