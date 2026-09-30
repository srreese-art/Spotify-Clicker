using System;

using nanoFramework.Json;
using SpotifyClicker.Core;

namespace SpotifyClicker.Spotify
{
    public sealed class SpotifyService
    {
        private readonly SettingsStore _store;
        private readonly NetworkService _network;
        private readonly Transport _http = new Transport();
        private readonly Transport _stateHttp = new Transport();
        private readonly object _sync = new object();
        private readonly object _requestSync = new object();
        private int _waitingCommands;
        private long _lastButtonAt = -DeviceOptions.PlaybackPollIdleMs;
        private long _nextPollAt;
        private string _access;
        private long _expires;
        private PlaybackState _state;
        private long _stateAt;
        private int _revision;
        private bool _commandInFlight;
        public string Status = "Spotify is not connected.";
        public bool CanAcceptCommands { get { return _network.Ready && _http.Available && _store.Current.RefreshToken.Length > 0; } }
        public SpotifyService(SettingsStore store, NetworkService network) { _store = store; _network = network; }
        public void NotifyButtonActivity()
        {
            lock (_sync) { _lastButtonAt = Clock.Milliseconds; }
        }
        private bool PollIsDue(CommandQueue queue)
        {
            if (_waitingCommands > 0 || _commandInFlight || queue.HasPending || Clock.Milliseconds < _nextPollAt) return false;
            // Startup and stale/invalid state need background recovery to enable inputs.
            bool needsRecovery = _state == null || Clock.Milliseconds - _stateAt >= DeviceOptions.PlaybackStateMaxAgeMs;
            return needsRecovery || Clock.Milliseconds - _lastButtonAt >= DeviceOptions.PlaybackPollIdleMs;
        }
        public void PollPlaybackState(CommandQueue queue)
        {
            lock (_sync) { if (!PollIsDue(queue)) return; }
            lock (_requestSync)
            {
                lock (_sync) { if (!PollIsDue(queue)) return; }
                RefreshPlaybackStateCore();
                lock (_sync)
                {
                    _nextPollAt = Clock.Milliseconds + (_state == null ? DeviceOptions.PlaybackPollRetryMs : DeviceOptions.PlaybackPollMs);
                }
            }
        }
        private void AcceptToken(Reply reply)
        {
            TokenResponse token = (TokenResponse)JsonConvert.DeserializeObject(reply.Body, typeof(TokenResponse));
            if (string.IsNullOrEmpty(token.access_token) || token.expires_in < 60)
                throw new ArgumentException("Spotify returned an incomplete token response. Authorize again.");
            if (!string.IsNullOrEmpty(token.refresh_token))
            {
                lock (_store.Sync) { _store.Current.RefreshToken = token.refresh_token; _store.Save(); }
            }
            lock (_sync)
            {
                _access = token.access_token;
                _expires = Clock.Milliseconds + (long)(token.expires_in - 30) * 1000;
            }
        }
        private bool EnsureToken()
        {
            lock (_sync) { if (_access != null && Clock.Milliseconds < _expires) return true; }
            if (!CanAcceptCommands) return false;
            Status = "Refreshing Spotify access...";
            Reply reply = _stateHttp.Send("https://accounts.spotify.com/api/token", "POST", "grant_type=refresh_token&client_id=" + _store.Current.ClientId + "&refresh_token=" + EncodingTools.Encode(_store.Current.RefreshToken), null);
            if (reply.Status == 200) { AcceptToken(reply); Status = "Spotify connected."; return true; }
            if (reply.Status == 400 || reply.Status == 401)
            {
                TokenResponse failure = null;
                try { failure = (TokenResponse)JsonConvert.DeserializeObject(reply.Body, typeof(TokenResponse)); } catch { }
                if (failure != null && (failure.error == "invalid_grant" || failure.error == "invalid_client"))
                {
                    lock (_store.Sync) { _store.Current.RefreshToken = ""; _store.Save(); _store.Save(); }
                    Status = "Authorization is no longer valid. " + DeviceOptions.SetupInstructions + " Then authorize again.";
                    return false;
                }
            }
            DescribeFailure(reply); return false;
        }
        // Separate workers share one network slot on this device. Waiting commands
        // take priority over starting another background poll.
        public void RefreshPlaybackState()
        {
            lock (_sync) { if (_waitingCommands > 0) return; }
            lock (_requestSync)
            {
                lock (_sync) { if (_waitingCommands > 0) return; }
                RefreshPlaybackStateCore();
            }
        }

        private void RefreshPlaybackStateCore()
        {
            int revision;
            lock (_sync)
            {
                if (!CanAcceptCommands) { _state = null; return; }
                if (_commandInFlight) return;
                revision = _revision;
            }
            try
            {
                if (!EnsureToken()) return;
                Reply reply = _stateHttp.Send("https://api.spotify.com/v1/me/player", "GET", null, _access);
                lock (_sync)
                {
                    // Discard a poll that overlaps a command, even if it has completed.
                    if (revision != _revision || _commandInFlight) return;
                    if (!CanAcceptCommands) { _state = null; return; }
                    if (reply.Status == 204 || reply.Status == 404)
                    { _state = null; Status = "No playback session. Start Spotify on your listening device."; return; }
                    if (reply.Status != 200) { DescribeFailure(reply); return; }
                    PlaybackState state = (PlaybackState)JsonConvert.DeserializeObject(reply.Body, typeof(PlaybackState));
                    if (state == null || state.device == null || !state.device.is_active || state.device.is_restricted)
                    { _state = null; Status = "No active controllable playback device."; return; }
                    _state = state;
                    _stateAt = Clock.Milliseconds;
                    Status = "Playback status updated. Volume control: " +
                        (state.device.supports_volume ? "supported" : "unsupported") +
                        "; volume: " + (state.device.volume_percent == null ? "unavailable" : state.device.volume_percent.ToString()) + ".";
                }
            }
            catch
            {
                lock (_sync)
                {
                    if (revision == _revision)
                    { _state = null; Status = "Could not refresh playback status; waiting for the next background check."; }
                }
            }
        }

        public void Execute(Command command)
        {
            lock (_sync) { _waitingCommands++; }
            try
            {
                lock (_requestSync) { ExecuteCached(command); }
            }
            finally { lock (_sync) { _waitingCommands--; _lastButtonAt = Clock.Milliseconds; } }
        }

        private void ExecuteCached(Command command)
        {
            string path, method = "PUT", access;
            int targetVolume = 0;
            bool targetPlaying = false;
            lock (_sync)
            {
                if (Clock.Milliseconds - command.Created > DeviceOptions.CommandMaxAgeMs)
                { Status = "Queued button command expired. Press again."; return; }
                if (!CanAcceptCommands) { _state = null; return; }
                if (_commandInFlight) return;
                if (_access == null || Clock.Milliseconds >= _expires || _state == null ||
                    Clock.Milliseconds - _stateAt >= DeviceOptions.PlaybackStateMaxAgeMs)
                { Status = "Waiting for fresh background playback status. Press again shortly."; return; }
                PlaybackState state = _state;
                Disallows denied = state.actions == null ? null : state.actions.disallows;
                switch (command.Kind)
                {
                    case CommandKind.Toggle:
                        if (denied != null && (state.is_playing ? denied.pausing : denied.resuming))
                        { Status = "Toggle not sent: cached Spotify state disallows this action."; return; }
                        targetPlaying = !state.is_playing;
                        path = targetPlaying ? "/play" : "/pause"; break;
                    case CommandKind.Next:
                        if (denied != null && denied.skipping_next) return;
                        path = "/next"; method = "POST"; break;
                    case CommandKind.Previous:
                        if (denied != null && denied.skipping_prev) return;
                        path = "/previous"; method = "POST"; break;
                    case CommandKind.Volume:
                        if (!state.device.supports_volume)
                        { Status = "Volume not sent: Spotify reports this playback device does not support volume control."; return; }
                        if (state.device.volume_percent == null)
                        { Status = "Volume not sent: Spotify did not report the playback device's volume."; return; }
                        if (command.Delta == 0)
                        { Status = "Volume not sent: queued up/down changes cancel each other."; return; }
                        int volume;
                        try { volume = int.Parse(state.device.volume_percent.ToString()); }
                        catch { _state = null; Status = "Invalid cached volume; waiting for background refresh."; return; }
                        targetVolume = CommandQueue.Clamp(volume + command.Delta, 0, 100);
                        if (targetVolume == volume)
                        { Status = "Volume not sent: already at " + volume + "%."; return; }
                        path = "/volume?volume_percent=" + targetVolume; break;
                    default: return;
                }
                access = _access;
                _commandInFlight = true;
                _revision++;
            }
            try
            {
                // Button presses send only the action; status and token requests are background work.
                Reply result = _http.Send("https://api.spotify.com/v1/me/player" + path, method, "", access);
                lock (_sync)
                {
                    if (result.Status >= 200 && result.Status < 300)
                    {
                        if (_state != null)
                        {
                            if (command.Kind == CommandKind.Volume) _state.device.volume_percent = targetVolume;
                            if (command.Kind == CommandKind.Toggle)
                            {
                                _state.is_playing = targetPlaying;
                                // These flags describe the pre-command state (e.g.
                                // resuming is disallowed while already playing).
                                // The server still enforces restrictions on each action.
                                if (_state.actions != null && _state.actions.disallows != null)
                                {
                                    _state.actions.disallows.pausing = false;
                                    _state.actions.disallows.resuming = false;
                                }
                            }
                        }
                        // Success does not extend the age of externally observed status.
                        Status = "Playback command accepted.";
                    }
                    else DescribeFailure(result);
                }
            }
            catch
            { lock (_sync) { _state = null; Status = "Playback command failed; waiting for background refresh."; } }
            finally
            { lock (_sync) { _commandInFlight = false; _revision++; } }
        }
        private void DescribeFailure(Reply reply)
        {
            lock (_sync)
            {
                _state = null;
                if (reply.Status == 401) { _access = null; Status = "Access token rejected; waiting for background renewal."; }
                else if (reply.Status == 403) Status = "Spotify refused this action. Check Premium, developer app access, and device restrictions.";
                else if (reply.Status == 429) Status = "Spotify rate limit: waiting " + reply.RetrySeconds + " seconds. Commands are discarded while waiting.";
                else if (reply.Status == 404) Status = "No controllable playback session.";
                else Status = "Spotify request failed (HTTP " + reply.Status + "). Command was not retried.";
            }
        }
    }
}
